using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using NeuzStrap.Boost;
using NeuzStrap.Core;
using NeuzStrap.UI.Controls;

namespace NeuzStrap.UI.Pages
{
    public sealed class HomePage : Page, IProfileAware
    {
        readonly HeroCard _hero;
        readonly SettingRow _pcRow;
        readonly SectionHeader _checkHeader;
        readonly SectionHeader _recentHeader;
        SystemInfo _sys;

        public override string Title => Greeting();
        public override string Subtitle => "Roblox, but smoother on your PC.";

        public HomePage(MainForm main) : base(main)
        {
            _hero = Add(new HeroCard(main));

            Section("Your PC");
            var useIt = new NButton("Use it", ButtonKind.Primary).FitToText(Theme.S(90));
            useIt.Visible = false;
            useIt.Click += (_, __) =>
            {
                if (_sys == null) return;
                Profiles.Apply(_sys.Recommended, S, _sys);
                Settings.Save();
                Main.OnProfileChanged();
                Refresh2();
            };
            _pcRow = Add(new SettingRow("Scanning your PC\u2026", "", useIt, Glyph.Chip));

            _checkHeader = Section("Check-up", "Common reasons Roblox lags on low-end PCs.");
            _recentHeader = Section("Jump back in");
        }

        static string Greeting()
        {
            int h = DateTime.Now.Hour;
            string part = h < 5 ? "Up late" : h < 12 ? "Good morning" : h < 18 ? "Good afternoon" : "Good evening";
            return part + "! Ready to play?";
        }

        public override void OnNavigatedTo()
        {
            State.Reload();
            _hero.RefreshInfo();
            _ = LoadSystemAsync();
            RebuildRecent();
        }

        public void ProfileChanged()
        {
            _hero.RefreshInfo();
            Refresh2();
        }

        async Task LoadSystemAsync()
        {
            _sys = await Task.Run(() => SystemInfo.Get());
            if (!IsDisposed) Refresh2();
        }

        void Refresh2()
        {
            if (_sys == null) return;
            _pcRow.TitleLabel.Text = $"{_sys.CpuName}";
            string ram = $"{Math.Round(_sys.RamGb):0} GB RAM";
            string laptop = _sys.IsLaptop ? " \u2022 Laptop" : "";
            bool isRecommended = S.Profile == _sys.Recommended;
            _pcRow.Description = $"{ram} \u2022 {_sys.GpuSummary}{laptop}\n" +
                                 $"Recommended: {Profiles.Title(_sys.Recommended)}. {_sys.RecommendationReason}" +
                                 (isRecommended ? " (active)" : "");
            _pcRow.Editor.Visible = !isRecommended;
            RebuildCheckUp();
            PerformLayout();
        }

        void RebuildCheckUp()
        {
            SuspendLayout();
            foreach (var old in Controls.OfType<Control>().Where(c => (c.Tag as string) == "check").ToList())
            {
                Controls.Remove(old);
                old.Dispose();
            }

            int index = Controls.GetChildIndex(_checkHeader) + 1;
            foreach (var result in CheckUp.Run(_sys, S))
            {
                NButton fix = null;
                if (result.Fix != null)
                {
                    fix = new NButton(result.FixLabel, ButtonKind.Secondary).FitToText();
                    var r = result;
                    fix.Click += (_, __) =>
                    {
                        try { r.Fix(); }
                        catch (Exception ex) { Dialog.Error(Main, "Couldn't fix that", ex.Message); }
                        Main.OnProfileChanged();
                        RebuildCheckUp();
                        PerformLayout();
                    };
                }
                string glyph = result.Severity == CheckSeverity.Good ? Glyph.Check : result.Severity == CheckSeverity.Warning ? Glyph.Warning : Glyph.Lightbulb;
                var row = new SettingRow(result.Title, result.Detail, fix, glyph)
                {
                    Tag = "check",
                    GlyphColor = result.Severity == CheckSeverity.Good ? Theme.Success : result.Severity == CheckSeverity.Warning ? Theme.Warning : Theme.Accent,
                };
                Controls.Add(row);
                Controls.SetChildIndex(row, index++);
            }
            ResumeLayout();
        }

        void RebuildRecent()
        {
            SuspendLayout();
            foreach (var old in Controls.OfType<Control>().Where(c => (c.Tag as string) == "recent").ToList())
            {
                Controls.Remove(old);
                old.Dispose();
            }

            var history = State.Current.History.Take(6).ToList();
            _recentHeader.Visible = true;
            if (history.Count == 0)
            {
                Controls.Add(new Paragraph("Games you play will show up here, so you can hop back in with one click.") { Tag = "recent" });
            }
            foreach (var h in history)
            {
                var play = new NButton("Play", ButtonKind.Secondary, Glyph.Play).FitToText(Theme.S(86));
                long placeId = h.PlaceId;
                play.Click += (_, __) => Main.PlayAndClose($"roblox://experiences/start?placeId={placeId}");
                string name = string.IsNullOrEmpty(h.Name) ? $"Place {h.PlaceId}" : h.Name;
                string detail = (string.IsNullOrEmpty(h.Creator) ? "" : $"by {h.Creator} \u2022 ") + Utils.TimeAgo(h.LastPlayedUtc) +
                                (string.IsNullOrEmpty(h.ServerLocation) ? "" : $" \u2022 server in {h.ServerLocation}") +
                                (h.TimesPlayed > 1 ? $" \u2022 played {h.TimesPlayed}x" : "");
                Controls.Add(new SettingRow(name, detail, play, Glyph.Game) { Tag = "recent" });
            }
            ResumeLayout();
            PerformLayout();
        }

        /// <summary>The big card at the top with the Play button.</summary>
        sealed class HeroCard : Card, IAutoHeight
        {
            readonly NButton _play;
            readonly NButton _update;
            readonly Label _line1;
            readonly Label _line2;

            public HeroCard(MainForm main)
            {
                Radius = Theme.S(14);
                BackColor = Theme.Surface;
                _line1 = Factory.Label("", Theme.H2);
                _line2 = Factory.Label("", Theme.Body, Theme.TextDim);
                _line1.BackColor = _line2.BackColor = Color.Transparent; // let the accent glow show through
                _play = new NButton("Play Roblox", ButtonKind.Primary, Glyph.Play) { Font = Theme.Title, Radius = Theme.S(10) };
                _play.FitToText(Theme.S(170));
                _play.Height = Theme.S(46);
                _play.Click += (_, __) => main.PlayOrCloseRoblox();
                _main = main;
                main.StylePlayButton(_play, main.RobloxRunning);
                main.RobloxStateChanged += OnRobloxStateChanged;
                _update = new NButton("Check for Roblox updates", ButtonKind.Ghost, Glyph.Refresh).FitToText();
                _update.Click += (_, __) => Launcher.UpdateRoblox();
                Controls.AddRange(new Control[] { _line1, _line2, _play, _update });
            }

            readonly MainForm _main;

            void OnRobloxStateChanged()
            {
                if (IsDisposed) return;
                _main.StylePlayButton(_play, _main.RobloxRunning);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && _main != null) _main.RobloxStateChanged -= OnRobloxStateChanged;
                base.Dispose(disposing);
            }

            public void RefreshInfo()
            {
                var s = Settings.Current;
                _line1.Text = $"{Profiles.Emoji(s.Profile)}  {Profiles.Title(s.Profile)} profile";
                string version = string.IsNullOrEmpty(State.Current.RobloxVersionName)
                    ? "Roblox will be downloaded the first time you press Play (about 220 MB)."
                    : $"Roblox {State.Current.RobloxVersionName} is installed. NeuzStrap checks for updates every launch.";
                _line2.Text = version;
                _line1.Font = Theme.H2;
                Invalidate();
            }

            public int MeasureHeight(int width) => Theme.S(148);

            protected override void OnLayout(LayoutEventArgs e)
            {
                if (_play == null) { base.OnLayout(e); return; } // still constructing
                int pad = Theme.S(24);
                int right = Width - pad - _play.Width;
                _line1.SetBounds(pad, Theme.S(30), right - pad - Theme.S(10), Theme.S(30));
                _line2.SetBounds(pad, Theme.S(64), right - pad - Theme.S(10), Theme.S(44));
                _play.Location = new Point(right, Theme.S(28));
                _update.Location = new Point(Width - pad - _update.Width, _play.Bottom + Theme.S(10));
                base.OnLayout(e);
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                base.OnPaintBackground(e);
                var g = e.Graphics;
                Draw.Smooth(g);
                // soft accent glow in the corner
                using (var path = new GraphicsPath())
                {
                    var glow = new RectangleF(-Width * 0.25f, -Height * 0.9f, Width * 0.9f, Height * 2.2f);
                    path.AddEllipse(glow);
                    using (var brush = new PathGradientBrush(path)
                    {
                        CenterColor = Color.FromArgb(48, Theme.Accent),
                        SurroundColors = new[] { Color.FromArgb(0, Theme.Accent) },
                    })
                    using (var clip = Draw.Round(new RectangleF(0, 0, Width - 1, Height - 1), Radius))
                    {
                        g.SetClip(clip);
                        g.FillEllipse(brush, glow);
                        g.ResetClip();
                    }
                }
                Draw.StrokeRound(g, new RectangleF(0, 0, Width, Height), Radius, Theme.Mix(Theme.Border, Theme.Accent, 0.25f));
            }
        }
    }
}
