using System;
using System.Collections.Generic;
using System.Linq;
using NeuzStrap.Core;

namespace NeuzStrap.Boost
{
    public enum CheckSeverity { Good, Tip, Warning }

    public class CheckResult
    {
        public CheckSeverity Severity { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        /// <summary>Optional one-click fix (label + action). Null when the user has to fix it themselves.</summary>
        public string FixLabel { get; set; }
        public Action Fix { get; set; }
    }

    /// <summary>"Why is my Roblox laggy?" - checks the usual suspects on low-end PCs.</summary>
    public static class CheckUp
    {
        public static List<CheckResult> Run(SystemInfo sys, Settings s)
        {
            var list = new List<CheckResult>();

            if (sys.MissingGpuDriver)
                list.Add(new CheckResult
                {
                    Severity = CheckSeverity.Warning,
                    Title = "Graphics driver missing",
                    Detail = "Windows is using the \"Basic Display Adapter\", so Roblox runs on the CPU and lags badly. Install the driver from your laptop or chip maker's website (Intel, AMD or NVIDIA).",
                });

            if (!Paths.IsPortable && Setup.AppInstaller.IsInstalled)
            {
                var shortcuts = Setup.OfficialShortcuts.Find();
                if (shortcuts.Count > 0)
                {
                    string names = string.Join(", ", shortcuts.Select(Describe).Distinct());
                    list.Add(new CheckResult
                    {
                        Severity = CheckSeverity.Warning,
                        Title = "Roblox shortcuts skip NeuzStrap",
                        Detail = $"{names} start Roblox without NeuzStrap, so your tweaks don't apply. Make them open through NeuzStrap (undone if you ever uninstall).",
                        FixLabel = "Fix shortcuts",
                        Fix = () => Setup.OfficialShortcuts.TakeOver(),
                    });
                }
            }

            if (sys.OnBattery)
                list.Add(new CheckResult
                {
                    Severity = CheckSeverity.Warning,
                    Title = "Running on battery",
                    Detail = "Most laptops slow down their CPU and GPU on battery. Plug in the charger for much smoother Roblox.",
                });

            if (PowerPlan.IsPowerSaverActive())
                list.Add(new CheckResult
                {
                    Severity = CheckSeverity.Warning,
                    Title = "Power saver is on",
                    Detail = "Windows' Power saver plan caps performance. Turn on Power boost so NeuzStrap switches plans while you play.",
                    FixLabel = s.PowerBoost ? null : "Turn on Power boost",
                    Fix = s.PowerBoost ? (Action)null : () => { s.PowerBoost = true; s.Profile = PerformanceProfile.Custom; Settings.Save(); },
                });

            if (sys.HasHybridGraphics && s.GpuPreference != GpuPreference.HighPerformance)
                list.Add(new CheckResult
                {
                    Severity = CheckSeverity.Warning,
                    Title = "Roblox may use the weaker GPU",
                    Detail = $"This PC has two graphics chips ({sys.GpuSummary}). Force Roblox onto the stronger one.",
                    FixLabel = "Use high-performance GPU",
                    Fix = () => { s.GpuPreference = GpuPreference.HighPerformance; s.Profile = PerformanceProfile.Custom; Settings.Save(); },
                });

            if (sys.SystemDriveFreeBytes > 0 && sys.SystemDriveFreeBytes < 5L * 1024 * 1024 * 1024)
                list.Add(new CheckResult
                {
                    Severity = CheckSeverity.Warning,
                    Title = "Drive almost full",
                    Detail = $"Only {Utils.FormatBytes(sys.SystemDriveFreeBytes)} free. A nearly-full drive makes Windows and Roblox stutter. Try Tools > Cleaner.",
                });

            if (GameBooster.IsGameDvrEnabled())
                list.Add(new CheckResult
                {
                    Severity = CheckSeverity.Tip,
                    Title = "Xbox Game Bar is recording in the background",
                    Detail = "Background recording uses GPU and disk while you play. Turn it off if you don't use it for clips.",
                    FixLabel = "Turn off",
                    Fix = () => GameBooster.SetGameDvr(false),
                });

            if (sys.RamGb < 7.5 && !s.FreeRamBeforeLaunch)
                list.Add(new CheckResult
                {
                    Severity = CheckSeverity.Tip,
                    Title = "Low memory",
                    Detail = $"{sys.RamGb:0.#} GB of RAM. Close browser tabs before playing, or let NeuzStrap free up RAM automatically.",
                    FixLabel = "Free RAM on launch",
                    Fix = () => { s.FreeRamBeforeLaunch = true; s.Profile = PerformanceProfile.Custom; Settings.Save(); },
                });

            if (list.Count == 0)
                list.Add(new CheckResult
                {
                    Severity = CheckSeverity.Good,
                    Title = "Everything looks good",
                    Detail = "No common lag causes found on this PC.",
                });

            return list;
        }

        static string Describe(string lnk)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(lnk);
            string where = lnk.StartsWith(Paths.Desktop, StringComparison.OrdinalIgnoreCase) ? "desktop" : "Start menu";
            return $"\"{name}\" ({where})";
        }
    }
}
