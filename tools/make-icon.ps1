# Generates src/NeuzStrap/Resources/NeuzStrap.ico (multi-size, drawn in code so anyone can tweak it).
# Usage:  powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1

$ErrorActionPreference = 'Stop'
$out = Join-Path $PSScriptRoot '..\src\NeuzStrap\Resources\NeuzStrap.ico'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

public static class NeuzIcon
{
    static GraphicsPath Rounded(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static Bitmap Draw(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            float pad = Math.Max(0.5f, size * 0.04f);
            var rect = new RectangleF(pad, pad, size - pad * 2, size - pad * 2);
            using (var path = Rounded(rect, size * 0.24f))
            using (var brush = new LinearGradientBrush(rect, Color.FromArgb(255, 255, 111, 174), Color.FromArgb(255, 139, 92, 246), 45f))
            {
                g.FillPath(brush, path);
                // soft top highlight
                using (var hl = new LinearGradientBrush(rect, Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                    g.FillPath(hl, path);
            }

            // The "N"
            float fs = size * 0.62f;
            using (var font = new Font("Segoe UI Black", fs, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                var textRect = new RectangleF(0, size * 0.02f, size, size);
                if (size >= 32)
                    using (var shadow = new SolidBrush(Color.FromArgb(60, 40, 0, 60)))
                        g.DrawString("N", font, shadow, new RectangleF(textRect.X + size * 0.02f, textRect.Y + size * 0.03f, size, size), sf);
                g.DrawString("N", font, Brushes.White, textRect, sf);
            }

            // tiny lightning spark (skip on the smallest sizes)
            if (size >= 48)
            {
                float s = size / 256f;
                var bolt = new PointF[] {
                    new PointF(196*s, 30*s), new PointF(166*s, 86*s), new PointF(190*s, 86*s),
                    new PointF(172*s, 132*s), new PointF(224*s, 70*s), new PointF(198*s, 70*s), new PointF(214*s, 30*s)
                };
                using (var b = new SolidBrush(Color.FromArgb(255, 255, 236, 140)))
                    g.FillPolygon(b, bolt);
            }
        }
        return bmp;
    }

    static byte[] Png(Bitmap bmp)
    {
        using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); return ms.ToArray(); }
    }

    // Classic BMP/DIB icon entry (best compatibility for small sizes)
    static byte[] Dib(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        int maskStride = ((w + 31) / 32) * 4;
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(40); bw.Write(w); bw.Write(h * 2); bw.Write((short)1); bw.Write((short)32);
            bw.Write(0); bw.Write(w * h * 4 + maskStride * h); bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
            for (int y = h - 1; y >= 0; y--)
                for (int x = 0; x < w; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    bw.Write(c.B); bw.Write(c.G); bw.Write(c.R); bw.Write(c.A);
                }
            for (int y = h - 1; y >= 0; y--)
            {
                var row = new byte[maskStride];
                for (int x = 0; x < w; x++)
                    if (bmp.GetPixel(x, y).A == 0) row[x / 8] |= (byte)(0x80 >> (x % 8));
                bw.Write(row);
            }
            return ms.ToArray();
        }
    }

    public static void Save(string path)
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        var images = new List<byte[]>();
        foreach (var s in sizes)
            using (var b = Draw(s))
                images.Add(s >= 128 ? Png(b) : Dib(b));

        using (var fs = File.Create(path))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write((short)0); bw.Write((short)1); bw.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                bw.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                bw.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                bw.Write((byte)0); bw.Write((byte)0);
                bw.Write((short)1); bw.Write((short)32);
                bw.Write(images[i].Length); bw.Write(offset);
                offset += images[i].Length;
            }
            foreach (var img in images) bw.Write(img);
        }
    }
}
'@

[NeuzIcon]::Save((Resolve-Path (Split-Path $out)).Path + '\NeuzStrap.ico')
$preview = Join-Path $PSScriptRoot '..\docs\icon.png'
$bmp = [NeuzIcon]::Draw(256); $bmp.Save($preview, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
Write-Host "Icon written to $out"

