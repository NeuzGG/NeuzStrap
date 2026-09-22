using System.Drawing;
using System.Windows.Forms;
using NeuzStrap.Core;
using NeuzStrap.UI.Controls;

namespace NeuzStrap.UI.Pages
{
    public sealed class AboutPage : Page
    {
        public override string Title => "About";
        public override string Subtitle => "Free and open source, forever.";

        public AboutPage(MainForm main) : base(main)
        {
            Add(new AboutHero());

            Section("What NeuzStrap does");
            Note("\u2022 Installs and updates Roblox straight from Roblox's official servers, with resumable downloads for slow internet.\n" +
                 "\u2022 Tunes Roblox for weak PCs with one-click profiles, using only Roblox-approved settings.\n" +
                 "\u2022 Game Booster: CPU priority, RAM clean-up, GPU selection, calmer background apps and a power boost.\n" +
                 "\u2022 Server location notices, Discord Rich Presence, game history, mods, custom fonts and a disk cleaner.\n" +
                 "\u2022 Gets out of the way: when nothing needs to run in the background, NeuzStrap closes as soon as Roblox starts.");

            Section("Links");
            if (AppInfo.HasRepo)
            {
                var gh = new NButton("GitHub", ButtonKind.Secondary, Glyph.OpenInNew).FitToText();
                gh.Click += (_, __) => Utils.OpenUrl(AppInfo.RepoUrl);
                ButtonRow("Source code", "Read the code, star the project or suggest features.", gh, Glyph.Code);

                var bug = new NButton("Report a bug", ButtonKind.Secondary, Glyph.OpenInNew).FitToText();
                bug.Click += (_, __) => Utils.OpenUrl(AppInfo.RepoUrl + "/issues/new");
                ButtonRow("Found a problem?", "Include your NeuzStrap log (Tools > NeuzStrap logs).", bug, Glyph.Warning);
            }
            var logs = new NButton("Open logs", ButtonKind.Ghost, Glyph.Folder).FitToText();
            logs.Click += (_, __) => Utils.OpenFolder(Paths.Logs);
            ButtonRow("Logs", "What NeuzStrap did recently, handy for troubleshooting.", logs, Glyph.Folder);

            Section("Credits");
            Note("Inspired by Bloxstrap (MIT License) and the Roblox bootstrapper community. NeuzStrap is written from scratch for low-end PCs.\n\n" +
                 "NeuzStrap is not affiliated with, endorsed by, or connected to Roblox Corporation or Bloxstrap. " +
                 "\"Roblox\" is a trademark of Roblox Corporation.\n\n" +
                 "Released under the MIT License.");
        }

        sealed class AboutHero : Card, IAutoHeight
        {
            public AboutHero()
            {
                Radius = Theme.S(14);
            }

            public int MeasureHeight(int width) => Theme.S(132);

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                Draw.Smooth(g);
                int pad = Theme.S(24);
                using (var icon = AppIcon.Bitmap(Theme.S(84)))
                    g.DrawImage(icon, pad, (Height - Theme.S(84)) / 2);
                int x = pad + Theme.S(104);
                Draw.Text(g, AppInfo.Name, Theme.Hero, new Rectangle(x, Theme.S(22), Width - x - pad, Theme.S(44)), Theme.Text);
                Draw.Text(g, $"Version {AppInfo.VersionString} \u2022 {AppInfo.Tagline}", Theme.Body, new Rectangle(x, Theme.S(66), Width - x - pad, Theme.S(22)), Theme.TextDim);

                string made = "Made with ";
                int mw = Draw.Measure(made, Theme.Body).Width;
                Draw.Text(g, made, Theme.Body, new Rectangle(x, Theme.S(92), mw + 2, Theme.S(22)), Theme.TextDim);
                Draw.Glyph(g, Glyph.Heart, Theme.Icons, new Rectangle(x + mw - Theme.S(2), Theme.S(92), Theme.S(20), Theme.S(22)), Theme.Accent);
                Draw.Text(g, $"by {AppInfo.Author}, for potato PCs everywhere", Theme.Body, new Rectangle(x + mw + Theme.S(18), Theme.S(92), Width - x - mw - pad, Theme.S(22)), Theme.TextDim);
            }
        }
    }
}
