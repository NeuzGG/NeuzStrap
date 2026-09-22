using System;
using System.Drawing;
using System.Windows.Forms;
using NeuzStrap.Core;

namespace NeuzStrap.UI
{
    /// <summary>A small notification in the bottom-right corner that never steals focus from the game.</summary>
    public sealed class Toast : Form
    {
        static Toast _current;

        readonly string _title, _body, _tag;
        readonly Timer _life = new Timer();

        public static void Show(string title, string body, string tag = null, int seconds = 7)
        {
            try
            {
                _current?.Close();
                _current = new Toast(title, body, tag, seconds);
                _current.Show();
            }
            catch (Exception ex) { Logger.Warn("Toast", ex.Message); }
        }

        Toast(string title, string body, string tag, int seconds)
        {
            _title = title;
            _body = body;
            _tag = tag;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Surface;
            DoubleBuffered = true;
            Font = Theme.Body;

            int width = Theme.S(360);
            int textW = width - Theme.S(66);
            int bodyH = Draw.Measure(body ?? "", Theme.Small, textW).Height;
            Size = new Size(width, Math.Max(Theme.S(76), Theme.S(44) + bodyH + Theme.S(14)));

            var area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - Theme.S(16), area.Bottom - Height - Theme.S(16));

            _life.Interval = seconds * 1000;
            _life.Tick += (_, __) => Close();
            _life.Start();
            Click += (_, __) => Close();
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 /* WS_EX_NOACTIVATE */ | 0x00000080 /* WS_EX_TOOLWINDOW */ | 0x00000008 /* WS_EX_TOPMOST */;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!Native.TryRoundCorners(Handle))
                Region = new Region(Draw.Round(new RectangleF(0, 0, Width, Height), Theme.S(10)));
            Native.SetBorderColor(Handle, Theme.Border);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Draw.Smooth(g);
            using (var pen = new Pen(Theme.Border)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            Draw.FillRound(g, new RectangleF(Theme.S(12), Theme.S(14), Theme.S(4), Height - Theme.S(28)), Theme.S(2), Theme.Accent);

            using (var icon = AppIcon.Bitmap(Theme.S(28)))
                g.DrawImage(icon, Theme.S(24), Theme.S(14));

            int x = Theme.S(62);
            var titleRect = new Rectangle(x, Theme.S(12), Width - x - Theme.S(14), Theme.S(22));
            Draw.Text(g, _title, Theme.BodyBold, titleRect, Theme.Text);
            if (!string.IsNullOrEmpty(_tag))
            {
                var tw = Draw.Measure(_title, Theme.BodyBold).Width;
                var tagSize = Draw.Measure(_tag, Theme.Small);
                var tagRect = new Rectangle(x + tw + Theme.S(8), Theme.S(14), tagSize.Width + Theme.S(12), Theme.S(18));
                Draw.FillRound(g, tagRect, Theme.S(9), Theme.AccentSoft);
                Draw.CenterText(g, _tag, Theme.Small, tagRect, Theme.Accent);
            }
            TextRenderer.DrawText(g, _body ?? "", Theme.Small, new Rectangle(x, Theme.S(36), Width - x - Theme.S(14), Height - Theme.S(40)), Theme.TextDim,
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _life.Dispose();
            if (_current == this) _current = null;
            base.OnFormClosed(e);
        }
    }
}
