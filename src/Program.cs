using System;
using System.IO;
using Raylib_cs;
using ImGuiNET;
using EmuFrontend.UI;
using EmuFrontend.CoreInterop;
using System.Numerics;

namespace EmuFrontend
{
    class Program
    {
        [System.STAThread]
        static void Main(string[] args)
        {
            Raylib.InitWindow(1280, 720, "Libretro Frontend");
            Raylib.SetTargetFPS(60);

            // Initialize ImGui Context to prevent crashes when ImGui methods are called
            var ctx = ImGui.CreateContext();
            ImGui.SetCurrentContext(ctx);

            var overlay = new PlayerOverlay();
            var coreManager = new CoreManager();

            while (!Raylib.WindowShouldClose())
            {
                var io = ImGui.GetIO();
                io.DisplaySize = new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
                io.DeltaTime = Raylib.GetFrameTime() > 0 ? Raylib.GetFrameTime() : 1.0f / 60.0f;
                
                ImGui.NewFrame();

                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.Black);

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

                    if (overlay.IsRecording)
                    {
                        // Recording Logic Placeholder
                    }

                    overlay.DrawPlaybackControls(Raylib.GetFPS(), Raylib.GetFrameTime() * 1000f);
                }

                ImGui.Render();
                // Note: A full ImGui-to-Raylib rendering backend loop is required here to visually draw the ImGui data to the screen.

                Raylib.EndDrawing();
            }

            ImGui.DestroyContext();
            Raylib.CloseWindow();
        }
    }
}
