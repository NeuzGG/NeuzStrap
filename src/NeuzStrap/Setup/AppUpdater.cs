using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NeuzStrap.Core;

namespace NeuzStrap.Setup
{
    public class AppUpdate
    {
        public Version Version { get; set; }
        public string Tag { get; set; }
        public string DownloadUrl { get; set; }
        public string PageUrl { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>Checks GitHub Releases for a newer NeuzStrap.exe and swaps it in place.</summary>
    public static class AppUpdater
    {
        public static async Task<AppUpdate> CheckAsync(CancellationToken ct = default)
        {
            if (!AppInfo.HasRepo) return null;

            var json = await Http.GetJsonAsync($"https://api.github.com/repos/{AppInfo.GitHubRepo}/releases/latest", ct).ConfigureAwait(false);
            string tag = Json.GetString(json, "tag_name") ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version)) return null;

            State.Current.LastAppUpdateCheckUtc = DateTime.UtcNow;
            State.Save();

            var current = new Version(AppInfo.Version.Major, AppInfo.Version.Minor, Math.Max(0, AppInfo.Version.Build));
            var remote = new Version(version.Major, version.Minor, Math.Max(0, version.Build));
            if (remote <= current) return null;

            string url = (Json.Get(json, "assets") as List<object> ?? new List<object>())
                .Select(a => (Name: Json.GetString(a, "name") ?? "", Url: Json.GetString(a, "browser_download_url")))
                .Where(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(a => a.Name.Equals("NeuzStrap.exe", StringComparison.OrdinalIgnoreCase))
                .Select(a => a.Url)
                .FirstOrDefault();
            if (url == null) return null;

            return new AppUpdate
            {
                Version = remote,
                Tag = tag,
                DownloadUrl = url,
                PageUrl = Json.GetString(json, "html_url"),
                Notes = Json.GetString(json, "body") ?? "",
            };
        }

        /// <summary>Downloads the new exe, swaps it with the installed one and restarts NeuzStrap.</summary>
        public static async Task InstallAsync(AppUpdate update, IProgress<double> progress, CancellationToken ct)
        {
            string tmp = Path.Combine(Paths.Base, "NeuzStrap.update.exe");
            using (var req = new HttpRequestMessage(HttpMethod.Get, update.DownloadUrl))
            using (var resp = await Http.SendAsync(req, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                long total = resp.Content.Headers.ContentLength ?? 0;
                using (var src = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var dst = File.Create(tmp))
                {
                    var buf = new byte[1 << 16];
                    long done = 0;
                    int n;
                    while ((n = await src.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(false)) > 0)
                    {
                        await dst.WriteAsync(buf, 0, n, ct).ConfigureAwait(false);
                        done += n;
                        if (total > 0) progress?.Report((double)done / total);
                    }
                }
            }

            // sanity check: must be a real Windows executable
            using (var fs = File.OpenRead(tmp))
                if (fs.Length < 64 * 1024 || fs.ReadByte() != 'M' || fs.ReadByte() != 'Z')
                    throw new InvalidDataException("The downloaded update doesn't look like a valid NeuzStrap.exe");

            string target = Paths.IsPortable ? Paths.CurrentExe : Paths.InstalledExe;
            string old = target + ".old";
            try { if (File.Exists(old)) File.Delete(old); } catch { }
            if (File.Exists(target)) File.Move(target, old);
            File.Move(tmp, target);

            Logger.Info("Updater", $"Updated NeuzStrap to {update.Tag}, restarting");
            Process.Start(new ProcessStartInfo(target, "-updated") { UseShellExecute = false });
        }
    }
}
