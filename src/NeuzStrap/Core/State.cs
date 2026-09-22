using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NeuzStrap.Core
{
    public class GameHistoryEntry
    {
        public long PlaceId { get; set; }
        public long UniverseId { get; set; }
        public string Name { get; set; } = "";
        public string Creator { get; set; } = "";
        public string JobId { get; set; } = "";
        public string ServerLocation { get; set; } = "";
        public bool IsPrivateServer { get; set; }
        public DateTime LastPlayedUtc { get; set; }
        public int TimesPlayed { get; set; }
    }

    /// <summary>
    /// Things NeuzStrap remembers that aren't user preferences (installed Roblox version,
    /// recently played games, what to undo after a crash). Saved as State.json.
    /// </summary>
    public class State
    {
        public string RobloxVersionGuid { get; set; } = "";
        public string RobloxVersionName { get; set; } = "";
        public DateTime RobloxInstalledUtc { get; set; }

        /// <summary>Protocol handler commands that were there before NeuzStrap took over (restored on uninstall).</summary>
        public Dictionary<string, string> PreviousProtocolCommands { get; set; } = new Dictionary<string, string>();

        public List<GameHistoryEntry> History { get; set; } = new List<GameHistoryEntry>();

        /// <summary>Official Roblox shortcuts NeuzStrap repointed (path -> original target), restored on uninstall.</summary>
        public Dictionary<string, Setup.ShortcutBackup> TakenOverShortcuts { get; set; } = new Dictionary<string, Setup.ShortcutBackup>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Power plan to restore if NeuzStrap was closed before Roblox exited.</summary>
        public string PendingPowerSchemeRestore { get; set; } = "";
        public string PendingPowerOverlayRestore { get; set; } = "";

        public DateTime LastAppUpdateCheckUtc { get; set; }
        public string SkippedAppVersion { get; set; } = "";

        public long TotalLaunches { get; set; }

        // ---------------------------------------------------------------- persistence

        static readonly object SaveLock = new object();

        [JsonIgnore]
        public static State Current { get; private set; } = new State();

        public static void Load()
        {
            try
            {
                if (File.Exists(Paths.StateFile))
                {
                    Current = Json.Deserialize<State>(File.ReadAllText(Paths.StateFile, Encoding.UTF8)) ?? new State();
                    Current.History ??= new List<GameHistoryEntry>();
                    Current.PreviousProtocolCommands ??= new Dictionary<string, string>();
                    Current.TakenOverShortcuts = new Dictionary<string, Setup.ShortcutBackup>(
                        Current.TakenOverShortcuts ?? new Dictionary<string, Setup.ShortcutBackup>(), StringComparer.OrdinalIgnoreCase);
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("State", ex, "State.json was unreadable, starting fresh");
            }
            Current = new State();
        }

        /// <summary>Re-reads the file so a second NeuzStrap window doesn't clobber newer history.</summary>
        public static void Reload() => Load();

        public static void Save()
        {
            lock (SaveLock)
            {
                try { AtomicFile.WriteAllText(Paths.StateFile, Json.Serialize(Current)); }
                catch (Exception ex) { Logger.Error("State", ex, "Couldn't save state"); }
            }
        }

        public void RecordGame(GameHistoryEntry entry)
        {
            lock (SaveLock)
            {
                var existing = History.FirstOrDefault(h => h.PlaceId == entry.PlaceId);
                if (existing != null)
                {
                    History.Remove(existing);
                    entry.TimesPlayed = existing.TimesPlayed + 1;
                    if (string.IsNullOrEmpty(entry.Name)) entry.Name = existing.Name;
                    if (string.IsNullOrEmpty(entry.Creator)) entry.Creator = existing.Creator;
                }
                else
                {
                    entry.TimesPlayed = 1;
                }
                History.Insert(0, entry);
                if (History.Count > 30) History.RemoveRange(30, History.Count - 30);
            }
        }
    }
}
