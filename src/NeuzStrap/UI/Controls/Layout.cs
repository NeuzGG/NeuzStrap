using System;
using System.Drawing;
using System.Windows.Forms;

namespace NeuzStrap.UI.Controls
{
    /// <summary>One settings line: title + description on the left, an editor (toggle, dropdown, button...) on the right.</summary>
    public class SettingRow : Card, IAutoHeight
    {
        readonly Label _title;
        readonly Label _desc;
        readonly Control _editor;
        readonly string _glyph;
        Color _glyphColor = Theme.TextDim;

        public Label TitleLabel => _title;
        public Label DescriptionLabel => _desc;
        public Control Editor => _editor;

        public string Description
        {
            get => _desc.Text;
            set { _desc.Text = value; Parent?.PerformLayout(); }
        }

        public Color GlyphColor { get => _glyphColor; set { _glyphColor = value; Invalidate(); } }

        public SettingRow(string title, string description, Control editor, string glyph = null)
        {
            _glyph = glyph;
            Padding = Theme.S(18, 13, 16, 13);
            Radius = Theme.S(10);

            _title = Factory.Label(title, Theme.BodyBold);
            _desc = Factory.Label(description ?? "", Theme.Small, Theme.TextDim);
            Controls.Add(_title);
            Controls.Add(_desc);
            if (editor != null)
            {
                _editor = editor;
                Controls.Add(editor);
            }

            // clicking anywhere on a toggle row flips the toggle - bigger target, friendlier on touchpads
            if (editor is Toggle t)
            {
                Cursor = Cursors.Hand;
                EventHandler flip = (_, __) => t.Checked = !t.Checked;
                Click += flip;
                _title.Click += flip;
                _desc.Click += flip;
                _title.Cursor = _desc.Cursor = Cursors.Hand;
            }
        }

        int GlyphSpace => _glyph == null ? 0 : Theme.S(34);
        int EditorSpace => _editor == null ? 0 : _editor.Width + Theme.S(18);

        int TextWidth(int width) => Math.Max(Theme.S(120), width - Padding.Horizontal - GlyphSpace - EditorSpace);

        public int MeasureHeight(int width)
        {
            int tw = TextWidth(width);
            int th = Draw.Measure(_title.Text, _title.Font, tw).Height;
            int dh = string.IsNullOrEmpty(_desc.Text) ? 0 : Draw.Measure(_desc.Text, _desc.Font, tw).Height + Theme.S(2);
            int content = Math.Max(th + dh, _editor?.Height ?? 0);
            return Math.Max(Theme.S(58), content + Padding.Vertical);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            if (_title == null) { base.OnLayout(e); return; } // still constructing
            int tw = TextWidth(Width);
            int th = Draw.Measure(_title.Text, _title.Font, tw).Height;
            int dh = string.IsNullOrEmpty(_desc.Text) ? 0 : Draw.Measure(_desc.Text, _desc.Font, tw).Height + Theme.S(2);
            int x = Padding.Left + GlyphSpace;
            int y = (Height - th - dh) / 2;
            _title.SetBounds(x, y, tw, th);
            _desc.SetBounds(x, y + th + Theme.S(1), tw, dh);
            _desc.Visible = dh > 0;
            if (_editor != null)
                _editor.Location = new Point(Width - Padding.Right - _editor.Width, (Height - _editor.Height) / 2);
            base.OnLayout(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_glyph != null)
                Draw.Glyph(e.Graphics, _glyph, Theme.IconsLarge, new Rectangle(Padding.Left - Theme.S(2), 0, Theme.S(30), Height), _glyphColor);
        }
    }

    /// <summary>Small uppercase section title between groups of rows.</summary>
    public class SectionHeader : Control, IAutoHeight
    {
        public string Hint { get; set; }

        public SectionHeader(string text, string hint = null)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Text = text;
            Hint = hint;
            Margin = new Padding(0, Theme.S(12), 0, 0);
        }

        public int MeasureHeight(int width) =>
            Theme.S(22) + (string.IsNullOrEmpty(Hint) ? 0 : Draw.Measure(Hint, Theme.Small, width).Height + Theme.S(2));

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? Theme.Bg);
            Draw.Text(e.Graphics, Text.ToUpperInvariant(), Theme.SmallBold, new Rectangle(Theme.S(2), 0, Width, Theme.S(22)), Theme.Accent);
            if (!string.IsNullOrEmpty(Hint))
                TextRenderer.DrawText(e.Graphics, Hint, Theme.Small, new Rectangle(Theme.S(2), Theme.S(22), Width - Theme.S(2), Height - Theme.S(22)), Theme.TextDim,
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>A block of wrapped text that sizes itself (for explanations inside a Stack).</summary>
    public class Paragraph : Control, IAutoHeight
    {
        public Color TextColor { get; set; } = Theme.TextDim;

        public Paragraph(string text, Font font = null, Color? color = null)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Text = text;
            Font = font ?? Theme.Body;
            if (color.HasValue) TextColor = color.Value;
        }

        public int MeasureHeight(int width) => Draw.Measure(Text, Font, width).Height + Theme.S(2);

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Parent?.PerformLayout(); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? Theme.Bg);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, TextColor, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>A row of buttons that wraps onto more lines in narrow windows.</summary>
    public class ButtonBar : Control, IAutoHeight
    {
        public ButtonBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        public NButton AddButton(NButton b, EventHandler onClick)
        {
            b.Click += onClick;
            Controls.Add(b);
            return b;
        }

        public int MeasureHeight(int width) => Arrange(width, false);

        int Arrange(int width, bool apply)
        {
            int x = 0, y = 0, gap = Theme.S(8), rowH = 0;
            foreach (Control c in Controls)
            {
                if (!c.Visible) continue;
                if (x > 0 && x + c.Width > width) { x = 0; y += rowH + gap; rowH = 0; }
                if (apply) c.Location = new Point(x, y);
                x += c.Width + gap;
                rowH = Math.Max(rowH, c.Height);
            }
            return y + rowH;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            Arrange(Width, true);
            base.OnLayout(e);
        }

        protected override void OnPaint(PaintEventArgs e) => e.Graphics.Clear(Parent?.BackColor ?? Theme.Bg);
    }

    /// <summary>Sidebar navigation entry.</summary>
    public class NavItem : NControl
    {
        bool _selected;
        public string Glyph { get; }
        public object Tag2 { get; set; }

        public bool Selected
        {
            get => _selected;
            set { _selected = value; Invalidate(); }
        }

        public NavItem(string text, string glyph)
        {
            Text = text;
            Glyph = glyph;
            Size = new Size(Theme.S(200), Theme.S(40));
            TabStop = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(ParentBack);
            Draw.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (_selected) Draw.FillRound(g, r, Theme.S(8), Theme.AccentSoft);
            else if (Hover) Draw.FillRound(g, r, Theme.S(8), Theme.SurfaceHover);

            if (_selected)
                Draw.FillRound(g, new RectangleF(Theme.S(4), Height / 2f - Theme.S(9), Theme.S(3), Theme.S(18)), Theme.S(1.5f), Theme.Accent);

            Color fg = _selected ? Theme.Text : Theme.TextDim;
            Draw.Glyph(g, Glyph, Theme.Icons, new Rectangle(Theme.S(14), 0, Theme.S(24), Height), _selected ? Theme.Accent : Theme.TextDim);
            Draw.Text(g, Text, _selected ? Theme.BodyBold : Theme.Body, new Rectangle(Theme.S(48), 0, Width - Theme.S(52), Height), fg);
            PaintFocus(g, r, Theme.S(8));
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) OnClick(EventArgs.Empty);
            base.OnKeyUp(e);
        }
    }

    /// <summary>Rounded progress bar with smooth easing and an indeterminate animation.</summary>
    public class ProgressX : Control
    {
        readonly Timer _timer = new Timer { Interval = 33 };
        double? _target;
        double _shown;
        float _marquee;

        public ProgressX()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = Theme.S(8);
            _timer.Tick += (_, __) => Tick();
            _timer.Start();
        }

        /// <summary>0..1, or null for "working on it" animation.</summary>
        public double? Value
        {
            get => _target;
            set
            {
                if (value.HasValue && !_target.HasValue) _shown = 0;
                _target = value.HasValue ? Math.Max(0, Math.Min(1, value.Value)) : (double?)null;
                if (!_timer.Enabled) _timer.Start();
            }
        }

        void Tick()
        {
            if (!Visible) return;
            if (_target.HasValue)
            {
                double diff = _target.Value - _shown;
                if (Math.Abs(diff) < 0.001) { _shown = _target.Value; _timer.Stop(); }
                else _shown += diff * 0.18;
            }
            else
            {
                _marquee = (_marquee + 0.012f) % 1.4f;
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Bg);
            Draw.Smooth(g);
            float rad = Height / 2f;
            var track = new RectangleF(0, 0, Width - 1, Height - 1);
            Draw.FillRound(g, track, rad, Theme.Hex("#2A2A38"));

            if (_target.HasValue)
            {
                float w = (float)(track.Width * _shown);
                if (w > 1) Draw.FillRound(g, new RectangleF(0, 0, Math.Max(w, Height), track.Height), rad, Theme.Accent);
            }
            else
            {
                float segment = track.Width * 0.3f;
                float x = (_marquee - 0.3f) * track.Width;
                var seg = RectangleF.Intersect(new RectangleF(x, 0, segment, track.Height), track);
                if (seg.Width > 1)
                {
                    using (var clip = Draw.Round(track, rad))
                    {
                        g.SetClip(clip);
                        Draw.FillRound(g, seg, rad, Theme.Accent);
                        g.ResetClip();
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
