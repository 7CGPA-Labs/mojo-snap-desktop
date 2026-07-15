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
            try
            {
                File.AppendAllText("crash.log", $"[{DateTime.Now}] Application Started\n");
                Run();
            }
            catch (Exception ex)
            {
                File.AppendAllText("crash.log", $"[{DateTime.Now}] FATAL CRASH:\n{ex}\n");
            }
        }

        static void Run()
        {
            Raylib.InitWindow(1280, 720, "Libretro Frontend");
            Raylib.SetTargetFPS(60);

            var ctx = ImGui.CreateContext();
            ImGui.SetCurrentContext(ctx);
            
            // Build the default font atlas to prevent C++ IM_ASSERT aborts on NewFrame
            ImGui.GetIO().Fonts.Build();

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
                            File.AppendAllText("crash.log", $"[{DateTime.Now}] Core Load Error: {ex.Message}\n");
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
                Raylib.EndDrawing();
            }

            ImGui.DestroyContext();
            Raylib.CloseWindow();
        }
    }
}
