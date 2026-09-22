using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NeuzStrap.Boost;
using NeuzStrap.Core;
using NeuzStrap.Roblox;
using NeuzStrap.Setup;

namespace NeuzStrap.Launch
{
    public enum LaunchMode { Play, UpdateOnly, Repair }

    /// <summary>What the bootstrapper window shows while we work.</summary>
    public interface ILaunchUI
    {
        void SetStatus(string status);
        /// <summary>fraction null = indeterminate (marquee).</summary>
        void SetProgress(double? fraction, string leftDetail = "", string rightDetail = "");
    }

    public class FriendlyException : Exception
    {
        public string Title { get; }
        public FriendlyException(string title, string message, Exception inner = null) : base(message, inner) { Title = title; }
    }

    /// <summary>
    /// The whole "click Play" pipeline: check version, update if needed, apply every tweak,
    /// start Roblox. Returns the Roblox process (or null in update/repair mode).
    /// </summary>
    public sealed class LaunchSession
    {
        readonly LaunchMode _mode;
        readonly string _robloxArgs;
        readonly ILaunchUI _ui;
        readonly CancellationToken _ct;
        readonly Settings _s = Settings.Current;

        public string VersionDir { get; private set; }
        public DateTime LaunchedUtc { get; private set; }

        public LaunchSession(LaunchMode mode, string robloxArgs, ILaunchUI ui, CancellationToken ct)
        {
            _mode = mode;
            _robloxArgs = string.IsNullOrWhiteSpace(robloxArgs) ? "--app" : robloxArgs;
            _ui = ui;
            _ct = ct;
        }

        public async Task<Process> RunAsync()
        {
            _ui.SetStatus("Connecting to Roblox\u2026");
            _ui.SetProgress(null);

            // Undo a power plan change if NeuzStrap was closed mid-game last time.
            if (!RobloxProcess.IsPlayerRunning()) PowerPlan.Restore();

            // The official Roblox launcher likes to take the website links back. Take them back again.
            if (_mode == LaunchMode.Play && !Paths.IsPortable && AppInstaller.IsInstalled && !ProtocolHandler.IsRegistered(Paths.InstalledExe))
            {
                try { ProtocolHandler.Register(Paths.InstalledExe); }
                catch (Exception ex) { Logger.Warn("Launch", "Couldn't re-register links: " + ex.Message); }
            }
            if (_mode == LaunchMode.Play) OfficialShortcuts.KeepTakenOver();

            string guid;
            using (var gate = await AcquireInstallGateAsync().ConfigureAwait(true))
            {
                guid = await EnsureRobloxAsync().ConfigureAwait(true);
            }
            VersionDir = RobloxInstaller.VersionDir(guid);
            string exe = RobloxInstaller.PlayerExe(guid);

            _ui.SetStatus("Applying your tweaks\u2026");
            _ui.SetProgress(null);
            await Task.Run(() =>
            {
                FastFlags.Write(VersionDir, _s);
                ModManager.Apply(VersionDir, _s);
                GameBooster.ApplyExeTweaks(exe, _s);
            }, _ct).ConfigureAwait(true);

            if (_mode != LaunchMode.Play)
            {
                await Task.Run(() => RobloxInstaller.CleanupOldVersions(guid)).ConfigureAwait(true);
                return null;
            }

            if (_s.FreeRamBeforeLaunch)
            {
                _ui.SetStatus("Freeing up memory\u2026");
                long freed = await Task.Run(() => GameBooster.FreeMemory(), _ct).ConfigureAwait(true);
                if (freed > 0) _ui.SetProgress(null, $"Freed {Utils.FormatBytes(freed)} of RAM");
            }

            // A Roblox that's already open (even one started without NeuzStrap) would be replaced by this
            // launch anyway, and it overwrites the in-game settings file when it exits. Close it first.
            if (GameSettingsFile.HasChanges(_s) && RobloxProcess.IsPlayerRunning())
            {
                _ui.SetStatus("Closing the Roblox that's already open\u2026");
                _ui.SetProgress(null, "So your settings apply to the new one");
                await RobloxProcess.CloseAllPlayersAsync(TimeSpan.FromSeconds(8), _ct).ConfigureAwait(true);
            }

            await Task.Run(() => GameSettingsFile.Apply(_s), _ct).ConfigureAwait(true);

            _ct.ThrowIfCancellationRequested();
            _ui.SetStatus("Starting Roblox\u2026");
            LaunchedUtc = DateTime.UtcNow;

            Process proc;
            try { proc = RobloxProcess.Launch(VersionDir, _robloxArgs); }
            catch (Exception ex)
            {
                throw new FriendlyException("Roblox couldn't start",
                    "Windows refused to start Roblox. If your antivirus quarantined it, allow it and try Tools > Repair Roblox.", ex);
            }

            GameBooster.SetPriority(proc, _s.RobloxPriority);
            State.Current.TotalLaunches++;
            State.Save();

            // Tidy up old versions in the background (the new one is running, so it's safe).
            _ = Task.Run(() =>
            {
                try { RobloxInstaller.CleanupOldVersions(guid); } catch { }
            });

            return proc;
        }

        /// <summary>Only one NeuzStrap may install at a time (two browser clicks in a row, for example).</summary>
        async Task<IDisposable> AcquireInstallGateAsync()
        {
            var mutex = new Mutex(false, @"Local\NeuzStrap_Install");
            bool waitedMessageShown = false;
            while (true)
            {
                try
                {
                    if (mutex.WaitOne(0)) break;
                }
                catch (AbandonedMutexException) { break; } // previous owner crashed; we own it now

                if (!waitedMessageShown)
                {
                    _ui.SetStatus("Waiting for another NeuzStrap window\u2026");
                    waitedMessageShown = true;
                }
                await Task.Delay(400, _ct).ConfigureAwait(true);
            }
            return new MutexReleaser(mutex);
        }

        sealed class MutexReleaser : IDisposable
        {
            readonly Mutex _m;
            public MutexReleaser(Mutex m) { _m = m; }
            public void Dispose()
            {
                try { _m.ReleaseMutex(); } catch { }
                _m.Dispose();
            }
        }

        async Task<string> EnsureRobloxAsync()
        {
            var st = State.Current;
            ClientVersion latest = null;
            try
            {
                latest = await Deployment.GetLatestAsync(_s.Channel, _ct).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (_ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                Logger.Error("Launch", ex, "Version check failed");
                if (RobloxInstaller.IsInstalled(st.RobloxVersionGuid) && _mode == LaunchMode.Play)
                {
                    _ui.SetProgress(null, "Offline: using the Roblox version you already have");
                    return st.RobloxVersionGuid;
                }
                throw new FriendlyException("Can't reach Roblox",
                    "NeuzStrap couldn't connect to Roblox's servers. Check your internet connection (or whether Roblox is down) and try again.", ex);
            }

            bool needsInstall = _mode == LaunchMode.Repair || !RobloxInstaller.IsInstalled(latest.Guid);
            if (!needsInstall)
            {
                if (st.RobloxVersionGuid != latest.Guid) SaveVersion(latest);
                return latest.Guid;
            }

            // Updating the folder that Roblox is currently running from isn't possible - but every
            // version gets its own folder, so only a repair of the running version needs Roblox closed.
            if (_mode == LaunchMode.Repair && RobloxProcess.RunningInstallDirs().Contains(RobloxInstaller.VersionDir(latest.Guid)))
                throw new FriendlyException("Close Roblox first", "Roblox is running, so it can't be repaired right now. Close Roblox and try again.");

            _ui.SetStatus(RobloxInstaller.IsInstalled(st.RobloxVersionGuid) ? "Updating Roblox\u2026" : "Downloading Roblox\u2026");

            var progress = new Progress<InstallProgress>(p =>
            {
                string left, right = "";
                switch (p.Phase)
                {
                    case InstallPhase.Preparing:
                        _ui.SetProgress(null, "Getting the file list\u2026");
                        return;
                    case InstallPhase.Finishing:
                        _ui.SetProgress(1, "Finishing up\u2026");
                        return;
                    case InstallPhase.Installing:
                        left = $"Unpacking {p.PackagesDone} of {p.PackagesTotal}";
                        break;
                    default:
                        left = $"{Utils.FormatBytes(p.Downloaded)} of {Utils.FormatBytes(p.DownloadTotal)}";
                        if (p.BytesPerSecond > 1024)
                        {
                            var eta = TimeSpan.FromSeconds((p.DownloadTotal - p.Downloaded) / p.BytesPerSecond);
                            right = $"{Utils.FormatBytes((long)p.BytesPerSecond)}/s \u2022 {Utils.FormatDuration(eta)} left";
                        }
                        break;
                }
                _ui.SetProgress(p.Fraction, left, right);
            });

            try
            {
                await new RobloxInstaller(latest, progress, _ct).InstallAsync().ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (_ct.IsCancellationRequested) { throw; }
            catch (NotEnoughSpaceException ex)
            {
                throw new FriendlyException("Not enough disk space", ex.Message, ex);
            }
            catch (Exception ex)
            {
                // Keep playing on the old version if the update failed but it's still there.
                if (_mode == LaunchMode.Play && RobloxInstaller.IsInstalled(st.RobloxVersionGuid))
                {
                    Logger.Error("Launch", ex, "Update failed, falling back to the installed version");
                    _ui.SetProgress(null, "Update failed, using your current version for now");
                    return st.RobloxVersionGuid;
                }
                throw new FriendlyException("Roblox couldn't be installed", Unwrap(ex), ex);
            }

            SaveVersion(latest);
            return latest.Guid;
        }

        static void SaveVersion(ClientVersion v)
        {
            State.Current.RobloxVersionGuid = v.Guid;
            State.Current.RobloxVersionName = v.Version;
            State.Current.RobloxInstalledUtc = DateTime.UtcNow;
            State.Save();
        }

        static string Unwrap(Exception ex)
        {
            string msg = ex.Message;
            if (ex.InnerException != null && !(ex is IOException)) msg += "\n\nDetails: " + ex.InnerException.Message;
            return msg;
        }
    }
}
