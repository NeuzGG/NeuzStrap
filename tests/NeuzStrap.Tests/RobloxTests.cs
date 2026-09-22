using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using NeuzStrap.Boost;
using NeuzStrap.Core;
using NeuzStrap.Integrations;
using NeuzStrap.Roblox;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)] // Paths/Settings are static

namespace NeuzStrap.Tests
{
    public class FastFlagTests
    {
        [Theory]
        [InlineData(PerformanceProfile.RobloxDefault)]
        [InlineData(PerformanceProfile.Balanced)]
        [InlineData(PerformanceProfile.Potato)]
        [InlineData(PerformanceProfile.UltraPotato)]
        public void Profiles_only_use_flags_roblox_accepts(PerformanceProfile profile)
        {
            var s = new Settings();
            Profiles.Apply(profile, s, null);
            foreach (var name in FastFlags.Generate(s).Keys)
                Assert.NotEqual(FastFlags.FlagStatus.Ignored, FastFlags.GetStatus(name));
        }

        [Fact]
        public void Every_option_maxed_out_still_only_uses_allowed_flags()
        {
            var s = new Settings
            {
                RenderingApi = RenderingApi.OpenGL, TextureQuality = TextureQuality.Lowest, AntiAliasing = 1,
                RenderQualityOverride = 1, MeshDetail = MeshDetail.Lowest, RemoveGrass = true, FreezeLighting = true,
                GraySky = true, ExclusiveFullscreen = true, DisableDpiScaling = true, RenderResolutionKilopixels = 230,
            };
            var flags = FastFlags.Generate(s);
            Assert.All(flags.Keys, k => Assert.NotEqual(FastFlags.FlagStatus.Ignored, FastFlags.GetStatus(k)));
            Assert.Equal("0", flags["DFIntTextureQualityOverride"]);
            Assert.Equal("True", flags["DFFlagTextureQualityOverrideEnabled"]);
            Assert.Equal("False", flags["FFlagHandleAltEnterFullscreenManually"]);
            Assert.Equal("0", flags["FIntFRMMaxGrassDistance"]);
        }

        [Fact]
        public void Fresh_settings_match_the_balanced_profile()
        {
            var fresh = new Settings();
            var balanced = new Settings();
            Profiles.Apply(PerformanceProfile.Balanced, balanced, null);
            Assert.Equal(Json.Serialize(balanced), Json.Serialize(fresh));
        }

        [Fact]
        public void Discord_uses_the_built_in_app_unless_overridden()
        {
            var s = new Settings();
            Assert.True(s.DiscordRichPresence);
            Assert.Equal(AppInfo.DiscordApplicationId, s.EffectiveDiscordApplicationId);
            s.DiscordApplicationId = "  42  ";
            Assert.Equal("42", s.EffectiveDiscordApplicationId);
            Assert.DoesNotContain("EffectiveDiscordApplicationId", Json.Serialize(s));
        }

        [Fact]
        public void Default_settings_generate_nothing()
        {
            var s = new Settings { RenderingApi = RenderingApi.Automatic };
            Profiles.Apply(PerformanceProfile.RobloxDefault, s, null);
            Assert.Empty(FastFlags.Generate(s));
        }

        [Fact]
        public void Custom_flags_override_generated_ones()
        {
            var s = new Settings { AntiAliasing = 1 };
            s.CustomFastFlags["FIntDebugForceMSAASamples"] = "4";
            s.CustomFastFlags["  "] = "ignored";
            var final = FastFlags.BuildFinal(s);
            Assert.Equal("4", final["FIntDebugForceMSAASamples"]);
            Assert.False(final.ContainsKey("  "));
        }

        [Fact]
        public void Import_normalizes_types()
        {
            var flags = FastFlags.ParseImport("{\"FFlagA\": true, \"FIntB\": 30, \"DFIntC\": \"5\", \"FStringD\": null}", out var err);
            Assert.Null(err);
            Assert.Equal("True", flags["FFlagA"]);
            Assert.Equal("30", flags["FIntB"]);
            Assert.Equal("5", flags["DFIntC"]);
            Assert.Equal("", flags["FStringD"]);
            Assert.Null(FastFlags.ParseImport("[1,2]", out err));
            Assert.NotNull(err);
        }

        [Fact]
        public void Writes_client_app_settings_file()
        {
            var dir = TempDir.Create();
            var s = new Settings { GraySky = true };
            FastFlags.Write(dir, s);
            var json = (Dictionary<string, object>)Json.Parse(File.ReadAllText(Path.Combine(dir, "ClientSettings", "ClientAppSettings.json")));
            Assert.Equal("True", json["FFlagDebugSkyGray"]);
            TempDir.Delete(dir);
        }

        [Fact]
        public void Hybrid_laptops_get_the_strong_gpu()
        {
            var sys = new SystemInfo
            {
                Gpus = new List<GpuInfo>
                {
                    new GpuInfo { Name = "Intel(R) UHD Graphics 620", IsIntegrated = true },
                    new GpuInfo { Name = "NVIDIA GeForce MX250", IsIntegrated = false },
                },
            };
            var s = new Settings();
            Profiles.Apply(PerformanceProfile.Potato, s, sys);
            Assert.Equal(GpuPreference.HighPerformance, s.GpuPreference);
            Profiles.Apply(PerformanceProfile.RobloxDefault, s, sys);
            Assert.Equal(GpuPreference.WindowsDefault, s.GpuPreference);
        }

        [Theory]
        [InlineData("Intel(R) UHD Graphics 620", true)]
        [InlineData("Intel(R) HD Graphics 4000", true)]
        [InlineData("Intel(R) Iris(R) Xe Graphics", true)]
        [InlineData("Intel(R) Arc(TM) A770 Graphics", false)]
        [InlineData("AMD Radeon(TM) Vega 8 Graphics", true)]
        [InlineData("AMD Radeon(TM) Graphics", true)]
        [InlineData("AMD Radeon 780M", true)]
        [InlineData("AMD Radeon RX 580", false)]
        [InlineData("AMD Radeon RX 6600M", false)]
        [InlineData("NVIDIA GeForce GTX 1650", false)]
        [InlineData("Microsoft Basic Display Adapter", false)]
        public void Gpu_classification(string name, bool integrated)
        {
            Assert.Equal(integrated, SystemInfo.IsIntegratedGpu(name));
        }
    }

    public class ModManagerTests : IDisposable
    {
        readonly string _base = TempDir.Create();
        readonly string _version;

        public ModManagerTests()
        {
            Paths.InitForTests(_base);
            _version = Path.Combine(_base, "Versions", "version-test");
            Write(Path.Combine(_version, @"content\sounds\ouch.ogg"), "original sound");
            Write(Path.Combine(_version, @"content\fonts\families\Arimo.json"),
                "{\"name\":\"Arimo\",\"faces\":[{\"name\":\"Regular\",\"weight\":400,\"style\":\"normal\",\"assetId\":\"rbxasset://fonts/Arimo-Regular.ttf\"}]}");
            Write(Path.Combine(_version, @"content\fonts\families\NotoSansCJKFallback.json"),
                "{\"name\":\"Noto\",\"faces\":[{\"assetId\":\"rbxasset://fonts/NotoSansCJK.ttf\"}]}");
        }

        static void Write(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }

        [Fact]
        public void Applies_backs_up_and_restores_mods()
        {
            var s = new Settings();
            Write(Path.Combine(Paths.Modifications, @"content\sounds\ouch.ogg"), "oof");
            Write(Path.Combine(Paths.Modifications, @"content\extra.txt"), "new file");
            Write(Path.Combine(Paths.Modifications, @"ClientSettings\ClientAppSettings.json"), "{}"); // must be ignored

            Assert.Equal(2, ModManager.Apply(_version, s));
            Assert.Equal("oof", File.ReadAllText(Path.Combine(_version, @"content\sounds\ouch.ogg")));
            Assert.True(File.Exists(Path.Combine(_version, @"content\extra.txt")));
            Assert.False(File.Exists(Path.Combine(_version, @"ClientSettings\ClientAppSettings.json")));

            // applying again must not overwrite the backup with the modded file
            ModManager.Apply(_version, s);

            Directory.Delete(Paths.Modifications, true);
            ModManager.Apply(_version, s);
            Assert.Equal("original sound", File.ReadAllText(Path.Combine(_version, @"content\sounds\ouch.ogg")));
            Assert.False(File.Exists(Path.Combine(_version, @"content\extra.txt")));
        }

        [Fact]
        public void Custom_font_rewrites_families_except_cjk_and_undoes_cleanly()
        {
            string arimo = Path.Combine(_version, @"content\fonts\families\Arimo.json");
            string noto = Path.Combine(_version, @"content\fonts\families\NotoSansCJKFallback.json");
            string originalArimo = File.ReadAllText(arimo);
            string originalNoto = File.ReadAllText(noto);

            File.WriteAllBytes(Paths.CustomFontFile, new byte[] { 1, 2, 3 });
            var s = new Settings { UseCustomFont = true };
            ModManager.Apply(_version, s);

            Assert.Contains("rbxasset://fonts/NeuzCustomFont.ttf", File.ReadAllText(arimo));
            Assert.Equal(originalNoto, File.ReadAllText(noto));
            Assert.True(File.Exists(Path.Combine(_version, @"content\fonts\NeuzCustomFont.ttf")));

            s.UseCustomFont = false;
            ModManager.Apply(_version, s);
            Assert.Equal(originalArimo, File.ReadAllText(arimo));
            Assert.False(File.Exists(Path.Combine(_version, @"content\fonts\NeuzCustomFont.ttf")));
        }

        public void Dispose() => TempDir.Delete(_base);
    }

    public class GameSettingsFileTests
    {
        const string Sample =
            "<roblox xmlns:xmime=\"http://www.w3.org/2005/05/xmlmime\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:noNamespaceSchemaLocation=\"http://www.roblox.com/roblox.xsd\" version=\"4\">\n" +
            "\t<External>null</External>\n\t<External>nil</External>\n" +
            "\t<Item class=\"UserGameSettings\" referent=\"RBX1\">\n\t\t<Properties>\n" +
            "\t\t\t<int name=\"FramerateCap\">-1</int>\n" +
            "\t\t\t<int name=\"GraphicsQualityLevel\">0</int>\n" +
            "\t\t\t<float name=\"MouseSensitivity\">1</float>\n" +
            "\t\t\t<token name=\"SavedQualityLevel\">0</token>\n" +
            "\t\t</Properties>\n\t</Item>\n</roblox>";

        [Fact]
        public void Sets_quality_mode_and_fps_like_the_ingame_menu()
        {
            string dir = TempDir.Create();
            string path = Path.Combine(dir, "GlobalBasicSettings_13.xml");
            File.WriteAllText(path, Sample, new UTF8Encoding(false));

            GameSettingsFile.ApplyTo(path, new Settings { GraphicsQualityLock = 3, OptimizationMode = 0, FramerateCap = 60 });

            string text = File.ReadAllText(path);
            Assert.StartsWith("<roblox", text); // no XML declaration added
            var doc = new XmlDocument();
            doc.LoadXml(text);
            string Get(string n) => doc.SelectSingleNode($"//Properties/*[@name='{n}']")?.InnerText;
            Assert.Equal("3", Get("SavedQualityLevel"));
            Assert.Equal("3", Get("GraphicsQualityLevel"));
            Assert.Equal("60", Get("FramerateCap"));
            Assert.Equal("0", Get("GraphicsOptimizationMode"));   // added when missing
            Assert.Equal("1", Get("MouseSensitivity"));           // untouched
            Assert.Equal("token", doc.SelectSingleNode("//Properties/*[@name='GraphicsOptimizationMode']").Name);
            Assert.True(File.Exists(path + ".neuzstrap.bak"));
            TempDir.Delete(dir);
        }
    }

    public class ActivityWatcherTests
    {
        static readonly string[] Log =
        {
            "2026-09-21T14:59:01.271Z,29.271572,3078,6 [FLog::Output] ! Joining game '208246d1-ab9b-4c82-96f1-621ce875f2b6' place 88147914030971 at 10.0.0.1",
            "2026-09-21T14:59:01.271Z,29.271572,3078,6 [FLog::GameJoinLoadTime] Report game_join_loadtime: placeid:88147914030971, join_time:0.66, universeid:10389304129, referral_page:, sid:x, clienttime:1, userid:1, ",
            "2026-09-21T14:59:01.279Z,29.279573,3078,7 [FLog::Network] UDMUX Address = 128.116.1.2, Port = 61356 | RCC Server Address = 10.0.0.9, Port = 61356",
            "2026-09-21T14:59:02.044Z,30.044880,0430,7 [FLog::Network] serverId: 128.116.1.2|61356",
            "2026-09-21T15:08:12.528Z,580.528503,3078,7 [FLog::Network] Time to disconnect replication data: 0.124500",
            "2026-09-21T15:08:12.600Z,580.6,3078,6 [FLog::GameJoinUtil] GameJoinUtil::joinGamePostPrivateServer",
            "2026-09-21T15:08:12.657Z,580.657532,0430,6 [FLog::Output] ! Joining game 'a68f4f62-6190-4fb6-a051-a8e4826a574c' place 82493790832598 at 10.0.0.2",
            "2026-09-21T15:08:12.915Z,580.915710,0898,7 [FLog::Network] serverId: 128.116.3.4|60623",
            "2026-09-21T15:26:01.921Z,1649.921265,0974,6 [FLog::SingleSurfaceApp] leaveUGCGameInternal",
        };

        [Fact]
        public void Tracks_joins_private_servers_and_leaves()
        {
            var w = new ActivityWatcher();
            var joined = new List<GameSession>();
            var left = new List<GameSession>();
            w.GameJoined += joined.Add;
            w.GameLeft += left.Add;

            foreach (var line in Log) w.HandleLine(line);

            Assert.Equal(2, joined.Count);
            Assert.Equal(88147914030971, joined[0].PlaceId);
            Assert.Equal(10389304129, joined[0].UniverseId);
            Assert.Equal("128.116.1.2", joined[0].ServerIp); // UDMUX (public) address, not the internal one
            Assert.False(joined[0].IsPrivateServer);

            Assert.Equal(82493790832598, joined[1].PlaceId);
            Assert.True(joined[1].IsPrivateServer);
            Assert.Equal("10.0.0.2", joined[1].ServerIp);
            Assert.Equal("roblox://experiences/start?placeId=82493790832598&gameInstanceId=a68f4f62-6190-4fb6-a051-a8e4826a574c", joined[1].DeepLink);

            Assert.Equal(2, left.Count);
            Assert.Null(w.Current);
        }

        [Fact]
        public void Redacts_join_tickets()
        {
            string redacted = RobloxProcess.Redact("roblox-player:1+launchmode:play+gameinfo:TOPSECRET+launchtime:1");
            Assert.DoesNotContain("TOPSECRET", redacted);
            Assert.Contains("launchtime:1", redacted);
        }
    }

    static class TempDir
    {
        public static string Create()
        {
            string dir = Path.Combine(Path.GetTempPath(), "NeuzStrapTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static void Delete(string dir)
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
