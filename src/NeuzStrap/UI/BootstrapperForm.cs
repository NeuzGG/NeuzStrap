using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NeuzStrap.Core;
using NeuzStrap.Launch;
using NeuzStrap.UI.Controls;

namespace NeuzStrap.UI
{
    /// <summary>The small window you see after clicking Play: update progress, then Roblox starts.</summary>
    public sealed class BootstrapperForm : Form, ILaunchUI
    {
        static readonly string[] Tips =
        {
            "Plug in your charger. Most laptops slow down a lot on battery.",
            "Close extra browser tabs before playing. They eat RAM.",
            "Potato mode caps FPS at 60 so your laptop stays cool and steady.",
            "Right-click the NeuzStrap tray icon to copy an invite link to your server.",
            "Landed on a far-away server? Leave and rejoin for a closer one.",
            "Tools > Cleaner frees the disk space Roblox leaves behind.",
            "Press Shift+F5 in-game to see your FPS.",
            "Keep your laptop on a hard, flat surface so the fans can breathe.",
            "Game Booster can switch Windows to High performance only while you play.",
            "Still laggy? Try the Ultra Potato profile on the Performance page.",
        };

        readonly LaunchMode _mode;
        readonly string _robloxArgs;
        readonly CancellationTokenSource _cts = new CancellationTokenSource();

        readonly Label _status;
        readonly Label _left;
        readonly Label _right;
        readonly ProgressX _bar;
        readonly string _tip;
        bool _closingAllowed;

        public BootstrapperForm(LaunchMode mode, string robloxArgs)
        {
            _mode = mode;
            _robloxArgs = robloxArgs;
            _tip = Settings.Current.ShowLaunchTips ? "Tip: " + Tips[new Random().Next(Tips.Length)] : "";

            Text = AppInfo.Name;
            Icon = AppIcon.Large;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            DoubleBuffered = true;
            ClientSize = Theme.S(500, 232);

            int pad = Theme.S(26);
            int w = ClientSize.Width - pad * 2;

            var close = new NButton("", ButtonKind.Ghost, Glyph.Close) { Size = Theme.S(34, 30), GlyphFont = new Font(Theme.Icons.FontFamily, 9f) };
            close.Location = new Point(ClientSize.Width - close.Width - Theme.S(12), Theme.S(12));
            close.Click += (_, __) => CancelAndClose();
            Controls.Add(close);

            _status = Factory.Label(mode == LaunchMode.Play ? "Getting Roblox ready\u2026" : "Checking Roblox\u2026", Theme.H2);
            _status.SetBounds(pad, Theme.S(78), w, Theme.S(32));
            Controls.Add(_status);

            _bar = new ProgressX();
            _bar.SetBounds(pad, Theme.S(122), w, Theme.S(8));
            _bar.Value = null;
            Controls.Add(_bar);

            _left = Factory.Label("", Theme.Small, Theme.TextDim);
            _left.SetBounds(pad, Theme.S(138), w / 2 + Theme.S(40), Theme.S(20));
            Controls.Add(_left);

            _right = Factory.Label("", Theme.Small, Theme.TextDim);
            _right.TextAlign = ContentAlignment.TopRight;
            _right.SetBounds(pad + w / 2 - Theme.S(40), Theme.S(138), w / 2 + Theme.S(40), Theme.S(20));
            Controls.Add(_right);

            MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) Native.BeginDrag(this); };
            foreach (Control c in Controls)
                if (c is Label) c.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) Native.BeginDrag(this); };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!Native.TryRoundCorners(Handle))
                Region = new Region(Draw.Round(new RectangleF(0, 0, Width, Height), Theme.S(12)));
            Native.SetBorderColor(Handle, Theme.Border);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Draw.Smooth(g);
            if (!Native.IsWindows11)
                Draw.StrokeRound(g, new RectangleF(0, 0, Width, Height), Theme.S(12), Theme.Border);

            int pad = Theme.S(26);
            using (var icon = AppIcon.Bitmap(Theme.S(30)))
                g.DrawImage(icon, pad, Theme.S(24));
            Draw.Text(g, AppInfo.Name, Theme.Title, new Rectangle(pad + Theme.S(40), Theme.S(20), Theme.S(200), Theme.S(22)), Theme.Text);
            Draw.Text(g, AppInfo.Tagline, Theme.Small, new Rectangle(pad + Theme.S(40), Theme.S(40), Theme.S(260), Theme.S(18)), Theme.TextFaint);

            if (_tip.Length > 0)
            {
                var tipRect = new Rectangle(pad, ClientSize.Height - Theme.S(58), ClientSize.Width - pad * 2, Theme.S(40));
                Draw.FillRound(g, tipRect, Theme.S(8), Theme.Input);
                Draw.Glyph(g, Glyph.Lightbulb, Theme.Icons, new Rectangle(tipRect.X + Theme.S(10), tipRect.Y, Theme.S(20), tipRect.Height), Theme.Accent);
                TextRenderer.DrawText(g, _tip, Theme.Small, new Rectangle(tipRect.X + Theme.S(36), tipRect.Y, tipRect.Width - Theme.S(44), tipRect.Height),
                    Theme.TextDim, TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            }
        }

        // ------------------------------------------------------------------ ILaunchUI

        public void SetStatus(string status) => OnUi(() => _status.Text = status);

        public void SetProgress(double? fraction, string leftDetail = "", string rightDetail = "")
        {
            OnUi(() =>
            {
                _bar.Value = fraction;
                _left.Text = leftDetail ?? "";
                _right.Text = rightDetail ?? "";
            });
        }

        void OnUi(Action a)
        {
            if (IsDisposed) return;
            if (InvokeRequired) BeginInvoke(a);
            else a();
        }

        // ------------------------------------------------------------------ flow

        bool _preview;

        /// <summary>A static-looking copy for screenshots / docs (doesn't launch anything).</summary>
        public static BootstrapperForm CreatePreview()
        {
            var f = new BootstrapperForm(LaunchMode.Play, null) { _preview = true };
            f._status.Text = "Downloading Roblox\u2026";
            f._bar.Value = 0.42;
            f._left.Text = "92.4 MB of 219.6 MB";
            f._right.Text = "4.8 MB/s \u2022 26s left";
            return f;
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_preview) return;
            await RunAsync();
        }

        async Task RunAsync()
        {
            try
            {
                var session = new LaunchSession(_mode, _robloxArgs, this, _cts.Token);
                Process roblox = await session.RunAsync();

                if (roblox == null)
                {
                    SetStatus(_mode == LaunchMode.Repair ? "Roblox repaired" : "Roblox is up to date");
                    SetProgress(1, Core.State.Current.RobloxVersionName);
                    await Task.Delay(1400);
                    AllowClose();
                    return;
                }

                SetStatus("Starting Roblox\u2026");
                SetProgress(null, "Roblox is loading, this can take a bit on slower PCs");
                await WaitForRobloxWindow(roblox);

                if (PlaySession.IsNeeded(Settings.Current) && !HasExited(roblox))
                {
                    Hide();
                    using (var play = new PlaySession(roblox, session.LaunchedUtc))
                        await play.RunAsync();
                }
                AllowClose();
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Bootstrapper", "Cancelled by user");
                AllowClose();
            }
            catch (FriendlyException ex)
            {
                Logger.Error("Bootstrapper", ex);
                Show();
                Dialog.Error(this, ex.Title, ex.Message);
                AllowClose();
            }
            catch (Exception ex)
            {
                Logger.Error("Bootstrapper", ex);
                Show();
                Dialog.Error(this, "Something went wrong",
                    ex.Message + "\n\nIf this keeps happening, open NeuzStrap > Tools > Open logs and share the newest file when reporting the bug.");
                AllowClose();
            }
        }

        async Task WaitForRobloxWindow(Process p)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromSeconds(60))
            {
                if (HasExited(p))
                {
                    int code = 0;
                    try { code = p.ExitCode; } catch { }
                    Logger.Info("Bootstrapper", $"Roblox exited early with code {code} after {sw.Elapsed.TotalSeconds:0.0}s");
                    if (code != 0 && sw.Elapsed < TimeSpan.FromSeconds(15))
                        throw new FriendlyException("Roblox closed right away",
                            $"Roblox stopped while starting (code {code}). Try NeuzStrap > Tools > Repair Roblox. If you use custom FastFlags or mods, try turning them off.");
                    return;
                }
                try
                {
                    p.Refresh();
                    if (p.MainWindowHandle != IntPtr.Zero) return;
                }
                catch { return; }
                await Task.Delay(250);
            }
        }

        static bool HasExited(Process p)
        {
            try { return p.HasExited; } catch { return true; }
        }

        void CancelAndClose()
        {
            _cts.Cancel();
            AllowClose();
        }

        void AllowClose()
        {
            _closingAllowed = true;
            if (!IsDisposed) Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Alt+F4 while installing = cancel
            if (!_closingAllowed) _cts.Cancel();
            base.OnFormClosing(e);
        }
    }
}
