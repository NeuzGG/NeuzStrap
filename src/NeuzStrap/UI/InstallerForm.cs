using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using NeuzStrap.Boost;
using NeuzStrap.Core;
using NeuzStrap.Roblox;
using NeuzStrap.Setup;
using NeuzStrap.UI.Controls;

namespace NeuzStrap.UI
{
    /// <summary>First-run window: scans the PC, recommends a profile and installs NeuzStrap.</summary>
    public sealed class InstallerForm : Form
    {
        enum Mode { Fresh, Upgrade, Reinstall }

        readonly Mode _mode;
        readonly SettingRow _pcRow;
        readonly Toggle _desktop = new Toggle();
        readonly Toggle _profile = new Toggle();
        readonly Toggle _import = new Toggle();
        readonly NButton _install;
        readonly string _bloxstrapFlags;
        SystemInfo _sys;

        public InstallerForm(bool forceFreshInstall = false)
        {
            var installed = AppInstaller.IsInstalled && !forceFreshInstall ? AppInstaller.InstalledVersion : null;
            _mode = installed == null ? Mode.Fresh
                  : installed < new Version(AppInfo.Version.Major, AppInfo.Version.Minor, AppInfo.Version.Build, Math.Max(0, AppInfo.Version.Revision)) ? Mode.Upgrade
                  : Mode.Reinstall;
            _bloxstrapFlags = FastFlags.FindOtherBootstrapperFlags();

            Text = _mode == Mode.Fresh ? "Install NeuzStrap" : "NeuzStrap";
            Icon = AppIcon.Large;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Body;

            var stack = new Stack { Dock = DockStyle.Fill, Padding = Theme.S(28, 4, 28, 12), Scrollable = false };
            Controls.Add(stack);
            var header = new Panel { Dock = DockStyle.Top, Height = Theme.S(124), BackColor = Theme.Bg };
            header.Paint += PaintHeader;
            Controls.Add(header);

            _pcRow = new SettingRow("Scanning your PC\u2026", "Checking your CPU, RAM and graphics to pick the best settings.", null, Glyph.Chip);
            stack.Controls.Add(_pcRow);

            if (_mode == Mode.Fresh)
            {
                _profile.SetSilently(true);
                stack.Controls.Add(new SettingRow("Use the recommended profile", "You can change it any time on the Performance page.", _profile, Glyph.Speed));
            }

            _desktop.SetSilently(_mode == Mode.Fresh || AppInstaller.HasDesktopShortcut);
            stack.Controls.Add(new SettingRow("Desktop shortcut", "A \"Roblox (NeuzStrap)\" icon that starts Roblox straight away.", _desktop, Glyph.Monitor));

            if (_mode == Mode.Fresh && _bloxstrapFlags != null)
            {
                _import.SetSilently(true);
                stack.Controls.Add(new SettingRow("Bring my FastFlags from Bloxstrap", "Found on this PC. Only the ones Roblox still accepts will matter.", _import, Glyph.Import));
            }

            stack.Controls.Add(new Paragraph(
                "NeuzStrap will open when you click Play on roblox.com. Your account, friends and games stay exactly the same, " +
                "and you can uninstall any time to go back to the official launcher. No admin rights needed.", Theme.Small, Theme.TextDim));

            var footer = new Panel { Dock = DockStyle.Bottom, Height = Theme.S(70), BackColor = Theme.Sidebar };
            Controls.Add(footer);

            _install = new NButton(_mode == Mode.Fresh ? "Install" : _mode == Mode.Upgrade ? "Update" : "Reinstall", ButtonKind.Primary, Glyph.Download) { Font = Theme.Title };
            _install.FitToText(Theme.S(140));
            _install.Height = Theme.S(40);
            _install.Click += async (_, __) => await InstallAsync();
            var cancel = new NButton(_mode == Mode.Reinstall ? "Open NeuzStrap" : "Cancel", ButtonKind.Secondary).FitToText(Theme.S(100));
            cancel.Height = Theme.S(40);
            cancel.Click += (_, __) =>
            {
                if (_mode == Mode.Reinstall) StartInstalled("-settings");
                Close();
            };
            footer.Controls.Add(_install);
            footer.Controls.Add(cancel);
            footer.Resize += (_, __) =>
            {
                _install.Location = new Point(footer.Width - Theme.S(24) - _install.Width, (footer.Height - _install.Height) / 2);
                cancel.Location = new Point(_install.Left - Theme.S(10) - cancel.Width, _install.Top);
            };
            AcceptButton = _install;

            int width = Theme.S(640);
            // + room for the PC description to grow once the scan finishes
            ClientSize = new Size(width, header.Height + stack.MeasureContent(width) + Theme.S(34) + footer.Height);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.UseDarkTitleBar(Handle, Theme.Bg);
        }

        void PaintHeader(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var panel = (Control)sender;
            Draw.Smooth(g);
            int pad = Theme.S(28);
            using (var icon = AppIcon.Bitmap(Theme.S(64)))
                g.DrawImage(icon, pad, Theme.S(30));
            int x = pad + Theme.S(82);
            string title = _mode == Mode.Fresh ? "Welcome to NeuzStrap" : _mode == Mode.Upgrade ? $"Update to NeuzStrap {AppInfo.VersionString}" : "NeuzStrap is already installed";
            string sub = _mode == Mode.Fresh ? "A lightweight Roblox launcher that makes Roblox run smoother on potato PCs and laptops."
                       : _mode == Mode.Upgrade ? $"You have v{AppInstaller.InstalledVersion?.ToString(3) ?? "?"}. Your settings are kept."
                       : $"Version {AppInfo.VersionString}. Reinstall to repair it, or just open it.";
            Draw.Text(g, title, Theme.H1, new Rectangle(x, Theme.S(26), panel.Width - x - pad, Theme.S(40)), Theme.Text);
            TextRenderer.DrawText(g, sub, Theme.Body, new Rectangle(x, Theme.S(68), panel.Width - x - pad, Theme.S(44)), Theme.TextDim, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _sys = await Task.Run(() => SystemInfo.Get());
            if (IsDisposed) return;
            _pcRow.TitleLabel.Text = $"{_sys.CpuName} \u2022 {Math.Round(_sys.RamGb):0} GB RAM";
            _pcRow.Description = $"{_sys.GpuSummary}\nBest profile: {Profiles.Emoji(_sys.Recommended)} {Profiles.Title(_sys.Recommended)}. {_sys.RecommendationReason}";
            _pcRow.Parent?.PerformLayout();
        }

        async Task InstallAsync()
        {
            _install.Enabled = false;
            try
            {
                bool desktop = _desktop.Checked;
                await Task.Run(() => AppInstaller.Install(desktop));

                var s = Settings.Current;
                if (_mode == Mode.Fresh && _profile.Checked)
                    Profiles.Apply((_sys ?? SystemInfo.Get()).Recommended, s, _sys ?? SystemInfo.Get());
                if (_mode == Mode.Fresh && _import.Checked && _bloxstrapFlags != null)
                {
                    try
                    {
                        var flags = FastFlags.ParseImport(File.ReadAllText(_bloxstrapFlags), out _);
                        if (flags != null) foreach (var kv in flags) s.CustomFastFlags[kv.Key] = kv.Value;
                    }
                    catch (Exception ex) { Logger.Warn("Installer", "FastFlag import failed: " + ex.Message); }
                }
                s.FirstRunDone = true;
                Settings.Save();

                StartInstalled(_mode == Mode.Fresh ? "-settings -welcome" : "-settings -updated");
                Close();
            }
            catch (Exception ex)
            {
                Logger.Error("Installer", ex, "Install failed");
                Dialog.Error(this, "Install failed", ex.Message);
                _install.Enabled = true;
            }
        }

        static void StartInstalled(string args)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Paths.InstalledExe, args) { UseShellExecute = false }); }
            catch (Exception ex) { Logger.Error("Installer", ex, "Couldn't start the installed copy"); }
        }
    }
}
