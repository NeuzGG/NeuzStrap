using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using NeuzStrap.Core;
using NeuzStrap.Roblox;
using NeuzStrap.Setup;
using NeuzStrap.UI;
using NeuzStrap.UI.Controls;
using Shortcut = NeuzStrap.Setup.Shortcut;
using Xunit;

namespace NeuzStrap.Tests
{
    public class UiLayoutTests
    {
        public UiLayoutTests() => Theme.Init("Sakura");

        [Fact]
        public void Button_bar_sizes_itself_while_its_page_is_still_hidden()
        {
            // Regression: the custom-font buttons measured 0 px wide because the page wasn't shown yet.
            using (var page = new Panel { Visible = false })
            {
                var bar = new ButtonBar();
                var a = new NButton("Remove");
                var b = new NButton("Choose font...");
                bar.AddButton(a, null);
                bar.AddButton(b, null);
                page.Controls.Add(bar);

                a.Visible = false;
                Assert.False(b.Visible);           // WinForms says "not visible" because the page is hidden...
                Assert.True(b.IsSetVisible());     // ...but it is meant to be shown

                bar.FitToButtons();
                Assert.Equal(b.Width, bar.Width);
                Assert.True(bar.Height > 0);
            }
        }

        [Fact]
        public void Stack_measures_rows_before_the_window_is_shown()
        {
            using (var stack = new Stack { Visible = false })
            {
                stack.Controls.Add(new SettingRow("Title", "Description", new Toggle()));
                stack.Controls.Add(new SettingRow("Title 2", "Description 2", null));
                int h = stack.MeasureContent(Theme.S(640));
                Assert.True(h > Theme.S(58) * 2, $"content height was {h}");
            }
        }
    }

    public class FramerateTests
    {
        [Theory]
        [InlineData(60, "60")]
        [InlineData(144, "144")]
        [InlineData(240, "240")]
        [InlineData(30, "-1")]   // not one of Roblox's options -> left alone
        [InlineData(9999, "-1")]
        public void Only_writes_values_roblox_offers(int cap, string expected)
        {
            string dir = TempDir.Create();
            string path = Path.Combine(dir, "GlobalBasicSettings_13.xml");
            File.WriteAllText(path,
                "<roblox version=\"4\"><Item class=\"UserGameSettings\" referent=\"R\"><Properties>" +
                "<int name=\"FramerateCap\">-1</int></Properties></Item></roblox>", new UTF8Encoding(false));

            GameSettingsFile.ApplyTo(path, new Settings { FramerateCap = cap, OptimizationMode = -1 });

            var doc = new XmlDocument();
            doc.Load(path);
            Assert.Equal(expected, doc.SelectSingleNode("//*[@name='FramerateCap']").InnerText);
            TempDir.Delete(dir);
        }

        [Fact]
        public void Has_changes_only_when_something_would_be_written()
        {
            Assert.False(GameSettingsFile.HasChanges(new Settings { OptimizationMode = -1, FramerateCap = 0 }));
            Assert.False(GameSettingsFile.HasChanges(new Settings { OptimizationMode = -1, FramerateCap = 30 }));
            Assert.True(GameSettingsFile.HasChanges(new Settings { OptimizationMode = -1, FramerateCap = 144 }));
            Assert.True(GameSettingsFile.HasChanges(new Settings())); // Balanced sets the optimization mode
        }
    }

    public class OfficialShortcutTests : IDisposable
    {
        readonly string _base = TempDir.Create();      // NeuzStrap's data folder
        readonly string _official = TempDir.Create();  // the official launcher's folder (elsewhere, like %LocalAppData%\Roblox)
        readonly string _desktop;
        readonly string _officialExe;

        public OfficialShortcutTests()
        {
            Paths.InitForTests(_base);
            State.Current.TakenOverShortcuts.Clear();
            _desktop = Path.Combine(_official, "Desktop");
            Directory.CreateDirectory(_desktop);
            _officialExe = Path.Combine(_official, "Versions", "version-abc", "RobloxPlayerBeta.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(_officialExe));
            File.WriteAllText(_officialExe, "");
        }

        [Fact]
        public void Takes_over_official_player_shortcuts_and_restores_them()
        {
            string player = Path.Combine(_desktop, "Roblox Player.lnk");
            string studio = Path.Combine(_desktop, "Roblox Studio.lnk");
            Shortcut.Create(player, _officialExe, "", "Roblox");
            Shortcut.Create(studio, Path.Combine(_official, "Versions", "RobloxStudioBeta.exe"), "", "Studio");

            var folders = new[] { _desktop };
            Assert.Single(OfficialShortcuts.Find(folders)); // Studio is never touched

            string neuz = Path.Combine(_base, "NeuzStrap.exe");
            Assert.Equal(1, OfficialShortcuts.TakeOver(folders, neuz));
            var now = Shortcut.Read(player).Value;
            Assert.Equal(neuz, now.Target, ignoreCase: true);
            Assert.Equal("-player", now.Arguments);
            Assert.Empty(OfficialShortcuts.Find(folders));

            OfficialShortcuts.Restore();
            Assert.Equal(_officialExe, Shortcut.Read(player).Value.Target, ignoreCase: true);
            Assert.Empty(State.Current.TakenOverShortcuts);
        }

        public void Dispose()
        {
            TempDir.Delete(_base);
            TempDir.Delete(_official);
        }
    }
}
