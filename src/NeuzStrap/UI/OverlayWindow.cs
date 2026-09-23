using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NeuzStrap.Core;
using NeuzStrap.Integrations;

namespace NeuzStrap.UI
{
    /// <summary>
    /// The in-game overlay: a click-through, transparent window on top of Roblox showing a CPS counter
    /// and which movement keys you're holding. It only shows while the Roblox window is in front, and it
    /// can't appear over exclusive fullscreen (Windows doesn't allow overlays there).
    /// </summary>
    public sealed class OverlayWindow : Form
    {
        readonly Process _roblox;
        readonly Settings _s = Settings.Current;
        readonly InputMonitor _input;
        readonly Timer _timer = new Timer { Interval = 33 };
        float _hue;
        string _lastState = "";

        public OverlayWindow(Process roblox)
        {
            _roblox = roblox;
            _input = new InputMonitor(_s.OverlayKeyList);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Text = "NeuzStrap overlay";
            Size = new Size(10, 10);
            _timer.Tick += (_, __) => Tick();
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _timer.Start();
        }

        // ------------------------------------------------------------------ per-frame work

        void Tick()
        {
            if (HasExited(_roblox)) { Close(); return; }

            try { _roblox.Refresh(); } catch { } // the game window doesn't exist yet right after launching
            IntPtr game = _roblox.MainWindowHandle;
            if (game == IntPtr.Zero || !IsForeground(game) || !GetWindowRect(game, out RECT r) || r.Width <= 0)
            {
                if (Visible) Hide();
                return;
            }

            _input.Poll();
            if (_s.OverlayRainbow) _hue = (_hue + 1.6f) % 360f;

            string state = $"{_input.LeftCps}/{_input.RightCps}/{_input.Kps}/{KeyMask()}/{(_s.OverlayRainbow ? (int)_hue : 0)}/{r.Left},{r.Top},{r.Width},{r.Height}";
            if (state == _lastState && Visible) return; // nothing changed, don't repaint
            _lastState = state;

            using (var bmp = Render())
            {
                var pos = AnchorTo(r, bmp.Size);
                Bounds = new Rectangle(pos, bmp.Size);
                if (!Visible) Show();
                ApplyBitmap(bmp);
            }
        }

        int KeyMask()
        {
            int mask = _input.LeftMouseDown ? 1 : 0;
            if (_input.RightMouseDown) mask |= 2;
            for (int i = 0; i < _input.KeyCount && i < 28; i++)
                if (_input.IsDown(i)) mask |= 4 << i;
            return mask;
        }

        Point AnchorTo(RECT game, Size size)
        {
            int margin = Scale(14);
            int x;
            switch (_s.OverlayPosition)
            {
                case OverlayCorner.TopCenter:
                case OverlayCorner.BottomCenter: x = game.Left + (game.Width - size.Width) / 2; break;
                case OverlayCorner.TopRight:
                case OverlayCorner.BottomRight: x = game.Right - size.Width - margin; break;
                default: x = game.Left + margin; break;
            }
            bool bottom = _s.OverlayPosition == OverlayCorner.BottomLeft || _s.OverlayPosition == OverlayCorner.BottomCenter
                          || _s.OverlayPosition == OverlayCorner.BottomRight;
            int y = bottom ? game.Bottom - size.Height - margin : game.Top + margin + Scale(28); // below Roblox's own top bar
            return new Point(x, y);
        }

        // ------------------------------------------------------------------ drawing

        int Scale(int px) => Math.Max(1, (int)Math.Round(px * OverlayRenderer.ScaleFor(_s.OverlayScale) * Theme.Scale));

        Bitmap Render()
        {
            var keys = new bool[_input.KeyCount];
            for (int i = 0; i < keys.Length; i++) keys[i] = _input.IsDown(i);
            var state = new OverlayState
            {
                LeftCps = _input.LeftCps,
                RightCps = _input.RightCps,
                Kps = _input.Kps,
                RightMouseDown = _input.RightMouseDown,
                Keys = keys,
                Labels = _input.Labels,
                Names = _input.Names,
            };
            return OverlayRenderer.Render(_s, state, _hue, Theme.Scale);
        }

        static bool HasExited(Process p)
        {
            try { return p.HasExited; } catch { return true; }
        }

        bool IsForeground(IntPtr gameWindow)
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            if (fg == gameWindow) return true;
            GetWindowThreadProcessId(fg, out int pid);
            try { return pid == _roblox.Id; } catch { return false; }
        }

        // ------------------------------------------------------------------ layered window plumbing

        void ApplyBitmap(Bitmap bmp)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero, oldBitmap = IntPtr.Zero;
            try
            {
                hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
                oldBitmap = SelectObject(memDc, hBitmap);
                var size = new SIZE { cx = bmp.Width, cy = bmp.Height };
                var source = new POINT { x = 0, y = 0 };
                var topLeft = new POINT { x = Left, y = Top };
                var blend = new BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
                UpdateLayeredWindow(Handle, screenDc, ref topLeft, ref size, memDc, ref source, 0, ref blend, 2 /* ULW_ALPHA */);
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screenDc);
                if (hBitmap != IntPtr.Zero)
                {
                    SelectObject(memDc, oldBitmap);
                    DeleteObject(hBitmap);
                }
                DeleteDC(memDc);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
            }
            base.Dispose(disposing);
        }

        const int WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x8000000, WS_EX_TOPMOST = 0x8;

        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left, Top, Right, Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int x, y; }

        [StructLayout(LayoutKind.Sequential)]
        struct SIZE { public int cx, cy; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);
    }
}
