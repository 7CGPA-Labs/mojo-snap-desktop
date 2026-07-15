using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace EmuFrontend.CoreInterop
{
    public class CoreManager
    {
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

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void RetroResetDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public delegate void RetroCheatSetDelegate(uint index, bool enabled, string code);

        public RetroResetDelegate? RetroReset { get; private set; }
        public RetroCheatSetDelegate? RetroCheatSet { get; private set; }

        private IntPtr coreHandle;
        private Dictionary<string, string> configMap = new();

        public void LoadCore(string coreName)
        {
            string corePath = $"cores/{coreName}_libretro.dll";
            if (!NativeLibrary.TryLoad(corePath, out coreHandle))
            {
                throw new Exception($"Failed to load core {coreName} from {corePath}");
            }
            
            if (NativeLibrary.TryGetExport(coreHandle, "retro_reset", out IntPtr resetPtr))
                RetroReset = Marshal.GetDelegateForFunctionPointer<RetroResetDelegate>(resetPtr);

            if (NativeLibrary.TryGetExport(coreHandle, "retro_cheat_set", out IntPtr cheatPtr))
                RetroCheatSet = Marshal.GetDelegateForFunctionPointer<RetroCheatSetDelegate>(cheatPtr);
        }

        public void LoadConfig(string romPath)
        {
            configMap.Clear();
            string cfgPath = Path.ChangeExtension(romPath, ".cfg");
            if (File.Exists(cfgPath))
            {
                foreach (var line in File.ReadAllLines(cfgPath))
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                        configMap[parts[0].Trim()] = parts[1].Trim();
                }
            }
        }

        public bool EnvironmentCallback(uint cmd, IntPtr data)
        {
            // Dummy hook for env var verifications
            return false;
        }
    }
}
