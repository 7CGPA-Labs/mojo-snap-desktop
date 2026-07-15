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
        public int AspectRatioSelection { get; set; } = 0; // 0: 4:3, 1: 16:9, 2: Integer
        public bool GraphicSmoothing { get; set; } = false;
        
        public bool ShouldReset { get; set; } = false;
        public bool ShouldClose { get; set; } = false;
        
        private string romPathInput = string.Empty;
        private string cheatInput = string.Empty;

        public void DrawSelectionScreen()
        {
            ImGui.Begin("Startup");
            ImGui.InputText("ROM Path", ref romPathInput, 512);
            if (ImGui.Button("Load ROM"))
            {
                SelectedRomPath = romPathInput;
                ShouldLoadRom = true;
            }
            ImGui.End();
        }

        public void DrawPlaybackControls(float fps, float frameTime)
        {
            ImGui.Begin("Settings Drawer");
            if (ImGui.BeginTabBar("SettingsTabs"))
            {
                if (ImGui.BeginTabItem("Video Settings"))
                {
                    bool vsync = VSync;
                    if (ImGui.Checkbox("VSync", ref vsync)) VSync = vsync;
                    // Add brightness/gamma sliders here
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Audio Settings"))
                {
                    float vol = MasterVolume;
                    ImGui.SliderFloat("Master Volume", ref vol, 0.0f, 1.0f);
                    MasterVolume = vol;
                    
                    bool muted = IsMuted;
                    ImGui.Checkbox("Mute", ref muted);
                    IsMuted = muted;
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Core Options"))
                {
                    ImGui.Text("Dynamic core options mapped here.");
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Cheat Manager"))
                {
                    ImGui.InputText("Add Cheat", ref cheatInput, 256);
                    if (ImGui.Button("Apply Cheat")) { }
                    ImGui.SameLine();
                    if (ImGui.Button("Clear All")) { }
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
            ImGui.End();

            // Performance HUD Overlay
            ImGui.SetNextWindowPos(new Vector2(10, 10));
            ImGui.Begin("HUD", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoBackground);
            ImGui.Text($"FPS: {fps:0.0}");
            ImGui.Text($"FrameTime: {frameTime:0.00} ms");
            if (IsRecording)
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "RECORDING");
            }
            ImGui.End();

            // Bottom Control Bar
            ImGui.Begin("Media Controls", ImGuiWindowFlags.AlwaysAutoResize);
            if (ImGui.Button("Play / Pause")) { }
            ImGui.SameLine();
            if (ImGui.Button("Fast Forward")) { }
            ImGui.SameLine();
            
            bool rec = IsRecording;
            if (ImGui.Checkbox("Record Screen", ref rec)) IsRecording = rec;

            ImGui.SameLine();
            if (ImGui.Button("Save State")) { }
            ImGui.SameLine();
            if (ImGui.Button("Load State")) { }
            ImGui.SameLine();
            
            int slot = SaveStateSlot;
            ImGui.Combo("Slot", ref slot, "Slot 0\0Slot 1\0Slot 2\0Slot 3\0Slot 4\0");
            SaveStateSlot = slot;

            int ar = AspectRatioSelection;
            ImGui.Combo("Aspect Ratio", ref ar, "4:3 Original\0 16:9 Stretch\0 Integer Scaling\0");
            AspectRatioSelection = ar;

            bool smooth = GraphicSmoothing;
            ImGui.Checkbox("Graphic Smoothing", ref smooth);
            GraphicSmoothing = smooth;

            if (ImGui.Button("Screenshot")) { }
            ImGui.SameLine();
            if (ImGui.Button("Reset Core")) { ShouldReset = true; }
            ImGui.SameLine();
            if (ImGui.Button("Close Game")) { ShouldClose = true; }
            ImGui.End();
        }
    }
}
