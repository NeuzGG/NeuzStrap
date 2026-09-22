using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace NeuzStrap.Core
{
    /// <summary>Tiny thread-safe file logger. Keeps the newest 10 log files.</summary>
    public static class Logger
    {
        static readonly object Lock = new object();
        static StreamWriter _writer;

        public static string CurrentFile { get; private set; }

        public static void Init(string mode)
        {
            try
            {
                Directory.CreateDirectory(Paths.Logs);
                foreach (var old in new DirectoryInfo(Paths.Logs).GetFiles("NeuzStrap_*.log")
                                                                 .OrderByDescending(f => f.CreationTimeUtc).Skip(9))
                    try { old.Delete(); } catch { /* in use by another instance */ }

                CurrentFile = Path.Combine(Paths.Logs, $"NeuzStrap_{DateTime.Now:yyyyMMdd_HHmmss}_{Process.GetCurrentProcess().Id}.log");
                _writer = new StreamWriter(new FileStream(CurrentFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false))
                {
                    AutoFlush = true
                };
            }
            catch
            {
                _writer = null; // logging must never break the launcher
            }

            Info("Logger", $"{AppInfo.Name} v{AppInfo.VersionString} started ({mode}) | portable={Paths.IsPortable} | os={Environment.OSVersion.Version} | 64bit={Environment.Is64BitProcess}");
        }

        public static void Info(string source, string message) => Write("INFO", source, message);
        public static void Warn(string source, string message) => Write("WARN", source, message);

        public static void Error(string source, Exception ex, string message = null) =>
            Write("ERROR", source, (message != null ? message + ": " : "") + ex);

        static void Write(string level, string source, string message)
        {
            string line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] [{source}] {message}";
            Debug.WriteLine(line);
            lock (Lock)
            {
                try { _writer?.WriteLine(line); } catch { }
            }
        }
    }
}
