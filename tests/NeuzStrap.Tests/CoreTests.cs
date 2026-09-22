using System;
using System.Collections.Generic;
using System.IO;
using NeuzStrap.Core;
using NeuzStrap.Roblox;
using Xunit;

namespace NeuzStrap.Tests
{
    public class JsonTests
    {
        [Fact]
        public void Parses_objects_arrays_numbers_and_escapes()
        {
            var v = (Dictionary<string, object>)Json.Parse("{\"a\": 1, \"b\": [true, null, 2.5], \"c\": \"x\\\"y\\u00e9\\n\"}");
            Assert.Equal(1L, v["a"]);
            var b = (List<object>)v["b"];
            Assert.Equal(true, b[0]);
            Assert.Null(b[1]);
            Assert.Equal(2.5, b[2]);
            Assert.Equal("x\"y\u00e9\n", v["c"]);
        }

        [Fact]
        public void Tolerates_comments_trailing_commas_and_bom()
        {
            var v = (Dictionary<string, object>)Json.Parse("\uFEFF{ // a comment\n \"FFlagX\": \"True\", /* block */ \"FIntY\": 3, }");
            Assert.Equal("True", v["FFlagX"]);
            Assert.Equal(3L, v["FIntY"]);
        }

        [Fact]
        public void Reports_errors_with_position()
        {
            Assert.False(Json.TryParse("{ \"a\": }", out _, out var error));
            Assert.Contains("line 1", error);
        }

        [Fact]
        public void Serialize_escapes_control_characters_and_round_trips()
        {
            var original = new Dictionary<string, object> { ["k"] = "tab\there \"quoted\" \u0001", ["n"] = 5L };
            string json = Json.Serialize(original);
            var back = (Dictionary<string, object>)Json.Parse(json);
            Assert.Equal(original["k"], back["k"]);
            Assert.Equal(5L, back["n"]);
        }

        [Fact]
        public void Settings_round_trip_keeps_values_enums_dates_and_dictionaries()
        {
            var s = new Settings
            {
                Profile = PerformanceProfile.UltraPotato,
                RenderingApi = RenderingApi.Vulkan,
                FramerateCap = 60,
                DiscordApplicationId = "123",
                CustomFastFlags = new Dictionary<string, string> { ["FIntDebugForceMSAASamples"] = "1" },
            };
            var back = Json.Deserialize<Settings>(Json.Serialize(s));
            Assert.Equal(PerformanceProfile.UltraPotato, back.Profile);
            Assert.Equal(RenderingApi.Vulkan, back.RenderingApi);
            Assert.Equal(60, back.FramerateCap);
            Assert.Equal("123", back.DiscordApplicationId);
            Assert.Equal("1", back.CustomFastFlags["FIntDebugForceMSAASamples"]);

            var st = new State { RobloxInstalledUtc = new DateTime(2026, 9, 22, 10, 30, 0, DateTimeKind.Utc) };
            st.History.Add(new GameHistoryEntry { PlaceId = 42, Name = "Game", LastPlayedUtc = st.RobloxInstalledUtc });
            var stBack = Json.Deserialize<State>(Json.Serialize(st));
            Assert.Equal(st.RobloxInstalledUtc, stBack.RobloxInstalledUtc);
            Assert.Equal(42, stBack.History[0].PlaceId);
        }

        [Fact]
        public void Unknown_keys_and_bad_values_fall_back_to_defaults()
        {
            var s = Json.Deserialize<Settings>("{\"Profile\": \"NotAProfile\", \"FramerateCap\": \"oops\", \"Unknown\": 1, \"GraySky\": true}");
            Assert.Equal(PerformanceProfile.Custom, s.Profile); // enum default
            Assert.Equal(0, s.FramerateCap);
            Assert.True(s.GraySky);
        }
    }

    public class DeploymentTests
    {
        const string Manifest = "v0\nRobloxApp.zip\n6bc4614df3393cbdaf13b466c5964cca\n136726729\n175510571\n" +
                                "content-terrain.zip\nfe7b3ecee91e83f9fade72e8b0f4d47f\n4807\n47610\n" +
                                "RobloxPlayerInstaller.exe\n09796d7a9b72e905b895443416da4f1c\n13525968\n13525968\n";

        [Fact]
        public void Parses_package_manifest()
        {
            var pkgs = Deployment.ParseManifest(Manifest);
            Assert.Equal(3, pkgs.Count);
            Assert.Equal("RobloxApp.zip", pkgs[0].Name);
            Assert.Equal("6bc4614df3393cbdaf13b466c5964cca", pkgs[0].Md5);
            Assert.Equal(136726729, pkgs[0].PackedSize);
            Assert.Equal(175510571, pkgs[0].Size);
        }

        [Fact]
        public void Rejects_unknown_manifest_versions()
        {
            Assert.Throws<FormatException>(() => Deployment.ParseManifest("v9\nfoo"));
        }

        [Theory]
        [InlineData("RobloxApp.zip", "")]
        [InlineData("content-textures2.zip", @"content\textures\")]
        [InlineData("content-textures3.zip", @"PlatformContent\pc\textures\")]
        [InlineData("extracontent-luapackages.zip", @"ExtraContent\LuaPackages\")]
        [InlineData("content-newthing.zip", @"content\newthing\")]
        [InlineData("extracontent-shiny.zip", @"ExtraContent\shiny\")]
        [InlineData("content-platform-foo.zip", @"PlatformContent\pc\foo\")]
        public void Package_map_known_and_guessed(string package, string folder)
        {
            Assert.Equal(folder, PackageMap.GetFolder(package));
        }
    }

    public class PathTests
    {
        [Fact]
        public void SafeCombine_blocks_zip_slip()
        {
            Assert.Throws<InvalidDataException>(() => Paths.SafeCombine(@"C:\root\dir", @"..\..\Windows\evil.dll"));
            Assert.Equal(@"C:\root\dir\a\b.txt", Paths.SafeCombine(@"C:\root\dir", @"a\b.txt"));
        }

        [Fact]
        public void Relative_paths()
        {
            Assert.Equal(@"content\sounds\ouch.ogg", Paths.GetRelativePath(@"C:\mods\", @"C:\mods\content\sounds\ouch.ogg"));
        }
    }

    public class CommandLineTests
    {
        [Fact]
        public void Protocol_link_from_browser()
        {
            var c = CommandLine.Parse(new[] { "-player", "roblox-player:1+launchmode:play+gameinfo:SECRET+placelauncherurl:x" });
            Assert.Equal(CommandAction.Play, c.Action);
            Assert.StartsWith("roblox-player:", c.Argument);
            Assert.DoesNotContain("SECRET", c.ToString()); // tickets never reach the log
        }

        [Fact]
        public void Bare_deeplink_and_plain_player()
        {
            Assert.Equal("roblox://experiences/start?placeId=1", CommandLine.Parse(new[] { "roblox://experiences/start?placeId=1" }).Argument);
            var app = CommandLine.Parse(new[] { "-player" });
            Assert.Equal(CommandAction.Play, app.Action);
            Assert.Null(app.Argument);
        }

        [Fact]
        public void Other_modes()
        {
            Assert.Equal(CommandAction.Update, CommandLine.Parse(new[] { "-update" }).Action);
            Assert.Equal(CommandAction.Repair, CommandLine.Parse(new[] { "-repair" }).Action);
            var un = CommandLine.Parse(new[] { "-uninstall", "-quiet" });
            Assert.Equal(CommandAction.Uninstall, un.Action);
            Assert.True(un.Quiet);
            var st = CommandLine.Parse(new[] { "-settings", "-page", "Performance" });
            Assert.Equal(CommandAction.Settings, st.Action);
            Assert.Equal("performance", st.Page);
        }

        [Fact]
        public void Protocol_command_parsing()
        {
            Assert.Equal(@"C:\Roblox\RobloxPlayerBeta.exe", ProtocolHandler.ExeFromCommand("\"C:\\Roblox\\RobloxPlayerBeta.exe\" %1"));
            Assert.Equal(@"C:\x.exe", ProtocolHandler.ExeFromCommand(@"C:\x.exe %1"));
            Assert.Null(ProtocolHandler.ExeFromCommand(""));
        }
    }
}
