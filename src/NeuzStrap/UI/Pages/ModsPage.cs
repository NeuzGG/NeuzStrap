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

        public override string Title => "Mods";
        public override string Subtitle => "Swap Roblox's sounds, textures and fonts. Originals come back automatically when you remove a mod.";

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
    }
}
