using System;
using System.IO;
using System.Text;
using System.Xml;
using NeuzStrap.Core;

namespace NeuzStrap.Roblox
{
    /// <summary>
    /// Edits %LocalAppData%\Roblox\GlobalBasicSettings_13.xml - the file where Roblox saves the in-game
    /// settings menu (graphics slider, FPS cap, optimization mode). Changing it is exactly the same as
    /// changing those options in-game, just done for you before every launch.
    /// </summary>
    public static class GameSettingsFile
    {
        /// <summary>The values Roblox's own "Maximum Frame Rate" menu offers (anything else gets reset by Roblox).</summary>
        public static readonly int[] FramerateOptions = { 60, 120, 144, 240 };

        public static bool Exists => File.Exists(Paths.GlobalBasicSettings);

        static bool WantsFps(Settings s) => Array.IndexOf(FramerateOptions, s.FramerateCap) >= 0;

        /// <summary>
        /// Roblox's own FPS panel: true = turn it on, false = turn it off again (only when NeuzStrap
        /// turned it on before), null = leave whatever the player set with Shift+F5.
        /// </summary>
        static bool? PerformanceStats(Settings s) =>
            s.RobloxFpsCounter ? true : State.Current.RobloxFpsCounterApplied ? (bool?)false : null;

        /// <summary>True when the current settings change something in Roblox's settings file.</summary>
        public static bool HasChanges(Settings s) =>
            s.GraphicsQualityLock > 0 || s.OptimizationMode >= 0 || WantsFps(s) || PerformanceStats(s) != null;

        public static void Apply(Settings s)
        {
            if (!HasChanges(s)) return;

            string path = Paths.GlobalBasicSettings;
            if (!File.Exists(path))
            {
                Logger.Info("GameSettings", "Roblox hasn't created its settings file yet; it'll be tuned on the next launch");
                return;
            }
            if (RobloxProcess.IsPlayerRunning())
            {
                Logger.Info("GameSettings", "Roblox is already running, so its in-game settings file was left alone");
                return;
            }
            bool? stats = PerformanceStats(s);
            ApplyTo(path, s, stats);

            if (stats.HasValue && State.Current.RobloxFpsCounterApplied != stats.Value)
            {
                State.Current.RobloxFpsCounterApplied = stats.Value;
                State.Save();
            }
        }

        internal static void ApplyTo(string path, Settings s, bool? performanceStats = null)
        {
            bool wantsQuality = s.GraphicsQualityLock > 0;
            bool wantsMode = s.OptimizationMode >= 0;
            bool wantsFps = WantsFps(s);
            try
            {
                string backup = path + ".neuzstrap.bak";
                if (!File.Exists(backup)) File.Copy(path, backup);

                var doc = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
                using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }))
                    doc.Load(reader);

                var props = doc.SelectSingleNode("//Item[@class='UserGameSettings']/Properties") as XmlElement;
                if (props == null)
                {
                    Logger.Warn("GameSettings", "Couldn't find UserGameSettings in the settings file");
                    return;
                }

                if (wantsQuality)
                {
                    int level = Utils.Clamp(s.GraphicsQualityLock, 1, 10);
                    Set(doc, props, "token", "SavedQualityLevel", level.ToString());
                    Set(doc, props, "int", "GraphicsQualityLevel", level.ToString());
                    Set(doc, props, "bool", "MaxQualityEnabled", "false");
                }
                if (wantsMode) Set(doc, props, "token", "GraphicsOptimizationMode", Utils.Clamp(s.OptimizationMode, 0, 2).ToString());
                if (wantsFps) Set(doc, props, "int", "FramerateCap", s.FramerateCap.ToString());
                if (performanceStats.HasValue) Set(doc, props, "bool", "PerformanceStatsVisible", performanceStats.Value ? "true" : "false");

                var ws = new XmlWriterSettings { Encoding = new UTF8Encoding(false), OmitXmlDeclaration = doc.FirstChild is not XmlDeclaration };
                string tmp = path + ".tmp";
                using (var w = XmlWriter.Create(tmp, ws)) doc.Save(w);
                File.Copy(tmp, path, true);
                File.Delete(tmp);

                Logger.Info("GameSettings", $"In-game settings applied (quality={s.GraphicsQualityLock}, mode={s.OptimizationMode}, fps={s.FramerateCap})");
            }
            catch (Exception ex)
            {
                Logger.Error("GameSettings", ex, "Couldn't update the in-game settings file");
            }
        }

        static void Set(XmlDocument doc, XmlElement props, string type, string name, string value)
        {
            if (!(props.SelectSingleNode($"*[@name='{name}']") is XmlElement el))
            {
                el = doc.CreateElement(type);
                el.SetAttribute("name", name);
                props.AppendChild(el);
            }
            el.InnerText = value;
        }

        /// <summary>Reads a value, used by the UI to show what Roblox currently has.</summary>
        public static string Read(string name)
        {
            try
            {
                if (!Exists) return null;
                var doc = new XmlDocument { XmlResolver = null };
                using (var reader = XmlReader.Create(Paths.GlobalBasicSettings, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }))
                    doc.Load(reader);
                return doc.SelectSingleNode($"//Item[@class='UserGameSettings']/Properties/*[@name='{name}']")?.InnerText;
            }
            catch { return null; }
        }
    }
}
