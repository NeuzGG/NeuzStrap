using System;
using System.IO;
using System.Linq;
using NeuzStrap.Boost;
using NeuzStrap.Core;
using NeuzStrap.Integrations;
using Xunit;

namespace NeuzStrap.Tests
{
    public class CrashRejoinTests
    {
        static readonly string[] JoinLog =
        {
            "2026-09-23T10:00:00.0Z,1,1,6 [FLog::Output] ! Joining game '208246d1-ab9b-4c82-96f1-621ce875f2b6' place 88147914030971 at 10.0.0.1",
            "2026-09-23T10:00:00.1Z,1,1,7 [FLog::Network] serverId: 128.116.1.2|61356",
        };

        [Fact]
        public void A_game_you_left_normally_is_not_a_crash()
        {
            var w = new ActivityWatcher();
            foreach (var line in JoinLog) w.HandleLine(line);
            Assert.NotNull(w.Current);

            w.HandleLine("2026-09-23T10:20:00.0Z,1,1,6 [FLog::SingleSurfaceApp] leaveUGCGameInternal");
            Assert.Null(w.Current); // nothing to offer a rejoin for
        }

        [Fact]
        public void Still_in_a_game_when_roblox_vanishes_means_a_crash()
        {
            var w = new ActivityWatcher();
            foreach (var line in JoinLog) w.HandleLine(line);

            // Roblox dies here: no leave line ever arrives, so the session stays put for the rejoin prompt
            Assert.NotNull(w.Current);
            Assert.Equal(88147914030971, w.Current.PlaceId);
            Assert.Equal("roblox://experiences/start?placeId=88147914030971&gameInstanceId=208246d1-ab9b-4c82-96f1-621ce875f2b6",
                         w.Current.DeepLink);
        }

        [Fact]
        public void Crash_note_expires_after_a_few_hours()
        {
            var fresh = new CrashInfo { PlaceId = 5, WhenUtc = DateTime.UtcNow.AddMinutes(-10) };
            var stale = new CrashInfo { PlaceId = 5, WhenUtc = DateTime.UtcNow.AddHours(-9) };
            Assert.True(fresh.IsRecent);
            Assert.False(stale.IsRecent);
            Assert.False(new CrashInfo().IsRecent);
        }

        [Fact]
        public void Rejoin_link_falls_back_to_the_game_when_the_server_is_unknown()
        {
            var noJob = new CrashInfo { PlaceId = 42 };
            Assert.Equal("roblox://experiences/start?placeId=42", noJob.DeepLink);
        }
    }

    public class AutoCleanTests : IDisposable
    {
        readonly string _base = TempDir.Create();

        public AutoCleanTests() => Paths.InitForTests(_base);

        [Fact]
        public void Only_due_when_turned_on_and_enough_days_passed()
        {
            var s = new Settings { AutoClean = true, AutoCleanDays = 30 };
            var state = new State { LastAutoCleanUtc = DateTime.UtcNow.AddDays(-31) };
            Assert.True(Cleaner.IsDue(s, state));

            state.LastAutoCleanUtc = DateTime.UtcNow.AddDays(-2);
            Assert.False(Cleaner.IsDue(s, state));

            s.AutoClean = false;
            state.LastAutoCleanUtc = DateTime.UtcNow.AddDays(-400);
            Assert.False(Cleaner.IsDue(s, state));

            // never run before -> due right away
            Assert.True(Cleaner.IsDue(new Settings { AutoClean = true }, new State()));
        }

        [Fact]
        public void Never_touches_the_official_roblox_install_automatically()
        {
            var official = Cleaner.GetItems().Single(i => i.Name == Cleaner.OfficialCopiesName);
            Assert.False(official.AutoCleanable);

            var assets = Cleaner.GetItems().Single(i => i.Name == Cleaner.AssetCacheName);
            Assert.True(assets.AutoCleanable);
            Assert.True(assets.NeedsRobloxClosed);
        }

        [Fact]
        public void Running_it_clears_leftovers_and_records_the_date()
        {
            Directory.CreateDirectory(Paths.Downloads);
            File.WriteAllBytes(Path.Combine(Paths.Downloads, "leftover.zip"), new byte[4096]);
            Directory.CreateDirectory(Paths.Logs);
            File.WriteAllText(Path.Combine(Paths.Logs, "old.log"), new string('x', 2048));

            var before = State.Current.LastAutoCleanUtc;
            long freed = Cleaner.RunAuto(new Settings { AutoClean = true, AutoCleanAssetCache = false });

            Assert.True(freed >= 4096, $"freed {freed}");
            Assert.False(File.Exists(Path.Combine(Paths.Downloads, "leftover.zip")));
            Assert.True(State.Current.LastAutoCleanUtc > before);
        }

        public void Dispose() => TempDir.Delete(_base);
    }
}
