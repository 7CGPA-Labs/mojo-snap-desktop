using System;
using System.IO;
using System.Runtime.InteropServices;

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
        public struct retro_game_info
        {
            [MarshalAs(UnmanagedType.LPUTF8Str)] public string path;
            public IntPtr data;
            public UIntPtr size;
            [MarshalAs(UnmanagedType.LPUTF8Str)] public string meta;
        }

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
        public UIntPtr FramePitch { get; private set; }

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
            if (!NativeLibrary.TryLoad(corePath, out coreHandle))
                throw new Exception($"Failed to load core from {corePath}");

            RetroInit = GetExport<retro_init_t>("retro_init");
            RetroDeinit = GetExport<retro_deinit_t>("retro_deinit");
            RetroRun = GetExport<retro_run_t>("retro_run");
            RetroLoadGame = GetExport<retro_load_game_t>("retro_load_game");
            RetroReset = GetExport<retro_reset_t>("retro_reset");

            var setEnv = GetExport<retro_set_environment_t>("retro_set_environment");
            var setVideo = GetExport<retro_set_video_refresh_t>("retro_set_video_refresh");
            var setAudio = GetExport<retro_set_audio_sample_t>("retro_set_audio_sample");
            var setAudioBatch = GetExport<retro_set_audio_sample_batch_t>("retro_set_audio_sample_batch");
            var setInputPoll = GetExport<retro_set_input_poll_t>("retro_set_input_poll");
            var setInputState = GetExport<retro_set_input_state_fn>("retro_set_input_state");

            EnvCallback = EnvironmentCallback;
            VideoCallback = VideoRefreshCallback;
            AudioCallback = (l, r) => { };
            AudioBatchCallback = (data, frames) => frames;
            InputPollCallback = () => { };
            InputStateCallback = (port, device, index, id) => 0;

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

        public bool LoadGame(string path)
        {
            var info = new retro_game_info { path = path, data = IntPtr.Zero, size = UIntPtr.Zero, meta = "" };
            return RetroLoadGame?.Invoke(ref info) ?? false;
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
                Marshal.WriteInt32(data, 1); // 1 = XRGB8888
                return true; 
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
    }
}
