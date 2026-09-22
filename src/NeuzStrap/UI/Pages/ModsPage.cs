using System;
using System.IO;
using System.Windows.Forms;
using NeuzStrap.Core;
using NeuzStrap.Roblox;
using NeuzStrap.UI.Controls;

namespace NeuzStrap.UI.Pages
{
    public sealed class ModsPage : Page
    {
        readonly SettingRow _folderRow;
        readonly SettingRow _fontRow;
        readonly NButton _removeFont;
        readonly ButtonBar _fontButtons;
        readonly CursorPicker _cursorPicker;

        public override string Title => "Mods";
        public override string Subtitle => "Swap Roblox's sounds, textures, fonts and cursor. Originals come back automatically when you remove a mod.";

        public ModsPage(MainForm main) : base(main)
        {
            var open = new NButton("Open folder", ButtonKind.Primary, Glyph.Folder).FitToText();
            open.Click += (_, __) => Utils.OpenFolder(Paths.Modifications);
            _folderRow = ButtonRow("Mods folder", "", open, Glyph.Folder);

            Note("How it works: put files in the Mods folder using the same layout as Roblox's own folder. " +
                 "For example, a file at content\\sounds\\ouch.ogg replaces Roblox's death sound. " +
                 "Changes apply the next time you press Play.");

            Section("Custom font");
            var choose = new NButton("Choose font\u2026", ButtonKind.Secondary, Glyph.Font).FitToText();
            choose.Click += (_, __) => ChooseFont();
            _removeFont = new NButton("Remove", ButtonKind.Ghost).FitToText();
            _removeFont.Click += (_, __) => RemoveFont();
            _fontButtons = new ButtonBar();
            _fontButtons.AddButton(_removeFont, null);
            _fontButtons.AddButton(choose, null);
            _fontRow = Add(new SettingRow("Use a custom font", "", _fontButtons, Glyph.Font));

            Section("Custom cursor", "Swap Roblox's mouse pointer. Only you see it.");
            _cursorPicker = Add(new CursorPicker());
            _cursorPicker.Picked += PickCursor;
            Note("Applies the next time you press Play. RGB turns Roblox's own arrow, hand pointer and shift-lock circle rainbow " +
                 "(Roblox only loads cursor images once, so the colors don't cycle). Arrows click at their tip; dots and crosshairs click in the middle. " +
                 "Your own image: a 64\u00D764 PNG made for Roblox is used as-is, and any other picture becomes a small cursor that clicks at its top-left corner.");

            Section("Good to know");
            Note("\u2022 Mods only change files on your PC. Other players see the normal game.\n" +
                 "\u2022 FastFlags don't go in the Mods folder. Use the Fast Flags page instead.\n" +
                 "\u2022 If Roblox acts weird after adding a mod, remove it (or use Tools > Repair Roblox).");
        }

        public override void OnNavigatedTo()
        {
            Directory.CreateDirectory(Paths.Modifications);
            int count = ModManager.CountUserMods();
            _folderRow.Description = count == 0
                ? "Empty right now. " + Paths.Modifications
                : $"{count} file(s) will be applied. " + Paths.Modifications;
            RefreshFont();
        }

        void RefreshFont()
        {
            bool has = S.UseCustomFont && File.Exists(Paths.CustomFontFile);
            _fontRow.Description = has
                ? $"Using \"{S.CustomFontName}\" for all in-game text (except Chinese/Japanese/Korean characters)."
                : "Replace every font in Roblox with one you like (.ttf or .otf).";
            _removeFont.Visible = has;
            _fontButtons.FitToButtons();
            _fontRow.PerformLayout();
            PerformLayout();
        }

        void ChooseFont()
        {
            using (var dlg = new OpenFileDialog { Filter = "Fonts (*.ttf;*.otf)|*.ttf;*.otf", Title = "Choose a font for Roblox" })
            {
                if (dlg.ShowDialog(Main) != DialogResult.OK) return;
                try
                {
                    File.Copy(dlg.FileName, Paths.CustomFontFile, true);
                    S.UseCustomFont = true;
                    S.CustomFontName = Path.GetFileNameWithoutExtension(dlg.FileName);
                    Settings.Save();
                    RefreshFont();
                    Toast.Show("Font set", $"\"{S.CustomFontName}\" will be used next time you play.", null, 4);
                }
                catch (Exception ex) { Dialog.Error(Main, "Couldn't use that font", ex.Message); }
            }
        }

        void RemoveFont()
        {
            S.UseCustomFont = false;
            S.CustomFontName = "";
            Settings.Save();
            try { File.Delete(Paths.CustomFontFile); } catch { }
            RefreshFont();
        }

        void PickCursor(CursorStyle style)
        {
            if (style == CursorStyle.Custom)
            {
                using (var dlg = new OpenFileDialog { Filter = "Images (*.png;*.jpg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp", Title = "Choose a cursor image" })
                {
                    if (dlg.ShowDialog(Main) != DialogResult.OK) return;
                    try
                    {
                        CursorMod.PrepareCustom(dlg.FileName); // make sure it's a readable image first
                        File.Copy(dlg.FileName, Paths.CustomCursorFile, true);
                    }
                    catch (Exception ex)
                    {
                        Dialog.Error(Main, "Couldn't use that image", ex.Message);
                        return;
                    }
                }
                _cursorPicker.ResetPreviews();
            }

            S.CursorStyle = style;
            Settings.Save();
            _cursorPicker.Invalidate();
            Toast.Show("Cursor set", style == CursorStyle.RobloxDefault
                ? "Roblox's normal cursor comes back the next time you press Play."
                : $"\"{CursorMod.Title(style)}\" shows up the next time you press Play.", null, 4);
        }

        /// <summary>Clickable tiles that preview each cursor style.</summary>
        sealed class CursorPicker : Control, IAutoHeight
        {
            static readonly CursorStyle[] Items =
            {
                CursorStyle.RobloxDefault, CursorStyle.Classic, CursorStyle.Sakura, CursorStyle.Rgb, CursorStyle.BigArrow,
                CursorStyle.Dot, CursorStyle.Crosshair, CursorStyle.Custom,
            };

            readonly System.Collections.Generic.Dictionary<CursorStyle, System.Drawing.Bitmap> _previews =
                new System.Collections.Generic.Dictionary<CursorStyle, System.Drawing.Bitmap>();
            int _hover = -1;

            public event Action<CursorStyle> Picked;

            static int TileW => Theme.S(100);
            static int TileH => Theme.S(104);
            static int Gap => Theme.S(10);

            public CursorPicker()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                Cursor = Cursors.Hand;
            }

            int Columns(int width) => Math.Max(1, (width + Gap) / (TileW + Gap));

            public int MeasureHeight(int width)
            {
                int rows = (int)Math.Ceiling(Items.Length / (double)Columns(width));
                return rows * TileH + (rows - 1) * Gap;
            }

            System.Drawing.Rectangle TileRect(int i)
            {
                int cols = Columns(Width);
                return new System.Drawing.Rectangle((i % cols) * (TileW + Gap), (i / cols) * (TileH + Gap), TileW, TileH);
            }

            int IndexAt(System.Drawing.Point p)
            {
                for (int i = 0; i < Items.Length; i++)
                    if (TileRect(i).Contains(p)) return i;
                return -1;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                int i = IndexAt(e.Location);
                if (i != _hover) { _hover = i; Invalidate(); }
                base.OnMouseMove(e);
            }

            protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                int i = IndexAt(e.Location);
                if (i >= 0 && e.Button == MouseButtons.Left) Picked?.Invoke(Items[i]);
                base.OnMouseClick(e);
            }

            public void ResetPreviews()
            {
                foreach (var b in _previews.Values) b?.Dispose();
                _previews.Clear();
                Invalidate();
            }

            System.Drawing.Bitmap Preview(CursorStyle style)
            {
                if (_previews.TryGetValue(style, out var cached)) return cached;
                System.Drawing.Bitmap bmp = null;
                try
                {
                    if (style == CursorStyle.RobloxDefault) bmp = LoadRobloxDefault();
                    else if (style == CursorStyle.Rgb)
                        using (var original = LoadRobloxDefault()) bmp = CursorMod.Rainbow(original, false);
                    else if (style == CursorStyle.Custom)
                    {
                        if (File.Exists(Paths.CustomCursorFile))
                            using (var ms = new MemoryStream(CursorMod.PrepareCustom(Paths.CustomCursorFile)))
                                bmp = new System.Drawing.Bitmap(ms);
                    }
                    else bmp = CursorMod.Render(style, false);
                }
                catch { bmp = null; }
                return _previews[style] = bmp;
            }

            /// <summary>Roblox's own cursor, from the installed copy (or its backup if a cursor mod is active).</summary>
            static System.Drawing.Bitmap LoadRobloxDefault()
            {
                string guid = State.Current.RobloxVersionGuid;
                var original = string.IsNullOrEmpty(guid) ? null : CursorMod.LoadOriginal(RobloxInstaller.VersionDir(guid), CursorMod.NormalPath);
                return original ?? CursorMod.Render(CursorStyle.Classic, false);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Parent?.BackColor ?? Theme.Bg);
                Draw.Smooth(g);
                for (int i = 0; i < Items.Length; i++)
                {
                    var style = Items[i];
                    var r = TileRect(i);
                    bool selected = Settings.Current.CursorStyle == style;
                    int rad = Theme.S(10);

                    Draw.FillRound(g, r, rad, selected ? Theme.AccentSoft : i == _hover ? Theme.SurfaceHover : Theme.Surface);
                    Draw.StrokeRound(g, r, rad, selected ? Theme.Accent : Theme.Border, selected ? Theme.S(2f) : 1f);

                    var well = new System.Drawing.Rectangle(r.X + Theme.S(10), r.Y + Theme.S(10), r.Width - Theme.S(20), r.Height - Theme.S(40));
                    Draw.FillRound(g, well, Theme.S(7), Theme.Hex("#2E4A3A")); // grassy backdrop, like a Roblox baseplate

                    var bmp = Preview(style);
                    if (bmp != null)
                    {
                        var vb = CursorMod.VisibleBounds(bmp);
                        float scale = Math.Min(1.6f * Theme.Scale, Math.Min((well.Width - 8f) / vb.Width, (well.Height - 8f) / vb.Height));
                        float w = vb.Width * scale, h = vb.Height * scale;
                        var dest = new System.Drawing.RectangleF(well.X + (well.Width - w) / 2, well.Y + (well.Height - h) / 2, w, h);
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(bmp, dest, vb, System.Drawing.GraphicsUnit.Pixel);
                    }
                    else
                    {
                        Draw.Glyph(g, Glyph.Add, Theme.IconsLarge, well, Theme.TextDim);
                    }

                    Draw.CenterText(g, CursorMod.Title(style), selected ? Theme.SmallBold : Theme.Small,
                        new System.Drawing.Rectangle(r.X, r.Bottom - Theme.S(28), r.Width, Theme.S(22)), selected ? Theme.Text : Theme.TextDim);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) ResetPreviews();
                base.Dispose(disposing);
            }
        }
    }
}
