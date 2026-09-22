using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace NeuzStrap.UI.Controls
{
    /// <summary>Base for custom-painted controls: double buffered, tracks hover/press.</summary>
    public class NControl : Control
    {
        protected bool Hover, Pressed;

        public NControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Font = Theme.Body;
            ForeColor = Theme.Text;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { Hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Hover = false; Pressed = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { Pressed = true; Invalidate(); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            Pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected void PaintFocus(Graphics g, Rectangle r, float radius)
        {
            if (Focused && ShowFocusCues)
                Draw.StrokeRound(g, r, radius, Theme.Mix(Theme.Accent, Color.White, 0.3f), Theme.S(1.5f));
        }

        protected Color ParentBack => Parent?.BackColor ?? Theme.Bg;
    }

    public enum ButtonKind { Primary, Secondary, Ghost, Danger }

    public class NButton : NControl, IButtonControl
    {
        ButtonKind _kind = ButtonKind.Secondary;
        string _glyph;

        public ButtonKind Kind { get => _kind; set { _kind = value; Invalidate(); } }
        public string Glyph { get => _glyph; set { _glyph = value; Invalidate(); } }
        public int Radius { get; set; } = Theme.S(8);
        public DialogResult DialogResult { get; set; }
        public Font GlyphFont { get; set; } = Theme.Icons;

        public NButton(string text, ButtonKind kind = ButtonKind.Secondary, string glyph = null)
        {
            Text = text;
            _kind = kind;
            _glyph = glyph;
            Font = kind == ButtonKind.Primary ? Theme.BodyBold : Theme.Body;
            TabStop = true;
            FitToText();
        }

        public NButton FitToText(int minWidth = 0)
        {
            int textW = string.IsNullOrEmpty(Text) ? 0 : Draw.Measure(Text, Font).Width;
            int glyphW = string.IsNullOrEmpty(_glyph) ? 0 : Theme.S(18) + (textW > 0 ? Theme.S(6) : 0);
            Size = new Size(Math.Max(minWidth, textW + glyphW + Theme.S(28)), Theme.S(34));
            return this;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(ParentBack);
            Draw.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);

            Color bg, fg, border = Color.Empty;
            switch (_kind)
            {
                case ButtonKind.Primary:
                    bg = Pressed ? Theme.AccentPressed : Hover ? Theme.AccentHover : Theme.Accent;
                    fg = Theme.OnAccent;
                    break;
                case ButtonKind.Danger:
                    bg = Pressed ? Theme.Darken(Theme.Danger, 0.5f) : Hover ? Theme.Mix(Theme.Surface, Theme.Danger, 0.35f) : Theme.Mix(Theme.Surface, Theme.Danger, 0.18f);
                    fg = Theme.Lighten(Theme.Danger, 0.3f);
                    break;
                case ButtonKind.Ghost:
                    bg = Pressed ? Theme.Border : Hover ? Theme.SurfaceHover : ParentBack;
                    fg = Theme.Text;
                    break;
                default:
                    bg = Pressed ? Theme.Border : Hover ? Theme.Mix(Theme.SurfaceHover, Color.White, 0.04f) : Theme.SurfaceHover;
                    fg = Theme.Text;
                    border = Theme.Border;
                    break;
            }
            if (!Enabled)
            {
                bg = Theme.Mix(ParentBack, bg, 0.45f);
                fg = Theme.TextFaint;
            }

            Draw.FillRound(g, r, Radius, bg);
            if (border != Color.Empty) Draw.StrokeRound(g, r, Radius, border);

            int textW = string.IsNullOrEmpty(Text) ? 0 : Draw.Measure(Text, Font).Width;
            int glyphW = string.IsNullOrEmpty(_glyph) ? 0 : Theme.S(18);
            int gap = glyphW > 0 && textW > 0 ? Theme.S(6) : 0;
            int x = (Width - (textW + glyphW + gap)) / 2;
            if (glyphW > 0)
            {
                Draw.Glyph(g, _glyph, GlyphFont, new Rectangle(x, 0, glyphW, Height), fg);
                x += glyphW + gap;
            }
            if (textW > 0)
                Draw.Text(g, Text, Font, new Rectangle(x, 0, textW + 2, Height), fg, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            PaintFocus(g, r, Radius);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) PerformClick();
            base.OnKeyUp(e);
        }

        public void NotifyDefault(bool value) { }

        public void PerformClick()
        {
            if (Enabled) OnClick(EventArgs.Empty);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (DialogResult != DialogResult.None && FindForm() is Form f) f.DialogResult = DialogResult;
        }
    }

    public class Toggle : NControl
    {
        bool _checked;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Sets the value without raising CheckedChanged (for loading settings).</summary>
        public void SetSilently(bool value) { _checked = value; Invalidate(); }

        public Toggle()
        {
            Size = Theme.S(44, 24);
            TabStop = true;
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) Checked = !Checked;
            base.OnKeyUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(ParentBack);
            Draw.Smooth(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float rad = r.Height / 2;

            Color track = _checked ? (Hover ? Theme.AccentHover : Theme.Accent) : (Hover ? Theme.Lighten(Theme.TrackOff, 0.08f) : Theme.TrackOff);
            if (!Enabled) track = Theme.Mix(ParentBack, track, 0.4f);
            Draw.FillRound(g, r, rad, track);

            float pad = Theme.S(3f);
            float d = r.Height - pad * 2;
            float x = _checked ? r.Right - pad - d : r.X + pad;
            using (var b = new SolidBrush(_checked ? Color.White : Theme.Hex("#D6D6E2")))
                g.FillEllipse(b, x, r.Y + pad, d, d);

            PaintFocus(g, Rectangle.Round(r), rad);
        }
    }

    /// <summary>A rounded card. Children inherit its fill color as their background.</summary>
    public class Card : Panel
    {
        public int Radius { get; set; } = Theme.S(12);
        public Color? BorderColor { get; set; }
        public int BorderWidth { get; set; } = 1;

        public Card()
        {
            // No layout while constructing: subclasses' OnLayout would run before their fields exist.
            SuspendLayout();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            Padding = Theme.S(18, 14, 18, 14);
            ResumeLayout(false);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Bg);
            Draw.Smooth(g);
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            Draw.FillRound(g, r, Radius, BackColor);
            if (BorderColor.HasValue) Draw.StrokeRound(g, new RectangleF(0, 0, Width, Height), Radius, BorderColor.Value, BorderWidth);
        }
    }

    /// <summary>Controls that know how tall they need to be for a given width.</summary>
    public interface IAutoHeight
    {
        int MeasureHeight(int width);
    }

    /// <summary>
    /// Vertical auto-layout. Children are stretched to full width and scroll with the mouse wheel;
    /// a thin custom scrollbar replaces the chunky light-grey Windows one.
    /// </summary>
    public class Stack : Panel
    {
        readonly ThinScrollBar _bar;
        bool _layingOut;
        int _scroll;
        int _contentHeight;

        public int Gap { get; set; } = Theme.S(10);
        public bool Scrollable { get; set; } = true;

        public Stack()
        {
            _bar = new ThinScrollBar(this) { Visible = false };
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Padding = Theme.S(28, 4, 28, 28);
            Controls.Add(_bar);
        }

        public int ContentHeight => _contentHeight;
        public int MaxScroll => Scrollable ? Math.Max(0, _contentHeight - ClientSize.Height) : 0;

        public int ScrollOffset
        {
            get => _scroll;
            set
            {
                int v = Math.Max(0, Math.Min(MaxScroll, value));
                if (v == _scroll) return;
                _scroll = v;
                PerformLayout();
                _bar.Invalidate();
            }
        }

        /// <summary>Total height the children need at this width (without scrolling).</summary>
        public int MeasureContent(int width)
        {
            int inner = width - Padding.Horizontal;
            int y = Padding.Top;
            bool any = false;
            foreach (Control c in Controls)
            {
                if (c == _bar || !c.IsSetVisible()) continue;
                y += c.Margin.Top + (c is IAutoHeight ah ? ah.MeasureHeight(inner) : c.Height) + Gap;
                any = true;
            }
            return (any ? y - Gap : y) + Padding.Bottom;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            if (_layingOut || _bar == null) return;
            _layingOut = true;
            try
            {
                int width = ClientSize.Width - Padding.Horizontal;
                var placed = new List<(Control Control, int Top, int Height)>();
                int y = Padding.Top;
                foreach (Control c in Controls)
                {
                    if (c == _bar || !c.IsSetVisible()) continue;
                    int h = c is IAutoHeight ah ? ah.MeasureHeight(width) : c.Height;
                    y += c.Margin.Top;
                    placed.Add((c, y, h));
                    y += h + Gap;
                }
                _contentHeight = (placed.Count > 0 ? y - Gap : y) + Padding.Bottom;
                _scroll = Math.Max(0, Math.Min(MaxScroll, _scroll));

                foreach (var p in placed)
                    p.Control.SetBounds(Padding.Left, p.Top - _scroll, width, p.Height);

                _bar.Visible = MaxScroll > 0;
                _bar.SetBounds(ClientSize.Width - Theme.S(14), Theme.S(6), Theme.S(10), Math.Max(0, ClientSize.Height - Theme.S(12)));
                _bar.BringToFront();
                _bar.Invalidate();
            }
            finally { _layingOut = false; }
            base.OnLayout(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollOffset -= e.Delta * Theme.S(72) / 120;
            if (e is HandledMouseEventArgs h) h.Handled = true;
            base.OnMouseWheel(e);
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            // keep keyboard (Tab) navigation visible
            if (e.Control != _bar) e.Control.Enter += (s, __) => EnsureVisible((Control)s);
        }

        public void EnsureVisible(Control c)
        {
            if (c.Top < 0) ScrollOffset += c.Top - Theme.S(8);
            else if (c.Bottom > ClientSize.Height) ScrollOffset += Math.Min(c.Top, c.Bottom - ClientSize.Height + Theme.S(8));
        }

        public void ScrollToTop() => ScrollOffset = 0;
    }

    /// <summary>Slim overlay scrollbar for <see cref="Stack"/>: drag the thumb, click the track, or use the wheel.</summary>
    public sealed class ThinScrollBar : Control
    {
        readonly Stack _owner;
        bool _hover, _drag;
        int _dragStartY, _dragStartScroll;

        public ThinScrollBar(Stack owner)
        {
            _owner = owner;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        Rectangle Thumb
        {
            get
            {
                int track = Height;
                int content = Math.Max(1, _owner.ContentHeight);
                int view = _owner.ClientSize.Height;
                int th = Math.Min(track, Math.Max(Theme.S(40), (int)(track * (double)view / content)));
                int max = Math.Max(1, _owner.MaxScroll);
                int ty = (int)((track - th) * (double)_owner.ScrollOffset / max);
                return new Rectangle(0, ty, Width, th);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(_owner.BackColor);
            Draw.Smooth(g);
            var t = Thumb;
            float w = _hover || _drag ? Theme.S(8f) : Theme.S(5f);
            var r = new RectangleF(Width - w - Theme.S(1f), t.Y, w, t.Height);
            Color c = _drag ? Theme.Hex("#6C6C84") : _hover ? Theme.Hex("#56566C") : Theme.Hex("#3A3A4C");
            Draw.FillRound(g, r, w / 2f, c);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var t = Thumb;
            if (t.Contains(e.Location))
            {
                _drag = true;
                _dragStartY = e.Y;
                _dragStartScroll = _owner.ScrollOffset;
                Capture = true;
            }
            else
            {
                int page = (int)(_owner.ClientSize.Height * 0.85);
                _owner.ScrollOffset += e.Y < t.Top ? -page : page;
            }
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_drag)
            {
                int track = Math.Max(1, Height - Thumb.Height);
                _owner.ScrollOffset = _dragStartScroll + (int)((e.Y - _dragStartY) * (double)_owner.MaxScroll / track);
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _drag = false;
            Capture = false;
            Invalidate();
            base.OnMouseUp(e);
        }
    }

    public static class ControlExtensions
    {
        static readonly System.Reflection.MethodInfo GetStateMethod =
            typeof(Control).GetMethod("GetState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(int) }, null);

        /// <summary>
        /// The control's own Visible flag. Control.Visible also says false whenever a parent is hidden
        /// (e.g. before a page or window is shown), which makes layout code measure things as zero.
        /// </summary>
        public static bool IsSetVisible(this Control c)
        {
            try { return GetStateMethod != null ? (bool)GetStateMethod.Invoke(c, new object[] { 0x2 /* STATE_VISIBLE */ }) : c.Visible; }
            catch { return c.Visible; }
        }
    }

    public static class Factory
    {
        public static Label Label(string text, Font font = null, Color? color = null, bool autoSize = false)
        {
            return new Label
            {
                Text = text,
                Font = font ?? Theme.Body,
                ForeColor = color ?? Theme.Text,
                AutoSize = autoSize,
                UseMnemonic = false,
            };
        }
    }
}
