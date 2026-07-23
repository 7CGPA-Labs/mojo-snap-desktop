# 🕹️ Mojo Snap Desktop

Mojo Snap Desktop is a high-performance C# frontend designed to run RetroArch WebAssembly cores natively via Native AOT, providing an out-of-the-box low-latency gaming experience for Windows and Linux.

---

## ⚡ Features

- **Native AOT Compilation:** Blazing fast startup times and minimal memory footprint thanks to .NET 9.0 Native AOT.
- **Cross-Platform:** Supports Windows and Linux targets.
- **Seamless Emulation:** Downloads nightly Libretro cores automatically during the build process, so you're always running the latest emulators.
- **Raylib & ImGui:** Leverages hardware-accelerated rendering through `Raylib-cs` and fluid user interfaces with `ImGui.NET`.

---

## 🚀 Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- On Linux, you must install `clang` and `zlib1g-dev` for AOT compilation:
  ```bash
  sudo apt-get update
  sudo apt-get install -y clang zlib1g-dev
  ```

### Build & Run

To build and run the application in Release mode:

```bash
dotnet run -c Release
```

To publish the self-contained native executable for your current platform:

**Windows:**
```bash
dotnet publish -c Release -r win-x64 /p:PublishAot=true
```

**Linux:**
```bash
dotnet publish -c Release -r linux-x64 /p:PublishAot=true
```

The output will be located in the `publish` directory.

---

## ⚙️ How It Works

Before the project compiles, an MSBuild `PrepareForPublish` hook fires off a script (PowerShell for Windows, Bash for Linux) that automatically connects to the Libretro buildbot. It downloads the necessary dynamic libraries (`.dll` or `.so`) for the retro cores (NES, SNES, Genesis, Gameboy, GBA, PSX, DOS) and places them in the `cores/` directory. The C# application then loads these cores to emulate games seamlessly.

---

## 📄 License

Distributed under the MIT License.
