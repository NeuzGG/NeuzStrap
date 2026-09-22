using System;
using System.Drawing;
using System.Windows.Forms;
using NeuzStrap.Core;
using NeuzStrap.Roblox;
using NeuzStrap.Setup;
using NeuzStrap.UI.Controls;

namespace NeuzStrap.UI.Pages
{
    public sealed class SettingsPage : Page
    {
        readonly SettingRow _shortcutRow;
        readonly Toggle _shortcut;

        public override string Title => "Settings";
        public override string Subtitle => "Make NeuzStrap yours.";

        public SettingsPage(MainForm main) : base(main)
        {
            Section("Look");
            Add(new SettingRow("Accent color", "Used for buttons, toggles and highlights.", new AccentPicker(), Glyph.Settings));
            ToggleRow("Tips on the launch window", "Little potato-PC tips while Roblox starts.", () => S.ShowLaunchTips, v => S.ShowLaunchTips = v, Glyph.Lightbulb);

            Section("NeuzStrap");
            _shortcut = new Toggle();
            _shortcut.CheckedChanged += (_, __) =>
            {
                try { AppInstaller.SetDesktopShortcut(_shortcut.Checked); }
                catch (Exception ex) { Dialog.Error(Main, "Couldn't change the shortcut", ex.Message); }
            };
            _shortcutRow = Add(new SettingRow("Desktop shortcut", "A \"Roblox (NeuzStrap)\" icon on your desktop that starts Roblox straight away.", _shortcut, Glyph.Monitor));

            ToggleRow("Check for NeuzStrap updates", AppInfo.HasRepo ? "Shows a banner when a new version is out." : "Turns on once this build knows its GitHub page.",
                () => S.CheckForAppUpdates, v => S.CheckForAppUpdates = v, Glyph.Download);
            var check = new NButton("Check now", ButtonKind.Secondary, Glyph.Refresh).FitToText();
            check.Enabled = AppInfo.HasRepo;
            check.Click += async (_, __) =>
            {
                check.Enabled = false;
                try
                {
                    if (!await Main.CheckForAppUpdateNowAsync())
                        Dialog.Info(Main, "You're up to date", $"NeuzStrap v{AppInfo.VersionString} is the latest version.");
                }
                catch (Exception ex) { Dialog.Error(Main, "Couldn't check for updates", ex.Message); }
                finally { check.Enabled = true; }
            };
            ButtonRow("NeuzStrap version", $"You have v{AppInfo.VersionString}.", check, Glyph.Info);

            Section("Advanced");
            ToggleRow("Keep downloaded Roblox packages", "Off saves about 220 MB of disk space. On makes a Repair faster.",
                () => S.KeepDownloadCache, v => S.KeepDownloadCache = v, Glyph.Download);

            var channel = new TextInput(160, true) { Text = S.Channel, Placeholder = "LIVE" };
            channel.TextChanged += (_, __) =>
            {
                S.Channel = string.IsNullOrWhiteSpace(channel.Text) ? Deployment.DefaultChannel : channel.Text.Trim();
                Settings.Save();
            };
            Add(new SettingRow("Roblox channel", "Leave this on LIVE. Other channels are for Roblox testers, and NeuzStrap falls back to LIVE if one is locked.", channel, Glyph.Code));

            if (!Paths.IsPortable)
            {
                Section("Uninstall");
                var uninstall = new NButton("Uninstall NeuzStrap", ButtonKind.Danger, Glyph.Delete).FitToText();
                uninstall.Click += (_, __) =>
                {
                    if (Launcher.Start("-uninstall")) Main.Close();
                };
                ButtonRow("Remove NeuzStrap", "Gives the website's Play button back to the official Roblox launcher and deletes NeuzStrap's files.", uninstall, Glyph.Delete);
            }
        }

        public override void OnNavigatedTo()
        {
            _shortcut.SetSilently(AppInstaller.HasDesktopShortcut);
            _shortcutRow.Visible = !Paths.IsPortable;
        }

        /// <summary>Row of colored circles.</summary>
        sealed class AccentPicker : Control
        {
            int _hover = -1;
            static int Dot => Theme.S(26);
            static int Gap => Theme.S(10);

            public AccentPicker()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
                Size = new Size(Theme.Accents.Length * (Dot + Gap) - Gap + Theme.S(4), Dot + Theme.S(6));
                Cursor = Cursors.Hand;
            }

            int IndexAt(Point p)
            {
                int i = (p.X - Theme.S(2)) / (Dot + Gap);
                return i >= 0 && i < Theme.Accents.Length ? i : -1;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                int i = IndexAt(e.Location);
                if (i != _hover) { _hover = i; Invalidate(); }
                base.OnMouseMove(e);
            }

            protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                int i = IndexAt(e.Location);
                if (i < 0) return;
                Settings.Current.Accent = Theme.Accents[i].Name;
                Settings.Save();
                Theme.SetAccent(Settings.Current.Accent);
                Invalidate();
                base.OnMouseClick(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Parent?.BackColor ?? Theme.Surface);
                Draw.Smooth(g);
                for (int i = 0; i < Theme.Accents.Length; i++)
                {
                    var (name, color) = Theme.Accents[i];
                    var r = new RectangleF(Theme.S(2) + i * (Dot + Gap), Theme.S(3), Dot, Dot);
                    bool selected = string.Equals(Settings.Current.Accent, name, StringComparison.OrdinalIgnoreCase);
                    if (selected || i == _hover)
                    {
                        var ring = RectangleF.Inflate(r, Theme.S(2.5f), Theme.S(2.5f));
                        using (var pen = new Pen(selected ? Color.White : Theme.TextFaint, Theme.S(1.5f)))
                            g.DrawEllipse(pen, ring.X + 0.5f, ring.Y + 0.5f, ring.Width - 1, ring.Height - 1);
                    }
                    using (var b = new SolidBrush(color)) g.FillEllipse(b, r);
                }
            }
        }
    }
}
