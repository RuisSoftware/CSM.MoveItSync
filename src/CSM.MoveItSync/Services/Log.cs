using System;
using System.IO;
using System.Globalization;
using UnityEngine;

namespace CSM.MoveItSync.Services
{
    internal static class Log
    {
        private static readonly string LogDirectory;
        private static string _dailyLogDateStamp = string.Empty;
        private static string _currentLogFilePath;
        private static bool _initialized = false;
        private static readonly object SyncRoot = new object();

        static Log()
        {
            try
            {
                // On Linux this is ~/.local/share
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var colossalOrderDir = Path.Combine(localAppData, "Colossal Order");
                var citiesDir = Path.Combine(colossalOrderDir, "Cities_Skylines");
                LogDirectory = Path.Combine(citiesDir, "CSM.MoveItSync");
                
                UpdateFilePath(DateTime.Now);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[CSM.MoveItSync] Failed to initialize logger: {ex}");
            }
        }

        private static void UpdateFilePath(DateTime now)
        {
            _dailyLogDateStamp = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            _currentLogFilePath = Path.Combine(LogDirectory, $"log-{_dailyLogDateStamp}.log");
            _initialized = false;
        }

        private static void EnsureDirectory()
        {
            if (_initialized) return;
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
            _initialized = true;
        }

        internal static void Info(string message) => Write("INFO", message);
        internal static void Warn(string message) => Write("WARN", message);
        internal static void Error(string message) => Write("ERROR", message);
        internal static void Error(Exception ex) => Write("ERROR", ex.ToString());

        private static void Write(string level, string message)
        {
            var now = DateTime.Now;
            var stamp = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            lock (SyncRoot)
            {
                try
                {
                    if (!string.Equals(_dailyLogDateStamp, stamp, StringComparison.Ordinal))
                    {
                        UpdateFilePath(now);
                    }

                    EnsureDirectory();

                    var line = $"{now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
                    
                    // Also mirror to Unity log for immediate visibility in console
                    if (level == "ERROR") UnityEngine.Debug.LogError($"[CSM.MoveItSync] {message}");
                    else if (level == "WARN") UnityEngine.Debug.LogWarning($"[CSM.MoveItSync] {message}");
                    else UnityEngine.Debug.Log($"[CSM.MoveItSync] {message}");

                    File.AppendAllText(_currentLogFilePath, line + Environment.NewLine);
                }
                catch
                {
                    // Fallback to Unity log if file writing fails
                    UnityEngine.Debug.Log($"[CSM.MoveItSync] (Logger Error) {message}");
                }
            }
        }
    }
}
