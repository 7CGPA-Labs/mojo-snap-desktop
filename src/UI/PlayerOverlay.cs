using ImGuiNET;
using System;
using System.Numerics;

namespace EmuFrontend.UI
{
    public enum ApplicationState
    {
        FileSelection,
        Gameplay
    }

    public class PlayerOverlay
    {
        public ApplicationState CurrentState { get; set; } = ApplicationState.FileSelection;
        public string SelectedRomPath { get; private set; } = string.Empty;
        public bool ShouldLoadRom { get; set; } = false;
        
        public bool IsRecording { get; set; } = false;
        public bool VSync { get; set; } = true;
        public float MasterVolume { get; set; } = 1.0f;
        public bool IsMuted { get; set; } = false;
        
        public int SaveStateSlot { get; set; } = 0;
        public int AspectRatioSelection { get; set; } = 0;
        public bool GraphicSmoothing { get; set; } = false;
        
        public bool ShouldReset { get; set; } = false;
        public bool ShouldClose { get; set; } = false;
        public bool ShowSettings { get; set; } = false;
        public bool ShowControllerSettings { get; set; } = false;
        
        public bool IsPaused { get; set; } = false;
        public bool IsFastForward { get; set; } = false;
        public bool ShouldSaveState { get; set; } = false;
        public bool ShouldLoadState { get; set; } = false;
        public bool ShouldToggleFullscreen { get; set; } = false;
        
        private string cheatCodeInput = string.Empty;
        
        private string romPathInput = string.Empty;
        private string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private string[] currentDirs = Array.Empty<string>();
        private string[] currentFiles = Array.Empty<string>();
        private bool dirInitialized = false;

        private void RefreshDirectory()
        {
            try
            {
                currentDirs = System.IO.Directory.GetDirectories(currentDirectory);
                currentFiles = System.IO.Directory.GetFiles(currentDirectory);
            }
            catch { }
        }

        public void DrawSelectionScreen()
        {
            if (!dirInitialized)
            {
                RefreshDirectory();
                dirInitialized = true;
            }

            ImGui.SetNextWindowSize(new Vector2(600, 450), ImGuiCond.FirstUseEver);
            ImGui.Begin("Load ROM");
            
            ImGui.Text("Current Directory: " + currentDirectory);
            
            var drives = System.IO.DriveInfo.GetDrives();
            if (ImGui.BeginCombo("Drives", System.IO.Path.GetPathRoot(currentDirectory)))
            {
                foreach (var drive in drives)
                {
                    if (drive.IsReady)
                    {
                        if (ImGui.Selectable(drive.Name))
                        {
                            currentDirectory = drive.Name;
                            RefreshDirectory();
                        }
                    }
                }
                ImGui.EndCombo();
            }
            ImGui.SameLine();
            if (ImGui.Button(".. (Up)"))
            {
                var parent = System.IO.Directory.GetParent(currentDirectory);
                if (parent != null)
                {
                    currentDirectory = parent.FullName;
                    RefreshDirectory();
                }
            }

            ImGui.BeginChild("FileBrowser", new Vector2(0, 300), ImGuiChildFlags.Border);
            foreach (var dir in currentDirs)
            {
                if (ImGui.Selectable("[Folder] " + System.IO.Path.GetFileName(dir)))
                {
                    currentDirectory = dir;
                    RefreshDirectory();
                }
            }
            foreach (var file in currentFiles)
            {
                if (ImGui.Selectable(System.IO.Path.GetFileName(file)))
                {
                    romPathInput = file;
                }
            }
            ImGui.EndChild();

            ImGui.InputText("ROM Path", ref romPathInput, 512);
            if (ImGui.Button("Load ROM"))
            {
                SelectedRomPath = romPathInput;
                ShouldLoadRom = true;
            }
            ImGui.End();
        }

        private string? keyNamesComboStr = null;
        private Raylib_cs.KeyboardKey[] keyValues = null!;
        private string[] buttonNames = new string[] { "B", "Y", "Select", "Start", "Up", "Down", "Left", "Right", "A", "X", "L", "R" };

        public void DrawPlaybackControls(float fps, float frameTime, EmuFrontend.CoreInterop.CoreManager coreManager = null)
        {
            float windowWidth = ImGui.GetIO().DisplaySize.X;
            float windowHeight = ImGui.GetIO().DisplaySize.Y;
            float barHeight = 50;
            
            ImGui.SetNextWindowPos(new Vector2(10, 10));
            ImGui.SetNextWindowBgAlpha(0.3f);
            ImGui.Begin("PerfHUD", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoInputs);
            ImGui.TextColored(new Vector4(0.4f, 1.0f, 0.4f, 1.0f), $"FPS: {fps:0.0}");
            ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.2f, 1.0f), $"{frameTime:0.00}ms");
            if (IsRecording)
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "(REC)");
            }
            ImGui.End();

            bool isMouseNearBottom = ImGui.GetIO().MousePos.Y > windowHeight - 120;
            if (!isMouseNearBottom && !ShowSettings) return;

            ImGui.SetNextWindowPos(new Vector2(0, windowHeight - barHeight));
            ImGui.SetNextWindowSize(new Vector2(windowWidth, barHeight));
            
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.08f, 0.08f, 0.10f, 0.95f));
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.2f, 0.2f, 0.2f, 1.0f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10.0f, 5.0f));

            ImGui.Begin("Media Controls", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar);
            
            float rightSideWidth = 40 + 8 + 80 + 8 + 40 + 8 + 40 + 8 + 40 + 20; // 5 elements + spacing
            
            // Left Side Controls
            if (ImGui.Button("\uf01e", new Vector2(40, 40))) { ShouldReset = true; }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Reset Core");
            ImGui.SameLine();
            
            if (ImGui.Button(IsPaused ? "\uf04b" : "\uf04c", new Vector2(40, 40))) { IsPaused = !IsPaused; }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(IsPaused ? "Play" : "Pause");
            ImGui.SameLine();
            
            if (ImGui.Button("\uf0c7", new Vector2(40, 40))) { ShouldSaveState = true; }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Save State");
            ImGui.SameLine();
            
            if (ImGui.Button("\uf07c", new Vector2(40, 40))) { ShouldLoadState = true; }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Load State");
            ImGui.SameLine();
            
            if (ImGui.Button("\uf11b", new Vector2(40, 40))) { ShowControllerSettings = !ShowControllerSettings; }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Controller Settings");
            ImGui.SameLine();
            
            ImGui.SetNextItemWidth(80);
            float currentY = ImGui.GetCursorPosY();
            ImGui.SetCursorPosY(currentY + 10);
            int slot = SaveStateSlot;
            if (ImGui.Combo("##Slot", ref slot, "Slot 0\0Slot 1\0Slot 2\0Slot 3\0Slot 4\0")) SaveStateSlot = slot;
            ImGui.SetCursorPosY(currentY);
            ImGui.SameLine();
            
            ImGui.PushStyleColor(ImGuiCol.Button, IsFastForward ? new Vector4(0.8f, 0.4f, 0.0f, 1.0f) : new Vector4(0.2f, 0.2f, 0.2f, 1.0f));
            if (ImGui.Button("\uf050", new Vector2(40, 40))) { IsFastForward = !IsFastForward; }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Fast Forward");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            
            ImGui.PushStyleColor(ImGuiCol.Button, IsRecording ? new Vector4(0.8f, 0.1f, 0.1f, 1.0f) : new Vector4(0.2f, 0.2f, 0.2f, 1.0f));
            if (ImGui.Button("\uf111", new Vector2(40, 40))) { IsRecording = !IsRecording; }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Record");
            ImGui.PopStyleColor();

            // Right Side Controls
            ImGui.SameLine(windowWidth - rightSideWidth);
            
            if (ImGui.Button(IsMuted ? "\uf6a9" : "\uf028", new Vector2(40, 40))) { IsMuted = !IsMuted; }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(IsMuted ? "Unmute" : "Mute");
            ImGui.SameLine();
            
            ImGui.SetNextItemWidth(80);
            currentY = ImGui.GetCursorPosY();
            ImGui.SetCursorPosY(currentY + 10);
            float vol = MasterVolume;
            if (ImGui.SliderFloat("##Vol", ref vol, 0.0f, 1.0f, "")) MasterVolume = vol;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Volume: {(int)(MasterVolume * 100)}%");
            ImGui.SetCursorPosY(currentY);
            ImGui.SameLine();
            
            if (ImGui.Button("\uf013", new Vector2(40, 40)))
            {
                ImGui.OpenPopup("SettingsPopup");
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Settings");
            
            ImGui.SetNextWindowPos(new Vector2(windowWidth - 280, windowHeight - barHeight - 250), ImGuiCond.Appearing);
            if (ImGui.BeginPopup("SettingsPopup"))
            {
                if (ImGui.BeginMenu("Graphics Settings"))
                {
                    ImGui.MenuItem("Shaders", "Disabled");
                    ImGui.MenuItem("Hardware Acceleration", "Native AOT");
                    ImGui.MenuItem("FPS", "hide");
                    
                    bool vsync = VSync;
                    if (ImGui.MenuItem("VSync", vsync ? "Enabled" : "Disabled")) { VSync = !vsync; }
                    
                    ImGui.MenuItem("Video Rotation", "0 deg");
                    
                    bool smooth = GraphicSmoothing;
                    if (ImGui.MenuItem("Bilinear Filtering", smooth ? "Enabled" : "Disabled")) { GraphicSmoothing = !smooth; }
                    
                    ImGui.EndMenu();
                }
                
                if (ImGui.BeginMenu("Screen Capture"))
                {
                    ImGui.MenuItem("Screenshot Source", "Native Canvas");
                    ImGui.MenuItem("Screenshot Format", "png");
                    ImGui.MenuItem("Screenshot Upscale", "1x");
                    ImGui.MenuItem("Screen Recording FPS", "60");
                    ImGui.MenuItem("Screen Recording Format", "mp4");
                    ImGui.MenuItem("Screen Recording Upscale", "1x");
                    ImGui.EndMenu();
                }
                
                if (ImGui.BeginMenu("Speed Options"))
                {
                    ImGui.MenuItem("Fast Forward", "Disabled");
                    ImGui.MenuItem("Slow Motion", "Disabled");
                    ImGui.EndMenu();
                }
                
                if (ImGui.BeginMenu("Input Options"))
                {
                    ImGui.MenuItem("Menubar Mouse Trigger", "Downward Movement");
                    ImGui.MenuItem("Direct Keyboard Input", "Disabled");
                    ImGui.MenuItem("Forward Alt key", "Disabled");
                    ImGui.MenuItem("Lock Mouse", "Disabled");
                    ImGui.EndMenu();
                }
                
                if (ImGui.BeginMenu("Save States"))
                {
                    ImGui.MenuItem("Load State");
                    ImGui.MenuItem("Save State");
                    ImGui.MenuItem("Change Slot");
                    ImGui.EndMenu();
                }
                
                if (ImGui.BeginMenu("Backend Core Options"))
                {
                    ImGui.MenuItem("Core-specific settings for ROM");
                    ImGui.EndMenu();
                }
                
                ImGui.EndPopup();
            }
            ImGui.SameLine();
            
            if (ImGui.Button("\uf065", new Vector2(40, 40))) { ShouldToggleFullscreen = true; }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Fullscreen");
            ImGui.SameLine();
            
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.2f, 0.2f, 1.0f));
            if (ImGui.Button("\uf2f5", new Vector2(40, 40))) { ShouldClose = true; }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Close ROM");
            ImGui.PopStyleColor();

            ImGui.End();
            ImGui.PopStyleVar(3);
            ImGui.PopStyleColor(2);

            // Removed old Settings Drawer

            // Drawer Controller Settings
            if (ShowControllerSettings)
            {
                ImGui.SetNextWindowPos(new Vector2(380, ImGui.GetIO().DisplaySize.Y - barHeight - 420), ImGuiCond.Always);
                ImGui.SetNextWindowSize(new Vector2(350, 400), ImGuiCond.Always);
                ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.12f, 0.12f, 0.14f, 0.98f));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8.0f);
                
                bool showCtrl = ShowControllerSettings;
                if (ImGui.Begin("Controller Key Mapping", ref showCtrl, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove))
                {
                    if (coreManager != null)
                    {
                        if (keyNamesComboStr == null)
                        {
                            keyValues = (Raylib_cs.KeyboardKey[])Enum.GetValues(typeof(Raylib_cs.KeyboardKey));
                            keyNamesComboStr = string.Join('\0', Enum.GetNames(typeof(Raylib_cs.KeyboardKey))) + "\0";
                        }

                        if (ImGui.BeginTabBar("ControllerTabs"))
                        {
                            if (ImGui.BeginTabItem("Player 1"))
                            {
                                ImGui.Spacing();
                                for (int i = 0; i < 12; i++)
                                {
                                    int currentIdx = Array.IndexOf(keyValues, coreManager.P1Mappings[i]);
                                    if (currentIdx < 0) currentIdx = 0;
                                    if (ImGui.Combo(buttonNames[i], ref currentIdx, keyNamesComboStr))
                                    {
                                        coreManager.P1Mappings[i] = keyValues[currentIdx];
                                    }
                                }
                                ImGui.EndTabItem();
                            }
                            if (ImGui.BeginTabItem("Player 2"))
                            {
                                ImGui.Spacing();
                                for (int i = 0; i < 12; i++)
                                {
                                    int currentIdx = Array.IndexOf(keyValues, coreManager.P2Mappings[i]);
                                    if (currentIdx < 0) currentIdx = 0;
                                    if (ImGui.Combo(buttonNames[i], ref currentIdx, keyNamesComboStr))
                                    {
                                        coreManager.P2Mappings[i] = keyValues[currentIdx];
                                    }
                                }
                                ImGui.EndTabItem();
                            }
                            ImGui.EndTabBar();
                        }
                    }
                    else
                    {
                        ImGui.Text("Core Manager not available.");
                    }
                }
                ImGui.End();
                ShowControllerSettings = showCtrl;
                ImGui.PopStyleVar();
                ImGui.PopStyleColor();
            }
        }
    }
}
