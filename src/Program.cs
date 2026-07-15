using System;
using System.IO;
using Raylib_cs;
using ImGuiNET;
using EmuFrontend.UI;
using EmuFrontend.CoreInterop;

namespace EmuFrontend
{
    class Program
    {
        [System.STAThread]
        static void Main(string[] args)
        {
            Raylib.InitWindow(1280, 720, "Libretro Frontend");
            Raylib.SetTargetFPS(60);

            // Note: In a full integration, rlImGui setup would happen here
            // rlImGui.Setup(true);

            var overlay = new PlayerOverlay();
            var coreManager = new CoreManager();

            while (!Raylib.WindowShouldClose())
            {
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.Black);
                
                // rlImGui.Begin();

                if (overlay.CurrentState == ApplicationState.FileSelection)
                {
                    overlay.DrawSelectionScreen();
                    if (overlay.ShouldLoadRom)
                    {
                        try
                        {
                            string core = coreManager.MatchCoreToExtension(overlay.SelectedRomPath);
                            coreManager.LoadCore(core);
                            coreManager.LoadConfig(overlay.SelectedRomPath);
                            
                            overlay.CurrentState = ApplicationState.Gameplay;
                            overlay.ShouldLoadRom = false;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                            overlay.ShouldLoadRom = false;
                        }
                    }
                }
                else if (overlay.CurrentState == ApplicationState.Gameplay)
                {
                    // Intercept Reset buttons
                    if (overlay.ShouldReset)
                    {
                        coreManager.RetroReset?.Invoke();
                        overlay.ShouldReset = false;
                    }
                    if (overlay.ShouldClose)
                    {
                        overlay.CurrentState = ApplicationState.FileSelection;
                        overlay.ShouldClose = false;
                    }

                    // Render game texture logic based on Aspect Ratio and Graphic Smoothing
                    // (Placeholder for core output rendering)
                    
                    // Recording Logic Placeholder
                    if (overlay.IsRecording)
                    {
                        // Safely capture byte buffer array from Raylib's RenderTexture2D
                        // Write to output stream via optimized thread
                    }

                    // Render full ImGui media control array
                    overlay.DrawPlaybackControls(Raylib.GetFPS(), Raylib.GetFrameTime() * 1000f);
                }

                // rlImGui.End();
                Raylib.EndDrawing();
            }

            // rlImGui.Shutdown();
            Raylib.CloseWindow();
        }
    }
}
