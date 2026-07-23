using System;
using System.IO;
using Xunit;
using EmuFrontend;

namespace EmuFrontend.Tests
{
    public class LoggerTests : IDisposable
    {
        private const string LogFile = "crash.log";

        public LoggerTests()
        {
            // Ensure clean state before each test
            if (File.Exists(LogFile))
            {
                File.Delete(LogFile);
            }
        }

        public void Dispose()
        {
            // Clean up after each test
            if (File.Exists(LogFile))
            {
                File.Delete(LogFile);
            }
        }

        [Fact]
        public void Initialize_DeletesExistingLogFile()
        {
            // Arrange
            File.WriteAllText(LogFile, "old log data");
            Assert.True(File.Exists(LogFile));

            // Act
            Logger.Initialize();

            // Assert
            Assert.False(File.Exists(LogFile));
        }

        [Fact]
        public void Info_WritesInfoLevelToLog()
        {
            // Arrange
            Logger.Initialize();

            // Act
            Logger.Info("Test info message");

            // Assert
            Assert.True(File.Exists(LogFile));
            var contents = File.ReadAllText(LogFile);
            Assert.Contains("[INFO] Test info message", contents);
        }

        [Fact]
        public void Error_WritesErrorLevelToLog()
        {
            // Arrange
            Logger.Initialize();

            // Act
            Logger.Error("Test error message");

            // Assert
            Assert.True(File.Exists(LogFile));
            var contents = File.ReadAllText(LogFile);
            Assert.Contains("[ERROR] Test error message", contents);
        }
    }
}
