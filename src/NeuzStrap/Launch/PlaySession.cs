using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NeuzStrap.Boost;
using NeuzStrap.Core;
using NeuzStrap.Integrations;
using NeuzStrap.Roblox;
using NeuzStrap.Setup;
using NeuzStrap.UI;
using NeuzStrap.UI.Controls;

namespace NeuzStrap.Launch
{
    /// <summary>
    /// Runs quietly in the tray while Roblox is open: activity tracking, server location, Discord,
    /// background-app calming and power boost. Everything is undone when Roblox closes.
    /// </summary>
    public sealed class PlaySession : IDisposable
    {
        readonly Process _roblox;
        readonly DateTime _launchedUtc;
        readonly Settings _s = Settings.Current;
        readonly SynchronizationContext _ui;
        readonly CancellationTokenSource _cts = new CancellationTokenSource();

        NotifyIcon _tray;
        ToolStripMenuItem _serverItem, _gameItem, _copyLinkItem;
        GameBooster.BackgroundCalmer _calmer;
        DiscordRpc _discord;
        GameSession _current;
        bool _powerBoosted;
        bool _disposed;

        public static bool IsNeeded(Settings s) =>
            s.ActivityTracking || s.DiscordRichPresence || s.ServerLocationNotice || s.CalmBackgroundApps || s.PowerBoost;

        public PlaySession(Process roblox, DateTime launchedUtc)
        {
            _roblox = roblox;
            _launchedUtc = launchedUtc;
            _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        }

        public async Task RunAsync()
        {
            Logger.Info("PlaySession", "Watching Roblox in the background");
            CreateTray();

            if (_s.CalmBackgroundApps)
            {
                _calmer = new GameBooster.BackgroundCalmer();
                await Task.Run(() => _calmer.Calm()).ConfigureAwait(true);
            }
            if (_s.PowerBoost) _powerBoosted = PowerPlan.Boost();
            GameBooster.TrimSelf();

            // Some Roblox builds reset their own priority while loading; nudge it again once.
            _ = Task.Delay(15000, _cts.Token).ContinueWith(t =>
            {
                if (!t.IsCanceled) GameBooster.SetPriority(_roblox, _s.RobloxPriority);
            }, TaskScheduler.Default);

            bool wantsActivity = _s.ActivityTracking || _s.DiscordRichPresence || _s.ServerLocationNotice;
            if (wantsActivity)
            {
                var watcher = new ActivityWatcher();
                watcher.GameJoined += g => _ui.Post(_ => OnGameJoined(g), null);
                watcher.GameLeft += g => _ui.Post(_ => OnGameLeft(g), null);
                // a background feature must never interrupt the game with an error
                try { await watcher.RunAsync(() => !HasExited(_roblox), _launchedUtc, _cts.Token).ConfigureAwait(true); }
                catch (Exception ex) { Logger.Error("PlaySession", ex, "Activity tracking stopped"); }
            }

            // wait for Roblox to close (the watcher returns when it does, or immediately if disabled)
            while (!HasExited(_roblox) && !_cts.IsCancellationRequested)
            {
                try { await Task.Delay(2000, _cts.Token).ConfigureAwait(true); }
                catch (OperationCanceledException) { break; }
            }

            Logger.Info("PlaySession", "Roblox closed, cleaning up");
            Cleanup();
        }

        static bool HasExited(Process p)
        {
            try { return p.HasExited; } catch { return true; }
        }

        // ------------------------------------------------------------------ activity events (UI thread)

        async void OnGameJoined(GameSession g)
        {
            try { await HandleGameJoinedAsync(g); }
            catch (Exception ex) { Logger.Error("PlaySession", ex, "Handling a game join failed"); }
        }

        async Task HandleGameJoinedAsync(GameSession g)
        {
            if (_disposed) return;
            _current = g;
            _gameItem.Text = "In a game";
            _copyLinkItem.Enabled = !g.IsPrivateServer && !g.IsReservedServer;
            _serverItem.Text = "Server: finding location\u2026";

            GameDetails details = null;
            try { details = await RobloxApi.GetGameAsync(g.UniverseId, g.PlaceId, _cts.Token).ConfigureAwait(true); }
            catch (Exception ex) { Logger.Warn("PlaySession", "Game info lookup failed: " + ex.Message); }
            if (_disposed || _current != g) return; // left already

            if (details != null && details.Name.Length > 0)
            {
                _gameItem.Text = Trim(details.Name, 60);
                _tray.Text = Trim("NeuzStrap - " + details.Name, 63);
            }

            if (_s.DiscordRichPresence)
            {
                try
                {
                    if (_discord == null || !_discord.IsConnected)
                    {
                        _discord?.Dispose();
                        _discord = new DiscordRpc(_s.EffectiveDiscordApplicationId);
                        await _discord.ConnectAsync().ConfigureAwait(true);
                    }
                    _discord.SetActivity(DiscordRpc.BuildActivity(g, details, _s.DiscordShowGameButton));
                }
                catch (Exception ex) { Logger.Warn("PlaySession", "Discord update failed: " + ex.Message); }
            }

            ServerInfo server = null;
            if (_s.ServerLocationNotice || _s.ActivityTracking)
            {
                try { server = await ServerLocation.LookupAsync(g.ServerIp, _cts.Token).ConfigureAwait(true); }
                catch (Exception ex) { Logger.Warn("PlaySession", "Server location lookup failed: " + ex.Message); }
            }
            if (_disposed || _current != g) return;

            _serverItem.Text = server != null ? "Server: " + Trim(server.Location, 50) : "Server location unknown";

            if (_s.ServerLocationNotice && server != null)
            {
                string title = g.IsTeleport ? "Teleported to a new server" : "Connected to a server";
                string body = server.Summary + (server.Hint.Length > 0 ? "\n" + server.Hint : "");
                Toast.Show(title, body, g.IsPrivateServer ? "Private server" : null);
            }

            if (_s.ActivityTracking)
            {
                State.Reload();
                State.Current.RecordGame(new GameHistoryEntry
                {
                    PlaceId = g.PlaceId,
                    UniverseId = g.UniverseId,
                    Name = details?.Name ?? "",
                    Creator = details?.Creator ?? "",
                    JobId = g.JobId,
                    ServerLocation = server?.Location ?? "",
                    IsPrivateServer = g.IsPrivateServer,
                    LastPlayedUtc = DateTime.UtcNow,
                });
                State.Save();
            }
            GameBooster.TrimSelf();
        }

        void OnGameLeft(GameSession g)
        {
            if (_disposed || _current != g) return;
            _current = null;
            _gameItem.Text = "In the Roblox app";
            _serverItem.Text = "Not in a server";
            _copyLinkItem.Enabled = false;
            _tray.Text = "NeuzStrap - Roblox is running";
            try { _discord?.ClearActivity(); } catch { }
        }

        static string Trim(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "\u2026";

        // ------------------------------------------------------------------ tray

        void CreateTray()
        {
            var menu = new ContextMenuStrip { Renderer = new DarkMenuRenderer(), ShowImageMargin = false };
            var header = new ToolStripMenuItem("NeuzStrap") { Enabled = false, Font = Theme.BodyBold };
            _gameItem = new ToolStripMenuItem("In the Roblox app") { Enabled = false };
            _serverItem = new ToolStripMenuItem("Not in a server") { Enabled = false };
            _copyLinkItem = new ToolStripMenuItem("Copy invite link to this server", null, (_, __) => CopyLink()) { Enabled = false };
            var open = new ToolStripMenuItem("Open NeuzStrap", null, (_, __) => OpenSettings());
            var closeRoblox = new ToolStripMenuItem("Close Roblox", null, async (_, __) =>
            {
                try { await RobloxProcess.CloseEverythingAsync(); }
                catch (Exception ex) { Logger.Error("PlaySession", ex, "Close Roblox failed"); }
            });
            var stop = new ToolStripMenuItem("Stop background features", null, (_, __) => _cts.Cancel());

            menu.Items.AddRange(new ToolStripItem[] { header, _gameItem, _serverItem, new ToolStripSeparator(), _copyLinkItem, open, closeRoblox, new ToolStripSeparator(), stop });

            _tray = new NotifyIcon
            {
                Icon = AppIcon.Small,
                Text = "NeuzStrap - Roblox is running",
                ContextMenuStrip = menu,
                Visible = true,
            };
            _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) OpenSettings(); };
        }

        void CopyLink()
        {
            if (_current == null) return;
            try
            {
                Clipboard.SetText(_current.DeepLink);
                Toast.Show("Invite link copied", "Send it to a friend. Opening it joins your exact server (they need Roblox installed).");
            }
            catch { }
        }

        static void OpenSettings()
        {
            try
            {
                string exe = !Paths.IsPortable && AppInstaller.IsInstalled ? Paths.InstalledExe : Paths.CurrentExe;
                Process.Start(new ProcessStartInfo(exe, "-settings") { UseShellExecute = false });
            }
            catch (Exception ex) { Logger.Error("PlaySession", ex, "Couldn't open settings"); }
        }

        // ------------------------------------------------------------------ cleanup

        void Cleanup()
        {
            try { _calmer?.Restore(); } catch { }
            if (_powerBoosted) PowerPlan.Restore();
            try { _discord?.Dispose(); } catch { }
            _discord = null;

            // Take the website links back if the official Roblox launcher grabbed them while we played.
            if (!Paths.IsPortable && AppInstaller.IsInstalled)
            {
                try { if (!ProtocolHandler.IsRegistered(Paths.InstalledExe)) ProtocolHandler.Register(Paths.InstalledExe); } catch { }
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _cts.Cancel();
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }
        }
    }
}
