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
            Logger.Initialize();
            try
            {
                Logger.Info("Application Started");
                Run();
            }
            catch (Exception ex)
            {
                Logger.Error($"FATAL CRASH:\n{ex}");
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
            uint[] pixelBuffer = Array.Empty<uint>();
            double accumulator = 0.0;

            while (!Raylib.WindowShouldClose())
            {
                double dt = Raylib.GetFrameTime();
                
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.DarkGray);
                
                if (overlay.CurrentState == ApplicationState.Gameplay)
                {
                    double targetFrameTime = 1.0 / coreManager.AVInfo.timing.fps;
                    accumulator += dt;
                    if (accumulator > 0.1) accumulator = 0.1;

                    while (accumulator >= targetFrameTime)
                    {
                        coreManager.RunFrame();
                        accumulator -= targetFrameTime;
                    }

                    if (coreManager.FrameData != IntPtr.Zero)
                    {
                        if (!textureInitialized || gameTexture.Width != coreManager.FrameWidth || gameTexture.Height != coreManager.FrameHeight)
                        {
                            if (textureInitialized) Raylib.UnloadTexture(gameTexture);
                            
                            Image img = Raylib.GenImageColor((int)coreManager.FrameWidth, (int)coreManager.FrameHeight, Color.Blank);
                            img.Format = PixelFormat.UncompressedR8G8B8A8;
                            gameTexture = Raylib.LoadTextureFromImage(img);
                            Raylib.UnloadImage(img);
                            textureInitialized = true;
                        }

                        unsafe
                        {
                            int count = (int)(coreManager.FrameWidth * coreManager.FrameHeight);
                            if (pixelBuffer.Length < count)
                            {
                                pixelBuffer = new uint[count];
                            }

                            if (coreManager.PixelFormat == 1) // XRGB8888
                            {
                                uint* src = (uint*)coreManager.FrameData.ToPointer();
                                fixed (uint* dst = pixelBuffer)
                                {
                                    for (int i = 0; i < count; i++)
                                    {
                                        uint p = src[i];
                                        uint r = (p >> 16) & 0xFF;
                                        uint g = (p >> 8) & 0xFF;
                                        uint b = p & 0xFF;
                                        dst[i] = (0xFFu << 24) | (b << 16) | (g << 8) | r;
                                    }
                                    Raylib.UpdateTexture(gameTexture, dst);
                                }
                            }
                            else if (coreManager.PixelFormat == 0) // 0RGB1555
                            {
                                ushort* src = (ushort*)coreManager.FrameData.ToPointer();
                                fixed (uint* dst = pixelBuffer)
                                {
                                    for (int i = 0; i < count; i++)
                                    {
                                        ushort p = src[i];
                                        uint r = (uint)((p >> 10) & 0x1F) << 3;
                                        uint g = (uint)((p >> 5) & 0x1F) << 3;
                                        uint b = (uint)(p & 0x1F) << 3;
                                        dst[i] = (0xFFu << 24) | (b << 16) | (g << 8) | r;
                                    }
                                    Raylib.UpdateTexture(gameTexture, dst);
                                }
                            }
                            else if (coreManager.PixelFormat == 2) // RGB565
                            {
                                ushort* src = (ushort*)coreManager.FrameData.ToPointer();
                                fixed (uint* dst = pixelBuffer)
                                {
                                    for (int i = 0; i < count; i++)
                                    {
                                        ushort p = src[i];
                                        uint r = (uint)((p >> 11) & 0x1F) << 3;
                                        uint g = (uint)((p >> 5) & 0x3F) << 2;
                                        uint b = (uint)(p & 0x1F) << 3;
                                        dst[i] = (0xFFu << 24) | (b << 16) | (g << 8) | r;
                                    }
                                    Raylib.UpdateTexture(gameTexture, dst);
                                }
                            }
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
                                coreManager.InitAudioStream();
                            }
                            else
                            {
                                Logger.Error($"Core rejected the ROM file: {overlay.SelectedRomPath}");
                            }
                            overlay.ShouldLoadRom = false;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Core Load Error: {ex.Message}");
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
