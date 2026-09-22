using System;
using System.IO;
using System.Windows.Forms;

namespace NeuzStrap.Core
{
    /// <summary>
    /// Every folder NeuzStrap touches. Data lives in %LocalAppData%\NeuzStrap unless a
    /// "portable.txt" file sits next to the exe, in which case everything stays in that folder.
    /// </summary>
    public static class Paths
    {
        public static string Base { get; private set; }
        public static bool IsPortable { get; private set; }

        public static string CurrentExe => Application.ExecutablePath;
        public static string CurrentExeDir => Path.GetDirectoryName(CurrentExe);

        public static string InstalledExe => Path.Combine(Base, "NeuzStrap.exe");
        public static string Versions => Path.Combine(Base, "Versions");
        public static string Downloads => Path.Combine(Base, "Downloads");
        public static string Modifications => Path.Combine(Base, "Modifications");
        public static string Logs => Path.Combine(Base, "Logs");
        public static string SettingsFile => Path.Combine(Base, "Settings.json");
        public static string StateFile => Path.Combine(Base, "State.json");
        public static string CustomFontFile => Path.Combine(Base, "CustomFont.ttf");
        public static string CustomCursorFile => Path.Combine(Base, "CustomCursor.png");

        // Roblox's own per-user folder (settings, logs, caches) - shared with the official launcher.
        public static string RobloxData => Path.Combine(LocalAppData, "Roblox");
        public static string RobloxLogs => Path.Combine(RobloxData, "logs");
        public static string GlobalBasicSettings => Path.Combine(RobloxData, "GlobalBasicSettings_13.xml");
        public static string RobloxOfficialVersions => Path.Combine(RobloxData, "Versions");

        public static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static string Desktop => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        public static string StartMenuPrograms => Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        public static string Temp => Path.GetTempPath();

        public static void Init()
        {
            string exeDir = CurrentExeDir;
            IsPortable = File.Exists(Path.Combine(exeDir, "portable.txt"));
            Base = IsPortable ? exeDir : Path.Combine(LocalAppData, AppInfo.Name);
            Directory.CreateDirectory(Base);
        }

        /// <summary>Points every data folder somewhere else (unit tests only).</summary>
        internal static void InitForTests(string baseDir)
        {
            IsPortable = true;
            Base = baseDir;
            Directory.CreateDirectory(Base);
        }

        /// <summary>True when this exe is the one installed in the data folder.</summary>
        public static bool IsRunningInstalledCopy =>
            string.Equals(Path.GetFullPath(CurrentExe), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase);

        /// <summary>net48 has no Path.GetRelativePath, so here is a small one.</summary>
        public static string GetRelativePath(string root, string fullPath)
        {
            root = Path.GetFullPath(root).TrimEnd('\\') + "\\";
            fullPath = Path.GetFullPath(fullPath);
            return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? fullPath.Substring(root.Length) : fullPath;
        }

        /// <summary>Combines and makes sure the result can't escape <paramref name="root"/> (zip-slip guard).</summary>
        public static string SafeCombine(string root, string relative)
        {
            string rootFull = Path.GetFullPath(root).TrimEnd('\\') + "\\";
            string full = Path.GetFullPath(Path.Combine(rootFull, relative));
            if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Blocked a path that tried to escape its folder: " + relative);
            return full;
        }
    }
}
