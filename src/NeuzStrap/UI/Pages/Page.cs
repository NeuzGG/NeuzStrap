using System;
using System.Windows.Forms;
using NeuzStrap.Core;
using NeuzStrap.UI.Controls;

namespace NeuzStrap.UI.Pages
{
    /// <summary>A scrollable settings page shown in the main window.</summary>
    public abstract class Page : Stack
    {
        protected MainForm Main { get; }
        protected Settings S => Settings.Current;

        public abstract string Title { get; }
        public abstract string Subtitle { get; }

        protected Page(MainForm main)
        {
            Main = main;
            SuspendLayout(); // MainForm resumes once the subclass has added all its rows
        }

        /// <summary>Called every time the page becomes visible (refresh live info here).</summary>
        public virtual void OnNavigatedTo() { }

        protected T Add<T>(T c) where T : Control
        {
            Controls.Add(c);
            return c;
        }

        protected SectionHeader Section(string text, string hint = null) => Add(new SectionHeader(text, hint));

        protected Paragraph Note(string text) => Add(new Paragraph(text));

        /// <summary>Saves settings. Performance/booster tweaks flip the profile to "Custom".</summary>
        protected void Changed(bool makesProfileCustom = false)
        {
            if (makesProfileCustom && S.Profile != PerformanceProfile.Custom)
            {
                S.Profile = PerformanceProfile.Custom;
                Main.OnProfileChanged();
            }
            Settings.Save();
        }

        protected SettingRow ToggleRow(string title, string description, Func<bool> get, Action<bool> set, string glyph = null, bool makesProfileCustom = false)
        {
            var t = new Toggle();
            t.SetSilently(get());
            t.CheckedChanged += (_, __) => { set(t.Checked); Changed(makesProfileCustom); };
            var row = Add(new SettingRow(title, description, t, glyph));
            row.Tag = (Action)(() => t.SetSilently(get()));
            return row;
        }

        protected SettingRow DropdownRow(string title, string description, Dropdown dd, Func<object> get, Action<object> set,
                                         string glyph = null, bool makesProfileCustom = false)
        {
            dd.SelectValueSilently(get());
            dd.SelectedChanged += (_, __) => { set(dd.SelectedValue); Changed(makesProfileCustom); };
            var row = Add(new SettingRow(title, description, dd, glyph));
            row.Tag = (Action)(() => dd.SelectValueSilently(get()));
            return row;
        }

        protected SettingRow ButtonRow(string title, string description, NButton button, string glyph = null) =>
            Add(new SettingRow(title, description, button, glyph));

        /// <summary>Re-reads every row's value from settings (after a profile was applied, for example).</summary>
        public void RefreshRows()
        {
            foreach (Control c in Controls)
                if (c.Tag is Action refresh) refresh();
        }
    }
}
