using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NeuzStrap.Boost;
using NeuzStrap.Core;
using NeuzStrap.UI.Controls;

namespace NeuzStrap.UI.Pages
{
    public sealed class PerformancePage : Page, IProfileAware
    {
        readonly ProfileGrid _grid;
        readonly SettingRow _fpsRow;

        public override string Title => "Performance";
        public override string Subtitle => "Pick a profile, or fine-tune every option. Applied each time you press Play.";

        public PerformancePage(MainForm main) : base(main)
        {
            _grid = Add(new ProfileGrid(this));

            Section("In-game settings", "Same as Roblox's own settings menu, set for you before every launch.");
            DropdownRow("Graphics quality", "Locks the in-game graphics slider. Lower = more FPS. Roblox's automatic mode tends to jump around on weak PCs.",
                LevelDropdown(), () => S.GraphicsQualityLock, v => S.GraphicsQualityLock = (int)v, Glyph.Monitor, true);
            DropdownRow("Optimization mode", "Roblox's own Performance / Balanced / Quality switch.",
                new Dropdown(200).Add("Don't change", -1).Add("Performance", 0).Add("Balanced", 1).Add("Quality", 2),
                () => S.OptimizationMode, v => S.OptimizationMode = (int)v, Glyph.Speed, true);
            _fpsRow = DropdownRow("FPS cap", FpsDescription(0),
                new Dropdown(200).Add("Don't change", 0).Add("60 FPS", 60).Add("120 FPS", 120).Add("144 FPS", 144).Add("240 FPS", 240),
                () => S.FramerateCap, v => S.FramerateCap = (int)v, Glyph.History, true);

            Section("Rendering", "Uses only FastFlags on Roblox's official allowlist, so it's safe.");
            DropdownRow("Graphics API", "Direct3D 11 is the most stable on older and integrated GPUs. Try Vulkan if you get stutters on AMD.",
                new Dropdown(200).Add("Automatic", RenderingApi.Automatic).Add("Direct3D 11", RenderingApi.Direct3D11).Add("Vulkan", RenderingApi.Vulkan).Add("OpenGL", RenderingApi.OpenGL),
                () => S.RenderingApi, v => S.RenderingApi = (RenderingApi)v, Glyph.Chip, true);
            DropdownRow("Texture quality", "Lower textures use less video memory, which matters a lot on integrated graphics.",
                new Dropdown(200).Add("Automatic", TextureQuality.Automatic).Add("Lowest", TextureQuality.Lowest).Add("Low", TextureQuality.Low).Add("Medium", TextureQuality.Medium).Add("High", TextureQuality.High),
                () => S.TextureQuality, v => S.TextureQuality = (TextureQuality)v, null, true);
            DropdownRow("Anti-aliasing", "Smooths jagged edges. Turning it off is a free FPS boost on weak GPUs.",
                new Dropdown(200).Add("Automatic", 0).Add("Off", 1).Add("2x", 2).Add("4x", 4).Add("8x", 8),
                () => S.AntiAliasing, v => S.AntiAliasing = (int)v, null, true);
            DropdownRow("Force render quality", "Overrides Roblox's internal quality level (1-21) regardless of the slider.",
                new Dropdown(200).Add("Off", 0).Add("1 (lowest)", 1).Add("2", 2).Add("3", 3).Add("5", 5).Add("8", 8).Add("10", 10).Add("15", 15).Add("21 (highest)", 21),
                () => S.RenderQualityOverride, v => S.RenderQualityOverride = (int)v, null, true);
            DropdownRow("Mesh detail", "\"Lowest\" makes distant and complex models blocky but much cheaper to draw.",
                new Dropdown(200).Add("Automatic", MeshDetail.Automatic).Add("Lowest", MeshDetail.Lowest),
                () => S.MeshDetail, v => S.MeshDetail = (MeshDetail)v, null, true);
            DropdownRow("Render resolution (experimental)", "Draws the 3D world at a lower resolution. Approved by Roblox in 2026 but may be ignored on your Roblox version.",
                new Dropdown(200).Add("Off", 0).Add("1280 \u00D7 720", 921).Add("1024 \u00D7 576", 590).Add("960 \u00D7 540", 518).Add("854 \u00D7 480", 410).Add("640 \u00D7 360", 230),
                () => S.RenderResolutionKilopixels, v => S.RenderResolutionKilopixels = (int)v, null, true);

            Section("World");
            ToggleRow("Remove grass", "No grass on terrain. Big win in open-world games.", () => S.RemoveGrass, v => S.RemoveGrass = v, null, true);
            ToggleRow("Freeze lighting", "Stops lighting from updating. Large FPS boost, but lighting can look wrong when things move.", () => S.FreezeLighting, v => S.FreezeLighting = v, null, true);
            ToggleRow("Gray sky", "Replaces the skybox with plain gray.", () => S.GraySky, v => S.GraySky = v, null, true);

            Section("Display");
            ToggleRow("Exclusive fullscreen", "Alt+Enter uses true fullscreen. Can add a few FPS and lower input delay on weak PCs.", () => S.ExclusiveFullscreen, v => S.ExclusiveFullscreen = v, null, true);
            ToggleRow("Ignore display scaling", "For laptops set to 125%/150% scaling: menus render smaller and sharper.", () => S.DisableDpiScaling, v => S.DisableDpiScaling = v, null, true);
        }

        static string FpsDescription(int refreshHz) =>
            "Roblox's own Maximum Frame Rate (default 60). A steady 60 feels smoother than a jumpy 40-90 on weak PCs. " +
            (refreshHz > 0
                ? $"Your screen runs at {refreshHz} Hz, so you'll see at most {refreshHz} FPS."
                : "Above 60 only helps on a high refresh rate screen.");

        public override void OnNavigatedTo()
        {
            _ = ShowRefreshRateAsync();
        }

        async System.Threading.Tasks.Task ShowRefreshRateAsync()
        {
            var sys = await System.Threading.Tasks.Task.Run(() => SystemInfo.Get());
            if (!IsDisposed) _fpsRow.Description = FpsDescription(sys.RefreshRateHz);
        }

        static Dropdown LevelDropdown()
        {
            var dd = new Dropdown(200).Add("Don't change", 0);
            for (int i = 1; i <= 10; i++) dd.Add(i == 1 ? "1 (lowest)" : i == 10 ? "10 (highest)" : i.ToString(), i);
            return dd;
        }

        public void ProfileChanged()
        {
            _grid.Invalidate(true);
            RefreshRows();
        }

        internal void ApplyProfile(PerformanceProfile p)
        {
            Profiles.Apply(p, S, SystemInfo.Get());
            Settings.Save();
            Main.OnProfileChanged();
        }

        /// <summary>The four profile cards.</summary>
        sealed class ProfileGrid : Control, IAutoHeight
        {
            readonly List<ProfileCard> _cards = new List<ProfileCard>();

            public ProfileGrid(PerformancePage page)
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
                BackColor = Theme.Bg;
                foreach (var p in new[] { PerformanceProfile.RobloxDefault, PerformanceProfile.Balanced, PerformanceProfile.Potato, PerformanceProfile.UltraPotato })
                {
                    var card = new ProfileCard(p);
                    card.Click += (_, __) => page.ApplyProfile(card.Profile);
                    _cards.Add(card);
                    Controls.Add(card);
                }
            }

            int Columns => Width >= Theme.S(1000) ? 4 : 2;

            public int MeasureHeight(int width)
            {
                int cols = width >= Theme.S(1000) ? 4 : 2;
                int rows = (int)Math.Ceiling(_cards.Count / (double)cols);
                return rows * CardHeight + (rows - 1) * Theme.S(10);
            }

            static int CardHeight => Theme.S(128);

            protected override void OnLayout(LayoutEventArgs e)
            {
                int gap = Theme.S(10);
                int cols = Columns;
                int w = (Width - gap * (cols - 1)) / cols;
                for (int i = 0; i < _cards.Count; i++)
                    _cards[i].SetBounds((i % cols) * (w + gap), (i / cols) * (CardHeight + gap), w, CardHeight);
                base.OnLayout(e);
            }

            protected override void OnPaint(PaintEventArgs e) => e.Graphics.Clear(BackColor);
        }

        sealed class ProfileCard : NControl
        {
            public PerformanceProfile Profile { get; }

            public ProfileCard(PerformanceProfile p)
            {
                Profile = p;
                TabStop = true;
            }

            protected override void OnKeyUp(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) OnClick(EventArgs.Empty);
                base.OnKeyUp(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(ParentBack);
                Draw.Smooth(g);
                bool selected = Settings.Current.Profile == Profile;
                var sys = SystemInfo.Get();
                bool recommended = sys.Recommended == Profile;
                var r = new RectangleF(0, 0, Width - 1, Height - 1);
                int rad = Theme.S(12);

                Draw.FillRound(g, r, rad, selected ? Theme.AccentSoft : Hover ? Theme.SurfaceHover : Theme.Surface);
                if (selected) Draw.StrokeRound(g, new RectangleF(0, 0, Width, Height), rad, Theme.Accent, Theme.S(2f));
                else Draw.StrokeRound(g, new RectangleF(0, 0, Width, Height), rad, Theme.Border);

                int pad = Theme.S(16);
                Draw.Glyph(g, Profiles.Emoji(Profile), Theme.Emoji, new Rectangle(pad - Theme.S(2), Theme.S(12), Theme.S(30), Theme.S(28)), selected ? Theme.Accent : Theme.Text);
                Draw.Text(g, Profiles.Title(Profile), Theme.Title, new Rectangle(pad + Theme.S(30), Theme.S(12), Width - pad * 2 - Theme.S(30), Theme.S(28)), Theme.Text);

                if (recommended || selected)
                {
                    string badge = selected ? "Active" : "Best for this PC";
                    var size = Draw.Measure(badge, Theme.SmallBold);
                    var br = new Rectangle(Width - pad - size.Width - Theme.S(14), Theme.S(15), size.Width + Theme.S(14), Theme.S(22));
                    Draw.FillRound(g, br, br.Height / 2f, selected ? Theme.Accent : Theme.Mix(Theme.Surface, Theme.Success, 0.2f));
                    Draw.CenterText(g, badge, Theme.SmallBold, br, selected ? Theme.OnAccent : Theme.Success);
                }

                TextRenderer.DrawText(g, Profiles.Description(Profile), Theme.Small,
                    new Rectangle(pad, Theme.S(46), Width - pad * 2, Height - Theme.S(54)), Theme.TextDim,
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                PaintFocus(g, Rectangle.Round(r), rad);
            }
        }
    }
}
