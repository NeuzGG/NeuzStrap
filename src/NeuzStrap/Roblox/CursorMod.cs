using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using NeuzStrap.Core;

namespace NeuzStrap.Roblox
{
    /// <summary>
    /// Custom mouse cursors. Roblox draws its cursor from two 64x64 PNGs whose click point is the
    /// exact center of the image: ArrowFarCursor (normal) and ArrowCursor (over something clickable).
    /// NeuzStrap draws its own styles in code, or uses an image you pick.
    /// </summary>
    public static class CursorMod
    {
        public const string NormalPath = @"content\textures\Cursors\KeyboardMouse\ArrowFarCursor.png";
        public const string HoverPath = @"content\textures\Cursors\KeyboardMouse\ArrowCursor.png";
        /// <summary>The shift-lock circle.</summary>
        public const string LockedPath = @"content\textures\MouseLockedCursor.png";

        const int Canvas = 64;
        const float Hot = 32f; // click point = center of the image

        static readonly Color SakuraPink = Color.FromArgb(255, 111, 174);
        static readonly Color SakuraDeep = Color.FromArgb(214, 51, 132);
        static readonly Color Ink = Color.FromArgb(24, 18, 28);

        public static string Title(CursorStyle s)
        {
            switch (s)
            {
                case CursorStyle.RobloxDefault: return "Default";
                case CursorStyle.Classic: return "Classic";
                case CursorStyle.Sakura: return "Sakura";
                case CursorStyle.Rgb: return "RGB";
                case CursorStyle.BigArrow: return "Big arrow";
                case CursorStyle.Dot: return "Dot";
                case CursorStyle.Crosshair: return "Crosshair";
                default: return "Your image";
            }
        }

        /// <summary>Files to write into the Roblox folder (relative path -> PNG). Empty = Roblox's own cursor.</summary>
        public static Dictionary<string, byte[]> Build(Settings s, string versionDir)
        {
            var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            if (s.CursorStyle == CursorStyle.RobloxDefault) return files;

            if (s.CursorStyle == CursorStyle.Rgb)
            {
                // Roblox's own arrow, hand and shift-lock circle, recolored as a rainbow
                foreach (var rel in new[] { NormalPath, HoverPath, LockedPath })
                {
                    using (var original = LoadOriginal(versionDir, rel))
                    {
                        if (original == null) { Logger.Warn("Cursor", "Roblox's original " + rel + " wasn't found; skipping it"); continue; }
                        files[rel] = ToPng(Rainbow(original, aroundCenter: rel == LockedPath));
                    }
                }
                return files;
            }

            if (s.CursorStyle == CursorStyle.Custom)
            {
                if (!File.Exists(Paths.CustomCursorFile)) return files;
                try
                {
                    byte[] png = PrepareCustom(Paths.CustomCursorFile);
                    files[NormalPath] = png;
                    files[HoverPath] = png;
                }
                catch (Exception ex) { Logger.Warn("Cursor", "Couldn't use the custom cursor image: " + ex.Message); }
                return files;
            }

            files[NormalPath] = ToPng(Render(s.CursorStyle, false));
            files[HoverPath] = ToPng(Render(s.CursorStyle, true));
            return files;
        }

        /// <summary>
        /// Your own image: a 64x64 PNG is used as-is (Roblox's format, click point in the center).
        /// Anything else is shrunk to fit 32x32 with its top-left corner as the click point, like a normal arrow.
        /// </summary>
        public static byte[] PrepareCustom(string file)
        {
            using (var src = Image.FromFile(file))
            {
                if (src.Width == Canvas && src.Height == Canvas)
                    return src.RawFormat.Equals(ImageFormat.Png) ? File.ReadAllBytes(file) : ToPng(new Bitmap(src));

                var bmp = new Bitmap(Canvas, Canvas, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    float scale = Math.Min(1f, Math.Min(32f / src.Width, 32f / src.Height));
                    g.DrawImage(src, new RectangleF(Hot, Hot, src.Width * scale, src.Height * scale));
                }
                return ToPng(bmp);
            }
        }

        /// <summary>
        /// Roblox's untouched image for a cursor file: the backup NeuzStrap made before modding it,
        /// or the file itself if no mod has touched it yet. Null if Roblox isn't installed.
        /// </summary>
        public static Bitmap LoadOriginal(string versionDir, string relativePath)
        {
            if (string.IsNullOrEmpty(versionDir)) return null;
            foreach (var candidate in new[] { Path.Combine(versionDir, ModManager.BackupFolder, relativePath), Path.Combine(versionDir, relativePath) })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    using (var fs = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var img = Image.FromStream(fs))
                        return new Bitmap(img);
                }
                catch { }
            }
            return null;
        }

        /// <summary>
        /// Recolors the light parts of a cursor (its white fill / gray ring) as a rainbow, keeping the
        /// shading, the dark outline and the soft shadow. aroundCenter = hue goes around the image
        /// center (for the shift-lock ring) instead of diagonally across the shape.
        /// </summary>
        public static Bitmap Rainbow(Bitmap src, bool aroundCenter)
        {
            // spread the rainbow over the light fill only (not the outline or shadow) so every shape gets the full spectrum
            var bounds = FillBounds(src);
            float cx = bounds.X + bounds.Width / 2f, cy = bounds.Y + bounds.Height / 2f;
            var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);

            for (int y = 0; y < src.Height; y++)
            {
                for (int x = 0; x < src.Width; x++)
                {
                    Color c = src.GetPixel(x, y);
                    if (c.A == 0) { dst.SetPixel(x, y, Color.Transparent); continue; }

                    double lum = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
                    double t = SmoothStep(0.42, 0.78, lum); // 0 = outline/shadow (keep), 1 = fill (recolor)
                    if (t <= 0) { dst.SetPixel(x, y, c); continue; }

                    double hue = aroundCenter
                        ? (Math.Atan2(y - cy, x - cx) * 180 / Math.PI + 450) % 360
                        : 330.0 * ((x - bounds.X) + (y - bounds.Y)) / Math.Max(1, bounds.Width + bounds.Height);
                    Color rgb = FromHsv(hue, 0.85, 0.55 + 0.45 * lum);

                    dst.SetPixel(x, y, Color.FromArgb(c.A,
                        (int)(c.R + (rgb.R - c.R) * t),
                        (int)(c.G + (rgb.G - c.G) * t),
                        (int)(c.B + (rgb.B - c.B) * t)));
                }
            }
            return dst;
        }

        /// <summary>Bounding box of the visible (non-transparent) pixels.</summary>
        public static Rectangle VisibleBounds(Bitmap bmp)
        {
            int minX = bmp.Width, minY = bmp.Height, maxX = -1, maxY = -1;
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                    if (bmp.GetPixel(x, y).A > 24)
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
            return maxX < 0 ? new Rectangle(0, 0, bmp.Width, bmp.Height) : Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
        }

        static Rectangle FillBounds(Bitmap bmp)
        {
            int minX = bmp.Width, minY = bmp.Height, maxX = -1, maxY = -1;
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    if (c.A <= 24 || (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0 < 0.6) continue;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            return maxX < 0 ? VisibleBounds(bmp) : Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
        }

        static double SmoothStep(double edge0, double edge1, double x)
        {
            double t = Math.Max(0, Math.Min(1, (x - edge0) / (edge1 - edge0)));
            return t * t * (3 - 2 * t);
        }

        static Color FromHsv(double h, double s, double v)
        {
            h = ((h % 360) + 360) % 360;
            double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
            double r, g, b;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }
            return Color.FromArgb((int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
        }

        public static Bitmap Render(CursorStyle style, bool hover)
        {
            var bmp = new Bitmap(Canvas, Canvas, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                switch (style)
                {
                    case CursorStyle.Classic:
                        DrawArrow(g, 1.2f, hover ? Color.FromArgb(255, 226, 122) : Color.White, Ink, 1.6f);
                        break;
                    case CursorStyle.BigArrow:
                        DrawArrow(g, 1.5f, hover ? Color.FromArgb(255, 226, 122) : Color.White, Ink, 2f);
                        break;
                    case CursorStyle.Sakura:
                        if (hover) DrawHeart(g);
                        else DrawArrow(g, 1.25f, SakuraPink, Color.White, 2f, SakuraDeep);
                        break;
                    case CursorStyle.Dot:
                        DrawDot(g, hover);
                        break;
                    case CursorStyle.Crosshair:
                        DrawCrosshair(g, hover);
                        break;
                }
            }
            return bmp;
        }

        // ------------------------------------------------------------------ shapes (tip / center at the click point)

        static void DrawArrow(Graphics g, float u, Color fill, Color outline, float outlineWidth, Color? fill2 = null)
        {
            // classic pointer outline in "units", tip at (0,0)
            var pts = new[]
            {
                new PointF(0, 0), new PointF(0, 17), new PointF(4, 13.2f), new PointF(7, 19.5f),
                new PointF(9.8f, 18.3f), new PointF(6.9f, 12), new PointF(12, 12),
            };
            using (var path = new GraphicsPath())
            {
                var scaled = Array.ConvertAll(pts, p => new PointF(Hot + p.X * u, Hot + p.Y * u));
                path.AddPolygon(scaled);

                // soft shadow
                using (var shadow = (GraphicsPath)path.Clone())
                using (var m = new Matrix())
                {
                    m.Translate(1.2f, 1.6f);
                    shadow.Transform(m);
                    using (var b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillPath(b, shadow);
                }

                if (fill2.HasValue)
                {
                    // two-tone rim: dark outside the colored one so it stays visible on bright and dark maps.
                    // Strokes are centered on the edge, so filling last hides their inner halves.
                    using (var dark = new Pen(Ink, outlineWidth * 2 + 2f) { LineJoin = LineJoin.Round }) g.DrawPath(dark, path);
                    using (var rim = new Pen(outline, outlineWidth * 2) { LineJoin = LineJoin.Round }) g.DrawPath(rim, path);
                    using (var b = new LinearGradientBrush(new PointF(Hot, Hot), new PointF(Hot + 12 * u, Hot + 20 * u), fill, fill2.Value))
                        g.FillPath(b, path);
                }
                else
                {
                    using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                    using (var pen = new Pen(outline, outlineWidth) { LineJoin = LineJoin.Round }) g.DrawPath(pen, path);
                }
            }
        }

        static void DrawHeart(Graphics g)
        {
            // heart centered on the click point
            using (var path = new GraphicsPath())
            {
                float s = 10f;
                path.AddBezier(Hot, Hot + s * 1.05f, Hot - s * 1.6f, Hot - s * 0.1f, Hot - s * 0.9f, Hot - s * 1.15f, Hot, Hot - s * 0.45f);
                path.AddBezier(Hot, Hot - s * 0.45f, Hot + s * 0.9f, Hot - s * 1.15f, Hot + s * 1.6f, Hot - s * 0.1f, Hot, Hot + s * 1.05f);
                path.CloseFigure();
                using (var dark = new Pen(Ink, 4f) { LineJoin = LineJoin.Round }) g.DrawPath(dark, path);
                using (var b = new LinearGradientBrush(new PointF(Hot - s, Hot - s), new PointF(Hot + s, Hot + s), SakuraPink, SakuraDeep)) g.FillPath(b, path);
                using (var pen = new Pen(Color.White, 1.8f) { LineJoin = LineJoin.Round }) g.DrawPath(pen, path);
            }
        }

        static void DrawDot(Graphics g, bool hover)
        {
            float r = 3.6f;
            using (var dark = new SolidBrush(Ink)) g.FillEllipse(dark, Hot - r - 1.4f, Hot - r - 1.4f, (r + 1.4f) * 2, (r + 1.4f) * 2);
            using (var white = new SolidBrush(Color.White)) g.FillEllipse(white, Hot - r, Hot - r, r * 2, r * 2);
            if (hover)
            {
                float rr = 9.5f;
                using (var dark = new Pen(Ink, 3.6f)) g.DrawEllipse(dark, Hot - rr, Hot - rr, rr * 2, rr * 2);
                using (var pen = new Pen(Color.White, 1.8f)) g.DrawEllipse(pen, Hot - rr, Hot - rr, rr * 2, rr * 2);
            }
        }

        static void DrawCrosshair(Graphics g, bool hover)
        {
            float gap = 4f, len = 11f;
            var arms = new[]
            {
                (new PointF(Hot, Hot - gap), new PointF(Hot, Hot - len)),
                (new PointF(Hot, Hot + gap), new PointF(Hot, Hot + len)),
                (new PointF(Hot - gap, Hot), new PointF(Hot - len, Hot)),
                (new PointF(Hot + gap, Hot), new PointF(Hot + len, Hot)),
            };
            using (var dark = new Pen(Ink, 4.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var white = new Pen(Color.White, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                foreach (var (a, b) in arms) g.DrawLine(dark, a, b);
                foreach (var (a, b) in arms) g.DrawLine(white, a, b);
                if (hover)
                {
                    float rr = 14f;
                    using (var ring = new Pen(Ink, 3.6f)) g.DrawEllipse(ring, Hot - rr, Hot - rr, rr * 2, rr * 2);
                    using (var ring = new Pen(SakuraPink, 1.8f)) g.DrawEllipse(ring, Hot - rr, Hot - rr, rr * 2, rr * 2);
                }
            }
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, Hot - 1.3f, Hot - 1.3f, 2.6f, 2.6f);
        }

        static byte[] ToPng(Bitmap bmp)
        {
            using (bmp)
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }
}
