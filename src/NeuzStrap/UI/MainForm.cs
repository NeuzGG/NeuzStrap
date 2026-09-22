using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using NeuzStrap.Core;
using NeuzStrap.Setup;
using NeuzStrap.UI.Controls;
using NeuzStrap.UI.Pages;

namespace NeuzStrap.UI
{
    /// <summary>The NeuzStrap settings window: sidebar navigation + pages.</summary>
    public sealed class MainForm : Form
    {
        readonly Panel _root;
        readonly Panel _sidebar;
        readonly Panel _header;
        readonly Panel _pageHost;
        readonly Label _title;
        readonly Label _subtitle;
        readonly Card _banner;
        readonly Label _bannerText;
        readonly NButton _bannerButton;
        readonly List<NavItem> _nav = new List<NavItem>();
        readonly Dictionary<string, Func<Page>> _factories = new Dictionary<string, Func<Page>>();
        readonly Dictionary<string, Page> _pages = new Dictionary<string, Page>();
        Page _current;
        AppUpdate _pendingUpdate;

        public string CurrentPageKey { get; private set; }
        public Panel Root => _root;

        public MainForm(string startPage = "home")
        {
            Text = AppInfo.Name;
            Icon = AppIcon.Large;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = Theme.S(1040, 690);
            MinimumSize = Theme.S(900, 580);

            _root = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            Controls.Add(_root);

            // -------- content (added first so the docked sidebar sits on the left)
            _pageHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            _header = new Panel { Dock = DockStyle.Top, Height = Theme.S(92), BackColor = Theme.Bg, Padding = Theme.S(30, 24, 30, 0) };
            _title = Factory.Label("", Theme.H1);
            _subtitle = Factory.Label("", Theme.Body, Theme.TextDim);
            _header.Controls.Add(_title);
            _header.Controls.Add(_subtitle);
            _header.Resize += (_, __) => LayoutHeader();

            _banner = new Card { Dock = DockStyle.Top, Height = Theme.S(52), Visible = false, BackColor = Theme.AccentSoft, Radius = Theme.S(10) };
            _bannerText = Factory.Label("", Theme.BodyBold);
            _bannerButton = new NButton("Update now", ButtonKind.Primary, Glyph.Download);
            _bannerButton.Click += async (_, __) => await InstallUpdateAsync();
            _banner.Controls.Add(_bannerText);
            _banner.Controls.Add(_bannerButton);
            _banner.Resize += (_, __) =>
            {
                _bannerButton.Location = new Point(_banner.Width - _bannerButton.Width - Theme.S(10), (_banner.Height - _bannerButton.Height) / 2);
                _bannerText.SetBounds(Theme.S(16), 0, _bannerButton.Left - Theme.S(24), _banner.Height);
                _bannerText.TextAlign = ContentAlignment.MiddleLeft;
            };
            var bannerWrap = new Panel { Dock = DockStyle.Top, Height = Theme.S(64), Padding = Theme.S(28, 12, 28, 0), Visible = false, BackColor = Theme.Bg };
            bannerWrap.Controls.Add(_banner);
            _banner.VisibleChanged += (_, __) => bannerWrap.Visible = _banner.Visible;

            _root.Controls.Add(_pageHost);
            _root.Controls.Add(_header);
            _root.Controls.Add(bannerWrap);

            // -------- sidebar
            _sidebar = new Panel { Dock = DockStyle.Left, Width = Theme.S(236), BackColor = Theme.Sidebar, Padding = Theme.S(14, 0, 14, 16) };
            _sidebar.Paint += PaintSidebar;
            _root.Controls.Add(_sidebar);

            Register("home", "Home", Glyph.Home, () => new HomePage(this));
            Register("performance", "Performance", Glyph.Speed, () => new PerformancePage(this));
            Register("booster", "Game Booster", Glyph.Bolt, () => new BoosterPage(this));
            Register("fastflags", "Fast Flags", Glyph.Flag, () => new FastFlagsPage(this));
            Register("mods", "Mods", Glyph.Puzzle, () => new ModsPage(this));
            Register("activity", "Activity & Discord", Glyph.Link, () => new ActivityPage(this));
            Register("tools", "Tools", Glyph.Repair, () => new ToolsPage(this));
            Register("settings", "Settings", Glyph.Settings, () => new SettingsPage(this));
            Register("about", "About", Glyph.Info, () => new AboutPage(this));

            int y = Theme.S(92);
            foreach (var item in _nav)
            {
                item.Location = new Point(Theme.S(14), y);
                item.Width = _sidebar.Width - Theme.S(28);
                _sidebar.Controls.Add(item);
                y += item.Height + Theme.S(4);
            }

            var play = new NButton("Play Roblox", ButtonKind.Primary, Glyph.Play) { Height = Theme.S(46), Radius = Theme.S(10) };
            play.Font = Theme.Title;
            play.Click += (_, __) => PlayAndClose();
            _sidebar.Controls.Add(play);
            void PlacePlay() => play.SetBounds(Theme.S(14), _sidebar.Height - Theme.S(16) - play.Height, _sidebar.Width - Theme.S(28), play.Height);
            _sidebar.Layout += (_, __) => PlacePlay();
            PlacePlay();

            Theme.AccentChanged += OnAccentChanged;
            Navigate(_factories.ContainsKey(startPage) ? startPage : "home");
        }

        void Register(string key, string label, string glyph, Func<Page> factory)
        {
            _factories[key] = factory;
            var item = new NavItem(label, glyph) { Tag2 = key };
            item.Click += (_, __) => Navigate(key);
            _nav.Add(item);
        }

        void PaintSidebar(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            Draw.Smooth(g);
            int x = Theme.S(22), y = Theme.S(26);
            using (var icon = AppIcon.Bitmap(Theme.S(38)))
                g.DrawImage(icon, x, y);
            Draw.Text(g, AppInfo.Name, Theme.H2, new Rectangle(x + Theme.S(48), y - Theme.S(2), Theme.S(160), Theme.S(24)), Theme.Text);
            Draw.Text(g, "v" + AppInfo.VersionString + " \u2022 potato-friendly", Theme.Small, new Rectangle(x + Theme.S(48), y + Theme.S(20), Theme.S(170), Theme.S(18)), Theme.TextFaint);
            using (var pen = new Pen(Theme.Border))
                g.DrawLine(pen, _sidebar.Width - 1, 0, _sidebar.Width - 1, _sidebar.Height);
        }

        void LayoutHeader()
        {
            int w = _header.ClientSize.Width - _header.Padding.Horizontal;
            _title.SetBounds(_header.Padding.Left - Theme.S(2), _header.Padding.Top, w, Theme.S(38));
            _subtitle.SetBounds(_header.Padding.Left, _header.Padding.Top + Theme.S(40), w, Theme.S(22));
        }

        public void Navigate(string key)
        {
            if (!_factories.ContainsKey(key)) return;
            if (!_pages.TryGetValue(key, out var page))
            {
                page = _factories[key]();
                page.ResumeLayout(false);
                page.Dock = DockStyle.Fill;
                page.Visible = false;
                _pages[key] = page;
                _pageHost.Controls.Add(page);
            }

            SuspendLayout();
            if (_current != null && _current != page) _current.Visible = false;
            _current = page;
            CurrentPageKey = key;
            _title.Text = page.Title;
            _subtitle.Text = page.Subtitle;
            page.OnNavigatedTo();
            page.Visible = true;
            page.PerformLayout();
            foreach (var item in _nav) item.Selected = (string)item.Tag2 == key;
            ResumeLayout(true);
        }

        public Page GetPage(string key) => _pages.TryGetValue(key, out var p) ? p : null;

        /// <summary>A profile was applied or a tweak made it "Custom": refresh the pages that show it.</summary>
        public void OnProfileChanged()
        {
            foreach (var p in _pages.Values)
            {
                if (p is IProfileAware aware) aware.ProfileChanged();
                else p.RefreshRows();
            }
        }

        void OnAccentChanged()
        {
            _root.Invalidate(true);
            _sidebar.Invalidate(true);
            foreach (var p in _pages.Values) p.Invalidate(true);
            _banner.BackColor = Theme.AccentSoft;
        }

        public void PlayAndClose(string uri = null)
        {
            if (Launcher.PlayRoblox(uri)) Close();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED: flicker-free page switching
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.UseDarkTitleBar(Handle, Theme.Sidebar);
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await CheckForAppUpdateAsync();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Theme.AccentChanged -= OnAccentChanged;
            Settings.Save();
            base.OnFormClosed(e);
        }

        // ------------------------------------------------------------------ self-update banner

        async Task CheckForAppUpdateAsync(bool force = false)
        {
            if (!Settings.Current.CheckForAppUpdates && !force) return;
            if (!force && (DateTime.UtcNow - State.Current.LastAppUpdateCheckUtc).TotalHours < 12) return;
            try
            {
                var update = await AppUpdater.CheckAsync();
                if (IsDisposed || update == null || (!force && update.Tag == State.Current.SkippedAppVersion)) return;
                ShowUpdateBanner(update);
            }
            catch (Exception ex)
            {
                Logger.Warn("MainForm", "Update check failed: " + ex.Message);
            }
        }

        public async Task<bool> CheckForAppUpdateNowAsync()
        {
            try
            {
                var update = await AppUpdater.CheckAsync();
                if (update == null) return false;
                ShowUpdateBanner(update);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn("MainForm", "Update check failed: " + ex.Message);
                throw;
            }
        }

        void ShowUpdateBanner(AppUpdate update)
        {
            _pendingUpdate = update;
            _bannerText.Text = $"NeuzStrap {update.Tag} is out! You have v{AppInfo.VersionString}.";
            _banner.Visible = true;
        }

        async Task InstallUpdateAsync()
        {
            if (_pendingUpdate == null) return;
            _bannerButton.Enabled = false;
            _bannerText.Text = "Downloading update\u2026";
            try
            {
                await AppUpdater.InstallAsync(_pendingUpdate, new Progress<double>(p => _bannerText.Text = $"Downloading update\u2026 {p:P0}"), default);
                Close();
            }
            catch (Exception ex)
            {
                Logger.Error("MainForm", ex, "Self-update failed");
                _bannerButton.Enabled = true;
                _bannerText.Text = "Update failed. Try again, or download it from GitHub.";
            }
        }
    }

    /// <summary>Pages that show the active performance profile.</summary>
    public interface IProfileAware
    {
        void ProfileChanged();
    }
}
