using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NeuzStrap.Core;
using NeuzStrap.Integrations;
using NeuzStrap.UI.Controls;

namespace NeuzStrap.UI.Pages
{
    /// <summary>The keys shown in the overlay: click one to remove it, or "+ Add key" to pick another.</summary>
    public sealed class KeyChips : Control, IAutoHeight
    {
        const int MaxKeys = 10;

        readonly List<Rectangle> _chips = new List<Rectangle>();
        int _hover = -1;

        public event Action Changed;

        public KeyChips()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        List<string> Keys => Settings.Current.OverlayKeyList;

        static int ChipH => Theme.S(32);
        static int Gap => Theme.S(8);

        int ChipWidth(string text) => Draw.Measure(text, Theme.BodyBold).Width + Theme.S(30);

        /// <summary>Lays the chips out (and returns the total height).</summary>
        int Arrange(int width, bool store)
        {
            if (store) _chips.Clear();
            int x = 0, y = 0, rowH = ChipH;
            foreach (var text in Labels())
            {
                int w = ChipWidth(text);
                if (x > 0 && x + w > width) { x = 0; y += rowH + Gap; }
                if (store) _chips.Add(new Rectangle(x, y, w, ChipH));
                x += w + Gap;
            }
            return y + rowH;
        }

        IEnumerable<string> Labels()
        {
            foreach (var k in Keys) yield return KeyNames.Label(k);
            if (Keys.Count < MaxKeys) yield return "+ Add key";
        }

        public int MeasureHeight(int width) => Arrange(width, false);

        protected override void OnLayout(LayoutEventArgs e)
        {
            Arrange(Width, true);
            base.OnLayout(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = _chips.FindIndex(r => r.Contains(e.Location));
            if (i != _hover) { _hover = i; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int i = _chips.FindIndex(r => r.Contains(e.Location));
            if (i < 0) return;

            if (i < Keys.Count)
            {
                if (Keys.Count <= 1) return; // keep at least one key
                Keys.RemoveAt(i);
            }
            else
            {
                string picked = KeyPickerDialog.Pick(FindForm());
                if (picked == null) return;
                if (Keys.Contains(picked)) return;
                Keys.Add(picked);
            }

            Settings.Current.OverlayKeyList = KeyNames.Clean(Keys);
            Settings.Save();
            Changed?.Invoke();
            Parent?.PerformLayout();
            Invalidate();
            base.OnMouseClick(e);
        }

        public void ResetToDefault()
        {
            Settings.Current.OverlayKeyList = new List<string>(KeyNames.Default);
            Settings.Save();
            Changed?.Invoke();
            Parent?.PerformLayout();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Bg);
            Draw.Smooth(g);

            var labels = Labels().ToList();
            for (int i = 0; i < _chips.Count && i < labels.Count; i++)
            {
                var r = _chips[i];
                bool isAdd = i >= Keys.Count;
                bool hover = i == _hover;
                int radius = Theme.S(8);

                if (isAdd)
                {
                    Draw.FillRound(g, r, radius, hover ? Theme.SurfaceHover : Theme.Surface);
                    Draw.StrokeRound(g, r, radius, hover ? Theme.Accent : Theme.Border);
                    Draw.CenterText(g, labels[i], Theme.Body, r, hover ? Theme.Accent : Theme.TextDim);
                }
                else
                {
                    Draw.FillRound(g, r, radius, hover ? Theme.Mix(Theme.Surface, Theme.Danger, 0.25f) : Theme.AccentSoft);
                    Draw.StrokeRound(g, r, radius, hover ? Theme.Danger : Theme.Mix(Theme.Border, Theme.Accent, 0.4f));
                    var textRect = new Rectangle(r.X, r.Y, r.Width - Theme.S(12), r.Height);
                    Draw.CenterText(g, labels[i], Theme.BodyBold, textRect, hover ? Theme.Lighten(Theme.Danger, 0.2f) : Theme.Text);
                    Draw.Glyph(g, "\u2715", Theme.Small, new Rectangle(r.Right - Theme.S(16), r.Y, Theme.S(12), r.Height),
                        hover ? Theme.Lighten(Theme.Danger, 0.2f) : Theme.TextFaint);
                }
            }
        }
    }

    /// <summary>"Press a key" dialog used to add a key to the overlay.</summary>
    public sealed class KeyPickerDialog : Form
    {
        string _picked;

        public static string Pick(IWin32Window owner)
        {
            using (var d = new KeyPickerDialog())
                return (owner != null ? d.ShowDialog(owner) : d.ShowDialog()) == DialogResult.OK ? d._picked : null;
        }

        KeyPickerDialog()
        {
            Dialog.StyleDialog(this);
            KeyPreview = true;
            ClientSize = new Size(Theme.S(460), Theme.S(210));

            var title = Factory.Label("Press the key you want to add", Theme.H2);
            title.SetBounds(Theme.S(24), Theme.S(22), ClientSize.Width - Theme.S(48), Theme.S(30));
            var hint = Factory.Label("Letters, numbers, F-keys, Space, Shift, Ctrl, Alt, Tab and arrows all work. " +
                                     "For mouse buttons use the buttons below.", Theme.Body, Theme.TextDim);
            hint.SetBounds(Theme.S(24), Theme.S(56), ClientSize.Width - Theme.S(48), Theme.S(44));
            Controls.Add(title);
            Controls.Add(hint);

            int inner = ClientSize.Width - Theme.S(48);
            var bar = new ButtonBar();
            bar.AddButton(new NButton("Left mouse", ButtonKind.Secondary, Glyph.Mouse).FitToText(), (_, __) => Take("MOUSE1"));
            bar.AddButton(new NButton("Right mouse", ButtonKind.Secondary, Glyph.Mouse).FitToText(), (_, __) => Take("MOUSE2"));
            bar.AddButton(new NButton("Middle mouse", ButtonKind.Secondary, Glyph.Mouse).FitToText(), (_, __) => Take("MOUSE3"));
            bar.SetBounds(Theme.S(24), Theme.S(108), inner, bar.MeasureHeight(inner)); // may wrap onto two rows
            Controls.Add(bar);

            var cancel = new NButton("Cancel").FitToText(Theme.S(100));
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(ClientSize.Width - Theme.S(24) - cancel.Width, bar.Bottom + Theme.S(18));
            Controls.Add(cancel);
            CancelButton = cancel;
            ClientSize = new Size(ClientSize.Width, cancel.Bottom + Theme.S(20));
        }

        void Take(string name)
        {
            _picked = name;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            string name = KeyNames.FromKeyPress(e.KeyCode);
            if (name != null && name != "ESC")
            {
                e.Handled = e.SuppressKeyPress = true;
                Take(name);
                return;
            }
            base.OnKeyDown(e);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            // Tab and arrows would move the focus instead of being picked
            string name = KeyNames.FromKeyPress(keyData);
            if (name != null && name != "ESC") { Take(name); return true; }
            return base.ProcessDialogKey(keyData);
        }
    }
}
