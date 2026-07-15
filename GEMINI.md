/clear
# OBJECTIVE: Bootstrap a zero-dependency, hardware-accelerated, single-process cross-platform Libretro frontend in C# (.NET 9) using Raylib, ImGui.NET, and Native AOT. The application features an extension-matching core loader and an advanced EmulatorJS-inspired media control dashboard, including cheat management, video/audio configurations, core options mapping, and a native frame recording loop.

# 1. DIRECTORY TREE MANIFEST
Create the following file architecture in the current workspace:
├── EmuFrontend.csproj
└── src/
    ├── Program.cs
    ├── UI/
    │   └── PlayerOverlay.cs
    └── CoreInterop/
        └── CoreManager.cs

# 2. FILE SPECIFICATIONS & CODE GENERATION

## FILE 1: EmuFrontend.csproj
Generate the project file adhering strictly to these Native AOT parameters:
- Target: net9.0, Exe output.
- Tags required: <PublishAot>true</PublishAot>, <OptimizationPreference>Speed</OptimizationPreference>, <TrimMode>link</TrimMode>, <InvariantGlobalization>true</InvariantGlobalization>.
- Dependencies: Include package references for Raylib-cs (v6.0.0) and ImGui.NET (v1.90.0 or latest stable AOT-safe version).
- MSBuild Automation: Inject a custom target 'FetchCoresBeforeBuild' executing before targets 'PrepareForPublish;BeforeBuild'.
  - Base URL: https://buildbot.libretro.com/stable/1.22.2/[Platform]
  - Download and embed the 7 specific core files: fceumm, snes9x, genesis_plus_gx, gambatte, mgba, pcsx_rearmed, dosbox_pure.

## FILE 2: src/CoreInterop/CoreManager.cs
Extend the assembly resource extractor to handle core routing and unmanaged configuration callbacks:
- Add a public method MatchCoreToExtension(string romPath) that evaluates the file extension string and returns the corresponding core key name.
- Include data structures and interop structures to interface with the core's 'retro_cheat_set' and 'retro_reset' function pointers.
- Implement a dictionary-backed config loader to parse game-specific configuration options string records (.cfg files matching the ROM name) and feed them to the core via the environment variable verification callback hook.

## FILE 3: src/UI/PlayerOverlay.cs
Write the expanded UI management layout using ImGui.NET commands:
- Application States: Define an enum with states: 'FileSelection' and 'Gameplay'.
- DrawSelectionScreen(): Render a clean startup landing pane containing a file path input string field and a 'Load ROM' action button.
- DrawPlaybackControls(): Render the comprehensive control interface panels surrounding and overlaying the central game container:
  - Top/Side Settings Drawer: Implement structured ImGui tabs for the following configurations:
    - Video Settings: VSync checkbox, brightness/gamma scaling sliders, texture filter parameters.
    - Audio Settings: Master volume output scale (0.0f to 1.0f), audio mute state, latency buffer sample thresholds.
    - Core/Game Options: Dynamically rendered checkboxes and dropdown menus mapped to current core options (e.g., color palettes, internal clocks).
    - Cheat Code Manager: Text entry box for adding active codes, a dynamic list interface tracking active codes, and buttons for 'Apply Cheat' and 'Clear All'.
  - Performance HUD Overlay: A small transparent panel in the viewport corner displaying Raylib FPS, frame times, and a flashing red indicator if recording is active.
  - Bottom Control Bar: Render persistent media and engine widgets:
    - Play / Pause state toggle button.
    - Fast Forward toggle button (adjusts internal engine iteration loops).
    - Screen recording toggle button (toggles active frame buffer streaming state).
    - Save State / Load State action button block alongside an integer Slot selector dropdown (0-4).
    - Screen Aspect Ratio dropdown combo selector (Options: 4:3 Original, 16:9 Stretch, Integer Scaling).
    - Graphic Smoothing checkbox (Bilinear filtering flag toggle).
    - Screenshot snapshot camera button.
    - Core Reset (triggers retro_reset execution) and Close Game utility controls.

## FILE 4: src/Program.cs
Write the primary loop orchestration bridging Raylib textures, ImGui frames, keyboard inputs, and state changes:
- Architecture Constraints: Apply the [System.STAThread] structural command block.
- Loop Initialization: Initialize a 1280x720 window running at a locked 60 FPS baseline. Setup the ImGui controller framework context within the Raylib loop window lifecycle.
- Video Recording System: Initialize a safe byte buffer array to cache raw frame data from Raylib's RenderTexture2D viewport. If the recording flag inside PlayerOverlay is enabled, capture the frame pixels using an optimized task thread loop, writing to an output file stream.
- State Machine Processing:
  - If state is 'FileSelection', render PlayerOverlay.DrawSelectionScreen(). Once a path is routed, pass the string through CoreManager.MatchCoreToExtension(), extract the target binary, initialize the NativeLibrary load sequence, load the game, and transition the engine state to 'Gameplay'.
  - If state is 'Gameplay', poll keyboard buttons maps locally, process the active frame iteration inside the single process framework, and render the core output texture inside the center viewport frame.
  - Apply the adjustments dictated by PlayerOverlay interface options dynamically:
    - Intercept Cheat actions to invoke the core's native cheat management bindings.
    - Intercept Reset buttons to invoke native core reset functions instantly.
    - Adjust the drawing vectors inside Raylib.DrawTexturePro based on the selected Aspect Ratio state.
    - Toggle texture filter attributes dynamically between Point and Bilinear sampling modes based on the Graphic Smoothing check state.
    - Render the full ImGui media control array via PlayerOverlay.DrawPlaybackControls().

# 3. VERIFICATION EXECUTION
After creating all assets, execute a workspace verification check using the terminal command `dotnet build` to guarantee proper AOT structure bindings and complete compilation success.