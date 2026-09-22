using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using NeuzStrap.Core;
using NeuzStrap.Roblox;
using NeuzStrap.UI.Controls;

namespace NeuzStrap.UI.Pages
{
    public sealed class FastFlagsPage : Page, IProfileAware
    {
        readonly DataGridView _grid;
        readonly Paragraph _generated;
        readonly Paragraph _summary;
        bool _loading;

        public override string Title => "Fast Flags";
        public override string Subtitle => "Advanced: Roblox engine switches. Most people only need the Performance page.";

        public FastFlagsPage(MainForm main) : base(main)
        {
            var info = Add(new SettingRow("Roblox only reads allowlisted flags",
                "Since September 2025 Roblox ignores any FastFlag that isn't on its official allowlist. Ignored flags do nothing, " +
                "but they won't get you in trouble either. Flags marked \"Ignored\" below won't have any effect.",
                null, Glyph.Info));
            info.GlyphColor = Theme.Accent;

            Section("From your Performance settings", "Generated automatically. Change them on the Performance page.");
            _generated = Add(new Paragraph("", Theme.Mono, Theme.TextDim));

            Section("Your custom flags", "Added on top of the generated ones (and override them). Double-click a cell to edit.");

            var buttons = Add(new ButtonBar());
            buttons.AddButton(new NButton("Add flag", ButtonKind.Primary, Glyph.Add), (_, __) => AddFlag());
            buttons.AddButton(new NButton("Import JSON", ButtonKind.Secondary, Glyph.Import), (_, __) => ImportJson());
            buttons.AddButton(new NButton("Import from Bloxstrap", ButtonKind.Secondary, Glyph.Import), (_, __) => ImportFromBloxstrap());
            buttons.AddButton(new NButton("Copy all", ButtonKind.Secondary, Glyph.Copy), (_, __) => CopyAll());
            buttons.AddButton(new NButton("Remove selected", ButtonKind.Danger, Glyph.Delete), (_, __) => RemoveSelected());

            _grid = CreateGrid();
            Add(new GridHost(_grid));
            _summary = Add(new Paragraph("", Theme.Small, Theme.TextFaint));
        }

        public override void OnNavigatedTo()
        {
            RefreshGenerated();
            LoadGrid();
        }

        public void ProfileChanged() => RefreshGenerated();

        void RefreshGenerated()
        {
            var gen = FastFlags.Generate(S);
            _generated.Text = gen.Count == 0
                ? "(none. Your performance settings don't need any flags right now)"
                : string.Join("\n", gen.OrderBy(k => k.Key).Select(kv => $"{kv.Key} = {kv.Value}"));
            PerformLayout();
        }

        // ------------------------------------------------------------------ grid

        DataGridView CreateGrid()
        {
            var g = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Theme.Surface,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = Theme.S(36),
                EnableHeadersVisualStyles = false,
                GridColor = Theme.Border,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                Font = Theme.Body,
            };
            g.RowTemplate.Height = Theme.S(34);
            g.DefaultCellStyle.BackColor = Theme.Surface;
            g.DefaultCellStyle.ForeColor = Theme.Text;
            g.DefaultCellStyle.SelectionBackColor = Theme.AccentSoft;
            g.DefaultCellStyle.SelectionForeColor = Theme.Text;
            g.DefaultCellStyle.Padding = new Padding(Theme.S(8), 0, Theme.S(8), 0);
            g.ColumnHeadersDefaultCellStyle.BackColor = Theme.SurfaceHover;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Theme.TextDim;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.SurfaceHover;
            g.ColumnHeadersDefaultCellStyle.Font = Theme.SmallBold;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(Theme.S(8), 0, 0, 0);

            g.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "FLAG", FillWeight = 55, DefaultCellStyle = { Font = Theme.Mono } });
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", HeaderText = "VALUE", FillWeight = 25, DefaultCellStyle = { Font = Theme.Mono } });
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "STATUS", FillWeight = 20, ReadOnly = true });

            g.CellEndEdit += (_, __) => SaveGrid();
            g.UserDeletedRow += (_, __) => SaveGrid();
            g.CellFormatting += FormatStatus;
            g.HandleCreated += (_, __) => Native.UseDarkScrollbars(g);
            return g;
        }

        void FormatStatus(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex != 2 || e.RowIndex < 0) return;
            string name = _grid.Rows[e.RowIndex].Cells[0].Value as string ?? "";
            switch (FastFlags.GetStatus(name.Trim()))
            {
                case FastFlags.FlagStatus.Allowed: e.Value = "\u2713 Allowed"; e.CellStyle.ForeColor = Theme.Success; break;
                case FastFlags.FlagStatus.Experimental: e.Value = "Experimental"; e.CellStyle.ForeColor = Theme.Warning; break;
                default: e.Value = "Ignored"; e.CellStyle.ForeColor = Theme.TextFaint; break;
            }
            e.FormattingApplied = true;
        }

        void LoadGrid()
        {
            _loading = true;
            _grid.Rows.Clear();
            foreach (var kv in S.CustomFastFlags.OrderBy(k => k.Key))
                _grid.Rows.Add(kv.Key, kv.Value, "");
            _loading = false;
            UpdateSummary();
        }

        void SaveGrid()
        {
            if (_loading) return;
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (DataGridViewRow row in _grid.Rows)
            {
                string name = (row.Cells[0].Value as string ?? "").Trim();
                if (name.Length == 0) continue;
                dict[name] = (row.Cells[1].Value as string ?? "").Trim();
            }
            S.CustomFastFlags = dict;
            Settings.Save();
            _grid.Invalidate();
            UpdateSummary();
        }

        void UpdateSummary()
        {
            int total = S.CustomFastFlags.Count;
            int ignored = S.CustomFastFlags.Keys.Count(k => FastFlags.GetStatus(k) == FastFlags.FlagStatus.Ignored);
            _summary.Text = total == 0
                ? "No custom flags yet."
                : $"{total} custom flag(s)" + (ignored > 0 ? $", {ignored} of them ignored by Roblox." : ", all on the allowlist.");
        }

        // ------------------------------------------------------------------ actions

        void AddFlag()
        {
            string name = Dialog.Input(Main, "Add a FastFlag", "Flag name (for example FIntDebugForceMSAASamples):");
            if (string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim();
            if (FastFlags.GetStatus(name) == FastFlags.FlagStatus.Ignored &&
                !Dialog.Confirm(Main, "Not on Roblox's allowlist", $"Roblox will ignore \"{name}\", so it won't do anything. Add it anyway?", "Add anyway"))
                return;
            string value = Dialog.Input(Main, "Add a FastFlag", $"Value for {name} (True/False or a number):", "");
            if (value == null) return;
            S.CustomFastFlags[name] = value.Trim();
            Settings.Save();
            LoadGrid();
        }

        void ImportJson()
        {
            string text = Dialog.Input(Main, "Import FastFlags", "Paste the JSON (the contents of a ClientAppSettings.json):", "{\n  \n}", true);
            if (text == null) return;
            Merge(FastFlags.ParseImport(text, out var error), error);
        }

        void ImportFromBloxstrap()
        {
            string file = FastFlags.FindOtherBootstrapperFlags();
            if (file == null)
            {
                Dialog.Info(Main, "Nothing to import", "Couldn't find FastFlags from Bloxstrap, Fishstrap, Voidstrap or Froststrap on this PC.");
                return;
            }
            try { Merge(FastFlags.ParseImport(File.ReadAllText(file), out var error), error); }
            catch (Exception ex) { Dialog.Error(Main, "Import failed", ex.Message); }
        }

        void Merge(Dictionary<string, string> flags, string error)
        {
            if (flags == null)
            {
                Dialog.Error(Main, "That doesn't look like valid JSON", error ?? "Unknown error");
                return;
            }
            int ignored = 0;
            foreach (var kv in flags)
            {
                S.CustomFastFlags[kv.Key] = kv.Value;
                if (FastFlags.GetStatus(kv.Key) == FastFlags.FlagStatus.Ignored) ignored++;
            }
            Settings.Save();
            LoadGrid();
            Dialog.Success(Main, "Imported", $"{flags.Count} flag(s) imported." +
                (ignored > 0 ? $"\n\n{ignored} aren't on Roblox's allowlist and will be ignored. You can remove them with \"Remove selected\"." : ""));
        }

        void CopyAll()
        {
            try
            {
                Clipboard.SetText(FastFlags.ToJson(FastFlags.BuildFinal(S)));
                Toast.Show("Copied", "All FastFlags (generated + custom) are on your clipboard as JSON.", null, 4);
            }
            catch (Exception ex) { Dialog.Error(Main, "Couldn't copy", ex.Message); }
        }

        void RemoveSelected()
        {
            if (_grid.SelectedRows.Count == 0) return;
            foreach (DataGridViewRow row in _grid.SelectedRows) _grid.Rows.Remove(row);
            SaveGrid();
        }

        /// <summary>Gives the grid a fixed height inside the scrolling page, with rounded corners around it.</summary>
        sealed class GridHost : Card, IAutoHeight
        {
            public GridHost(DataGridView grid)
            {
                Padding = new Padding(Theme.S(2));
                grid.Dock = DockStyle.Fill;
                Controls.Add(grid);
            }

            public int MeasureHeight(int width) => Theme.S(300);
        }
    }
}
