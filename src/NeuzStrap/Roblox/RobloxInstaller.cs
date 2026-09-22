using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using NeuzStrap.Core;

namespace NeuzStrap.Roblox
{
    public enum InstallPhase { Preparing, Downloading, Installing, Finishing }

    public class InstallProgress
    {
        public InstallPhase Phase { get; set; }
        public long Downloaded { get; set; }
        public long DownloadTotal { get; set; }
        public long Extracted { get; set; }
        public long ExtractTotal { get; set; }
        public double BytesPerSecond { get; set; }
        public int PackagesDone { get; set; }
        public int PackagesTotal { get; set; }

        /// <summary>Overall 0..1 progress: downloading is ~75% of the work, unpacking ~25%.</summary>
        public double Fraction
        {
            get
            {
                double dl = DownloadTotal > 0 ? (double)Downloaded / DownloadTotal : 1;
                double ex = ExtractTotal > 0 ? (double)Extracted / ExtractTotal : 0;
                return Math.Max(0, Math.Min(1, dl * 0.75 + ex * 0.25));
            }
        }
    }

    public class NotEnoughSpaceException : Exception
    {
        public long Needed { get; }
        public long Available { get; }

        public NotEnoughSpaceException(long needed, long available)
            : base($"Not enough free disk space to install Roblox. Needed {Utils.FormatBytes(needed)}, " +
                   $"but only {Utils.FormatBytes(available)} is free. Try Tools > Cleaner in NeuzStrap, or free up some space.")
        {
            Needed = needed;
            Available = available;
        }
    }

    /// <summary>
    /// Downloads and unpacks one Roblox version into Versions\version-xxxx.
    /// Parallel downloads, resumable .part files, MD5 verification and mirror fallback.
    /// </summary>
    public sealed class RobloxInstaller
    {
        const string CompleteMarker = ".neuzstrap-complete";
        const int ParallelDownloads = 3;
        const int BufferSize = 1 << 16;

        readonly ClientVersion _version;
        readonly IProgress<InstallProgress> _progress;
        readonly CancellationToken _ct;
        readonly SemaphoreSlim _extractGate = new SemaphoreSlim(1, 1);
        readonly Stopwatch _clock = Stopwatch.StartNew();
        readonly object _reportLock = new object();
        readonly Queue<KeyValuePair<long, long>> _speedSamples = new Queue<KeyValuePair<long, long>>();

        long _downloaded, _extracted, _downloadTotal, _extractTotal;
        int _packagesDone, _packagesTotal;
        long _lastReportMs = -1000;
        InstallPhase _phase = InstallPhase.Preparing;

        public RobloxInstaller(ClientVersion version, IProgress<InstallProgress> progress, CancellationToken ct)
        {
            _version = version;
            _progress = progress;
            _ct = ct;
        }

        public static string VersionDir(string guid) => Path.Combine(Paths.Versions, guid);
        public static string PlayerExe(string guid) => Path.Combine(VersionDir(guid), "RobloxPlayerBeta.exe");

        public static bool IsInstalled(string guid) =>
            !string.IsNullOrEmpty(guid)
            && File.Exists(PlayerExe(guid))
            && File.Exists(Path.Combine(VersionDir(guid), CompleteMarker));

        public async Task InstallAsync()
        {
            string dir = VersionDir(_version.Guid);
            Logger.Info("Installer", $"Installing {_version.Guid} into {dir}");
            Report(force: true);

            var manifest = await Deployment.GetManifestAsync(_version, _ct).ConfigureAwait(false);
            var packages = manifest.Where(ShouldInstall).OrderByDescending(p => p.PackedSize).ToList();
            Logger.Info("Installer", $"{packages.Count} of {manifest.Count} packages needed: {string.Join(", ", packages)}");

            Directory.CreateDirectory(Paths.Downloads);
            Directory.CreateDirectory(Paths.Versions);

            // ---- disk space check (potato laptops often have tiny, full drives)
            long toDownload = packages.Where(p => !IsCached(p)).Sum(p => p.PackedSize);
            long toExtract = packages.Sum(p => p.Size);
            long needed = toDownload + toExtract + 64L * 1024 * 1024;
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(Paths.Base)));
                if (drive.IsReady && drive.AvailableFreeSpace < needed)
                    throw new NotEnoughSpaceException(needed, drive.AvailableFreeSpace);
            }
            catch (ArgumentException) { /* network paths etc. - just try */ }

            // ---- fresh folder (a previous attempt may have been interrupted)
            if (Directory.Exists(dir))
            {
                try { Directory.Delete(dir, true); }
                catch (Exception ex) { throw new IOException("Couldn't clean up an unfinished Roblox install. Close Roblox and try again.", ex); }
            }
            Directory.CreateDirectory(dir);

            _downloadTotal = packages.Sum(p => p.PackedSize);
            _extractTotal = toExtract;
            _packagesTotal = packages.Count;
            _phase = InstallPhase.Downloading;
            Report(force: true);

            // ---- download (3 at a time) and unpack (one at a time, kinder to slow HDDs)
            using (var throttle = new SemaphoreSlim(ParallelDownloads))
            {
                var tasks = packages.Select(async p =>
                {
                    await throttle.WaitAsync(_ct).ConfigureAwait(false);
                    string file;
                    try { file = await DownloadAsync(p).ConfigureAwait(false); }
                    finally { throttle.Release(); }

                    await _extractGate.WaitAsync(_ct).ConfigureAwait(false);
                    try
                    {
                        await Task.Run(() => Extract(p, file, dir), _ct).ConfigureAwait(false);
                        Interlocked.Increment(ref _packagesDone);
                        if (Volatile.Read(ref _downloaded) >= _downloadTotal) _phase = InstallPhase.Installing;
                        Report(force: true);
                    }
                    finally { _extractGate.Release(); }
                }).ToList();

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            _phase = InstallPhase.Finishing;
            Report(force: true);

            // byte-for-byte what the official launcher writes
            File.WriteAllText(Path.Combine(dir, "AppSettings.xml"),
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Settings>\n\t<ContentFolder>content</ContentFolder>\n\t<BaseUrl>http://www.roblox.com</BaseUrl>\n</Settings>\n");

            if (!File.Exists(Path.Combine(dir, "RobloxPlayerBeta.exe")))
                throw new FileNotFoundException("Roblox was downloaded but RobloxPlayerBeta.exe is missing. Roblox may have changed its install format.");

            File.WriteAllText(Path.Combine(dir, CompleteMarker), _version.Version + "\r\n" + DateTime.UtcNow.ToString("o"));

            if (!Settings.Current.KeepDownloadCache)
                Utils.DeleteDirectoryContents(Paths.Downloads);

            Logger.Info("Installer", $"Installed {_version.Guid} in {_clock.Elapsed.TotalSeconds:0.0}s");
        }

        static bool ShouldInstall(Package p)
        {
            // Roblox's own installer exe isn't needed (and would try to take over as the launcher).
            if (p.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
            if (!p.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return false;
            // Only needed on PCs without the Edge WebView2 runtime (almost every Win10/11 PC has it).
            if (p.Name.Equals("WebView2RuntimeInstaller.zip", StringComparison.OrdinalIgnoreCase) && IsWebView2Installed()) return false;
            return true;
        }

        static string CachePath(Package p) => Path.Combine(Paths.Downloads, p.Md5);

        static bool IsCached(Package p)
        {
            var fi = new FileInfo(CachePath(p));
            return fi.Exists && fi.Length == p.PackedSize;
        }

        async Task<string> DownloadAsync(Package p)
        {
            string final = CachePath(p);
            if (IsCached(p))
            {
                try
                {
                    if (Utils.Md5File(final) == p.Md5)
                    {
                        AddDownloaded(p.PackedSize);
                        return final;
                    }
                }
                catch { }
                TryDelete(final);
            }

            string part = final + ".part";
            string mirror = await Deployment.GetMirrorAsync(_ct).ConfigureAwait(false);
            int mirrorIndex = Math.Max(0, Array.IndexOf(Deployment.Mirrors, mirror));
            Exception last = null;

            for (int attempt = 0; attempt < 6; attempt++)
            {
                _ct.ThrowIfCancellationRequested();
                string m = Deployment.Mirrors[(mirrorIndex + attempt / 2) % Deployment.Mirrors.Length];
                string url = Deployment.PackageUrl(m, _version, p.Name);
                long counted = 0; // bytes of this attempt already added to the progress total

                try
                {
                    long existing = File.Exists(part) ? new FileInfo(part).Length : 0;
                    if (existing > p.PackedSize) { TryDelete(part); existing = 0; }

                    using (var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url))
                    {
                        if (existing > 0) Http.AddRange(req, existing);
                        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_ct))
                        using (var resp = await Http.SendAsync(req, cts.Token).ConfigureAwait(false))
                        {
                            bool append = resp.StatusCode == HttpStatusCode.PartialContent && existing > 0;
                            if (!resp.IsSuccessStatusCode) throw new HttpStatusException(resp.StatusCode, url);
                            if (!append) existing = 0;

                            AddDownloaded(existing);
                            counted = existing;

                            using (var net = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            using (var fs = new FileStream(part, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true))
                            {
                                var buffer = new byte[BufferSize];
                                while (true)
                                {
                                    // stall watchdog: if nothing arrives for 30s, retry (flaky Wi-Fi friendly)
                                    cts.CancelAfter(TimeSpan.FromSeconds(30));
                                    int read = await net.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false);
                                    if (read <= 0) break;
                                    await fs.WriteAsync(buffer, 0, read, cts.Token).ConfigureAwait(false);
                                    counted += read;
                                    AddDownloaded(read);
                                }
                            }
                        }
                    }

                    string hash = Utils.Md5File(part);
                    if (hash != p.Md5)
                    {
                        TryDelete(part);
                        throw new InvalidDataException($"{p.Name} failed its integrity check (got {hash}, expected {p.Md5})");
                    }

                    TryDelete(final);
                    File.Move(part, final);
                    return final;
                }
                catch (OperationCanceledException) when (_ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    last = ex;
                    AddDownloaded(-counted); // undo this attempt's progress; a resumed attempt re-adds what's on disk
                    Logger.Warn("Installer", $"Download of {p.Name} from {m} failed (attempt {attempt + 1}): {ex.Message}");
                    await Task.Delay(Math.Min(8000, 800 * (attempt + 1)), _ct).ConfigureAwait(false);
                }
            }

            throw new Exception($"Couldn't download {p.Name}. Check your internet connection and try again.", last);
        }

        void Extract(Package p, string zipPath, string versionDir)
        {
            string target = Path.Combine(versionDir, PackageMap.GetFolder(p.Name));
            var buffer = new byte[BufferSize];

            using (var zip = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in zip.Entries)
                {
                    _ct.ThrowIfCancellationRequested();

                    // Roblox zips mix '\' and '/' and include a bare "\" root entry.
                    string name = entry.FullName.Replace('/', '\\').TrimStart('\\');
                    if (name.Length == 0 || name.EndsWith("\\", StringComparison.Ordinal)) continue;

                    string dest = Paths.SafeCombine(target, name);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));

                    using (var src = entry.Open())
                    using (var dst = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize))
                    {
                        int read;
                        while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
                            dst.Write(buffer, 0, read);
                    }

                    Interlocked.Add(ref _extracted, entry.Length);
                    Report();
                }
            }
        }

        void AddDownloaded(long bytes)
        {
            if (bytes == 0) return;
            Interlocked.Add(ref _downloaded, bytes);
            Report();
        }

        void Report(bool force = false)
        {
            if (_progress == null) return;
            InstallProgress snapshot;
            lock (_reportLock)
            {
                long now = _clock.ElapsedMilliseconds;
                if (!force && now - _lastReportMs < 120) return;
                _lastReportMs = now;

                long dl = Interlocked.Read(ref _downloaded);
                _speedSamples.Enqueue(new KeyValuePair<long, long>(now, dl));
                while (_speedSamples.Count > 2 && now - _speedSamples.Peek().Key > 4000) _speedSamples.Dequeue();
                var first = _speedSamples.Peek();
                double speed = now - first.Key > 300 ? (dl - first.Value) * 1000.0 / (now - first.Key) : 0;

                snapshot = new InstallProgress
                {
                    Phase = _phase,
                    Downloaded = Math.Min(dl, _downloadTotal),
                    DownloadTotal = _downloadTotal,
                    Extracted = Math.Min(Interlocked.Read(ref _extracted), _extractTotal),
                    ExtractTotal = _extractTotal,
                    BytesPerSecond = Math.Max(0, speed),
                    PackagesDone = _packagesDone,
                    PackagesTotal = _packagesTotal,
                };
            }
            _progress.Report(snapshot);
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        public static bool IsWebView2Installed()
        {
            const string clientKey = @"Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
            var candidates = new[]
            {
                (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\" + clientKey),
                (Registry.LocalMachine, @"SOFTWARE\" + clientKey),
                (Registry.CurrentUser, @"Software\" + clientKey),
            };
            foreach (var (hive, path) in candidates)
            {
                try
                {
                    using (var k = hive.OpenSubKey(path))
                    {
                        if (k?.GetValue("pv") is string pv && pv.Length > 0 && pv != "0.0.0.0") return true;
                    }
                }
                catch { }
            }
            return false;
        }

        /// <summary>Removes Roblox versions NeuzStrap no longer needs (skipping any that are running).</summary>
        public static long CleanupOldVersions(string keepGuid)
        {
            if (!Directory.Exists(Paths.Versions)) return 0;
            var inUse = RobloxProcess.RunningInstallDirs();
            long freed = 0;
            foreach (var d in new DirectoryInfo(Paths.Versions).GetDirectories())
            {
                if (string.Equals(d.Name, keepGuid, StringComparison.OrdinalIgnoreCase)) continue;
                if (inUse.Contains(d.FullName.TrimEnd('\\'))) continue;
                long size = Utils.DirectorySize(d.FullName);
                try
                {
                    d.Delete(true);
                    freed += size;
                    Logger.Info("Installer", $"Removed old Roblox version {d.Name} ({Utils.FormatBytes(size)})");
                }
                catch (Exception ex)
                {
                    Logger.Warn("Installer", $"Couldn't remove {d.Name}: {ex.Message}");
                }
            }
            return freed;
        }
    }
}
