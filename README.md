# Mojo Snap Desktop

Mojo Snap Desktop is a zero-dependency, hardware-accelerated, single-process cross-platform Libretro frontend written in C# (.NET 9). It leverages **Raylib** for rendering and audio, **ImGui.NET** for a premium sleek media dashboard (inspired by EmulatorJS), and is fully compiled ahead-of-time using **Native AOT** for maximum performance and a minimal footprint.

## Features

- **Native AOT Compiled:** Produces a single, highly optimized native executable. No .NET runtime installation required.
- **Libretro Core Integration:** Supports unmanaged interop with Libretro cores (e.g., FCEUmm, Snes9x, Genesis Plus GX, Gambatte, mGBA, PCSX ReARMed, DOSBox Pure).
- **Dynamic Rate Control:** Advanced C#-based decoupled audio ring buffering seamlessly paces the core emulation speed based on the audio buffer capacity, eliminating video stuttering and audio desync.
- **EmulatorJS-Inspired UI:** A premium, fully integrated ImGui dashboard offering playback controls, advanced video/audio settings, save states, and more.
- **Cross-Drive File Browser:** Quickly navigate through system drives and USB thumb drives directly within the frontend.
- **Virtual Controller Ecosystem:** Features a dual TCP/UDP architecture (UDP for ultra-fast local LAN inputs, TCP WebSocket for WAN fallback) using mDNS for zero-configuration discovery, allowing mobile apps to serve as low-latency controllers.

## Getting Started

### Prerequisites
- .NET 9.0 SDK (with Native AOT workload installed)
- C++ Build Tools (for AOT compilation)

### Building
Clone the repository and execute the standard build command. The custom MSBuild targets will automatically fetch the required Libretro cores from the buildbot before compilation.

```bash
dotnet publish -c Release
```

### Running the Emulator
Once compiled, you can launch the executable directly. Use the built-in file browser to navigate your drives and select a valid ROM. The engine will automatically match the ROM extension to the correct embedded Libretro core and boot the game.

## Project Structure
- `EmuFrontend.csproj:` Manages AOT directives and auto-downloads the Libretro cores.
- `src/Program.cs:` Orchestrates the primary Raylib execution, fixed-timestep pacing, and texture manipulation.
- `src/CoreInterop/CoreManager.cs:` Interacts with Libretro's unmanaged C API using function pointers, managing audio callbacks, environment variables, and video refresh streams.
- `src/UI/PlayerOverlay.cs:` Renders the EmulatorJS-styled ImGui dashboard over the viewport.
