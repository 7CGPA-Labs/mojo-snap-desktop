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
            Raylib.InitWindow(1280, 720, "Mojo Snap");
            Raylib.SetTargetFPS(60);
            
            string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "logo96.png");
            if (File.Exists(logoPath))
            {
                Image logo = Raylib.LoadImage(logoPath);
                Raylib.SetWindowIcon(logo);
                Raylib.UnloadImage(logo);
            }

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
                Raylib.ClearBackground(Color.Black);
                
                if (overlay.CurrentState == ApplicationState.Gameplay)
                {
                    double targetFrameTime = 1.0 / coreManager.AVInfo.timing.fps;
                    accumulator += dt;
                    if (accumulator > 0.1) accumulator = 0.1;

                    if (!overlay.IsPaused)
                    {
                        if (overlay.IsFastForward)
                        {
                            // Ignore timing and run multiple frames for Fast Forward
                            for (int i = 0; i < 4; i++) coreManager.RunFrame();
                            accumulator = 0.0;
                        }
                        else
                        {
                            while (accumulator >= targetFrameTime)
                            {
                                coreManager.RunFrame();
                                accumulator -= targetFrameTime;
                            }
                        }
                    }
                    
                    if (Raylib.IsAudioStreamReady(coreManager.GameAudioStream))
                    {
                        Raylib.SetAudioStreamVolume(coreManager.GameAudioStream, overlay.IsMuted ? 0.0f : overlay.MasterVolume);
                    }
                    coreManager.UpdateAudio();

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

                            byte* srcBase = (byte*)coreManager.FrameData.ToPointer();
                            int pitch = (int)coreManager.FramePitch;
                            int width = (int)coreManager.FrameWidth;
                            int height = (int)coreManager.FrameHeight;

                            if (coreManager.PixelFormat == 1) // XRGB8888
                            {
                                fixed (uint* dst = pixelBuffer)
                                {
                                    for (int y = 0; y < height; y++)
                                    {
                                        uint* srcLine = (uint*)(srcBase + y * pitch);
                                        uint* dstLine = dst + y * width;
                                        for (int x = 0; x < width; x++)
                                        {
                                            uint p = srcLine[x];
                                            uint r = (p >> 16) & 0xFF;
                                            uint g = (p >> 8) & 0xFF;
                                            uint b = p & 0xFF;
                                            dstLine[x] = (0xFFu << 24) | (b << 16) | (g << 8) | r;
                                        }
                                    }
                                    Raylib.UpdateTexture(gameTexture, dst);
                                }
                            }
                            else if (coreManager.PixelFormat == 0) // 0RGB1555
                            {
                                fixed (uint* dst = pixelBuffer)
                                {
                                    for (int y = 0; y < height; y++)
                                    {
                                        ushort* srcLine = (ushort*)(srcBase + y * pitch);
                                        uint* dstLine = dst + y * width;
                                        for (int x = 0; x < width; x++)
                                        {
                                            ushort p = srcLine[x];
                                            uint r = (uint)((p >> 10) & 0x1F) << 3;
                                            uint g = (uint)((p >> 5) & 0x1F) << 3;
                                            uint b = (uint)(p & 0x1F) << 3;
                                            dstLine[x] = (0xFFu << 24) | (b << 16) | (g << 8) | r;
                                        }
                                    }
                                    Raylib.UpdateTexture(gameTexture, dst);
                                }
                            }
                            else if (coreManager.PixelFormat == 2) // RGB565
                            {
                                fixed (uint* dst = pixelBuffer)
                                {
                                    for (int y = 0; y < height; y++)
                                    {
                                        ushort* srcLine = (ushort*)(srcBase + y * pitch);
                                        uint* dstLine = dst + y * width;
                                        for (int x = 0; x < width; x++)
                                        {
                                            ushort p = srcLine[x];
                                            uint r = (uint)((p >> 11) & 0x1F) << 3;
                                            uint g = (uint)((p >> 5) & 0x3F) << 2;
                                            uint b = (uint)(p & 0x1F) << 3;
                                            dstLine[x] = (0xFFu << 24) | (b << 16) | (g << 8) | r;
                                        }
                                    }
                                    Raylib.UpdateTexture(gameTexture, dst);
                                }
                            }
                        }
                    }

                    if (textureInitialized)
                    {
                        // Render centered and scaled
                        float targetW = gameTexture.Width;
                        float targetH = gameTexture.Height;

                        if (overlay.AspectRatioSelection == 0) // 4:3 Original (or Aspect Preserved)
                        {
                            float scale = Math.Min((float)Raylib.GetScreenWidth() / gameTexture.Width, (float)Raylib.GetScreenHeight() / gameTexture.Height);
                            targetW = gameTexture.Width * scale;
                            targetH = gameTexture.Height * scale;
                        }
                        else if (overlay.AspectRatioSelection == 1) // 16:9 Stretch (Fill)
                        {
                            targetW = Raylib.GetScreenWidth();
                            targetH = Raylib.GetScreenHeight();
                        }
                        else if (overlay.AspectRatioSelection == 2) // Integer Scaling
                        {
                            float scale = (float)Math.Floor(Math.Min((float)Raylib.GetScreenWidth() / gameTexture.Width, (float)Raylib.GetScreenHeight() / gameTexture.Height));
                            if (scale < 1.0f) scale = 1.0f;
                            targetW = gameTexture.Width * scale;
                            targetH = gameTexture.Height * scale;
                        }
                        
                        float offsetX = (Raylib.GetScreenWidth() - targetW) / 2.0f;
                        float offsetY = (Raylib.GetScreenHeight() - targetH) / 2.0f;
                        
                        Rectangle sourceRec = new Rectangle(0, 0, gameTexture.Width, gameTexture.Height);
                        Rectangle destRec = new Rectangle(offsetX, offsetY, targetW, targetH);
                        
                        Raylib.SetTextureFilter(gameTexture, overlay.GraphicSmoothing ? TextureFilter.Bilinear : TextureFilter.Point);
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
                                string romName = Path.GetFileNameWithoutExtension(overlay.SelectedRomPath);
                                Raylib.SetWindowTitle($"Mojo Snap - {romName} ({core})");
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
                    // Audio Bindings
                    Raylib.SetMasterVolume(overlay.IsMuted ? 0.0f : overlay.MasterVolume);

                    // Speed Options
                    if (overlay.IsFastForward) Raylib.SetTargetFPS(240);
                    else if (overlay.IsSlowMotion) Raylib.SetTargetFPS(30);
                    else Raylib.SetTargetFPS(60);

                    // Screenshot Capture
                    if (overlay.ShouldTakeScreenshot)
                    {
                        string timeStamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        Raylib.TakeScreenshot($"screenshot_{timeStamp}.png");
                        overlay.ShouldTakeScreenshot = false;
                    }

                    if (overlay.ShouldToggleFullscreen)
                    {
                        Raylib.ToggleFullscreen();
                        overlay.ShouldToggleFullscreen = false;
                    }
                    if (overlay.ShouldReset)
                    {
                        coreManager.RetroReset?.Invoke();
                        overlay.ShouldReset = false;
                    }
                    if (overlay.ShouldSaveState)
                    {
                        coreManager.SaveState(overlay.SaveStateSlot);
                        overlay.ShouldSaveState = false;
                    }
                    if (overlay.ShouldLoadState)
                    {
                        coreManager.LoadState(overlay.SaveStateSlot);
                        overlay.ShouldLoadState = false;
                    }
                    if (overlay.ShouldClose)
                    {
                        overlay.CurrentState = ApplicationState.FileSelection;
                        overlay.ShouldClose = false;
                        Raylib.SetWindowTitle("Mojo Snap");
                    }

                    overlay.DrawPlaybackControls(Raylib.GetFPS(), Raylib.GetFrameTime() * 1000f, coreManager);
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
