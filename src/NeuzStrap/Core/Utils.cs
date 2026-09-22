using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace NeuzStrap.Core
{
    public static class Utils
    {
        public static string FormatBytes(long bytes)
        {
            if (bytes < 0) bytes = 0;
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double v = bytes;
            int u = 0;
            while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
            return u == 0 ? $"{bytes} B" : v.ToString(v >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture) + " " + units[u];
        }

        public static string FormatDuration(TimeSpan t)
        {
            if (t.TotalSeconds < 1) return "a moment";
            if (t.TotalMinutes < 1) return $"{(int)t.TotalSeconds}s";
            if (t.TotalHours < 1) return $"{(int)t.TotalMinutes}m {t.Seconds:00}s";
            return $"{(int)t.TotalHours}h {t.Minutes:00}m";
        }

        public static string TimeAgo(DateTime utc)
        {
            var d = DateTime.UtcNow - utc;
            if (d.TotalMinutes < 1) return "just now";
            if (d.TotalHours < 1) return $"{(int)d.TotalMinutes} min ago";
            if (d.TotalDays < 1) return $"{(int)d.TotalHours} hr ago";
            if (d.TotalDays < 2) return "yesterday";
            if (d.TotalDays < 30) return $"{(int)d.TotalDays} days ago";
            return utc.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
        }

        public static string Md5File(string path)
        {
            using (var md5 = MD5.Create())
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16))
                return ToHex(md5.ComputeHash(fs));
        }

        public static string ToHex(byte[] bytes)
        {
            var c = new char[bytes.Length * 2];
            const string hex = "0123456789abcdef";
            for (int i = 0; i < bytes.Length; i++)
            {
                c[i * 2] = hex[bytes[i] >> 4];
                c[i * 2 + 1] = hex[bytes[i] & 0xF];
            }
            return new string(c);
        }

        public static long DirectorySize(string path)
        {
            if (!Directory.Exists(path)) return 0;
            long total = 0;
            try
            {
                foreach (var f in new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    try { total += f.Length; } catch { }
                }
            }
            catch { }
            return total;
        }

        /// <summary>Deletes what it can and reports how many bytes were freed. Locked files are skipped.</summary>
        public static long DeleteDirectoryContents(string path, bool removeRoot = false, string pattern = "*")
        {
            if (!Directory.Exists(path)) return 0;
            long freed = 0;
            var root = new DirectoryInfo(path);
            foreach (var f in SafeEnumerateFiles(root, pattern))
            {
                try
                {
                    long len = f.Length;
                    if (f.IsReadOnly) f.IsReadOnly = false;
                    f.Delete();
                    freed += len;
                }
                catch { }
            }
            if (pattern == "*")
            {
                try
                {
                    foreach (var d in root.GetDirectories("*", SearchOption.AllDirectories))
                        try { if (d.Exists && d.GetFileSystemInfos().Length == 0) d.Delete(); } catch { }
                    // second pass for nested empties
                    foreach (var d in root.GetDirectories())
                        try { d.Delete(true); } catch { }
                    if (removeRoot) root.Delete(true);
                }
                catch { }
            }
            return freed;
        }

        static System.Collections.Generic.IEnumerable<FileInfo> SafeEnumerateFiles(DirectoryInfo root, string pattern)
        {
            FileInfo[] files;
            try { files = root.GetFiles(pattern, SearchOption.AllDirectories); }
            catch { yield break; }
            foreach (var f in files) yield return f;
        }

        public static void OpenFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
            }
            catch (Exception ex) { Logger.Error("Utils", ex, "Couldn't open folder"); }
        }

        public static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { Logger.Error("Utils", ex, "Couldn't open URL"); }
        }

        public static long UnixSeconds(DateTime utc) =>
            (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        public static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
