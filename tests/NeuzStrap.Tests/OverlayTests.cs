using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using NeuzStrap.Core;
using NeuzStrap.Integrations;
using NeuzStrap.Roblox;
using NeuzStrap.UI;
using Xunit;

namespace NeuzStrap.Tests
{
    public class OverlayRendererTests
    {
        public OverlayRendererTests() => Theme.Init("Sakura");

        static bool Contains(Bitmap bmp, Color c, int tolerance = 12)
        {
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                {
                    var p = bmp.GetPixel(x, y);
                    if (Math.Abs(p.R - c.R) <= tolerance && Math.Abs(p.G - c.G) <= tolerance && Math.Abs(p.B - c.B) <= tolerance && p.A > 200)
                        return true;
                }
            return false;
        }

        [Fact]
        public void Draws_cps_and_keys_and_highlights_held_keys()
        {
            // keys only, so the (always accent-colored) CPS text doesn't take part in this check
            var keysOnly = new Settings { Overlay = true, OverlayCps = false, OverlayKeys = true };
            var labels = new[] { "W", "A", "S", "D", "SPACE", "SHIFT" };
            var pressed = new OverlayState { Labels = labels, Keys = new[] { true, false, false, false, false, false } };
            var idle = new OverlayState { Labels = labels, Keys = new bool[labels.Length] };

            using (var a = OverlayRenderer.Render(keysOnly, pressed, 0))
            using (var b = OverlayRenderer.Render(keysOnly, idle, 0))
            {
                Assert.Equal(a.Size, b.Size);            // size doesn't jump around while you play
                Assert.True(a.Width > 0 && a.Height > 0);
                Assert.True(Contains(a, Theme.Accent));  // the held key is filled with the accent color
                Assert.False(Contains(b, Theme.Accent, 4));
            }

            // with CPS on, the panel is taller and still the same size whatever you press
            var both = new Settings { Overlay = true, OverlayCps = true, OverlayKeys = true };
            using (var withCps = OverlayRenderer.Render(both, new OverlayState { LeftCps = 9, Labels = labels, Keys = pressed.Keys }, 0))
            using (var keys = OverlayRenderer.Render(keysOnly, pressed, 0))
                Assert.True(withCps.Height > keys.Height);
        }

        [Fact]
        public void Rainbow_changes_color_over_time_and_static_does_not()
        {
            var rainbow = new Settings { OverlayRainbow = true };
            Assert.NotEqual(OverlayRenderer.Highlight(rainbow, 0).ToArgb(), OverlayRenderer.Highlight(rainbow, 120).ToArgb());

            var plain = new Settings { OverlayRainbow = false };
            Assert.Equal(Theme.Accent.ToArgb(), OverlayRenderer.Highlight(plain, 0).ToArgb());
            Assert.Equal(Theme.Accent.ToArgb(), OverlayRenderer.Highlight(plain, 200).ToArgb());
        }

        [Fact]
        public void Turning_parts_off_makes_it_smaller()
        {
            var both = new Settings { OverlayCps = true, OverlayKeys = true };
            var cpsOnly = new Settings { OverlayCps = true, OverlayKeys = false };
            var state = OverlayState.Sample;
            using (var a = OverlayRenderer.Render(both, state, 0))
            using (var b = OverlayRenderer.Render(cpsOnly, state, 0))
            {
                Assert.True(b.Height < a.Height);
                Assert.True(b.Width <= a.Width);
            }
        }

        [Fact]
        public void Sizes_scale_the_panel()
        {
            var small = new Settings { OverlayScale = OverlaySize.Small };
            var large = new Settings { OverlayScale = OverlaySize.Large };
            using (var a = OverlayRenderer.Render(small, OverlayState.Sample, 0))
            using (var b = OverlayRenderer.Render(large, OverlayState.Sample, 0))
                Assert.True(b.Width > a.Width && b.Height > a.Height);
        }
    }

    public class KeyNameTests
    {
        [Theory]
        [InlineData("W", 0x57)]
        [InlineData("w", 0x57)]
        [InlineData("5", 0x35)]
        [InlineData("SPACE", 0x20)]
        [InlineData("SHIFT", 0xA0)]
        [InlineData("CTRL", 0xA2)]
        [InlineData("F5", 0x74)]
        [InlineData("MOUSE1", 0x01)]
        [InlineData("NUM3", 0x63)]
        [InlineData("UP", 0x26)]
        public void Names_map_to_virtual_keys(string name, int vk) => Assert.Equal(vk, KeyNames.ToVirtualKey(name));

        [Theory]
        [InlineData("")]
        [InlineData("BANANA")]
        [InlineData("F19")]
        public void Unknown_names_are_rejected(string name) => Assert.False(KeyNames.IsSupported(name));

        [Theory]
        [InlineData(System.Windows.Forms.Keys.W, "W")]
        [InlineData(System.Windows.Forms.Keys.D4, "4")]
        [InlineData(System.Windows.Forms.Keys.Space, "SPACE")]
        [InlineData(System.Windows.Forms.Keys.LShiftKey, "SHIFT")]
        [InlineData(System.Windows.Forms.Keys.ControlKey, "CTRL")]
        [InlineData(System.Windows.Forms.Keys.F3, "F3")]
        [InlineData(System.Windows.Forms.Keys.Left, "LEFT")]
        public void Key_presses_become_storable_names(System.Windows.Forms.Keys key, string expected) =>
            Assert.Equal(expected, KeyNames.FromKeyPress(key));

        [Fact]
        public void Cleaning_drops_junk_duplicates_and_keeps_a_sane_list()
        {
            var cleaned = KeyNames.Clean(new[] { "w", "W", "space", "banana", "", "CTRL" });
            Assert.Equal(new[] { "W", "SPACE", "CTRL" }, cleaned);
            Assert.Equal(KeyNames.Default, KeyNames.Clean(new[] { "nope" }));   // never end up with nothing
            Assert.Equal(KeyNames.Default, KeyNames.Clean(null));
            Assert.True(KeyNames.Clean(Enumerable.Repeat("A", 1).Concat(Enumerable.Range(0, 30).Select(i => "F" + (i % 12 + 1)))).Count <= 10);
        }

        [Fact]
        public void Overlay_draws_whatever_keys_you_picked()
        {
            Theme.Init("Sakura");
            var s = new Settings { OverlayKeys = true, OverlayCps = false, OverlayKeyList = new List<string> { "MOUSE1", "F", "CTRL" } };
            var state = OverlayState.SampleFor(s);
            Assert.Equal(new[] { "LMB", "F", "CTRL" }, state.Labels);
            using (var bmp = OverlayRenderer.Render(s, state, 0))
                Assert.True(bmp.Width > 0 && bmp.Height > 0);
        }
    }

    public class DiscordActivityTests
    {
        static GameSession Public => new GameSession { PlaceId = 123, JobId = "job-1", JoinedUtc = DateTime.UtcNow };
        static GameDetails Game => new GameDetails { Name = "Mega Obby Tower", Creator = "ObbyMakers", IconUrl = "https://tr.rbxcdn.com/icon.png" };

        static List<Dictionary<string, object>> Buttons(Dictionary<string, object> activity) =>
            (activity.TryGetValue("buttons", out var b) ? (List<object>)b : new List<object>()).Cast<Dictionary<string, object>>().ToList();

        [Fact]
        public void Join_button_points_at_the_exact_server()
        {
            var buttons = Buttons(DiscordRpc.BuildActivity(Public, Game, showButton: false, showJoinButton: true));
            Assert.Single(buttons);
            Assert.Equal("Join server", buttons[0]["label"]);
            Assert.Equal("https://www.roblox.com/games/start?placeId=123&gameInstanceId=job-1", buttons[0]["url"]);
        }

        [Fact]
        public void Both_buttons_fit_and_join_comes_first()
        {
            var buttons = Buttons(DiscordRpc.BuildActivity(Public, Game, showButton: true, showJoinButton: true));
            Assert.Equal(2, buttons.Count); // Discord allows at most two
            Assert.Equal("Join server", buttons[0]["label"]);
            Assert.Equal("View game", buttons[1]["label"]);
        }

        [Fact]
        public void No_join_button_for_private_or_reserved_servers()
        {
            var priv = new GameSession { PlaceId = 123, JobId = "job-1", IsPrivateServer = true };
            var reserved = new GameSession { PlaceId = 123, JobId = "job-1", IsReservedServer = true };
            Assert.Empty(Buttons(DiscordRpc.BuildActivity(priv, Game, false, true)));
            Assert.Empty(Buttons(DiscordRpc.BuildActivity(reserved, Game, false, true)));
            Assert.Empty(Buttons(DiscordRpc.BuildActivity(Public, Game, false, false)));
        }
    }

    public class FpsCounterSettingTests
    {
        const string Sample = "<roblox version=\"4\"><Item class=\"UserGameSettings\" referent=\"R\"><Properties>" +
                              "<int name=\"FramerateCap\">-1</int></Properties></Item></roblox>";

        [Theory]
        [InlineData(true, "true")]
        [InlineData(false, "false")]
        public void Writes_robloxs_performance_panel_flag(bool on, string expected)
        {
            string dir = TempDir.Create();
            string path = Path.Combine(dir, "GlobalBasicSettings_13.xml");
            File.WriteAllText(path, Sample, new UTF8Encoding(false));

            GameSettingsFile.ApplyTo(path, new Settings { OptimizationMode = -1 }, on);

            var doc = new XmlDocument();
            doc.Load(path);
            Assert.Equal(expected, doc.SelectSingleNode("//*[@name='PerformanceStatsVisible']").InnerText);
            TempDir.Delete(dir);
        }

        [Fact]
        public void Leaves_it_alone_when_not_asked()
        {
            string dir = TempDir.Create();
            string path = Path.Combine(dir, "GlobalBasicSettings_13.xml");
            File.WriteAllText(path, Sample, new UTF8Encoding(false));

            GameSettingsFile.ApplyTo(path, new Settings { OptimizationMode = 1 });

            var doc = new XmlDocument();
            doc.Load(path);
            Assert.Null(doc.SelectSingleNode("//*[@name='PerformanceStatsVisible']"));
            TempDir.Delete(dir);
        }
    }
}
