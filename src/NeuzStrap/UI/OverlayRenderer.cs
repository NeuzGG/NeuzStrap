using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        /// <summary>Stored key names ("W", "SPACE"...), used to place them on the keyboard.</summary>
        public string[] Names;
        /// <summary>Text drawn on each key box.</summary>
        public string[] Labels;

        public static OverlayState Sample => SampleFor(new Settings());

        /// <summary>A "someone is playing" snapshot using the keys the user picked (for the settings preview).</summary>
        public static OverlayState SampleFor(Settings s)
        {
            var names = KeyNames.Clean(s.OverlayKeyList);
            var labels = new string[names.Count];
            var keys = new bool[names.Count];
            for (int i = 0; i < names.Count; i++) labels[i] = KeyNames.Label(names[i]);
            if (keys.Length > 0) keys[0] = true;                       // holding forward...
            if (keys.Length > 3) keys[3] = true;                       // ...and strafing...
            if (keys.Length > 5) keys[5] = true;                       // ...while sprinting
            return new OverlayState { LeftCps = 7, Kps = 12, Names = names.ToArray(), Labels = labels, Keys = keys };
        }
    }

    /// <summary>Draws the overlay panel (shared by the in-game window and the settings preview).</summary>
    public static class OverlayRenderer
    {
        public static float ScaleFor(OverlaySize size) =>
            size == OverlaySize.Small ? 0.8f : size == OverlaySize.Large ? 1.35f : 1f;

        /// <summary>The plain color used when the rainbow is off (the rainbow uses a gradient brush instead).</summary>
        public static Color Highlight(Settings s, float hue) => s.OverlayRainbow ? FromHsv(hue, 0.8, 1.0) : Theme.Accent;

        public static Bitmap Render(Settings s, OverlayState state, float hue, float dpiScale = 1f)
        {
            float f = ScaleFor(s.OverlayScale) * dpiScale;
            int S(int px) => Math.Max(1, (int)Math.Round(px * f));

            int pad = S(10), gap = S(7), keyH = S(26), keyGap = S(4), radius = S(8);
            var names = state.Names ?? KeyNames.Default;
            var labels = state.Labels ?? Array.ConvertAll(names, KeyNames.Label);
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

                // ---- work out where each key box goes
                var boxes = new List<RectangleF>();
                int keysWidth = 0, keysHeight = 0;
                if (showKeys)
                {
                    if (s.OverlayKeyLayout == KeyLayout.Keyboard)
                    {
                        var slots = KeyboardLayout.Arrange(names, out float widthUnits, out int rows);
                        float step = keyH + keyGap;
                        foreach (var slot in slots)
                            boxes.Add(new RectangleF(slot.Col * step, slot.Row * step, slot.Width * step - keyGap, keyH));
                        keysWidth = (int)Math.Ceiling(widthUnits * step - keyGap);
                        keysHeight = (int)(rows * step - keyGap);
                    }
                    else
                    {
                        float x = 0;
                        foreach (var label in labels)
                        {
                            float w = Math.Max(keyH, Draw.Measure(label, keyFont).Width + S(12));
                            boxes.Add(new RectangleF(x, 0, w, keyH));
                            x += w + keyGap;
                        }
                        keysWidth = (int)Math.Ceiling(x - keyGap);
                        keysHeight = keyH;
                    }
                }

                int cpsWidth = cpsText == null ? 0
                    : Draw.Measure(cpsText, cpsFont).Width + (rightText != null ? Draw.Measure(rightText, keyFont).Width + S(10) : 0);
                int cpsHeight = cpsText != null ? S(21) : 0;
                int width = Math.Max(S(56), Math.Max(cpsWidth, keysWidth) + pad * 2);
                int height = Math.Max(S(28), pad * 2 + cpsHeight + (cpsText != null && showKeys ? gap : 0) + keysHeight);

                var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    Draw.Smooth(g);
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                    var panel = new RectangleF(0, 0, bmp.Width - 1, bmp.Height - 1);
                    Draw.FillRound(g, panel, radius, Color.FromArgb(150, 8, 8, 12));
                    Draw.StrokeRound(g, panel, radius, Color.FromArgb(60, 255, 255, 255));

                    // One brush for the whole panel: with the rainbow on, every key and the counters pick up a
                    // different part of the gradient, and it slides along as the hue moves.
                    using (Brush highlight = MakeHighlightBrush(s, hue, new RectangleF(0, 0, bmp.Width, bmp.Height)))
                    {
                        int y = pad;
                        if (cpsText != null)
                        {
                            g.DrawString(cpsText, cpsFont, highlight, pad - S(2), y - S(3));
                            if (rightText != null)
                                using (var b = new SolidBrush(Color.FromArgb(190, 255, 255, 255)))
                                    g.DrawString(rightText, keyFont, b, pad + Draw.Measure(cpsText, cpsFont).Width + S(8), y + S(3));
                            y += cpsHeight + gap;
                        }

                        for (int i = 0; i < boxes.Count && i < labels.Length; i++)
                        {
                            var box = new RectangleF(boxes[i].X + pad, boxes[i].Y + y, boxes[i].Width, boxes[i].Height);
                            bool down = state.Keys != null && i < state.Keys.Length && state.Keys[i];
                            if (down)
                            {
                                using (var path = Draw.Round(box, S(5))) g.FillPath(highlight, path);
                            }
                            else
                            {
                                Draw.FillRound(g, box, S(5), Color.FromArgb(70, 255, 255, 255));
                            }
                            Draw.StrokeRound(g, box, S(5), Color.FromArgb(down ? 220 : 90, 255, 255, 255));
                            using (var b = new SolidBrush(down ? Color.FromArgb(20, 16, 24) : Color.FromArgb(225, 255, 255, 255)))
                            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
                                g.DrawString(labels[i], keyFont, b, box, sf);
                        }
                    }
                }
                return bmp;
            }
        }

        /// <summary>Solid accent, or a rainbow gradient sliding across the panel.</summary>
        static Brush MakeHighlightBrush(Settings s, float hue, RectangleF area)
        {
            if (!s.OverlayRainbow) return new SolidBrush(Theme.Accent);

            var brush = new LinearGradientBrush(area, Color.Red, Color.Blue, 20f);
            const int stops = 7;
            var colors = new Color[stops];
            var positions = new float[stops];
            for (int i = 0; i < stops; i++)
            {
                positions[i] = i / (float)(stops - 1);
                colors[i] = FromHsv(hue + positions[i] * 300f, 0.8, 1.0);
            }
            brush.InterpolationColors = new ColorBlend { Colors = colors, Positions = positions };
            return brush;
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
