using System;
using System.Drawing;
using System.Windows.Forms;
using NeuzStrap.Core;
using NeuzStrap.UI.Controls;

namespace NeuzStrap.UI.Pages
{
    public sealed class OverlayPage : Page
    {
        readonly OverlayPreview _preview;
        readonly KeyChips _chips;

        public override string Title => "On-screen";
        public override string Subtitle => "FPS, clicks per second and the keys you pick, on top of the game.";

        public OverlayPage(MainForm main) : base(main)
        {
            Section("FPS counter");
            ToggleRow("Show Roblox's FPS counter", "Turns on Roblox's own FPS, ping and memory panel (the same one as Shift+F5 in-game). " +
                      "NeuzStrap can't measure Roblox's frame rate itself without injecting into the game, which would risk your account, so it uses Roblox's own counter.",
                      () => S.RobloxFpsCounter, v => S.RobloxFpsCounter = v, Glyph.Speed);

            Section("NeuzStrap overlay");
            _preview = Add(new OverlayPreview());
            ToggleRow("Show the overlay while playing", "A small panel drawn on top of Roblox. It can't appear over exclusive fullscreen, so use windowed or normal fullscreen.",
                      () => S.Overlay, v => { S.Overlay = v; _preview.Invalidate(); }, Glyph.Monitor);
            ToggleRow("Clicks per second (CPS)", "Counts your left clicks each second, plus right clicks when you use them.",
                      () => S.OverlayCps, v => { S.OverlayCps = v; _preview.Invalidate(); }, Glyph.Mouse);
            ToggleRow("Keys per second (KPS)", "How many times you press the keys below each second.",
                      () => S.OverlayKps, v => { S.OverlayKps = v; _preview.Invalidate(); }, Glyph.Keyboard);
            ToggleRow("Key display", "Lights up each key below as you hold it.",
                      () => S.OverlayKeys, v => { S.OverlayKeys = v; _preview.Invalidate(); }, Glyph.Keyboard);

            Note("Your keys (click one to remove it):");
            _chips = Add(new KeyChips());
            _chips.Changed += () => _preview.Invalidate();
            var reset = Add(new ButtonBar());
            reset.AddButton(new NButton("Reset to W A S D", ButtonKind.Ghost, Glyph.Refresh).FitToText(), (_, __) => _chips.ResetToDefault());
            ToggleRow("Rainbow colors", "Cycles the colors while you play. Off uses your accent color.",
                      () => S.OverlayRainbow, v => { S.OverlayRainbow = v; _preview.Restart(); }, Glyph.Heart);

            DropdownRow("Position", "Where it sits on the Roblox window.",
                new Dropdown(200).Add("Top left", OverlayCorner.TopLeft).Add("Top center", OverlayCorner.TopCenter).Add("Top right", OverlayCorner.TopRight)
                                 .Add("Bottom left", OverlayCorner.BottomLeft).Add("Bottom center", OverlayCorner.BottomCenter).Add("Bottom right", OverlayCorner.BottomRight),
                () => S.OverlayPosition, v => { S.OverlayPosition = (OverlayCorner)v; _preview.Invalidate(); }, Glyph.Monitor);
            DropdownRow("Size", "How big the panel is.",
                new Dropdown(200).Add("Small", OverlaySize.Small).Add("Medium", OverlaySize.Medium).Add("Large", OverlaySize.Large),
                () => S.OverlayScale, v => { S.OverlayScale = (OverlaySize)v; _preview.Invalidate(); });

            Section("Good to know");
            Note("\u2022 The overlay only checks whether the keys you picked (and the mouse buttons) are held down right now. " +
                 "It doesn't install a keyboard hook and never sees what you type.\n" +
                 "\u2022 It only shows while the Roblox window is in front, and it never blocks your clicks.\n" +
                 "\u2022 It redraws about 30 times a second, so leave it off if you want every last frame.");
        }

        public override void OnNavigatedTo() => _preview.Restart();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _preview?.Stop();
            base.Dispose(disposing);
        }

        /// <summary>Live preview of the overlay, on a "game screen" backdrop.</summary>
        sealed class OverlayPreview : Control, IAutoHeight
        {
            readonly Timer _timer = new Timer { Interval = 40 };
            float _hue;

            public OverlayPreview()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                _timer.Tick += (_, __) =>
                {
                    if (!Settings.Current.OverlayRainbow) { _timer.Stop(); return; }
                    _hue = (_hue + 3f) % 360f;
                    Invalidate();
                };
            }

            public int MeasureHeight(int width) => Theme.S(150);

            public void Restart()
            {
                _hue = 0;
                Invalidate();
                if (Settings.Current.OverlayRainbow && !_timer.Enabled) _timer.Start();
            }

            public void Stop() => _timer.Stop();

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Parent?.BackColor ?? Theme.Bg);
                Draw.Smooth(g);

                // a pretend Roblox window
                var screen = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var sky = new System.Drawing.Drawing2D.LinearGradientBrush(screen, Theme.Hex("#3E6E92"), Theme.Hex("#2C4A33"), 90f))
                using (var path = Draw.Round(screen, Theme.S(10)))
                    g.FillPath(sky, path);
                Draw.StrokeRound(g, screen, Theme.S(10), Theme.Border);

                var s = Settings.Current;
                if (!s.Overlay || (!s.OverlayCps && !s.OverlayKps && !s.OverlayKeys))
                {
                    Draw.CenterText(g, "Overlay is off", Theme.Body, screen, Color.FromArgb(200, 255, 255, 255));
                    return;
                }

                using (var panel = OverlayRenderer.Render(s, OverlayState.SampleFor(s), _hue, Theme.Scale))
                {
                    int margin = Theme.S(12);
                    int x;
                    switch (s.OverlayPosition)
                    {
                        case OverlayCorner.TopCenter:
                        case OverlayCorner.BottomCenter: x = (Width - panel.Width) / 2; break;
                        case OverlayCorner.TopRight:
                        case OverlayCorner.BottomRight: x = Width - panel.Width - margin; break;
                        default: x = margin; break;
                    }
                    bool bottom = s.OverlayPosition == OverlayCorner.BottomLeft || s.OverlayPosition == OverlayCorner.BottomCenter
                                  || s.OverlayPosition == OverlayCorner.BottomRight;
                    int y = bottom ? Height - panel.Height - margin : margin;
                    g.DrawImage(panel, x, y);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) { _timer.Stop(); _timer.Dispose(); }
                base.Dispose(disposing);
            }
        }
    }
}
