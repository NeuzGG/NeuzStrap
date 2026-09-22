using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

        /// <summary>
        /// Closes every running Roblox player (politely first, then forcefully after the timeout).
        /// Starting a new Roblox replaces the old one anyway; closing it ourselves first lets us
        /// apply the in-game settings without the old copy overwriting them when it exits.
        /// </summary>
        public static async Task<bool> CloseAllPlayersAsync(TimeSpan timeout, CancellationToken ct)
        {
            var procs = Process.GetProcessesByName(PlayerProcessName);
            if (procs.Length == 0) return true;
            try
            {
                Logger.Info("RobloxProcess", $"Closing {procs.Length} running Roblox window(s) before launching");
                foreach (var p in procs)
                    try { p.CloseMainWindow(); } catch { }

                var sw = Stopwatch.StartNew();
                while (sw.Elapsed < timeout && procs.Any(p => !HasExited(p)))
                    await Task.Delay(250, ct).ConfigureAwait(true);

                foreach (var p in procs.Where(p => !HasExited(p)))
                {
                    Logger.Warn("RobloxProcess", $"Roblox (pid {p.Id}) didn't close in time, ending it");
                    try { p.Kill(); p.WaitForExit(3000); } catch { }
                }
                return procs.All(HasExited);
            }
            finally
            {
                foreach (var p in procs) p.Dispose();
            }
        }

        /// <summary>
        /// "Close Roblox" button: closes every Roblox player (official copies too), force-ends any that
        /// don't close within a few seconds, and removes the crash handler Roblox leaves running.
        /// Roblox Studio is never touched. Returns how many player windows were open.
        /// </summary>
        public static async Task<int> CloseEverythingAsync(CancellationToken ct = default)
        {
            int players;
            using (var probe = new DisposableList(Process.GetProcessesByName(PlayerProcessName))) players = probe.Items.Length;
            if (players > 0) await CloseAllPlayersAsync(TimeSpan.FromSeconds(4), ct).ConfigureAwait(true);

            foreach (var p in Process.GetProcessesByName("RobloxCrashHandler"))
            {
                using (p)
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(GetImagePath(p.Id) ?? "");
                        bool playerFolder = dir.Length > 0 && File.Exists(Path.Combine(dir, "RobloxPlayerBeta.exe"))
                                            && !File.Exists(Path.Combine(dir, "RobloxStudioBeta.exe"));
                        if (playerFolder) p.Kill();
                    }
                    catch { }
                }
            }
            Logger.Info("RobloxProcess", $"Close Roblox: {players} player window(s) closed");
            return players;
        }

        sealed class DisposableList : IDisposable
        {
            public Process[] Items { get; }
            public DisposableList(Process[] items) { Items = items; }
            public void Dispose() { foreach (var p in Items) p.Dispose(); }
        }

        static bool HasExited(Process p)
        {
            try { return p.HasExited; } catch { return true; }
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
