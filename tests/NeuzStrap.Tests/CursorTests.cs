using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using NeuzStrap.Core;
using NeuzStrap.Roblox;
using Xunit;

namespace NeuzStrap.Tests
{
    public class CursorTests : IDisposable
    {
        readonly string _base = TempDir.Create();

        public CursorTests()
        {
            Paths.InitForTests(_base);
        }

        static Bitmap Load(byte[] png)
        {
            using (var ms = new MemoryStream(png)) return new Bitmap(ms);
        }

        [Theory]
        [InlineData(CursorStyle.Classic, 34, 38)]   // just inside the arrow, below-right of the tip
        [InlineData(CursorStyle.BigArrow, 34, 38)]
        [InlineData(CursorStyle.Sakura, 34, 38)]
        [InlineData(CursorStyle.Dot, 32, 32)]       // dot sits right on the click point
        [InlineData(CursorStyle.Crosshair, 32, 26)] // crosshair arm above the click point
        public void Built_in_styles_are_roblox_sized_and_click_at_the_center(CursorStyle style, int x, int y)
        {
            foreach (bool hover in new[] { false, true })
                using (var bmp = CursorMod.Render(style, hover))
                {
                    Assert.Equal(64, bmp.Width);
                    Assert.Equal(64, bmp.Height);
                    Assert.Equal(0, bmp.GetPixel(0, 0).A); // transparent background
                    if (!hover || style != CursorStyle.Sakura)
                        Assert.True(bmp.GetPixel(x, y).A > 200, $"{style} (hover={hover}) should be drawn at {x},{y}");
                }
            // nothing drawn up-left of the click point for arrows (the tip must be the top-left of the shape)
            if (style == CursorStyle.Classic)
                using (var bmp = CursorMod.Render(style, false))
                    Assert.True(bmp.GetPixel(26, 26).A < 20);
        }

        [Fact]
        public void Own_64px_image_is_used_as_is_and_other_sizes_click_at_top_left()
        {
            string exact = Path.Combine(_base, "exact.png");
            using (var b = new Bitmap(64, 64)) { b.SetPixel(10, 10, Color.Red); b.Save(exact, ImageFormat.Png); }
            Assert.Equal(File.ReadAllBytes(exact), CursorMod.PrepareCustom(exact));

            string small = Path.Combine(_base, "small.png");
            using (var b = new Bitmap(16, 16))
            {
                using (var g = Graphics.FromImage(b)) g.Clear(Color.Blue);
                b.Save(small, ImageFormat.Png);
            }
            using (var result = Load(CursorMod.PrepareCustom(small)))
            {
                Assert.Equal(64, result.Width);
                Assert.True(result.GetPixel(33, 33).A > 200); // image starts at the click point
                Assert.Equal(0, result.GetPixel(30, 30).A);   // nothing before it
            }
        }

        [Fact]
        public void Mod_manager_applies_the_cursor_and_restores_robloxs()
        {
            string version = Path.Combine(_base, "Versions", "version-test");
            string normal = Path.Combine(version, CursorMod.NormalPath);
            Directory.CreateDirectory(Path.GetDirectoryName(normal));
            File.WriteAllText(normal, "roblox cursor");
            File.WriteAllText(Path.Combine(version, CursorMod.HoverPath), "roblox hand");

            var s = new Settings { CursorStyle = CursorStyle.Sakura };
            ModManager.Apply(version, s);
            using (var bmp = Load(File.ReadAllBytes(normal))) Assert.Equal(64, bmp.Width);

            s.CursorStyle = CursorStyle.RobloxDefault;
            ModManager.Apply(version, s);
            Assert.Equal("roblox cursor", File.ReadAllText(normal));
            Assert.Equal("roblox hand", File.ReadAllText(Path.Combine(version, CursorMod.HoverPath)));
        }

        [Fact]
        public void Default_and_missing_custom_image_change_nothing()
        {
            Assert.Empty(CursorMod.Build(new Settings(), null));
            Assert.Empty(CursorMod.Build(new Settings { CursorStyle = CursorStyle.Custom }, null));
            Assert.Empty(CursorMod.Build(new Settings { CursorStyle = CursorStyle.Rgb }, null)); // Roblox not installed yet
        }

        /// <summary>A little "Roblox-like" cursor: white fill, black outline, transparent around it.</summary>
        static Bitmap FakeCursor(Color fill)
        {
            var b = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(b))
            {
                g.Clear(Color.Transparent);
                g.FillRectangle(Brushes.Black, 30, 30, 20, 20);
                using (var f = new SolidBrush(fill)) g.FillRectangle(f, 32, 32, 16, 16);
            }
            return b;
        }

        static bool IsColorful(Color c) => Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B)) > 80;

        [Fact]
        public void Rgb_recolors_the_fill_but_keeps_outline_and_transparency()
        {
            using (var src = FakeCursor(Color.White))
            using (var rgb = CursorMod.Rainbow(src, aroundCenter: false))
            {
                Assert.True(IsColorful(rgb.GetPixel(33, 33)), "fill should turn rainbow");
                Assert.True(IsColorful(rgb.GetPixel(46, 46)));
                Assert.NotEqual(rgb.GetPixel(33, 33).ToArgb(), rgb.GetPixel(46, 46).ToArgb()); // different colors across the shape
                Assert.Equal(Color.Black.ToArgb(), rgb.GetPixel(30, 30).ToArgb());           // outline untouched
                Assert.Equal(0, rgb.GetPixel(5, 5).A);                                       // background still transparent
            }
        }

        [Fact]
        public void Rgb_starts_from_robloxs_original_not_the_current_custom_cursor()
        {
            string version = Path.Combine(_base, "Versions", "version-rgb");
            foreach (var rel in new[] { CursorMod.NormalPath, CursorMod.HoverPath, CursorMod.LockedPath })
            {
                string current = Path.Combine(version, rel);
                string backup = Path.Combine(version, ModManager.BackupFolder, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(current));
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                using (var custom = FakeCursor(Color.Blue)) custom.Save(current, ImageFormat.Png);    // a custom cursor is active
                using (var original = FakeCursor(Color.White)) original.Save(backup, ImageFormat.Png); // Roblox's original
            }

            var files = CursorMod.Build(new Settings { CursorStyle = CursorStyle.Rgb }, version);
            Assert.Equal(3, files.Count); // arrow, hand and shift-lock circle
            using (var bmp = Load(files[CursorMod.NormalPath]))
                Assert.True(IsColorful(bmp.GetPixel(40, 40)) && bmp.GetPixel(40, 40).ToArgb() != Color.Blue.ToArgb());
        }

        public void Dispose() => TempDir.Delete(_base);
    }
}
