using System;
using System.IO;
using System.Runtime.InteropServices;
using Raylib_cs;

namespace EmuFrontend.CoreInterop
{
    public class CoreManager
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void retro_init_t();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void retro_deinit_t();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void retro_run_t();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate bool retro_load_game_t(ref retro_game_info game);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void retro_set_environment_t(retro_environment_t cb);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void retro_set_video_refresh_t(retro_video_refresh_t cb);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void retro_set_audio_sample_t(retro_audio_sample_t cb);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void retro_set_audio_sample_batch_t(retro_audio_sample_batch_t cb);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void retro_set_input_poll_t(retro_input_poll_t cb);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void retro_set_input_state_fn(retro_input_state_t cb);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate bool retro_environment_t(uint cmd, IntPtr data);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void retro_video_refresh_t(IntPtr data, uint width, uint height, UIntPtr pitch);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void retro_audio_sample_t(short left, short right);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate UIntPtr retro_audio_sample_batch_t(IntPtr data, UIntPtr frames);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void retro_input_poll_t();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate short retro_input_state_t(uint port, uint device, uint index, uint id);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void retro_reset_t();

        [StructLayout(LayoutKind.Sequential)]
        public struct retro_game_info { public IntPtr path; public IntPtr data; public UIntPtr size; public IntPtr meta; }

        [StructLayout(LayoutKind.Sequential)]
        public struct retro_system_timing { public double fps; public double sample_rate; }

        [StructLayout(LayoutKind.Sequential)]
        public struct retro_game_geometry { public uint base_width; public uint base_height; public uint max_width; public uint max_height; public float aspect_ratio; }

        [StructLayout(LayoutKind.Sequential)]
        public struct retro_system_av_info { public retro_game_geometry geometry; public retro_system_timing timing; }
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void retro_get_system_av_info_t(ref retro_system_av_info info);

        public retro_init_t? RetroInit;
        public retro_deinit_t? RetroDeinit;
        public retro_run_t? RetroRun;
        public retro_load_game_t? RetroLoadGame;
        public retro_reset_t? RetroReset;

        private retro_environment_t? EnvCallback;
        private retro_video_refresh_t? VideoCallback;
        private retro_audio_sample_t? AudioCallback;
        private retro_audio_sample_batch_t? AudioBatchCallback;
        private retro_input_poll_t? InputPollCallback;
        private retro_input_state_t? InputStateCallback;

        public uint FrameWidth { get; private set; }
        public uint FrameHeight { get; private set; }
        public IntPtr FrameData { get; private set; }
        public int PixelFormat { get; private set; } = 0;
        public UIntPtr FramePitch { get; private set; }
        public retro_system_av_info AVInfo { get; private set; }
        public AudioStream GameAudioStream;
        public retro_get_system_av_info_t? RetroGetSystemAvInfo;

        private IntPtr coreHandle;

        public string MatchCoreToExtension(string romPath)
        {
            string ext = Path.GetExtension(romPath).ToLower();
            return ext switch
            {
                ".nes" => "fceumm",
                ".smc" or ".sfc" => "snes9x",
                ".gen" or ".md" => "genesis_plus_gx",
                ".gb" or ".gbc" => "gambatte",
                ".gba" => "mgba",
                ".bin" or ".cue" or ".iso" => "pcsx_rearmed",
                ".exe" or ".bat" or ".com" or ".dos" => "dosbox_pure",
                _ => throw new Exception($"No core found for extension {ext}")
            };
        }

        public void LoadCore(string coreName)
        {
            string ext = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".dll" : ".so";
            string corePath = $"cores/{coreName}_libretro{ext}";
            Logger.Info($"Attempting to load core from: {corePath}");
            if (!NativeLibrary.TryLoad(corePath, out coreHandle))
            {
                Logger.Error($"Failed to load core library: {corePath}");
                throw new Exception($"Failed to load core from {corePath}");
            }
            Logger.Info($"Core library {corePath} loaded successfully. Handle: {coreHandle}");

            RetroInit = GetExport<retro_init_t>("retro_init");
            RetroDeinit = GetExport<retro_deinit_t>("retro_deinit");
            RetroRun = GetExport<retro_run_t>("retro_run");
            RetroLoadGame = GetExport<retro_load_game_t>("retro_load_game");
            RetroReset = GetExport<retro_reset_t>("retro_reset");
            RetroGetSystemAvInfo = GetExport<retro_get_system_av_info_t>("retro_get_system_av_info");

            var setEnv = GetExport<retro_set_environment_t>("retro_set_environment");
            var setVideo = GetExport<retro_set_video_refresh_t>("retro_set_video_refresh");
            var setAudio = GetExport<retro_set_audio_sample_t>("retro_set_audio_sample");
            var setAudioBatch = GetExport<retro_set_audio_sample_batch_t>("retro_set_audio_sample_batch");
            var setInputPoll = GetExport<retro_set_input_poll_t>("retro_set_input_poll");
            var setInputState = GetExport<retro_set_input_state_fn>("retro_set_input_state");

            EnvCallback = EnvironmentCallback;
            VideoCallback = VideoRefreshCallback;
            AudioCallback = (l, r) => { };
            AudioBatchCallback = AudioBatchCallbackImpl;
            InputPollCallback = () => { };
            InputStateCallback = InputStateCallbackImpl;

            setEnv?.Invoke(EnvCallback);
            RetroInit?.Invoke();
            
            setVideo?.Invoke(VideoCallback);
            setAudio?.Invoke(AudioCallback);
            setAudioBatch?.Invoke(AudioBatchCallback);
            setInputPoll?.Invoke(InputPollCallback);
            setInputState?.Invoke(InputStateCallback);
        }

        private T? GetExport<T>(string name) where T : Delegate
        {
            if (NativeLibrary.TryGetExport(coreHandle, name, out IntPtr ptr))
                return Marshal.GetDelegateForFunctionPointer<T>(ptr);
            return null;
        }

        public bool LoadGame(string romPath)
        {
            Logger.Info($"Loading ROM: {romPath}");
            
            byte[] romBytes = File.ReadAllBytes(romPath);
            IntPtr unmanagedPointer = Marshal.AllocHGlobal(romBytes.Length);
            Marshal.Copy(romBytes, 0, unmanagedPointer, romBytes.Length);

            IntPtr pathPtr = Marshal.StringToCoTaskMemUTF8(romPath);

            var info = new retro_game_info 
            { 
                path = pathPtr, 
                data = unmanagedPointer, 
                size = (UIntPtr)romBytes.Length, 
                meta = IntPtr.Zero 
            };
            
            bool result = RetroLoadGame?.Invoke(ref info) ?? false;
            
            if (result)
            {
                var avInfo = new retro_system_av_info();
                RetroGetSystemAvInfo?.Invoke(ref avInfo);
                AVInfo = avInfo;
                Logger.Info($"Core successfully loaded the ROM. FPS: {AVInfo.timing.fps}, SampleRate: {AVInfo.timing.sample_rate}");
            }
            else Logger.Error("Core failed to load the ROM.");

            Marshal.FreeHGlobal(unmanagedPointer);
            Marshal.FreeCoTaskMem(pathPtr);

            return result;
        }

        public void RunFrame()
        {
            RetroRun?.Invoke();
        }

        public void LoadConfig(string path) {}

        private bool EnvironmentCallback(uint cmd, IntPtr data)
        {
            if (cmd == 10) // RETRO_ENVIRONMENT_SET_PIXEL_FORMAT
            {
                int format = Marshal.ReadInt32(data);
                if (format == 0 || format == 1 || format == 2)
                {
                    PixelFormat = format;
                    Logger.Info($"Core requested Pixel Format: {format}");
                    return true;
                }
                Logger.Warn($"Core requested unknown Pixel Format: {format}");
                return false; 
            }
            return false;
        }

        private void VideoRefreshCallback(IntPtr data, uint width, uint height, UIntPtr pitch)
        {
            if (data == IntPtr.Zero) return;
            FrameWidth = width;
            FrameHeight = height;
            FrameData = data;
            FramePitch = pitch;
        }

        private short InputStateCallbackImpl(uint port, uint device, uint index, uint id)
        {
            if (device != 1) return 0; // Only Joypad
            
            bool pressed = false;
            
            if (port == 0) // Player 1
            {
                switch (id)
                {
                    case 0: pressed = Raylib.IsKeyDown(KeyboardKey.Z); break; // B
                    case 1: pressed = Raylib.IsKeyDown(KeyboardKey.A); break; // Y
                    case 2: pressed = Raylib.IsKeyDown(KeyboardKey.RightShift); break; // Select
                    case 3: pressed = Raylib.IsKeyDown(KeyboardKey.Enter); break; // Start
                    case 4: pressed = Raylib.IsKeyDown(KeyboardKey.Up); break; // Up
                    case 5: pressed = Raylib.IsKeyDown(KeyboardKey.Down); break; // Down
                    case 6: pressed = Raylib.IsKeyDown(KeyboardKey.Left); break; // Left
                    case 7: pressed = Raylib.IsKeyDown(KeyboardKey.Right); break; // Right
                    case 8: pressed = Raylib.IsKeyDown(KeyboardKey.X); break; // A
                    case 9: pressed = Raylib.IsKeyDown(KeyboardKey.S); break; // X
                    case 10: pressed = Raylib.IsKeyDown(KeyboardKey.Q); break; // L
                    case 11: pressed = Raylib.IsKeyDown(KeyboardKey.W); break; // R
                }
            }
            else if (port == 1) // Player 2
            {
                switch (id)
                {
                    case 0: pressed = Raylib.IsKeyDown(KeyboardKey.C); break; // B
                    case 1: pressed = Raylib.IsKeyDown(KeyboardKey.F); break; // Y
                    case 2: pressed = Raylib.IsKeyDown(KeyboardKey.Tab); break; // Select
                    case 3: pressed = Raylib.IsKeyDown(KeyboardKey.Space); break; // Start
                    case 4: pressed = Raylib.IsKeyDown(KeyboardKey.I); break; // Up
                    case 5: pressed = Raylib.IsKeyDown(KeyboardKey.K); break; // Down
                    case 6: pressed = Raylib.IsKeyDown(KeyboardKey.J); break; // Left
                    case 7: pressed = Raylib.IsKeyDown(KeyboardKey.L); break; // Right
                    case 8: pressed = Raylib.IsKeyDown(KeyboardKey.V); break; // A
                    case 9: pressed = Raylib.IsKeyDown(KeyboardKey.G); break; // X
                    case 10: pressed = Raylib.IsKeyDown(KeyboardKey.U); break; // L
                    case 11: pressed = Raylib.IsKeyDown(KeyboardKey.O); break; // R
                }
            }

            return (short)(pressed ? 1 : 0);
        }

        public void InitAudioStream()
        {
            if (Raylib.IsAudioStreamReady(GameAudioStream)) Raylib.UnloadAudioStream(GameAudioStream);
            if (!Raylib.IsAudioDeviceReady()) Raylib.InitAudioDevice();
            
            GameAudioStream = Raylib.LoadAudioStream((uint)AVInfo.timing.sample_rate, 16, 2);
            Raylib.PlayAudioStream(GameAudioStream);
        }

        private UIntPtr AudioBatchCallbackImpl(IntPtr data, UIntPtr frames)
        {
            if (Raylib.IsAudioStreamReady(GameAudioStream))
            {
                unsafe
                {
                    Raylib.UpdateAudioStream(GameAudioStream, data.ToPointer(), (int)frames);
                }
            }
            return frames;
        }
    }
}
