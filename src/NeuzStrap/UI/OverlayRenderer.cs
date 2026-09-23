using System;
using System.Drawing;
using System.Drawing.Imaging;
using NeuzStrap.Core;
using NeuzStrap.Integrations;

namespace NeuzStrap.UI
{
    /// <summary>What the overlay should show right now.</summary>
    public struct OverlayState
    {
        public int LeftCps;
        public int RightCps;
        public int Kps;
        public bool RightMouseDown;
        public bool[] Keys;
        /// <summary>Text on each key box, in the order the user arranged them.</summary>
        public string[] Labels;

        public static OverlayState Sample => SampleFor(new Settings());

        /// <summary>A "someone is playing" snapshot using the keys the user picked (for the settings preview).</summary>
        public static OverlayState SampleFor(Settings s)
        {
            var names = Integrations.KeyNames.Clean(s.OverlayKeyList);
            var labels = new string[names.Count];
            var keys = new bool[names.Count];
            for (int i = 0; i < names.Count; i++) labels[i] = Integrations.KeyNames.Label(names[i]);
            if (keys.Length > 0) keys[0] = true;                       // holding forward...
            if (keys.Length > 3) keys[3] = true;                       // ...and strafing...
            if (keys.Length > 5) keys[5] = true;                       // ...while sprinting
            return new OverlayState { LeftCps = 7, Kps = 12, Labels = labels, Keys = keys };
        }
    }

    /// <summary>Draws the overlay panel (shared by the in-game window and the settings preview).</summary>
    public static class OverlayRenderer
    {
        public static float ScaleFor(OverlaySize size) =>
            size == OverlaySize.Small ? 0.8f : size == OverlaySize.Large ? 1.35f : 1f;

        public static Color Highlight(Settings s, float hue) => s.OverlayRainbow ? FromHsv(hue, 0.75, 1.0) : Theme.Accent;

        public static Bitmap Render(Settings s, OverlayState state, float hue, float dpiScale = 1f)
        {
            float f = ScaleFor(s.OverlayScale) * dpiScale;
            int S(int px) => Math.Max(1, (int)Math.Round(px * f));

            int pad = S(10), gap = S(6), keyH = S(26), radius = S(8);
            var labels = state.Labels ?? Array.ConvertAll(KeyNames.Default, KeyNames.Label);
            bool showKeys = s.OverlayKeys && labels.Length > 0;
            bool showCps = s.OverlayCps;
            bool showKps = s.OverlayKps;

            using (var cpsFont = new Font("Segoe UI", S(14), FontStyle.Bold, GraphicsUnit.Pixel))
            using (var keyFont = new Font("Segoe UI", S(11), FontStyle.Bold, GraphicsUnit.Pixel))
            {
                string cpsText = showCps && showKps ? $"{state.LeftCps} CPS  \u00B7  {state.Kps} KPS"
                               : showCps ? $"{state.LeftCps} CPS"
                               : showKps ? $"{state.Kps} KPS"
                               : null;
                string rightText = showCps && (state.RightCps > 0 || state.RightMouseDown) ? $"R {state.RightCps}" : null;

                var keyWidths = new int[labels.Length];
                int keysWidth = 0;
                if (showKeys)
                    for (int i = 0; i < labels.Length; i++)
                    {
                        keyWidths[i] = Math.Max(keyH, Draw.Measure(labels[i], keyFont).Width + S(12));
                        keysWidth += keyWidths[i] + (i > 0 ? gap : 0);
                    }

                int cpsWidth = cpsText == null ? 0
                    : Draw.Measure(cpsText, cpsFont).Width + (rightText != null ? Draw.Measure(rightText, keyFont).Width + S(10) : 0);
                int cpsHeight = cpsText != null ? S(21) : 0;
                int width = Math.Max(S(56), Math.Max(cpsWidth, keysWidth) + pad * 2);
                int height = pad * 2 + cpsHeight + (cpsText != null && showKeys ? gap : 0) + (showKeys ? keyH : 0);

                var bmp = new Bitmap(width, Math.Max(height, S(28)), PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    Draw.Smooth(g);
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                    var panel = new RectangleF(0, 0, bmp.Width - 1, bmp.Height - 1);
                    Draw.FillRound(g, panel, radius, Color.FromArgb(150, 8, 8, 12));
                    Draw.StrokeRound(g, panel, radius, Color.FromArgb(60, 255, 255, 255));

                    var highlight = Highlight(s, hue);
                    int y = pad;
                    if (cpsText != null)
                    {
                        using (var b = new SolidBrush(highlight))
                            g.DrawString(cpsText, cpsFont, b, pad - S(2), y - S(3));
                        if (rightText != null)
                            using (var b = new SolidBrush(Color.FromArgb(190, 255, 255, 255)))
                                g.DrawString(rightText, keyFont, b, pad + Draw.Measure(cpsText, cpsFont).Width + S(8), y + S(3));
                        y += cpsHeight + gap;
                    }

                    if (showKeys)
                    {
                        int x = pad;
                        for (int i = 0; i < labels.Length; i++)
                        {
                            var box = new RectangleF(x, y, keyWidths[i], keyH);
                            bool down = state.Keys != null && i < state.Keys.Length && state.Keys[i];
                            Draw.FillRound(g, box, S(5), down ? highlight : Color.FromArgb(70, 255, 255, 255));
                            Draw.StrokeRound(g, box, S(5), Color.FromArgb(down ? 220 : 90, 255, 255, 255));
                            using (var b = new SolidBrush(down ? Color.FromArgb(20, 16, 24) : Color.FromArgb(225, 255, 255, 255)))
                            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                                g.DrawString(labels[i], keyFont, b, box, sf);
                            x += keyWidths[i] + gap;
                        }
                    }
                }
                return bmp;
            }
        }

        public static Color FromHsv(double h, double s, double v)
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
            return Color.FromArgb(255, (int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
        }
    }
}
