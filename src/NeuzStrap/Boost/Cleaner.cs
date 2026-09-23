using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeuzStrap.Core;
using NeuzStrap.Roblox;

namespace NeuzStrap.Boost
{
    public class CleanItem
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool CheckedByDefault { get; set; } = true;
        public bool NeedsRobloxClosed { get; set; }
        /// <summary>May the automatic clean-up touch this? (Never the official launcher's own copy of Roblox.)</summary>
        public bool AutoCleanable { get; set; } = true;
        public Func<long> Measure { get; set; }
        public Func<long> Clean { get; set; }
    }

    /// <summary>Frees disk space - potato laptops are usually also nearly-full-SSD laptops.</summary>
    public static class Cleaner
    {
        public const string AssetCacheName = "Roblox asset cache";
        public const string OfficialCopiesName = "Official Roblox player copies";

        static string RobloxTemp => Path.Combine(Paths.Temp, "Roblox");

        /// <summary>Is the scheduled clean-up due? (Checked after you finish playing.)</summary>
        public static bool IsDue(Settings s, State state) =>
            s.AutoClean && (DateTime.UtcNow - state.LastAutoCleanUtc).TotalDays >= Math.Max(1, s.AutoCleanDays);

        /// <summary>
        /// The scheduled clean-up: logs, temp files, old versions and download caches, plus the big asset
        /// cache when that's turned on and Roblox is closed. Returns how many bytes were freed.
        /// </summary>
        public static long RunAuto(Settings s)
        {
            long freed = 0;
            foreach (var item in GetItems())
            {
                if (!item.AutoCleanable) continue;
                if (item.Name == AssetCacheName && !s.AutoCleanAssetCache) continue;
                if (item.NeedsRobloxClosed && RobloxProcess.IsPlayerRunning()) continue;
                try { freed += item.Clean(); }
                catch (Exception ex) { Logger.Warn("Cleaner", $"Auto clean-up of {item.Name} failed: {ex.Message}"); }
            }

            State.Current.LastAutoCleanUtc = DateTime.UtcNow;
            State.Save();
            Logger.Info("Cleaner", $"Automatic clean-up freed {Utils.FormatBytes(freed)}");
            return freed;
        }

        public static List<CleanItem> GetItems()
        {
            return new List<CleanItem>
            {
                new CleanItem
                {
                    Name = "Roblox log files",
                    Description = "Text logs Roblox writes every session. Safe to delete.",
                    Measure = () => SizeOf(Paths.RobloxLogs, "*.log"),
                    Clean = () => Utils.DeleteDirectoryContents(Paths.RobloxLogs, false, "*.log"),
                },
                new CleanItem
                {
                    Name = "Roblox temp files",
                    Description = "Leftover temporary files in your Temp folder.",
                    Measure = () => Utils.DirectorySize(RobloxTemp),
                    Clean = () => Utils.DeleteDirectoryContents(RobloxTemp),
                },
                new CleanItem
                {
                    Name = "Old Roblox versions",
                    Description = "Previous Roblox versions NeuzStrap no longer uses.",
                    Measure = () => OldVersionsSize(),
                    Clean = () => RobloxInstaller.CleanupOldVersions(State.Current.RobloxVersionGuid),
                },
                new CleanItem
                {
                    Name = "Download cache",
                    Description = "Roblox packages kept after installing (NeuzStrap and the official installer).",
                    Measure = () => Utils.DirectorySize(Paths.Downloads) + Utils.DirectorySize(Path.Combine(Paths.RobloxData, "Downloads")),
                    Clean = () => Utils.DeleteDirectoryContents(Paths.Downloads) + Utils.DeleteDirectoryContents(Path.Combine(Paths.RobloxData, "Downloads")),
                },
                new CleanItem
                {
                    Name = "NeuzStrap logs",
                    Description = "NeuzStrap's own logs (the current one is kept).",
                    Measure = () => SizeOf(Paths.Logs, "*.log") - CurrentLogSize(),
                    Clean = () => DeleteOldNeuzLogs(),
                },
                new CleanItem
                {
                    Name = OfficialCopiesName,
                    AutoCleanable = false, // deleting someone's other Roblox install is never automatic
                    Description = "Roblox's own launcher keeps its own copy of the game, which NeuzStrap doesn't need. Roblox Studio is never touched.",
                    CheckedByDefault = false,
                    NeedsRobloxClosed = true,
                    Measure = () => OfficialPlayerDirs().Sum(d => Utils.DirectorySize(d)),
                    Clean = () => OfficialPlayerDirs().Sum(d => { long size = Utils.DirectorySize(d); try { Directory.Delete(d, true); return size; } catch { return 0L; } }),
                },
                new CleanItem
                {
                    Name = AssetCacheName,
                    Description = "Downloaded game assets. Frees the most space, but games load slower the first time after.",
                    CheckedByDefault = false,
                    NeedsRobloxClosed = true,
                    Measure = () => AssetCacheSize(),
                    Clean = () => CleanAssetCache(),
                },
            };
        }

        static long SizeOf(string dir, string pattern)
        {
            if (!Directory.Exists(dir)) return 0;
            try { return new DirectoryInfo(dir).GetFiles(pattern, SearchOption.AllDirectories).Sum(f => f.Length); }
            catch { return 0; }
        }

        static long CurrentLogSize()
        {
            try { return Logger.CurrentFile != null && File.Exists(Logger.CurrentFile) ? new FileInfo(Logger.CurrentFile).Length : 0; }
            catch { return 0; }
        }

        static long DeleteOldNeuzLogs()
        {
            long freed = 0;
            if (!Directory.Exists(Paths.Logs)) return 0;
            foreach (var f in new DirectoryInfo(Paths.Logs).GetFiles("*.log"))
            {
                if (string.Equals(f.FullName, Logger.CurrentFile, StringComparison.OrdinalIgnoreCase)) continue;
                try { long l = f.Length; f.Delete(); freed += l; } catch { }
            }
            return freed;
        }

        static long OldVersionsSize()
        {
            if (!Directory.Exists(Paths.Versions)) return 0;
            var inUse = RobloxProcess.RunningInstallDirs();
            return new DirectoryInfo(Paths.Versions).GetDirectories()
                .Where(d => !string.Equals(d.Name, State.Current.RobloxVersionGuid, StringComparison.OrdinalIgnoreCase) && !inUse.Contains(d.FullName.TrimEnd('\\')))
                .Sum(d => Utils.DirectorySize(d.FullName));
        }

        /// <summary>
        /// Player-only folders from the official launcher (%LocalAppData%\Roblox\Versions\version-*).
        /// Anything containing Studio, or currently running, is skipped.
        /// </summary>
        static List<string> OfficialPlayerDirs()
        {
            var result = new List<string>();
            if (!Directory.Exists(Paths.RobloxOfficialVersions)) return result;
            var running = RobloxProcess.RunningInstallDirs();
            foreach (var d in new DirectoryInfo(Paths.RobloxOfficialVersions).GetDirectories("version-*"))
            {
                bool player = File.Exists(Path.Combine(d.FullName, "RobloxPlayerBeta.exe"));
                bool studio = File.Exists(Path.Combine(d.FullName, "RobloxStudioBeta.exe"));
                if (player && !studio && !running.Contains(d.FullName.TrimEnd('\\')))
                    result.Add(d.FullName);
            }
            return result;
        }

        static IEnumerable<string> AssetCacheFiles()
        {
            foreach (var name in new[] { "rbx-storage.db", "rbx-storage.db-shm", "rbx-storage.db-wal" })
                yield return Path.Combine(Paths.RobloxData, name);
        }

        static long AssetCacheSize()
        {
            long total = 0;
            foreach (var f in AssetCacheFiles())
                try { if (File.Exists(f)) total += new FileInfo(f).Length; } catch { }
            total += Utils.DirectorySize(Path.Combine(Paths.RobloxData, "rbx-storage"));
            return total;
        }

        static long CleanAssetCache()
        {
            if (RobloxProcess.IsPlayerRunning()) return 0;
            long freed = 0;
            foreach (var f in AssetCacheFiles())
            {
                try
                {
                    if (!File.Exists(f)) continue;
                    long l = new FileInfo(f).Length;
                    File.Delete(f);
                    freed += l;
                }
                catch { }
            }
            freed += Utils.DeleteDirectoryContents(Path.Combine(Paths.RobloxData, "rbx-storage"));
            return freed;
        }
    }
}
