using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace NeuzStrap.UI
{
    public static class Draw
    {
        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        public static void FillRound(Graphics g, RectangleF r, float radius, Color c)
        {
            using (var path = Round(r, radius))
            using (var b = new SolidBrush(c))
                g.FillPath(b, path);
        }

        public static void StrokeRound(Graphics g, RectangleF r, float radius, Color c, float width = 1f)
        {
            float inset = width / 2f;
            var rr = new RectangleF(r.X + inset, r.Y + inset, r.Width - width, r.Height - width);
            using (var path = Round(rr, Math.Max(0, radius - inset)))
            using (var pen = new Pen(c, width))
                g.DrawPath(pen, path);
        }

        public static void Text(Graphics g, string text, Font font, Rectangle r, Color c,
                                TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left)
        {
            TextRenderer.DrawText(g, text, font, r, c, flags | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        public static void CenterText(Graphics g, string text, Font font, Rectangle r, Color c) =>
            Text(g, text, font, r, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

        /// <summary>Draws an icon-font glyph or emoji centered in the box (never truncated with "...").</summary>
        public static void Glyph(Graphics g, string glyph, Font font, Rectangle r, Color c) =>
            TextRenderer.DrawText(g, glyph, font, r, c,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.NoClipping);

        public static Size Measure(string text, Font font, int maxWidth = int.MaxValue)
        {
            var flags = TextFormatFlags.NoPrefix | (maxWidth == int.MaxValue ? TextFormatFlags.SingleLine : TextFormatFlags.WordBreak);
            return TextRenderer.MeasureText(text ?? "", font, new Size(maxWidth, int.MaxValue), flags);
        }
    }

    /// <summary>The embedded NeuzStrap icon in whatever size the UI needs.</summary>
    public static class AppIcon
    {
        static byte[] _bytes;

        static byte[] Bytes
        {
            get
            {
                if (_bytes != null) return _bytes;
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("NeuzStrap.ico"))
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    return _bytes = ms.ToArray();
                }
            }
        }

        static Icon _large, _small;
        public static Icon Large => _large ??= Load(new Size(Theme.S(32), Theme.S(32)));
        public static Icon Small => _small ??= Load(SystemInformation.SmallIconSize);

        public static Icon Load(Size size)
        {
            using (var ms = new MemoryStream(Bytes)) return new Icon(ms, size);
        }

        public static Bitmap Bitmap(int size)
        {
            // pick the best source frame, then scale smoothly
            int source = size <= 16 ? 16 : size <= 32 ? 32 : size <= 48 ? 48 : size <= 64 ? 64 : size <= 128 ? 128 : 256;
            Bitmap src;
            using (var ico = Load(new Size(source, source))) src = ico.ToBitmap();
            if (src.Width == size) return src;
            var dst = new Bitmap(size, size);
            using (var g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(src, new Rectangle(0, 0, size, size));
            }
            src.Dispose();
            return dst;
        }
    }
}
