using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NeuzStrap.UI
{
    /// <summary>Small Win32 helpers for a modern look: dark title bars, rounded corners, dark scrollbars.</summary>
    public static class Native
    {
        const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        const int DWMWA_BORDER_COLOR = 34;
        const int DWMWA_CAPTION_COLOR = 35;

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int cmd);

        public static bool IsWindows11 => Environment.OSVersion.Version.Build >= 22000;

        public static void UseDarkTitleBar(IntPtr hwnd, System.Drawing.Color? caption = null)
        {
            try
            {
                int on = 1;
                if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, 4) != 0)
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref on, 4);
                if (caption.HasValue && IsWindows11)
                {
                    int c = caption.Value.R | (caption.Value.G << 8) | (caption.Value.B << 16);
                    DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref c, 4);
                }
            }
            catch { /* older Windows */ }
        }

        /// <summary>Windows 11 rounded corners for borderless windows (no-op on Windows 10).</summary>
        public static bool TryRoundCorners(IntPtr hwnd, bool small = false)
        {
            if (!IsWindows11) return false;
            try
            {
                int pref = small ? 3 : 2; // DWMWCP_ROUNDSMALL : DWMWCP_ROUND
                return DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, 4) == 0;
            }
            catch { return false; }
        }

        public static void SetBorderColor(IntPtr hwnd, System.Drawing.Color color)
        {
            if (!IsWindows11) return;
            try
            {
                int c = color.R | (color.G << 8) | (color.B << 16);
                DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref c, 4);
            }
            catch { }
        }

        /// <summary>Dark scrollbars on Windows 10 1809+ / 11.</summary>
        public static void UseDarkScrollbars(Control c)
        {
            try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { }
        }

        /// <summary>Lets the user drag a borderless window from anywhere.</summary>
        public static void BeginDrag(Form f)
        {
            ReleaseCapture();
            SendMessage(f.Handle, 0xA1 /* WM_NCLBUTTONDOWN */, (IntPtr)2 /* HTCAPTION */, IntPtr.Zero);
        }
    }
}
