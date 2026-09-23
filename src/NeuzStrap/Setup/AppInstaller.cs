using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using NeuzStrap.Boost;
using NeuzStrap.Core;
using NeuzStrap.Roblox;

namespace NeuzStrap.Setup
{
    /// <summary>Installs NeuzStrap itself (per-user, no admin): copies the exe, registers links, adds shortcuts.</summary>
    public static class AppInstaller
    {
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\NeuzStrap";

        public static string DesktopShortcut => Path.Combine(Paths.Desktop, "Roblox (NeuzStrap).lnk");
        public static string StartMenuPlayShortcut => Path.Combine(Paths.StartMenuPrograms, "Roblox (NeuzStrap).lnk");
        public static string StartMenuAppShortcut => Path.Combine(Paths.StartMenuPrograms, "NeuzStrap.lnk");

        public static bool IsInstalled
        {
            get
            {
                if (Paths.IsPortable) return true;
                using (var k = Registry.CurrentUser.OpenSubKey(UninstallKey))
                    return k != null && File.Exists(Paths.InstalledExe);
            }
        }

        public static Version InstalledVersion
        {
            get
            {
                try { return File.Exists(Paths.InstalledExe) ? new Version(FileVersionInfo.GetVersionInfo(Paths.InstalledExe).FileVersion) : null; }
                catch { return null; }
            }
        }

        public static void Install(bool desktopShortcut)
        {
            Directory.CreateDirectory(Paths.Base);
            if (!Paths.IsRunningInstalledCopy)
                CopySelfTo(Paths.InstalledExe);

            string exe = Paths.InstalledExe;
            ProtocolHandler.Register(exe);
            WriteUninstallEntry(exe);

            Shortcut.Create(StartMenuAppShortcut, exe, "", "NeuzStrap settings and tools");
            Shortcut.Create(StartMenuPlayShortcut, exe, "-player", "Play Roblox with NeuzStrap");
            if (desktopShortcut) Shortcut.Create(DesktopShortcut, exe, "-player", "Play Roblox with NeuzStrap");

            Logger.Info("Setup", $"Installed NeuzStrap {AppInfo.VersionString} to {Paths.Base}");
        }

        /// <summary>Overwrites the installed exe even if it's running (Windows lets you rename a running exe).</summary>
        public static void CopySelfTo(string dest)
        {
            if (File.Exists(dest))
            {
                string old = dest + ".old";
                try { if (File.Exists(old)) File.Delete(old); } catch { }
                try { File.Move(dest, old); }
                catch { File.Delete(dest); }
            }
            File.Copy(Paths.CurrentExe, dest, true);
        }

        public static void CleanupOldExe()
        {
            try
            {
                string old = Paths.InstalledExe + ".old";
                if (File.Exists(old)) File.Delete(old);
            }
            catch { /* still running, next time */ }
        }

        static void WriteUninstallEntry(string exe)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                k.SetValue("DisplayName", "NeuzStrap");
                k.SetValue("DisplayIcon", $"{exe},0");
                k.SetValue("DisplayVersion", AppInfo.VersionString);
                k.SetValue("Publisher", AppInfo.Author);
                k.SetValue("InstallLocation", Paths.Base);
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                k.SetValue("UninstallString", $"\"{exe}\" -uninstall");
                k.SetValue("QuietUninstallString", $"\"{exe}\" -uninstall -quiet");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                if (AppInfo.HasRepo)
                {
                    k.SetValue("URLInfoAbout", AppInfo.RepoUrl);
                    k.SetValue("URLUpdateInfo", AppInfo.RepoUrl + "/releases");
                }
                try { k.SetValue("EstimatedSize", (int)(new FileInfo(exe).Length / 1024), RegistryValueKind.DWord); } catch { }
            }
        }

        /// <summary>Refreshes version info in Add/Remove Programs after a self-update.</summary>
        public static void RefreshUninstallEntry()
        {
            if (Paths.IsPortable || !IsInstalled) return;
            try { WriteUninstallEntry(Paths.InstalledExe); } catch { }
        }

        /// <summary>After a silent auto-update, make Windows' apps list show the new version.</summary>
        public static void EnsureUninstallEntryCurrent()
        {
            if (Paths.IsPortable || !IsInstalled || !Paths.IsRunningInstalledCopy) return;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(UninstallKey))
                    if ((k?.GetValue("DisplayVersion") as string) == AppInfo.VersionString) return;
                WriteUninstallEntry(Paths.InstalledExe);
            }
            catch { }
        }

        public static bool HasDesktopShortcut => File.Exists(DesktopShortcut);

        public static void SetDesktopShortcut(bool enabled)
        {
            if (enabled) Shortcut.Create(DesktopShortcut, Paths.InstalledExe, "-player", "Play Roblox with NeuzStrap");
            else Shortcut.Delete(DesktopShortcut);
        }

        /// <summary>Removes everything NeuzStrap added and hands Roblox links back to the official launcher.</summary>
        public static void Uninstall()
        {
            Logger.Info("Setup", "Uninstalling NeuzStrap");
            PowerPlan.Restore();
            ProtocolHandler.Unregister();
            GameBooster.RemoveAllExeTweaks();
            OfficialShortcuts.Restore();

            Shortcut.Delete(DesktopShortcut);
            Shortcut.Delete(StartMenuPlayShortcut);
            Shortcut.Delete(StartMenuAppShortcut);
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }

            if (Paths.IsPortable) return;

            // Delete everything we can now; the running exe (and folder) goes right after we exit.
            foreach (var entry in new DirectoryInfo(Paths.Base).GetFileSystemInfos())
            {
                if (string.Equals(entry.FullName, Paths.CurrentExe, StringComparison.OrdinalIgnoreCase)) continue;
                if (entry.FullName.StartsWith(Paths.Logs, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    if (entry is DirectoryInfo d) d.Delete(true);
                    else entry.Delete();
                }
                catch { }
            }

            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe", $"/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"{Paths.Base}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                });
            }
            catch { }
        }
    }
}
