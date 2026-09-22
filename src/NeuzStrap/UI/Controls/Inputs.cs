using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NeuzStrap.UI.Controls
{
    /// <summary>A dark dropdown (custom-drawn button + themed popup menu).</summary>
    public class Dropdown : NControl
    {
        readonly List<KeyValuePair<string, object>> _items = new List<KeyValuePair<string, object>>();
        int _selected = -1;

        public event EventHandler SelectedChanged;

        public Dropdown(int width = 200)
        {
            Size = new Size(Theme.S(width), Theme.S(34));
            TabStop = true;
        }

        public Dropdown Add(string text, object value)
        {
            _items.Add(new KeyValuePair<string, object>(text, value));
            return this;
        }

        public int SelectedIndex
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                _selected = value;
                Invalidate();
                SelectedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public object SelectedValue => _selected >= 0 && _selected < _items.Count ? _items[_selected].Value : null;

        /// <summary>Selects the item with this value without raising SelectedChanged. Falls back to the first item.</summary>
        public void SelectValueSilently(object value)
        {
            int idx = _items.FindIndex(i => Equals(i.Value, value));
            _selected = idx >= 0 ? idx : (_items.Count > 0 ? 0 : -1);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(ParentBack);
            Draw.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            int rad = Theme.S(8);
            Draw.FillRound(g, r, rad, Hover ? Theme.Mix(Theme.Input, Theme.SurfaceHover, 0.6f) : Theme.Input);
            Draw.StrokeRound(g, new Rectangle(0, 0, Width, Height), rad, Hover || Focused ? Theme.Mix(Theme.Border, Theme.Accent, 0.45f) : Theme.Border);

            string text = _selected >= 0 && _selected < _items.Count ? _items[_selected].Key : "";
            int chevronW = Theme.S(28);
            Draw.Text(g, text, Font, new Rectangle(Theme.S(12), 0, Width - Theme.S(12) - chevronW, Height), Enabled ? Theme.Text : Theme.TextFaint);
            using (var small = new Font(Theme.Icons.FontFamily, 8f))
                Draw.Glyph(g, Glyph.ChevronDown, small, new Rectangle(Width - chevronW - Theme.S(2), 0, chevronW, Height), Theme.TextDim);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            ShowMenu();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter || (e.Alt && e.KeyCode == Keys.Down)) ShowMenu();
            else if (e.KeyCode == Keys.Down && _selected < _items.Count - 1) SelectedIndex++;
            else if (e.KeyCode == Keys.Up && _selected > 0) SelectedIndex--;
            base.OnKeyDown(e);
        }

        protected override bool IsInputKey(Keys keyData) => keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData);

        void ShowMenu()
        {
            var menu = new ContextMenuStrip { Renderer = new DarkMenuRenderer(), ShowImageMargin = false, ShowCheckMargin = false, Font = Font };
            for (int i = 0; i < _items.Count; i++)
            {
                int index = i;
                var item = new ToolStripMenuItem(_items[i].Key)
                {
                    Checked = i == _selected,
                    AutoSize = false,
                    Size = new Size(Width - Theme.S(4), Theme.S(32)),
                };
                item.Click += (_, __) => SelectedIndex = index;
                menu.Items.Add(item);
            }
            menu.Closed += (_, __) => BeginInvoke((Action)(() => menu.Dispose()));
            menu.Show(this, new Point(0, Height + Theme.S(2)));
        }
    }

    /// <summary>A dark text box with rounded border.</summary>
    public class TextInput : Panel
    {
        public TextBox Box { get; }

        public new event EventHandler TextChanged
        {
            add => Box.TextChanged += value;
            remove => Box.TextChanged -= value;
        }

        public override string Text
        {
            get => Box.Text;
            set => Box.Text = value;
        }

        public string Placeholder { get; set; }

        public TextInput(int width = 220, bool mono = false)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Input;
            Size = new Size(Theme.S(width), Theme.S(34));
            Cursor = Cursors.IBeam;

            Box = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Input,
                ForeColor = Theme.Text,
                Font = mono ? Theme.Mono : Theme.Body,
            };
            Controls.Add(Box);
            Box.GotFocus += (_, __) => Invalidate();
            Box.LostFocus += (_, __) => Invalidate();
            Box.TextChanged += (_, __) => { if (!string.IsNullOrEmpty(Placeholder)) Invalidate(); };
            Click += (_, __) => Box.Focus();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (Box == null) return; // still constructing
            int pad = Theme.S(11);
            Box.SetBounds(pad, (Height - Box.PreferredHeight) / 2 + 1, Width - pad * 2, Box.PreferredHeight);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Bg);
            Draw.Smooth(g);
            int rad = Theme.S(8);
            Draw.FillRound(g, new Rectangle(0, 0, Width - 1, Height - 1), rad, Theme.Input);
            Draw.StrokeRound(g, new Rectangle(0, 0, Width, Height), rad, Box.Focused ? Theme.Accent : Theme.Border);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!string.IsNullOrEmpty(Placeholder) && Box.TextLength == 0 && !Box.Focused)
                Draw.Text(e.Graphics, Placeholder, Box.Font, new Rectangle(Box.Left, 0, Box.Width, Height), Theme.TextFaint);
        }
    }

    /// <summary>Dark styling for every popup menu (dropdowns, tray menu).</summary>
    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColors()) { RoundedEdges = false; }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (var pen = new Pen(Theme.Border))
                e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var g = e.Graphics;
            var r = new Rectangle(Theme.S(3), 1, e.Item.Width - Theme.S(6), e.Item.Height - 2);
            if (e.Item.Selected && e.Item.Enabled)
            {
                Draw.Smooth(g);
                Draw.FillRound(g, r, Theme.S(6), Theme.SurfaceHover);
            }
            if (e.Item is ToolStripMenuItem mi && mi.Checked)
            {
                Draw.Smooth(g);
                Draw.FillRound(g, new RectangleF(r.X + Theme.S(3), r.Y + r.Height / 2f - Theme.S(7), Theme.S(3), Theme.S(14)), Theme.S(1.5f), Theme.Accent);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.Text : Theme.TextDim;
            if (e.Item is ToolStripMenuItem mi && mi.Checked) e.TextColor = Theme.Accent;
            var r = e.TextRectangle;
            if (e.Item.Owner is ToolStripDropDownMenu dd && !dd.ShowImageMargin && !dd.ShowCheckMargin)
                r = new Rectangle(Theme.S(14), 0, e.Item.Width - Theme.S(20), e.Item.Height);
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, r, e.TextColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) { }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (var pen = new Pen(Theme.Border))
                e.Graphics.DrawLine(pen, Theme.S(8), y, e.Item.Width - Theme.S(8), y);
        }

        sealed class DarkColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Theme.Surface;
            public override Color ImageMarginGradientBegin => Theme.Surface;
            public override Color ImageMarginGradientMiddle => Theme.Surface;
            public override Color ImageMarginGradientEnd => Theme.Surface;
            public override Color MenuBorder => Theme.Border;
            public override Color MenuItemBorder => Theme.SurfaceHover;
            public override Color MenuItemSelected => Theme.SurfaceHover;
            public override Color SeparatorDark => Theme.Border;
            public override Color SeparatorLight => Theme.Border;
        }
    }
}
