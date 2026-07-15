using System;
using System.IO;

namespace EmuFrontend
{
    public static class Logger
    {
        private static readonly string LogFile = "crash.log";

        public static void Initialize()
        {
            if (File.Exists(LogFile))
            {
                File.Delete(LogFile);
            }
        }

        public static void Log(string level, string message)
        {
            try
            {
                File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}\n");
            }
            catch { }
        }

        public static void Info(string msg) => Log("INFO", msg);
        public static void Warn(string msg) => Log("WARN", msg);
        public static void Error(string msg) => Log("ERROR", msg);
        public static void Debug(string msg) => Log("DEBUG", msg);
    }
}
