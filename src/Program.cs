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
            Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
            Raylib.InitWindow(1280, 720, "Libretro Frontend");
            Raylib.SetTargetFPS(60);

            rlImGui_cs.rlImGui.Setup(true);

            var overlay = new PlayerOverlay();
            var coreManager = new CoreManager();
            
            Texture2D gameTexture = new Texture2D();
            bool textureInitialized = false;

            while (!Raylib.WindowShouldClose())
            {
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.DarkGray);
                
                if (overlay.CurrentState == ApplicationState.Gameplay)
                {
                    // Execute the Libretro core frame
                    coreManager.RunFrame();

                    if (coreManager.FrameData != IntPtr.Zero)
                    {
                        if (!textureInitialized || gameTexture.Width != coreManager.FrameWidth || gameTexture.Height != coreManager.FrameHeight)
                        {
                            if (textureInitialized) Raylib.UnloadTexture(gameTexture);
                            
                            Image img = Raylib.GenImageColor((int)coreManager.FrameWidth, (int)coreManager.FrameHeight, Color.Blank);
                            img.Format = PixelFormat.UncompressedR8G8B8A8; // XRGB8888
                            gameTexture = Raylib.LoadTextureFromImage(img);
                            Raylib.UnloadImage(img);
                            textureInitialized = true;
                        }

                        // Update GPU texture with native pixel buffer
                        unsafe
                        {
                            Raylib.UpdateTexture(gameTexture, (void*)coreManager.FrameData);
                        }
                    }

                    if (textureInitialized)
                    {
                        // Render centered and scaled
                        float scale = Math.Min((float)Raylib.GetScreenWidth() / gameTexture.Width, (float)Raylib.GetScreenHeight() / gameTexture.Height);
                        float targetW = gameTexture.Width * scale;
                        float targetH = gameTexture.Height * scale;
                        float offsetX = (Raylib.GetScreenWidth() - targetW) / 2.0f;
                        float offsetY = (Raylib.GetScreenHeight() - targetH) / 2.0f;
                        
                        Rectangle sourceRec = new Rectangle(0, 0, gameTexture.Width, gameTexture.Height);
                        Rectangle destRec = new Rectangle(offsetX, offsetY, targetW, targetH);
                        
                        Raylib.DrawTexturePro(gameTexture, sourceRec, destRec, Vector2.Zero, 0.0f, Color.White);
                    }
                }

                rlImGui_cs.rlImGui.Begin();

                if (overlay.CurrentState == ApplicationState.FileSelection)
                {
                    overlay.DrawSelectionScreen();
                    if (overlay.ShouldLoadRom)
                    {
                        try
                        {
                            string core = coreManager.MatchCoreToExtension(overlay.SelectedRomPath);
                            coreManager.LoadCore(core);
                            
                            if (coreManager.LoadGame(overlay.SelectedRomPath))
                            {
                                overlay.CurrentState = ApplicationState.Gameplay;
                            }
                            else
                            {
                                File.AppendAllText("crash.log", $"[{DateTime.Now}] Core rejected the ROM file.\n");
                            }
                            overlay.ShouldLoadRom = false;
                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText("crash.log", $"[{DateTime.Now}] Core Load Error: {ex.Message}\n");
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

                    overlay.DrawPlaybackControls(Raylib.GetFPS(), Raylib.GetFrameTime() * 1000f);
                }

                rlImGui_cs.rlImGui.End();
                Raylib.EndDrawing();
            }

            if (textureInitialized) Raylib.UnloadTexture(gameTexture);
            rlImGui_cs.rlImGui.Shutdown();
            Raylib.CloseWindow();
        }
    }
}
