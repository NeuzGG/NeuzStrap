using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using NeuzStrap.UI.Controls;

namespace NeuzStrap.UI
{
    public enum DialogIcon { Info, Success, Warning, Error, Question }

    /// <summary>Dark-themed replacement for MessageBox, plus a text input dialog.</summary>
    public static class Dialog
    {
        public static void Info(IWin32Window owner, string title, string message) =>
            Show(owner, title, message, DialogIcon.Info, ("OK", DialogResult.OK, ButtonKind.Primary));

        public static void Success(IWin32Window owner, string title, string message) =>
            Show(owner, title, message, DialogIcon.Success, ("Nice!", DialogResult.OK, ButtonKind.Primary));

        public static void Error(IWin32Window owner, string title, string message) =>
            Show(owner, title, message, DialogIcon.Error, ("OK", DialogResult.OK, ButtonKind.Primary));

        public static bool Confirm(IWin32Window owner, string title, string message, string yes = "Yes", string no = "Cancel",
                                   bool danger = false, bool topMost = false) =>
            Show(owner, title, message, danger ? DialogIcon.Warning : DialogIcon.Question, topMost,
                 (no, DialogResult.Cancel, ButtonKind.Secondary),
                 (yes, DialogResult.OK, danger ? ButtonKind.Danger : ButtonKind.Primary)) == DialogResult.OK;

        public static DialogResult Show(IWin32Window owner, string title, string message, DialogIcon icon,
                                        params (string Text, DialogResult Result, ButtonKind Kind)[] buttons) =>
            Show(owner, title, message, icon, false, buttons);

        public static DialogResult Show(IWin32Window owner, string title, string message, DialogIcon icon, bool topMost,
                                        params (string Text, DialogResult Result, ButtonKind Kind)[] buttons)
        {
            using (var f = new MessageForm(title, message, icon, buttons))
            {
                // used when the game just vanished and there's no window left to sit on top of
                if (topMost) { f.TopMost = true; f.ShowInTaskbar = true; f.StartPosition = FormStartPosition.CenterScreen; }
                return owner != null ? f.ShowDialog(owner) : f.ShowDialog();
            }
        }

        /// <summary>Asks for text. Returns null if cancelled.</summary>
        public static string Input(IWin32Window owner, string title, string prompt, string initial = "", bool multiline = false)
        {
            using (var f = new InputForm(title, prompt, initial, multiline))
                return (owner != null ? f.ShowDialog(owner) : f.ShowDialog()) == DialogResult.OK ? f.Value : null;
        }

        internal static void StyleDialog(Form f)
        {
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.ShowInTaskbar = false;
            f.StartPosition = FormStartPosition.CenterParent;
            f.BackColor = Theme.Surface;
            f.ForeColor = Theme.Text;
            f.Font = Theme.Body;
            f.Icon = AppIcon.Large;
            f.Text = AppInfo.Name;
            f.HandleCreated += (_, __) => Native.UseDarkTitleBar(f.Handle, Theme.Surface);
        }

        sealed class MessageForm : Form
        {
            readonly DialogIcon _icon;

            public MessageForm(string title, string message, DialogIcon icon, (string Text, DialogResult Result, ButtonKind Kind)[] buttons)
            {
                _icon = icon;
                StyleDialog(this);
                if (owner() == null) { StartPosition = FormStartPosition.CenterScreen; ShowInTaskbar = true; }

                int width = Theme.S(460);
                int textX = Theme.S(72);
                int textW = width - textX - Theme.S(24);
                var titleLabel = Factory.Label(title, Theme.H2);
                int titleH = Draw.Measure(title, Theme.H2, textW).Height;
                titleLabel.SetBounds(textX, Theme.S(22), textW, titleH);
                var msg = Factory.Label(message, Theme.Body, Theme.TextDim);
                int msgH = Draw.Measure(message, Theme.Body, textW).Height;
                msg.SetBounds(textX, Theme.S(26) + titleH, textW, msgH);
                Controls.Add(titleLabel);
                Controls.Add(msg);

                int buttonsTop = Math.Max(Theme.S(96), msg.Bottom + Theme.S(22));
                int x = width - Theme.S(20);
                for (int i = buttons.Length - 1; i >= 0; i--)
                {
                    var b = new NButton(buttons[i].Text, buttons[i].Kind).FitToText(Theme.S(88));
                    b.DialogResult = buttons[i].Result;
                    x -= b.Width;
                    b.Location = new Point(x, buttonsTop);
                    x -= Theme.S(8);
                    Controls.Add(b);
                    if (i == buttons.Length - 1) AcceptButton = b;
                    if (buttons[i].Result == DialogResult.Cancel) CancelButton = b;
                }
                if (CancelButton == null && buttons.Length == 1) CancelButton = (IButtonControl)AcceptButton;

                ClientSize = new Size(width, buttonsTop + Theme.S(34) + Theme.S(20));
            }

            static Form owner() => Form.ActiveForm;

            protected override void OnShown(EventArgs e)
            {
                base.OnShown(e);
                (AcceptButton as Control)?.Focus();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                Draw.Smooth(g);
                Color c;
                string glyph;
                switch (_icon)
                {
                    case DialogIcon.Success: c = Theme.Success; glyph = Glyph.Check; break;
                    case DialogIcon.Warning: c = Theme.Warning; glyph = Glyph.Warning; break;
                    case DialogIcon.Error: c = Theme.Danger; glyph = Glyph.Warning; break;
                    case DialogIcon.Question: c = Theme.Accent; glyph = Glyph.Lightbulb; break;
                    default: c = Theme.Accent; glyph = Glyph.Info; break;
                }
                var circle = new Rectangle(Theme.S(22), Theme.S(22), Theme.S(36), Theme.S(36));
                Draw.FillRound(g, circle, circle.Width / 2f, Theme.Mix(Theme.Surface, c, 0.18f));
                Draw.Glyph(g, glyph, Theme.Icons, circle, c);
            }
        }

        sealed class InputForm : Form
        {
            readonly TextBox _box;
            public string Value => _box.Text;

            public InputForm(string title, string prompt, string initial, bool multiline)
            {
                StyleDialog(this);
                int width = Theme.S(multiline ? 620 : 460);
                var titleLabel = Factory.Label(title, Theme.H2);
                titleLabel.SetBounds(Theme.S(22), Theme.S(18), width - Theme.S(44), Theme.S(30));
                var p = Factory.Label(prompt, Theme.Body, Theme.TextDim);
                int ph = Draw.Measure(prompt, Theme.Body, width - Theme.S(44)).Height;
                p.SetBounds(Theme.S(22), Theme.S(52), width - Theme.S(44), ph);

                int boxTop = p.Bottom + Theme.S(12);
                Control editor;
                if (multiline)
                {
                    _box = new TextBox
                    {
                        Multiline = true,
                        ScrollBars = ScrollBars.Vertical,
                        AcceptsReturn = true,
                        AcceptsTab = true,
                        WordWrap = false,
                        BorderStyle = BorderStyle.FixedSingle,
                        BackColor = Theme.Input,
                        ForeColor = Theme.Text,
                        Font = Theme.Mono,
                        Text = initial ?? "",
                    };
                    _box.SetBounds(Theme.S(22), boxTop, width - Theme.S(44), Theme.S(260));
                    _box.HandleCreated += (_, __) => Native.UseDarkScrollbars(_box);
                    editor = _box;
                }
                else
                {
                    var ti = new TextInput(0) { Text = initial ?? "" };
                    ti.SetBounds(Theme.S(22), boxTop, width - Theme.S(44), Theme.S(34));
                    _box = ti.Box;
                    editor = ti;
                }

                var ok = new NButton("OK", ButtonKind.Primary).FitToText(Theme.S(88));
                ok.DialogResult = DialogResult.OK;
                var cancel = new NButton("Cancel").FitToText(Theme.S(88));
                cancel.DialogResult = DialogResult.Cancel;
                int btnTop = editor.Bottom + Theme.S(18);
                ok.Location = new Point(width - Theme.S(20) - ok.Width, btnTop);
                cancel.Location = new Point(ok.Left - Theme.S(8) - cancel.Width, btnTop);

                Controls.AddRange(new Control[] { titleLabel, p, editor, cancel, ok });
                if (!multiline) AcceptButton = ok;
                CancelButton = cancel;
                ClientSize = new Size(width, btnTop + ok.Height + Theme.S(20));
            }

            protected override void OnShown(EventArgs e)
            {
                base.OnShown(e);
                _box.Focus();
                _box.SelectAll();
            }
        }
    }
}
