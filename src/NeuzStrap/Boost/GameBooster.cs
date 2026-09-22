using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using NeuzStrap.Core;

namespace NeuzStrap.Boost
{
    /// <summary>Windows-side tweaks that help Roblox on weak PCs. All per-user and reversible.</summary>
    public static class GameBooster
    {
        const string GpuPrefKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
        const string CompatLayersKey = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
        const string DisableFsoValue = "~ DISABLEDXMAXIMIZEDWINDOWEDMODE";

        // ------------------------------------------------------------------ per-exe registry tweaks

        /// <summary>
        /// Sets the GPU preference / fullscreen-optimization flags for this exact RobloxPlayerBeta.exe
        /// (the path changes every Roblox update, so stale entries for old versions are cleaned up).
        /// </summary>
        public static void ApplyExeTweaks(string robloxExe, Settings s)
        {
            string gpuValue = s.GpuPreference == GpuPreference.HighPerformance ? "GpuPreference=2;"
                            : s.GpuPreference == GpuPreference.PowerSaving ? "GpuPreference=1;"
                            : null;
            SetPerExeValue(GpuPrefKey, robloxExe, gpuValue);
            SetPerExeValue(CompatLayersKey, robloxExe, s.DisableFullscreenOptimizations ? DisableFsoValue : null);
        }

        public static void RemoveAllExeTweaks()
        {
            SetPerExeValue(GpuPrefKey, null, null);
            SetPerExeValue(CompatLayersKey, null, null);
        }

        static void SetPerExeValue(string keyPath, string exe, string value)
        {
            try
            {
                // Only create the key when there's something to write; otherwise just tidy an existing one.
                using (var key = value != null ? Registry.CurrentUser.CreateSubKey(keyPath) : Registry.CurrentUser.OpenSubKey(keyPath, true))
                {
                    if (key == null) return;
                    foreach (var name in key.GetValueNames())
                    {
                        bool ours = name.StartsWith(Paths.Versions, StringComparison.OrdinalIgnoreCase);
                        if (ours && !string.Equals(name, exe, StringComparison.OrdinalIgnoreCase))
                            key.DeleteValue(name, false);
                    }
                    if (exe == null) return;
                    if (value == null) key.DeleteValue(exe, false);
                    else key.SetValue(exe, value, RegistryValueKind.String);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Booster", $"Couldn't update {keyPath}: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------ process priority

        public static void SetPriority(Process p, PriorityLevel level)
        {
            if (level == PriorityLevel.Normal) return;
            try
            {
                if (p.HasExited) return;
                p.PriorityClass = level == PriorityLevel.High ? ProcessPriorityClass.High : ProcessPriorityClass.AboveNormal;
                Logger.Info("Booster", $"Roblox priority set to {level}");
            }
            catch (Exception ex)
            {
                Logger.Warn("Booster", "Couldn't change Roblox priority: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ RAM clean-up

        static readonly HashSet<string> NeverTrim = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System", "Idle", "Registry", "Memory Compression", "smss", "csrss", "wininit", "winlogon", "services",
            "lsass", "dwm", "audiodg", "fontdrvhost", "MsMpEng", "RobloxPlayerBeta", "RobloxCrashHandler",
        };

        /// <summary>
        /// Asks background apps to hand back memory they aren't actively using (like Windows does under
        /// pressure, just ahead of time). Gives Roblox more free RAM to load into on 4-8 GB machines.
        /// </summary>
        public static long FreeMemory()
        {
            long before = (long)Memory.GetStatus().ullAvailPhys;
            int trimmed = 0;
            int self = Process.GetCurrentProcess().Id;
            int session = Process.GetCurrentProcess().SessionId;

            foreach (var p in Process.GetProcesses())
            {
                using (p)
                {
                    try
                    {
                        if (p.Id == self || p.Id <= 4 || p.SessionId != session || NeverTrim.Contains(p.ProcessName)) continue;
                        IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_SET_QUOTA, false, p.Id);
                        if (h == IntPtr.Zero) continue;
                        try { if (EmptyWorkingSet(h)) trimmed++; }
                        finally { CloseHandle(h); }
                    }
                    catch { }
                }
            }

            long after = (long)Memory.GetStatus().ullAvailPhys;
            long freed = Math.Max(0, after - before);
            Logger.Info("Booster", $"Trimmed {trimmed} background processes, about {Utils.FormatBytes(freed)} RAM freed");
            return freed;
        }

        /// <summary>
        /// Shrinks NeuzStrap's own memory while it waits in the tray, so the background features
        /// cost Roblox as little RAM as possible.
        /// </summary>
        public static void TrimSelf()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                using (var self = Process.GetCurrentProcess())
                    EmptyWorkingSet(self.Handle);
            }
            catch { }
        }

        // ------------------------------------------------------------------ calm background apps

        static readonly string[] CalmTargets =
        {
            "chrome", "msedge", "firefox", "opera", "brave", "vivaldi",
            "steam", "steamwebhelper", "EpicGamesLauncher", "EpicWebHelper", "RiotClientServices", "Battle.net", "EADesktop", "upc", "GalaxyClient",
            "OneDrive", "Dropbox", "GoogleDriveFS", "Teams", "ms-teams", "Slack", "Spotify",
        };

        /// <summary>Lowers browsers, game launchers and sync apps to "below normal" while Roblox runs, then puts them back.</summary>
        public sealed class BackgroundCalmer
        {
            readonly List<(int Pid, DateTime Started, ProcessPriorityClass Original)> _changed = new List<(int, DateTime, ProcessPriorityClass)>();

            public int Calm()
            {
                foreach (var name in CalmTargets)
                {
                    foreach (var p in Process.GetProcessesByName(name))
                    {
                        using (p)
                        {
                            try
                            {
                                var original = p.PriorityClass;
                                if (original != ProcessPriorityClass.Normal) continue;
                                p.PriorityClass = ProcessPriorityClass.BelowNormal;
                                _changed.Add((p.Id, p.StartTime, original));
                            }
                            catch { /* not ours to touch */ }
                        }
                    }
                }
                if (_changed.Count > 0) Logger.Info("Booster", $"Calmed {_changed.Count} background processes");
                return _changed.Count;
            }

            public void Restore()
            {
                foreach (var (pid, started, original) in _changed)
                {
                    try
                    {
                        using (var p = Process.GetProcessById(pid))
                        {
                            if (p.StartTime == started) p.PriorityClass = original;
                        }
                    }
                    catch { /* already closed */ }
                }
                if (_changed.Count > 0) Logger.Info("Booster", $"Restored priority of {_changed.Count} background processes");
                _changed.Clear();
            }
        }

        // ------------------------------------------------------------------ Xbox Game Bar background recording

        public static bool IsGameDvrEnabled()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore"))
                {
                    object v = k?.GetValue("GameDVR_Enabled");
                    return v == null || Convert.ToInt32(v) != 0;
                }
            }
            catch { return false; }
        }

        public static void SetGameDvr(bool enabled)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(@"System\GameConfigStore"))
                k.SetValue("GameDVR_Enabled", enabled ? 1 : 0, RegistryValueKind.DWord);
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\GameDVR"))
                k.SetValue("AppCaptureEnabled", enabled ? 1 : 0, RegistryValueKind.DWord);
            Logger.Info("Booster", "Game Bar background recording " + (enabled ? "enabled" : "disabled"));
        }

        // ------------------------------------------------------------------ native

        const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        const uint PROCESS_SET_QUOTA = 0x0100;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr h);

        [DllImport("psapi.dll")]
        static extern bool EmptyWorkingSet(IntPtr hProcess);
    }
}
