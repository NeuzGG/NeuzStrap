using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using NeuzStrap.Core;

namespace NeuzStrap.Roblox
{
    /// <summary>
    /// Makes the website's "Play" button (roblox-player: and roblox: links) open NeuzStrap.
    /// Everything is per-user (HKCU), no admin needed.
    /// </summary>
    public static class ProtocolHandler
    {
        static readonly string[] Schemes = { "roblox-player", "roblox" };

        static string CommandFor(string exe) => $"\"{exe}\" -player \"%1\"";

        public static bool IsRegistered(string exe)
        {
            foreach (var scheme in Schemes)
            {
                using (var k = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{scheme}\shell\open\command"))
                {
                    if (!string.Equals(k?.GetValue("") as string, CommandFor(exe), StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            return true;
        }

        public static void Register(string exe)
        {
            bool stateChanged = false;
            foreach (var scheme in Schemes)
            {
                using (var root = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{scheme}"))
                using (var cmd = root.CreateSubKey(@"shell\open\command"))
                using (var icon = root.CreateSubKey("DefaultIcon"))
                {
                    string existing = cmd.GetValue("") as string;
                    if (!string.IsNullOrEmpty(existing) && existing.IndexOf("NeuzStrap", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        State.Current.PreviousProtocolCommands[scheme] = existing;
                        stateChanged = true;
                    }

                    root.SetValue("", "URL: Roblox Protocol");
                    root.SetValue("URL Protocol", "");
                    icon.SetValue("", $"\"{exe}\",0");
                    cmd.SetValue("", CommandFor(exe));
                }
            }
            if (stateChanged) State.Save();
            Logger.Info("Protocol", "NeuzStrap is now the handler for roblox-player: and roblox: links");
        }

        /// <summary>Hands the protocol back to the official Roblox launcher if it's installed, otherwise removes it.</summary>
        public static void Unregister()
        {
            string official = FindOfficialPlayer();
            foreach (var scheme in Schemes)
            {
                try
                {
                    string restore = null;
                    if (State.Current.PreviousProtocolCommands.TryGetValue(scheme, out var prev) && File.Exists(ExeFromCommand(prev)))
                        restore = prev;
                    else if (official != null)
                        restore = $"\"{official}\" %1";

                    if (restore != null)
                    {
                        using (var root = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{scheme}"))
                        using (var cmd = root.CreateSubKey(@"shell\open\command"))
                        using (var icon = root.CreateSubKey("DefaultIcon"))
                        {
                            cmd.SetValue("", restore);
                            icon.SetValue("", ExeFromCommand(restore) ?? "");
                        }
                        Logger.Info("Protocol", $"Restored {scheme}: to the official Roblox launcher");
                    }
                    else
                    {
                        Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{scheme}", false);
                        Logger.Info("Protocol", $"Removed {scheme}: handler");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Protocol", ex, $"Couldn't unregister {scheme}:");
                }
            }
        }

        public static string ExeFromCommand(string command)
        {
            if (string.IsNullOrEmpty(command)) return null;
            command = command.Trim();
            if (command.StartsWith("\"", StringComparison.Ordinal))
            {
                int end = command.IndexOf('"', 1);
                return end > 1 ? command.Substring(1, end - 1) : null;
            }
            int space = command.IndexOf(' ');
            return space > 0 ? command.Substring(0, space) : command;
        }

        /// <summary>The newest official Roblox player install, if there is one.</summary>
        public static string FindOfficialPlayer()
        {
            try
            {
                if (!Directory.Exists(Paths.RobloxOfficialVersions)) return null;
                return new DirectoryInfo(Paths.RobloxOfficialVersions).GetDirectories("version-*")
                    .Select(d => Path.Combine(d.FullName, "RobloxPlayerBeta.exe"))
                    .Where(File.Exists)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch { return null; }
        }
    }
}
