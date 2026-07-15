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

        public void DrawPlaybackControls(float fps, float frameTime)
        {
            // EmulatorJS-inspired sleek bottom bar
            ImGui.SetNextWindowPos(new Vector2(0, ImGui.GetIO().DisplaySize.Y - 60));
            ImGui.SetNextWindowSize(new Vector2(ImGui.GetIO().DisplaySize.X, 60));
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.05f, 0.05f, 0.05f, 0.85f));
            
            ImGui.Begin("Media Controls", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar);
            
            if (ImGui.Button("Settings", new Vector2(100, 40))) { ShowSettings = !ShowSettings; }
            ImGui.SameLine();
            if (ImGui.Button("Reset Core", new Vector2(100, 40))) { ShouldReset = true; }
            ImGui.SameLine();
            if (ImGui.Button("Close Game", new Vector2(100, 40))) { ShouldClose = true; }

            ImGui.SameLine(ImGui.GetWindowWidth() - 250);
            ImGui.TextColored(new Vector4(0, 1, 0, 1), $"FPS: {fps:0.0}");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1, 1, 0, 1), $"{frameTime:0.00}ms");

            ImGui.End();
            ImGui.PopStyleColor();

            if (ShowSettings)
            {
                ImGui.SetNextWindowPos(new Vector2(50, 50), ImGuiCond.FirstUseEver);
                ImGui.SetNextWindowSize(new Vector2(400, 300), ImGuiCond.FirstUseEver);
                ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.1f, 0.1f, 0.1f, 0.95f));
                ImGui.Begin("Advanced Settings", ref ShowSettings);
                
                bool vsync = VSync;
                if (ImGui.Checkbox("VSync", ref vsync)) VSync = vsync;
                
                float vol = MasterVolume;
                ImGui.SliderFloat("Master Volume", ref vol, 0.0f, 1.0f);
                MasterVolume = vol;

                bool smooth = GraphicSmoothing;
                ImGui.Checkbox("Graphic Smoothing", ref smooth);
                GraphicSmoothing = smooth;

                ImGui.End();
                ImGui.PopStyleColor();
            }
        }
    }
}
