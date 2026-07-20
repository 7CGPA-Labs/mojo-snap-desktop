using System;
using System.IO;
using System.Runtime.InteropServices;
using Raylib_cs;
using System.Collections.Concurrent;

namespace EmuFrontend.CoreInterop
{
    public class CoreManager
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void retro_init_t();
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
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate UIntPtr retro_serialize_size_t();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate bool retro_serialize_t(IntPtr data, UIntPtr size);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate bool retro_unserialize_t(IntPtr data, UIntPtr size);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void retro_cheat_reset_t();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void retro_cheat_set_t(uint index, bool enabled, [MarshalAs(UnmanagedType.LPStr)] string code);

        [StructLayout(LayoutKind.Sequential)]
        public struct retro_game_info { public IntPtr path; public IntPtr data; public UIntPtr size; public IntPtr meta; }

        [StructLayout(LayoutKind.Sequential)]
        public struct retro_system_timing { public double fps; public double sample_rate; }

        [StructLayout(LayoutKind.Sequential)]
        public struct retro_game_geometry { public uint base_width; public uint base_height; public uint max_width; public uint max_height; public float aspect_ratio; }

        [StructLayout(LayoutKind.Sequential)]
        public struct retro_system_av_info { public retro_game_geometry geometry; public retro_system_timing timing; }
        
        [StructLayout(LayoutKind.Sequential)]
        public struct retro_variable { public IntPtr key; public IntPtr value; }

        public class CoreOption
        {
            public string Key { get; set; } = "";
            public string Description { get; set; } = "";
            public System.Collections.Generic.List<string> Choices { get; set; } = new System.Collections.Generic.List<string>();
            public string CurrentValue { get; set; } = "";
            public IntPtr UnmanagedValuePtr { get; set; } = IntPtr.Zero;
        }
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void retro_get_system_av_info_t(ref retro_system_av_info info);

        public retro_init_t? RetroInit;
        public retro_run_t? RetroRun;
        public retro_load_game_t? RetroLoadGame;
        public retro_reset_t? RetroReset;
        public retro_serialize_size_t? RetroSerializeSize;
        public retro_serialize_t? RetroSerialize;
        public retro_unserialize_t? RetroUnserialize;
        public retro_cheat_reset_t? RetroCheatReset;
        public retro_cheat_set_t? RetroCheatSet;

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

        public string CurrentCoreName { get; set; } = "";
        public event Action<string> OnCoreLoaded;

        private IntPtr coreHandle;

        public string MatchCoreToExtension(string romPath)
        {
            string ext = Path.GetExtension(romPath).ToLower();
            return ext switch
            {
                ".nes" => "fceumm",
                ".smc" or ".sfc" => "snes9x",
                ".md" or ".sms" or ".gg" => "genesis_plus_gx",
                ".gb" or ".gbc" => "gambatte",
                ".gba" => "mgba",
                ".cue" or ".iso" or ".img" => "pcsx_rearmed",
                ".exe" or ".bat" or ".com" or ".zip" or ".dos" => "dosbox_pure",
                _ => throw new Exception($"No core found for extension {ext}")
            };
        }

        public void LoadCore(string coreName)
        {
            CurrentCoreName = coreName;
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
            RetroRun = GetExport<retro_run_t>("retro_run");
            RetroLoadGame = GetExport<retro_load_game_t>("retro_load_game");
            RetroReset = GetExport<retro_reset_t>("retro_reset");
            RetroGetSystemAvInfo = GetExport<retro_get_system_av_info_t>("retro_get_system_av_info");
            RetroSerializeSize = GetExport<retro_serialize_size_t>("retro_serialize_size");
            RetroSerialize = GetExport<retro_serialize_t>("retro_serialize");
            RetroUnserialize = GetExport<retro_unserialize_t>("retro_unserialize");
            RetroCheatReset = GetExport<retro_cheat_reset_t>("retro_cheat_reset");
            RetroCheatSet = GetExport<retro_cheat_set_t>("retro_cheat_set");

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
            
            OnCoreLoaded?.Invoke(coreName);
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

        public void SaveState(int slot)
        {
            if (RetroSerializeSize == null || RetroSerialize == null) return;
            UIntPtr size = RetroSerializeSize();
            if (size == UIntPtr.Zero) return;
            
            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            if (RetroSerialize(buffer, size))
            {
                byte[] data = new byte[(int)size];
                Marshal.Copy(buffer, data, 0, (int)size);
                File.WriteAllBytes($"state_{slot}.sav", data);
                Logger.Info($"Saved state to slot {slot}");
            }
            Marshal.FreeHGlobal(buffer);
        }

        public void LoadState(int slot)
        {
            if (RetroUnserialize == null || !File.Exists($"state_{slot}.sav")) return;
            
            byte[] data = File.ReadAllBytes($"state_{slot}.sav");
            IntPtr buffer = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, buffer, data.Length);
            
            if (RetroUnserialize(buffer, (UIntPtr)data.Length))
            {
                Logger.Info($"Loaded state from slot {slot}");
            }
            Marshal.FreeHGlobal(buffer);
        }

        public System.Collections.Generic.List<CoreOption> CoreOptions { get; private set; } = new System.Collections.Generic.List<CoreOption>();
        public bool VariablesUpdated { get; set; } = false;

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
            else if (cmd == 16) // RETRO_ENVIRONMENT_SET_VARIABLES
            {
                CoreOptions.Clear();
                if (data != IntPtr.Zero)
                {
                    IntPtr current = data;
                    while (true)
                    {
                        var v = Marshal.PtrToStructure<retro_variable>(current);
                        if (v.key == IntPtr.Zero || v.value == IntPtr.Zero) break;
                        
                        string key = Marshal.PtrToStringUTF8(v.key) ?? "";
                        string val = Marshal.PtrToStringUTF8(v.value) ?? "";
                        
                        var parts = val.Split(';');
                        if (parts.Length >= 2)
                        {
                            var opt = new CoreOption();
                            opt.Key = key;
                            opt.Description = parts[0].Trim();
                            var choices = parts[1].Trim().Split('|');
                            opt.Choices.AddRange(choices);
                            if (choices.Length > 0) opt.CurrentValue = choices[0];
                            CoreOptions.Add(opt);
                        }
                        
                        current = IntPtr.Add(current, Marshal.SizeOf<retro_variable>());
                    }
                }
                return true;
            }
            else if (cmd == 15) // RETRO_ENVIRONMENT_GET_VARIABLE
            {
                if (data == IntPtr.Zero) return false;
                var v = Marshal.PtrToStructure<retro_variable>(data);
                if (v.key != IntPtr.Zero)
                {
                    string key = Marshal.PtrToStringUTF8(v.key) ?? "";
                    var opt = CoreOptions.Find(o => o.Key == key);
                    if (opt != null && !string.IsNullOrEmpty(opt.CurrentValue))
                    {
                        if (opt.UnmanagedValuePtr != IntPtr.Zero) Marshal.FreeCoTaskMem(opt.UnmanagedValuePtr);
                        opt.UnmanagedValuePtr = Marshal.StringToCoTaskMemUTF8(opt.CurrentValue);
                        v.value = opt.UnmanagedValuePtr;
                        Marshal.StructureToPtr(v, data, false);
                        return true;
                    }
                }
                return false;
            }
            else if (cmd == 17) // RETRO_ENVIRONMENT_GET_VARIABLE_UPDATED
            {
                if (data != IntPtr.Zero)
                {
                    Marshal.WriteByte(data, VariablesUpdated ? (byte)1 : (byte)0);
                    VariablesUpdated = false;
                    return true;
                }
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

        public KeyboardKey[] P1Mappings = new KeyboardKey[12] 
        {
            KeyboardKey.Z, KeyboardKey.A, KeyboardKey.RightShift, KeyboardKey.Enter,
            KeyboardKey.Up, KeyboardKey.Down, KeyboardKey.Left, KeyboardKey.Right,
            KeyboardKey.X, KeyboardKey.S, KeyboardKey.Q, KeyboardKey.W
        };

        public KeyboardKey[] P2Mappings = new KeyboardKey[12] 
        {
            KeyboardKey.C, KeyboardKey.F, KeyboardKey.Tab, KeyboardKey.Space,
            KeyboardKey.I, KeyboardKey.K, KeyboardKey.J, KeyboardKey.L,
            KeyboardKey.V, KeyboardKey.G, KeyboardKey.U, KeyboardKey.O
        };

        public bool[] VirtualP1Buttons = new bool[16];
        public bool[] VirtualP2Buttons = new bool[16];


        private short InputStateCallbackImpl(uint port, uint device, uint index, uint id)
        {
            if (device != 1) return 0; // Only Joypad
            
            bool pressed = false;
            
            if (port == 0 && id < 16)
            {
                pressed = VirtualP1Buttons[id] || 
                          (id < 12 && Raylib.IsKeyDown(P1Mappings[id])) ||
                          IsPhysicalGamepadButtonPressed(0, id);
            }
            else if (port == 1 && id < 16)
            {
                pressed = VirtualP2Buttons[id] || 
                          (id < 12 && Raylib.IsKeyDown(P2Mappings[id])) ||
                          IsPhysicalGamepadButtonPressed(1, id);
            }

            return (short)(pressed ? 1 : 0);
        }

        private bool IsPhysicalGamepadButtonPressed(int gamepadPort, uint libretroId)
        {
            if (!Raylib.IsGamepadAvailable(gamepadPort)) return false;

            return libretroId switch
            {
                0 => Raylib.IsGamepadButtonDown(gamepadPort, GamepadButton.RightFaceRight), // B (Circle/B)
                1 => Raylib.IsGamepadButtonDown(gamepadPort, GamepadButton.RightFaceUp),    // Y (Triangle/Y)
                2 => Raylib.IsGamepadButtonDown(gamepadPort, GamepadButton.MiddleLeft),     // Select
                3 => Raylib.IsGamepadButtonDown(gamepadPort, GamepadButton.MiddleRight),    // Start
                4 => Raylib.IsGamepadButtonDown(gamepadPort, GamepadButton.LeftFaceUp),     // Up
                5 => Raylib.IsGamepadButtonDown(gamepadPort, GamepadButton.LeftFaceDown),   // Down
                6 => Raylib.IsGamepadButtonDown(gamepadPort, GamepadButton.LeftFaceLeft),   // Left
                7 => Raylib.IsGamepadButtonDown(gamepadPort, GamepadButton.LeftFaceRight),  // Right
                8 => Raylib.IsGamepadButtonDown(gamepadPort, GamepadButton.RightFaceDown),  // A (Cross/A)
                9 => Raylib.IsGamepadButtonDown(gamepadPort, GamepadButton.RightFaceLeft),  // X (Square/X)
                10 => Raylib.IsGamepadButtonDown(gamepadPort, GamepadButton.LeftTrigger1),  // L1
                11 => Raylib.IsGamepadButtonDown(gamepadPort, GamepadButton.RightTrigger1), // R1
                12 => Raylib.IsGamepadButtonDown(gamepadPort, GamepadButton.LeftTrigger2),  // L2
                13 => Raylib.IsGamepadButtonDown(gamepadPort, GamepadButton.RightTrigger2), // R2
                14 => Raylib.IsGamepadButtonDown(gamepadPort, GamepadButton.LeftThumb),     // L3
                15 => Raylib.IsGamepadButtonDown(gamepadPort, GamepadButton.RightThumb),    // R3
                _ => false
            };
        }

        private ConcurrentQueue<short> audioQueue = new ConcurrentQueue<short>();

        public void InitAudioStream()
        {
            if (Raylib.IsAudioStreamReady(GameAudioStream)) Raylib.UnloadAudioStream(GameAudioStream);
            
            Raylib.SetAudioStreamBufferSizeDefault(4096);
            if (!Raylib.IsAudioDeviceReady()) Raylib.InitAudioDevice();
            
            GameAudioStream = Raylib.LoadAudioStream((uint)AVInfo.timing.sample_rate, 16, 2);
            Raylib.PlayAudioStream(GameAudioStream);
            while (audioQueue.TryDequeue(out _)) { }
        }

        private UIntPtr AudioBatchCallbackImpl(IntPtr data, UIntPtr frames)
        {
            unsafe
            {
                short* src = (short*)data.ToPointer();
                int count = (int)frames * 2;
                for (int i = 0; i < count; i++)
                {
                    audioQueue.Enqueue(src[i]);
                }
            }
            return frames;
        }

        public void UpdateAudio()
        {
            if (Raylib.IsAudioStreamReady(GameAudioStream) && Raylib.IsAudioStreamProcessed(GameAudioStream))
            {
                int samplesNeeded = 4096 * 2; 
                if (audioQueue.Count >= samplesNeeded)
                {
                    short[] chunk = new short[samplesNeeded];
                    for (int i = 0; i < samplesNeeded; i++)
                    {
                        audioQueue.TryDequeue(out chunk[i]);
                    }
                    unsafe
                    {
                        fixed (short* ptr = chunk)
                        {
                            Raylib.UpdateAudioStream(GameAudioStream, ptr, 4096);
                        }
                    }
                }
                
                while (audioQueue.Count > 4096 * 4)
                {
                    audioQueue.TryDequeue(out _);
                }
            }
        }
    }
}
