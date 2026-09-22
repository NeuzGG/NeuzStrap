using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeuzStrap.Core;
using NeuzStrap.Roblox;

namespace NeuzStrap.Setup
{
    public class ShortcutBackup
    {
        public string Target { get; set; } = "";
        public string Arguments { get; set; } = "";
    }

    /// <summary>
    /// The official launcher's "Roblox Player" shortcuts start Roblox directly, skipping NeuzStrap
    /// (and every tweak). This points them at NeuzStrap instead and remembers the originals, which
    /// the uninstaller puts back.
    /// </summary>
    public static class OfficialShortcuts
    {
        static readonly string[] PlayerExeNames = { "RobloxPlayerBeta.exe", "RobloxPlayerLauncher.exe" };

        static IEnumerable<string> DefaultFolders()
        {
            yield return Paths.Desktop;
            yield return Paths.StartMenuPrograms;
            yield return Path.Combine(Paths.StartMenuPrograms, "Roblox");
        }

        public static List<string> Find() => Find(DefaultFolders());

        internal static List<string> Find(IEnumerable<string> folders)
        {
            var found = new List<string>();
            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder)) continue;
                foreach (var lnk in Directory.GetFiles(folder, "*.lnk"))
                {
                    var info = Shortcut.Read(lnk);
                    if (info == null) continue;
                    string target = info.Value.Target;
                    bool isPlayer = PlayerExeNames.Any(n => string.Equals(Path.GetFileName(target), n, StringComparison.OrdinalIgnoreCase));
                    if (isPlayer && target.IndexOf(Paths.Base, StringComparison.OrdinalIgnoreCase) < 0)
                        found.Add(lnk);
                }
            }
            return found;
        }

        /// <summary>Points every official Roblox Player shortcut at NeuzStrap. Returns how many were changed.</summary>
        public static int TakeOver() => TakeOver(DefaultFolders(), AppInstaller.IsInstalled && !Paths.IsPortable ? Paths.InstalledExe : Paths.CurrentExe);

        internal static int TakeOver(IEnumerable<string> folders, string neuzStrapExe)
        {
            int changed = 0;
            foreach (var lnk in Find(folders))
            {
                var info = Shortcut.Read(lnk);
                if (info == null) continue;
                try
                {
                    if (!State.Current.TakenOverShortcuts.ContainsKey(lnk))
                        State.Current.TakenOverShortcuts[lnk] = new ShortcutBackup { Target = info.Value.Target, Arguments = info.Value.Arguments };
                    Shortcut.Create(lnk, neuzStrapExe, "-player", "Play Roblox with NeuzStrap");
                    changed++;
                    Logger.Info("Shortcuts", $"\"{Path.GetFileName(lnk)}\" now opens NeuzStrap");
                }
                catch (Exception ex)
                {
                    Logger.Warn("Shortcuts", $"Couldn't update {lnk}: {ex.Message}");
                }
            }
            if (changed > 0) State.Save();
            return changed;
        }

        /// <summary>Puts the original shortcuts back (or removes them if Roblox isn't installed anymore).</summary>
        public static void Restore()
        {
            var state = State.Current;
            if (state.TakenOverShortcuts.Count == 0) return;
            string official = ProtocolHandler.FindOfficialPlayer();

            foreach (var kv in state.TakenOverShortcuts.ToList())
            {
                string lnk = kv.Key;
                if (!File.Exists(lnk)) continue;
                try
                {
                    var current = Shortcut.Read(lnk);
                    // leave it alone if something else already changed it again
                    if (current != null && current.Value.Target.IndexOf(Paths.Base, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    string target = File.Exists(kv.Value.Target) ? kv.Value.Target : official;
                    if (target != null)
                    {
                        Shortcut.Create(lnk, target, target == kv.Value.Target ? kv.Value.Arguments : "", "Roblox");
                        Logger.Info("Shortcuts", $"Restored \"{Path.GetFileName(lnk)}\"");
                    }
                    else
                    {
                        Shortcut.Delete(lnk);
                        Logger.Info("Shortcuts", $"Removed \"{Path.GetFileName(lnk)}\" (Roblox isn't installed anymore)");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn("Shortcuts", $"Couldn't restore {lnk}: {ex.Message}");
                }
            }
            state.TakenOverShortcuts.Clear();
            State.Save();
        }

        /// <summary>
        /// If the user opted in before, quietly redo it when the official launcher recreated its shortcuts.
        /// </summary>
        public static void KeepTakenOver()
        {
            if (State.Current.TakenOverShortcuts.Count == 0 || Paths.IsPortable) return;
            try { TakeOver(); } catch (Exception ex) { Logger.Warn("Shortcuts", ex.Message); }
        }
    }
}
