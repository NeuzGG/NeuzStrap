using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NeuzStrap.Boost;
using NeuzStrap.Core;
using NeuzStrap.Roblox;
using NeuzStrap.Setup;
using NeuzStrap.UI.Controls;

namespace NeuzStrap.UI.Pages
{
    public sealed class ToolsPage : Page
    {
        readonly List<(CleanItem Item, Toggle Toggle, SettingRow Row)> _clean = new List<(CleanItem, Toggle, SettingRow)>();
        readonly NButton _cleanButton;
        readonly Paragraph _cleanResult;
        readonly SettingRow _linksRow;
        readonly NButton _linksFix;
        readonly SettingRow _autoCleanRow;
        long[] _sizes = new long[0];
        bool _busy;

        public override string Title => "Tools";
        public override string Subtitle => "Free up space, fix Roblox and keep things tidy.";

        public ToolsPage(MainForm main) : base(main)
        {
            Section("Cleaner", "Potato laptops often have small, full drives. Clear out what Roblox leaves behind.");
            foreach (var item in Cleaner.GetItems())
            {
                var t = new Toggle();
                t.SetSilently(item.CheckedByDefault);
                t.CheckedChanged += (_, __) => UpdateCleanButton();
                var row = Add(new SettingRow(item.Name, item.Description, t, Glyph.Clean));
                _clean.Add((item, t, row));
            }
            var bar = Add(new ButtonBar());
            _cleanButton = bar.AddButton(new NButton("Clean selected", ButtonKind.Primary, Glyph.Clean).FitToText(), async (_, __) => await CleanAsync());
            bar.AddButton(new NButton("Rescan", ButtonKind.Secondary, Glyph.Refresh).FitToText(), async (_, __) => await MeasureAsync());
            _cleanResult = Add(new Paragraph("", Theme.Body, Theme.Success));

            Section("Automatic clean-up", "Runs by itself after you finish playing, so a small drive never fills up.");
            _autoCleanRow = ToggleRow("Clean up on a schedule", "", () => S.AutoClean, v => { S.AutoClean = v; RefreshAutoClean(); }, Glyph.Clean);
            DropdownRow("How often", "Only runs when it's due, right after Roblox closes.",
                new Dropdown(200).Add("Every week", 7).Add("Every 2 weeks", 14).Add("Every month", 30).Add("Every 3 months", 90),
                () => S.AutoCleanDays, v => { S.AutoCleanDays = (int)v; RefreshAutoClean(); });
            ToggleRow("Include the asset cache", "The big one (often gigabytes). Games re-download what they need, so the first load after a clean-up is slower.",
                () => S.AutoCleanAssetCache, v => S.AutoCleanAssetCache = v);

            Section("Roblox");
            var update = new NButton("Update now", ButtonKind.Secondary, Glyph.Download).FitToText();
            update.Click += (_, __) => Launcher.UpdateRoblox();
            ButtonRow("Check for Roblox updates", "NeuzStrap does this automatically every time you press Play.", update, Glyph.Refresh);

            var repair = new NButton("Repair", ButtonKind.Secondary, Glyph.Repair).FitToText();
            repair.Click += (_, __) =>
            {
                if (Dialog.Confirm(Main, "Repair Roblox?", "NeuzStrap will download a fresh copy of Roblox (about 220 MB). Your settings, mods and FastFlags are kept.", "Repair"))
                    Launcher.RepairRoblox();
            };
            ButtonRow("Repair Roblox", "Crashing, missing textures or won't start? A clean reinstall usually fixes it.", repair, Glyph.Repair);

            var openRoblox = new NButton("Open", ButtonKind.Ghost, Glyph.Folder).FitToText();
            openRoblox.Click += (_, __) =>
            {
                string dir = RobloxInstaller.IsInstalled(State.Current.RobloxVersionGuid) ? RobloxInstaller.VersionDir(State.Current.RobloxVersionGuid) : Paths.Versions;
                Utils.OpenFolder(dir);
            };
            ButtonRow("Roblox install folder", "Where NeuzStrap keeps Roblox.", openRoblox, Glyph.Folder);

            var openLogs = new NButton("Open", ButtonKind.Ghost, Glyph.Folder).FitToText();
            openLogs.Click += (_, __) => Utils.OpenFolder(Paths.RobloxLogs);
            ButtonRow("Roblox logs", "Useful when reporting crashes to a game's developer.", openLogs, Glyph.Folder);

            Section("Roblox links");
            _linksFix = new NButton("Fix", ButtonKind.Primary).FitToText(Theme.S(80));
            _linksFix.Click += (_, __) =>
            {
                try { ProtocolHandler.Register(Launcher.SelfExe); }
                catch (Exception ex) { Dialog.Error(Main, "Couldn't fix the links", ex.Message); }
                RefreshLinks();
            };
            _linksRow = ButtonRow("Website Play button", "", _linksFix, Glyph.Link);

            Section("NeuzStrap");
            var openData = new NButton("Open", ButtonKind.Ghost, Glyph.Folder).FitToText();
            openData.Click += (_, __) => Utils.OpenFolder(Paths.Base);
            ButtonRow("NeuzStrap folder", Paths.Base, openData, Glyph.Folder);

            var openOwnLogs = new NButton("Open", ButtonKind.Ghost, Glyph.Folder).FitToText();
            openOwnLogs.Click += (_, __) => Utils.OpenFolder(Paths.Logs);
            ButtonRow("NeuzStrap logs", "Attach the newest one when reporting a bug.", openOwnLogs, Glyph.Folder);

            var reset = new NButton("Reset", ButtonKind.Danger, Glyph.Refresh).FitToText();
            reset.Click += (_, __) =>
            {
                if (!Dialog.Confirm(Main, "Reset all settings?", "Every NeuzStrap option (profile, FastFlags, Discord ID...) goes back to default. Game history and Roblox stay.", "Reset", danger: true))
                    return;
                Settings.Reset();
                Theme.SetAccent(Settings.Current.Accent);
                Main.OnProfileChanged();
                Toast.Show("Settings reset", "Everything is back to default.", null, 3);
            };
            ButtonRow("Reset settings", "Start fresh if something feels off.", reset, Glyph.Refresh);
        }

        public override void OnNavigatedTo()
        {
            RefreshLinks();
            RefreshAutoClean();
            _ = MeasureAsync();
        }

        void RefreshAutoClean()
        {
            var last = State.Current.LastAutoCleanUtc;
            string when = last == default(DateTime)
                ? "It hasn't run yet."
                : $"Last run {Utils.TimeAgo(last)}.";
            _autoCleanRow.Description = S.AutoClean
                ? $"Clears logs, temp files, old versions and download caches every {S.AutoCleanDays} days. {when}"
                : "Off. Use the Cleaner above whenever you want instead.";
            _autoCleanRow.PerformLayout();
        }

        void RefreshLinks()
        {
            if (Paths.IsPortable)
            {
                bool reg = ProtocolHandler.IsRegistered(Paths.CurrentExe);
                _linksRow.Description = reg ? "This portable copy opens Roblox links from the website." : "Portable mode: the website's Play button still opens the official launcher. Press Fix to use this copy.";
                _linksFix.Visible = !reg;
                return;
            }
            bool ok = AppInstaller.IsInstalled && ProtocolHandler.IsRegistered(Paths.InstalledExe);
            _linksRow.Description = ok
                ? "\u2713 Clicking Play on roblox.com opens NeuzStrap."
                : "The official Roblox launcher took the website's Play button back. Press Fix to use NeuzStrap again.";
            _linksFix.Visible = !ok;
            _linksRow.PerformLayout();
        }

        async Task MeasureAsync()
        {
            if (_busy) return;
            _busy = true;
            foreach (var c in _clean) c.Row.Description = c.Item.Description + "  Measuring\u2026";
            try
            {
                // one at a time so quick items show up right away (the asset cache can take seconds)
                var sizes = new long[_clean.Count];
                for (int i = 0; i < _clean.Count; i++)
                {
                    var item = _clean[i].Item;
                    sizes[i] = await Task.Run(() => SafeMeasure(item));
                    if (IsDisposed) return; // window closed while we were measuring
                    _clean[i].Row.Description = $"{item.Description}  ({Utils.FormatBytes(sizes[i])})";
                }
                _sizes = sizes;
                UpdateCleanButton();
            }
            catch (Exception ex)
            {
                Logger.Error("Tools", ex, "Measuring clean-up sizes failed");
            }
            finally
            {
                _busy = false;
                if (!IsDisposed) PerformLayout();
            }
        }

        void UpdateCleanButton()
        {
            if (IsDisposed) return;
            long total = 0;
            for (int i = 0; i < _clean.Count && i < _sizes.Length; i++)
                if (_clean[i].Toggle.Checked) total += _sizes[i];
            _cleanButton.Text = total > 0 ? $"Clean {Utils.FormatBytes(total)}" : "Clean selected";
            _cleanButton.FitToText();
            _cleanButton.Parent?.PerformLayout();
        }

        static long SafeMeasure(CleanItem item)
        {
            try { return Math.Max(0, item.Measure()); } catch { return 0; }
        }

        async Task CleanAsync()
        {
            if (_busy) return;
            var selected = _clean.Where(c => c.Toggle.Checked).ToList();
            if (selected.Count == 0) return;
            if (selected.Any(c => c.Item.NeedsRobloxClosed) && RobloxProcess.IsPlayerRunning())
            {
                Dialog.Info(Main, "Close Roblox first", "The asset cache can only be cleaned while Roblox is closed.");
                return;
            }

            _busy = true;
            _cleanButton.Enabled = false;
            long freed = 0;
            try
            {
                freed = await Task.Run(() => selected.Sum(c =>
                {
                    try { return c.Item.Clean(); } catch (Exception ex) { Logger.Warn("Cleaner", $"{c.Item.Name}: {ex.Message}"); return 0L; }
                }));
            }
            finally
            {
                _busy = false;
                if (!IsDisposed) _cleanButton.Enabled = true;
            }
            Logger.Info("Cleaner", $"Freed {Utils.FormatBytes(freed)}");
            if (IsDisposed) return;
            _cleanResult.Text = freed > 0 ? $"\u2713 Freed {Utils.FormatBytes(freed)}." : "\u2713 Already clean.";
            await MeasureAsync();
        }
    }
}
