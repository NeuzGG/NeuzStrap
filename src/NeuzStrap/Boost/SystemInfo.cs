using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using NeuzStrap.Core;

namespace NeuzStrap.Boost
{
    public class GpuInfo
    {
        public string Name { get; set; }
        public long VideoMemoryBytes { get; set; }
        public bool IsIntegrated { get; set; }
        public bool IsBasicDriver { get; set; }
        public override string ToString() => Name;
    }

    /// <summary>Quick hardware scan used to recommend a profile and to spot common "why is it laggy" problems.</summary>
    public class SystemInfo
    {
        public string CpuName { get; set; } = "Unknown CPU";
        public int LogicalCores { get; set; }
        public long TotalRamBytes { get; set; }
        public long AvailableRamBytes { get; set; }
        public List<GpuInfo> Gpus { get; set; } = new List<GpuInfo>();
        public bool IsLaptop { get; set; }
        public bool OnBattery { get; set; }
        public int BatteryPercent { get; set; } = -1;
        public string WindowsName { get; set; } = "Windows";
        public long SystemDriveFreeBytes { get; set; }
        /// <summary>Highest current refresh rate of any display (0 if unknown).</summary>
        public int RefreshRateHz { get; set; }

        public PerformanceProfile Recommended { get; set; } = PerformanceProfile.Balanced;
        public string RecommendationReason { get; set; } = "";

        public double RamGb => TotalRamBytes / 1024.0 / 1024 / 1024;
        public bool HasDedicatedGpu => Gpus.Any(g => !g.IsIntegrated && !g.IsBasicDriver);
        public bool HasIntegratedGpu => Gpus.Any(g => g.IsIntegrated);
        public bool HasHybridGraphics => HasDedicatedGpu && HasIntegratedGpu;
        public bool MissingGpuDriver => Gpus.Count > 0 && Gpus.All(g => g.IsBasicDriver);

        public string GpuSummary => Gpus.Count == 0 ? "Unknown GPU" : string.Join(" + ", Gpus.Where(g => !g.IsBasicDriver || Gpus.All(x => x.IsBasicDriver)).Select(g => g.Name));

        static SystemInfo _cached;
        static readonly object CacheLock = new object();

        public static SystemInfo Get()
        {
            lock (CacheLock) return _cached ??= Detect();
        }

        public static SystemInfo Refresh()
        {
            lock (CacheLock) return _cached = Detect();
        }

        /// <summary>Screenshot "demo mode": show a typical potato laptop instead of this PC.</summary>
        internal static void UseDemo()
        {
            var demo = new SystemInfo
            {
                CpuName = "Intel(R) Core(TM) i3-1005G1 CPU @ 1.20GHz",
                LogicalCores = 4,
                TotalRamBytes = 8L * 1024 * 1024 * 1024,
                Gpus = new List<GpuInfo> { new GpuInfo { Name = "Intel(R) UHD Graphics", IsIntegrated = true } },
                IsLaptop = true,
                OnBattery = true,
                WindowsName = "Windows 11 24H2",
                SystemDriveFreeBytes = 40L * 1024 * 1024 * 1024,
                RefreshRateHz = 60,
            };
            Recommend(demo);
            lock (CacheLock) _cached = demo;
        }

        static SystemInfo Detect()
        {
            var info = new SystemInfo { LogicalCores = Environment.ProcessorCount };

            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                    info.CpuName = CleanName((k?.GetValue("ProcessorNameString") as string) ?? info.CpuName);
            }
            catch { }

            var mem = Memory.GetStatus();
            info.TotalRamBytes = (long)mem.ullTotalPhys;
            info.AvailableRamBytes = (long)mem.ullAvailPhys;

            info.Gpus = DetectGpus(out int refreshHz);
            info.RefreshRateHz = refreshHz;

            if (GetSystemPowerStatus(out var ps))
            {
                info.IsLaptop = ps.BatteryFlag != 128 && ps.BatteryFlag != 255;
                info.OnBattery = info.IsLaptop && ps.ACLineStatus == 0;
                info.BatteryPercent = ps.BatteryLifePercent <= 100 ? ps.BatteryLifePercent : -1;
            }

            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    string display = k?.GetValue("DisplayVersion") as string ?? k?.GetValue("ReleaseId") as string ?? "";
                    int build = int.TryParse(k?.GetValue("CurrentBuildNumber") as string, out var b) ? b : Environment.OSVersion.Version.Build;
                    info.WindowsName = (build >= 22000 ? "Windows 11" : "Windows 10") + (display.Length > 0 ? " " + display : "");
                }
            }
            catch { }

            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));
                info.SystemDriveFreeBytes = drive.AvailableFreeSpace;
            }
            catch { }

            Recommend(info);
            Logger.Info("SystemInfo", $"{info.CpuName} ({info.LogicalCores} threads) | {info.RamGb:0.0} GB RAM | GPU: {string.Join(", ", info.Gpus.Select(g => g.Name + (g.IsIntegrated ? " [iGPU]" : "")))} | laptop={info.IsLaptop} | recommend={info.Recommended}");
            return info;
        }

        static void Recommend(SystemInfo i)
        {
            bool integratedOnly = !i.HasDedicatedGpu;
            bool oldIntel = i.Gpus.Any(g => Regex.IsMatch(g.Name, @"Intel\(R\) HD Graphics( \d{3,4})?$", RegexOptions.IgnoreCase) || g.Name.IndexOf("GMA", StringComparison.OrdinalIgnoreCase) >= 0);

            if (i.MissingGpuDriver)
            {
                i.Recommended = PerformanceProfile.UltraPotato;
                i.RecommendationReason = "Your graphics driver isn't installed, so Windows is drawing everything with the CPU.";
            }
            else if (i.RamGb < 5.5 || (integratedOnly && (oldIntel || i.LogicalCores <= 2 || (i.LogicalCores <= 4 && i.RamGb < 7.5))))
            {
                i.Recommended = PerformanceProfile.UltraPotato;
                i.RecommendationReason = i.RamGb < 5.5
                    ? $"Only {i.RamGb:0.#} GB of RAM, so every bit counts."
                    : oldIntel ? "Older Intel graphics. Maximum savings mode."
                    : "Built-in graphics with a small CPU. Maximum savings mode.";
            }
            else if (integratedOnly || i.RamGb < 7.5 || i.LogicalCores <= 4)
            {
                i.Recommended = PerformanceProfile.Potato;
                i.RecommendationReason = integratedOnly
                    ? "Built-in (integrated) graphics. Potato mode keeps FPS steady."
                    : "Modest hardware. Potato mode trims the heavy stuff.";
            }
            else
            {
                i.Recommended = PerformanceProfile.Balanced;
                i.RecommendationReason = "Decent hardware. Balanced keeps things smooth without looking bad.";
            }
        }

        static List<GpuInfo> DetectGpus(out int refreshHz)
        {
            var result = new List<GpuInfo>();
            var vram = ReadVramFromRegistry();
            refreshHz = 0;
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM, CurrentRefreshRate FROM Win32_VideoController"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject mo in results)
                    {
                        using (mo)
                        {
                            try { refreshHz = Math.Max(refreshHz, Convert.ToInt32(mo["CurrentRefreshRate"] ?? 0)); } catch { }
                            string name = CleanName(mo["Name"] as string ?? "");
                            if (name.Length == 0 || IsVirtualAdapter(name) || result.Any(g => g.Name == name)) continue;
                            long mem = vram.TryGetValue(name, out var v) ? v : Convert.ToInt64(mo["AdapterRAM"] ?? 0L);
                            result.Add(new GpuInfo
                            {
                                Name = name,
                                VideoMemoryBytes = mem,
                                IsBasicDriver = name.IndexOf("Basic Display", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Basic Render", StringComparison.OrdinalIgnoreCase) >= 0,
                                IsIntegrated = IsIntegratedGpu(name),
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("SystemInfo", "WMI GPU query failed: " + ex.Message);
                foreach (var kv in vram)
                    if (!IsVirtualAdapter(kv.Key))
                        result.Add(new GpuInfo { Name = kv.Key, VideoMemoryBytes = kv.Value, IsIntegrated = IsIntegratedGpu(kv.Key) });
            }
            return result;
        }

        static Dictionary<string, long> ReadVramFromRegistry()
        {
            var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var cls = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                {
                    if (cls == null) return map;
                    foreach (var sub in cls.GetSubKeyNames().Where(n => n.Length == 4 && n.All(char.IsDigit)))
                    {
                        try
                        {
                            using (var k = cls.OpenSubKey(sub))
                            {
                                string desc = CleanName(k?.GetValue("DriverDesc") as string ?? "");
                                if (desc.Length == 0) continue;
                                long mem = 0;
                                object q = k.GetValue("HardwareInformation.qwMemorySize");
                                if (q is long ql) mem = ql;
                                else if (q is byte[] qb && qb.Length >= 8) mem = BitConverter.ToInt64(qb, 0);
                                if (mem == 0)
                                {
                                    object d = k.GetValue("HardwareInformation.MemorySize");
                                    if (d is int di) mem = (uint)di;
                                    else if (d is byte[] db && db.Length >= 4) mem = BitConverter.ToUInt32(db, 0);
                                }
                                if (!map.ContainsKey(desc) || map[desc] < mem) map[desc] = mem;
                            }
                        }
                        catch { /* some subkeys are protected */ }
                    }
                }
            }
            catch { }
            return map;
        }

        static bool IsVirtualAdapter(string name)
        {
            string[] virtuals = { "Remote Display", "Hyper-V", "Parsec", "Citrix", "Meta Virtual", "Virtual Display", "IddSample", "spacedesk", "DisplayLink", "Radmin", "VMware", "VirtualBox", "Microsoft Remote" };
            return virtuals.Any(v => name.IndexOf(v, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static bool IsIntegratedGpu(string name)
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("basic display") || n.Contains("basic render")) return false;
            if (n.Contains("nvidia") || n.Contains("geforce") || n.Contains("quadro")) return false;
            if (n.Contains("intel"))
                return !Regex.IsMatch(n, @"arc(\(tm\))?\s+a\d{3}") && !Regex.IsMatch(n, @"arc(\(tm\))?\s+b\d{3}");
            if (n.Contains("adreno") || n.Contains("qualcomm")) return true;
            if (n.Contains("amd") || n.Contains("radeon") || n.Contains("ati "))
            {
                if (Regex.IsMatch(n, @"\brx\s?\d") || Regex.IsMatch(n, @"\br[579]\s?\d{3}") || n.Contains("pro w") || n.Contains("firepro") || n.Contains("hd 7") || n.Contains("hd 6"))
                    return false;
                // APUs: "Radeon(TM) Graphics", "Radeon Vega 8 Graphics", "Radeon 780M", "Radeon R5 Graphics", ...
                return n.Contains("graphics") || n.Contains("vega") || Regex.IsMatch(n, @"\b\d{3}m\b");
            }
            return false;
        }

        static string CleanName(string s) => Regex.Replace(s ?? "", @"\s+", " ").Trim();

        // ------------------------------------------------------------------ native

        [StructLayout(LayoutKind.Sequential)]
        struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll")]
        static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);
    }

    public static class Memory
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

        public static MEMORYSTATUSEX GetStatus()
        {
            var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
            GlobalMemoryStatusEx(ref m);
            return m;
        }
    }
}
