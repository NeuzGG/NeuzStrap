using System;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace NeuzStrap.UI
{
    /// <summary>Colors, fonts and DPI scaling for the whole app (dark theme with a user-picked accent).</summary>
    public static class Theme
    {
        public static readonly Color Bg = Hex("#0F0F14");
        public static readonly Color Sidebar = Hex("#15151D");
        public static readonly Color Surface = Hex("#1C1C26");
        public static readonly Color SurfaceHover = Hex("#252532");
        public static readonly Color Input = Hex("#13131A");
        public static readonly Color Border = Hex("#2C2C3A");
        public static readonly Color Text = Hex("#F4F4F8");
        public static readonly Color TextDim = Hex("#A6A6B8");
        public static readonly Color TextFaint = Hex("#6F6F84");
        public static readonly Color Success = Hex("#34D399");
        public static readonly Color Warning = Hex("#FBBF24");
        public static readonly Color Danger = Hex("#F87171");
        public static readonly Color TrackOff = Hex("#3A3A4A");

        public static readonly (string Name, Color Color)[] Accents =
        {
            ("Sakura", Hex("#FF6FAE")),
            ("Lavender", Hex("#A78BFA")),
            ("Ocean", Hex("#38BDF8")),
            ("Mint", Hex("#34D399")),
            ("Sunset", Hex("#FB923C")),
            ("Cherry", Hex("#F43F5E")),
        };

        public static Color Accent { get; private set; } = Accents[0].Color;
        public static Color AccentHover => Lighten(Accent, 0.12f);
        public static Color AccentPressed => Darken(Accent, 0.12f);
        /// <summary>Accent mixed into the card color, for soft highlights.</summary>
        public static Color AccentSoft => Mix(Surface, Accent, 0.16f);
        /// <summary>Readable text color on top of the accent.</summary>
        public static Color OnAccent => Luminance(Accent) > 0.35 ? Hex("#16121C") : Color.White;

        public static event Action AccentChanged;

        public static void SetAccent(string name)
        {
            var match = Accents.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
            Accent = match.Name != null ? match.Color : Accents[0].Color;
            AccentChanged?.Invoke();
        }

        // ------------------------------------------------------------------ fonts

        public static Font Body { get; private set; }
        public static Font BodyBold { get; private set; }
        public static Font Small { get; private set; }
        public static Font SmallBold { get; private set; }
        public static Font Title { get; private set; }
        public static Font H2 { get; private set; }
        public static Font H1 { get; private set; }
        public static Font Hero { get; private set; }
        public static Font Mono { get; private set; }
        public static Font Icons { get; private set; }
        public static Font IconsLarge { get; private set; }
        public static Font Emoji { get; private set; }
        public static Font EmojiLarge { get; private set; }

        public static float Scale { get; private set; } = 1f;

        public static int S(int px) => (int)Math.Round(px * Scale);
        public static float S(float px) => px * Scale;
        public static Size S(int w, int h) => new Size(S(w), S(h));
        public static Padding S(int l, int t, int r, int b) => new Padding(S(l), S(t), S(r), S(b));

        public static void Init(string accent)
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) Scale = Math.Max(1f, g.DpiX / 96f);

            string ui = "Segoe UI";
            string semibold = FontExists("Segoe UI Semibold") ? "Segoe UI Semibold" : ui;
            string iconFont = FontExists("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";

            Body = new Font(ui, 9.75f);
            BodyBold = new Font(semibold, 9.75f);
            Small = new Font(ui, 8.75f);
            SmallBold = new Font(semibold, 8.25f);
            Title = new Font(semibold, 10.5f);
            H2 = new Font(semibold, 13f);
            H1 = new Font(semibold, 19f);
            Hero = new Font(semibold, 24f);
            Mono = new Font(FontExists("Cascadia Mono") ? "Cascadia Mono" : "Consolas", 9f);
            Icons = new Font(iconFont, 11f);
            IconsLarge = new Font(iconFont, 16f);
            Emoji = new Font("Segoe UI Emoji", 11f);
            EmojiLarge = new Font("Segoe UI Emoji", 20f);

            SetAccent(accent);
        }

        static bool FontExists(string name)
        {
            using (var fonts = new InstalledFontCollection())
                return fonts.Families.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        // ------------------------------------------------------------------ color helpers

        public static Color Hex(string hex)
        {
            hex = hex.TrimStart('#');
            return Color.FromArgb(Convert.ToInt32(hex.Substring(0, 2), 16), Convert.ToInt32(hex.Substring(2, 2), 16), Convert.ToInt32(hex.Substring(4, 2), 16));
        }

        public static Color Mix(Color a, Color b, float t) => Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

        public static Color Lighten(Color c, float t) => Mix(c, Color.White, t);
        public static Color Darken(Color c, float t) => Mix(c, Color.Black, t);

        public static double Luminance(Color c)
        {
            double Ch(int v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
            return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
        }
    }

    /// <summary>Icon glyphs from Segoe MDL2 Assets / Segoe Fluent Icons (same code points).</summary>
    public static class Glyph
    {
        public const string Home = "\uE80F";
        public const string Speed = "\uEC4A";
        public const string Bolt = "\uE945";
        public const string Flag = "\uE7C1";
        public const string Puzzle = "\uEA86";
        public const string Link = "\uE71B";
        public const string Repair = "\uE90F";
        public const string Settings = "\uE713";
        public const string Info = "\uE946";
        public const string Play = "\uE768";
        public const string Stop = "\uE71A";
        public const string Mouse = "\uE962";
        public const string Keyboard = "\uE765";
        public const string Close = "\uE8BB";
        public const string ChevronDown = "\uE70D";
        public const string Check = "\uE73E";
        public const string Warning = "\uE7BA";
        public const string Lightbulb = "\uEA80";
        public const string Folder = "\uED25";
        public const string Delete = "\uE74D";
        public const string Add = "\uE710";
        public const string Download = "\uE896";
        public const string Refresh = "\uE72C";
        public const string Copy = "\uE8C8";
        public const string Import = "\uE8B5";
        public const string Font = "\uE8D2";
        public const string Heart = "\uEB51";
        public const string Globe = "\uE774";
        public const string Game = "\uE7FC";
        public const string Clean = "\uEA99";
        public const string Chip = "\uE950";
        public const string Memory = "\uE964";
        public const string Monitor = "\uE7F4";
        public const string Battery = "\uE83F";
        public const string History = "\uE81C";
        public const string Shield = "\uEA18";
        public const string Code = "\uE943";
        public const string OpenInNew = "\uE8A7";
    }
}
