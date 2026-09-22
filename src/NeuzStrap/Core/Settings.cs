using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NeuzStrap.Core
{
    public enum PerformanceProfile { Custom, RobloxDefault, Balanced, Potato, UltraPotato }
    public enum RenderingApi { Automatic, Direct3D11, Vulkan, OpenGL }
    public enum TextureQuality { Automatic, Lowest, Low, Medium, High }
    public enum MeshDetail { Automatic, Lowest }
    public enum PriorityLevel { Normal, AboveNormal, High }
    public enum GpuPreference { WindowsDefault, HighPerformance, PowerSaving }
    public enum CursorStyle { RobloxDefault, Classic, Sakura, BigArrow, Dot, Crosshair, Custom, Rgb }

    /// <summary>
    /// Everything the user can change. Saved as Settings.json.
    /// Numbers use 0 / -1 for "leave Roblox's own value alone" (documented per field).
    /// </summary>
    public class Settings
    {
        // ---------------------------------------------------------------- appearance
        public string Accent { get; set; } = "Sakura";
        public bool ShowLaunchTips { get; set; } = true;

        // ---------------------------------------------------------------- launcher
        public string Channel { get; set; } = "LIVE";
        public bool KeepDownloadCache { get; set; } = false;
        public bool CheckForAppUpdates { get; set; } = true;
        public bool FirstRunDone { get; set; } = false;

        // ---------------------------------------------------------------- performance profile
        // The defaults below are exactly the "Balanced" profile (see Profiles.Apply).
        public PerformanceProfile Profile { get; set; } = PerformanceProfile.Balanced;

        // in-game settings (written to GlobalBasicSettings_13.xml before launch)
        /// <summary>0 = don't touch, 1-10 = lock the in-game graphics slider to this level.</summary>
        public int GraphicsQualityLock { get; set; } = 0;
        /// <summary>-1 = don't touch, 0 = Performance, 1 = Balanced, 2 = Quality.</summary>
        public int OptimizationMode { get; set; } = 1;
        /// <summary>0 = don't touch, otherwise the in-game "Maximum Frame Rate" value (60, 120, 144 or 240).</summary>
        public int FramerateCap { get; set; } = 0;

        // rendering (written as allowlisted FastFlags)
        public RenderingApi RenderingApi { get; set; } = RenderingApi.Direct3D11;
        public TextureQuality TextureQuality { get; set; } = TextureQuality.Automatic;
        /// <summary>0 = automatic, otherwise MSAA sample count (1 = off).</summary>
        public int AntiAliasing { get; set; } = 0;
        /// <summary>0 = off, 1-21 = force Roblox's internal render quality level.</summary>
        public int RenderQualityOverride { get; set; } = 0;
        public MeshDetail MeshDetail { get; set; } = MeshDetail.Automatic;
        public bool RemoveGrass { get; set; } = false;
        public bool FreezeLighting { get; set; } = false;
        public bool GraySky { get; set; } = false;
        public bool ExclusiveFullscreen { get; set; } = false;
        public bool DisableDpiScaling { get; set; } = false;
        /// <summary>0 = off, otherwise render resolution budget in kilopixels (experimental flag).</summary>
        public int RenderResolutionKilopixels { get; set; } = 0;

        // ---------------------------------------------------------------- game booster (Windows-side tweaks)
        public PriorityLevel RobloxPriority { get; set; } = PriorityLevel.AboveNormal;
        public GpuPreference GpuPreference { get; set; } = GpuPreference.WindowsDefault;
        public bool DisableFullscreenOptimizations { get; set; } = false;
        public bool FreeRamBeforeLaunch { get; set; } = false;
        public bool CalmBackgroundApps { get; set; } = false;
        public bool PowerBoost { get; set; } = false;

        // ---------------------------------------------------------------- integrations
        public bool ActivityTracking { get; set; } = true;
        public bool ServerLocationNotice { get; set; } = true;
        public bool DiscordRichPresence { get; set; } = true;
        /// <summary>Optional override. Empty = NeuzStrap's built-in Discord application.</summary>
        public string DiscordApplicationId { get; set; } = "";
        public bool DiscordShowGameButton { get; set; } = true;

        [JsonIgnore]
        public string EffectiveDiscordApplicationId =>
            string.IsNullOrWhiteSpace(DiscordApplicationId) ? AppInfo.DiscordApplicationId : DiscordApplicationId.Trim();

        // ---------------------------------------------------------------- fast flags & mods
        /// <summary>User-added FastFlags. These are applied on top of the ones NeuzStrap generates.</summary>
        public Dictionary<string, string> CustomFastFlags { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public bool UseCustomFont { get; set; } = false;
        public string CustomFontName { get; set; } = "";
        public CursorStyle CursorStyle { get; set; } = CursorStyle.RobloxDefault;

        // ---------------------------------------------------------------- persistence

        [JsonIgnore]
        public static Settings Current { get; private set; } = new Settings();

        public static void Load()
        {
            try
            {
                if (File.Exists(Paths.SettingsFile))
                {
                    Current = Json.Deserialize<Settings>(File.ReadAllText(Paths.SettingsFile, Encoding.UTF8)) ?? new Settings();
                    if (Current.CustomFastFlags == null) Current.CustomFastFlags = new Dictionary<string, string>(StringComparer.Ordinal);
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Settings", ex, "Settings.json was unreadable, backing it up and starting fresh");
                try { File.Copy(Paths.SettingsFile, Paths.SettingsFile + ".broken", true); } catch { }
            }
            Current = new Settings();
        }

        public static void Save() => Save(Current);

        public static void Save(Settings s)
        {
            try
            {
                AtomicFile.WriteAllText(Paths.SettingsFile, Json.Serialize(s));
            }
            catch (Exception ex)
            {
                Logger.Error("Settings", ex, "Couldn't save settings");
            }
        }

        public static void Reset()
        {
            var fresh = new Settings { FirstRunDone = true };
            Current = fresh;
            Save();
        }
    }

    /// <summary>Writes to a temp file first so a crash or power cut never leaves a half-written file.</summary>
    public static class AtomicFile
    {
        public static void WriteAllText(string path, string contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, contents, new UTF8Encoding(false));
            if (File.Exists(path))
                File.Replace(tmp, path, null, true);
            else
                File.Move(tmp, path);
        }
    }
}
