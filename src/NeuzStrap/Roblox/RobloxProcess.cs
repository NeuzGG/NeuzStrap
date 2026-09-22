using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using NeuzStrap.Core;

namespace NeuzStrap.Roblox
{
    public static class RobloxProcess
    {
        public const string PlayerProcessName = "RobloxPlayerBeta";

        public static bool IsPlayerRunning()
        {
            var procs = Process.GetProcessesByName(PlayerProcessName);
            foreach (var p in procs) p.Dispose();
            return procs.Length > 0;
        }

        /// <summary>Folders that running Roblox players were started from (so we never delete them).</summary>
        public static HashSet<string> RunningInstallDirs()
        {
            var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in Process.GetProcessesByName(PlayerProcessName))
            {
                using (p)
                {
                    string path = GetImagePath(p.Id);
                    if (path != null) dirs.Add(Path.GetDirectoryName(path).TrimEnd('\\'));
                }
            }
            return dirs;
        }

        public static Process Launch(string versionDir, string arguments)
        {
            string exe = Path.Combine(versionDir, "RobloxPlayerBeta.exe");
            var psi = new ProcessStartInfo(exe, arguments ?? "")
            {
                WorkingDirectory = versionDir,
                UseShellExecute = false,
            };
            Logger.Info("RobloxProcess", $"Starting {exe} {Redact(arguments)}");
            return Process.Start(psi);
        }

        /// <summary>Join tickets are secrets - never write them to the log.</summary>
        public static string Redact(string args)
        {
            if (string.IsNullOrEmpty(args)) return "";
            int i = args.IndexOf("gameinfo:", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return args.Length > 160 ? args.Substring(0, 160) + "..." : args;
            int end = args.IndexOf('+', i);
            return args.Substring(0, i + 9) + "<hidden>" + (end > 0 ? args.Substring(end) : "");
        }

        const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr h);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder buffer, ref int size);

        public static string GetImagePath(int pid)
        {
            IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return null;
            try
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString(0, size) : null;
            }
            finally { CloseHandle(h); }
        }
    }
}
