using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NeuzStrap.Core;

namespace NeuzStrap.Roblox
{
    /// <summary>
    /// Builds ClientSettings\ClientAppSettings.json.
    ///
    /// Since 29 Sep 2025 Roblox only honours an allowlist of FastFlags from this file; anything else is
    /// silently ignored (no ban, no penalty). NeuzStrap's performance options only use allowlisted flags,
    /// and the FastFlag editor warns you when a custom flag isn't on the list.
    /// https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569
    /// </summary>
    public static class FastFlags
    {
        public static readonly Dictionary<string, string> Allowlist = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Geometry
            ["DFIntCSGLevelOfDetailSwitchingDistance"] = "Mesh/union level-of-detail switching distance",
            ["DFIntCSGLevelOfDetailSwitchingDistanceL12"] = "LOD distance, level 1 to 2",
            ["DFIntCSGLevelOfDetailSwitchingDistanceL23"] = "LOD distance, level 2 to 3",
            ["DFIntCSGLevelOfDetailSwitchingDistanceL34"] = "LOD distance, level 3 to 4",
            // Rendering
            ["FFlagHandleAltEnterFullscreenManually"] = "False = Alt+Enter uses exclusive fullscreen",
            ["DFFlagTextureQualityOverrideEnabled"] = "Enables the texture quality override",
            ["DFIntTextureQualityOverride"] = "Texture quality (0 lowest - 3 highest)",
            ["FIntDebugForceMSAASamples"] = "Anti-aliasing samples (1 = off)",
            ["DFFlagDisableDPIScale"] = "Ignore Windows display scaling",
            ["FFlagDebugGraphicsPreferD3D11"] = "Use Direct3D 11",
            ["FFlagDebugSkyGray"] = "Plain gray sky",
            ["DFFlagDebugPauseVoxelizer"] = "Freeze voxel lighting updates",
            ["DFIntDebugFRMQualityLevelOverride"] = "Force render quality level",
            ["FIntFRMMaxGrassDistance"] = "Max grass draw distance",
            ["FIntFRMMinGrassDistance"] = "Min grass draw distance",
            ["FFlagDebugGraphicsPreferVulkan"] = "Use Vulkan",
            ["FFlagDebugGraphicsPreferOpenGL"] = "Use OpenGL",
            // User interface
            ["FIntGrassMovementReducedMotionFactor"] = "Grass sway amount",
        };

        /// <summary>Approved by Roblox's rendering team (Jan 2026) but not confirmed live on every channel.</summary>
        public static readonly Dictionary<string, string> Experimental = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DFIntDebugDynamicRenderKiloPixels"] = "Render resolution budget in kilopixels (experimental)",
        };

        public enum FlagStatus { Allowed, Experimental, Ignored }

        public static FlagStatus GetStatus(string name)
        {
            if (Allowlist.ContainsKey(name)) return FlagStatus.Allowed;
            if (Experimental.ContainsKey(name)) return FlagStatus.Experimental;
            return FlagStatus.Ignored;
        }

        public static string Describe(string name) =>
            Allowlist.TryGetValue(name, out var d) ? d : Experimental.TryGetValue(name, out d) ? d : "";

        /// <summary>The flags produced by the Performance page options.</summary>
        public static Dictionary<string, string> Generate(Settings s)
        {
            var f = new Dictionary<string, string>(StringComparer.Ordinal);

            switch (s.RenderingApi)
            {
                case RenderingApi.Direct3D11: f["FFlagDebugGraphicsPreferD3D11"] = "True"; break;
                case RenderingApi.Vulkan: f["FFlagDebugGraphicsPreferVulkan"] = "True"; break;
                case RenderingApi.OpenGL: f["FFlagDebugGraphicsPreferOpenGL"] = "True"; break;
            }

            if (s.TextureQuality != TextureQuality.Automatic)
            {
                f["DFFlagTextureQualityOverrideEnabled"] = "True";
                f["DFIntTextureQualityOverride"] = ((int)s.TextureQuality - 1).ToString(CultureInfo.InvariantCulture);
            }

            if (s.AntiAliasing > 0)
                f["FIntDebugForceMSAASamples"] = s.AntiAliasing.ToString(CultureInfo.InvariantCulture);

            if (s.RenderQualityOverride > 0)
                f["DFIntDebugFRMQualityLevelOverride"] = Utils.Clamp(s.RenderQualityOverride, 1, 21).ToString(CultureInfo.InvariantCulture);

            if (s.MeshDetail == MeshDetail.Lowest)
            {
                f["DFIntCSGLevelOfDetailSwitchingDistance"] = "0";
                f["DFIntCSGLevelOfDetailSwitchingDistanceL12"] = "0";
                f["DFIntCSGLevelOfDetailSwitchingDistanceL23"] = "0";
                f["DFIntCSGLevelOfDetailSwitchingDistanceL34"] = "0";
            }

            if (s.RemoveGrass)
            {
                f["FIntFRMMinGrassDistance"] = "0";
                f["FIntFRMMaxGrassDistance"] = "0";
                f["FIntGrassMovementReducedMotionFactor"] = "0";
            }

            if (s.FreezeLighting) f["DFFlagDebugPauseVoxelizer"] = "True";
            if (s.GraySky) f["FFlagDebugSkyGray"] = "True";
            if (s.ExclusiveFullscreen) f["FFlagHandleAltEnterFullscreenManually"] = "False";
            if (s.DisableDpiScaling) f["DFFlagDisableDPIScale"] = "True";

            if (s.RenderResolutionKilopixels > 0)
                f["DFIntDebugDynamicRenderKiloPixels"] = s.RenderResolutionKilopixels.ToString(CultureInfo.InvariantCulture);

            return f;
        }

        /// <summary>Generated flags with the user's custom flags layered on top.</summary>
        public static Dictionary<string, string> BuildFinal(Settings s)
        {
            var result = Generate(s);
            foreach (var kv in s.CustomFastFlags)
                if (!string.IsNullOrWhiteSpace(kv.Key))
                    result[kv.Key.Trim()] = kv.Value ?? "";
            return result;
        }

        public static string ToJson(Dictionary<string, string> flags)
        {
            var obj = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var kv in flags.OrderBy(k => k.Key, StringComparer.Ordinal)) obj[kv.Key] = kv.Value;
            return Json.Serialize(obj);
        }

        public static void Write(string versionDir, Settings s)
        {
            var flags = BuildFinal(s);
            string dir = Path.Combine(versionDir, "ClientSettings");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "ClientAppSettings.json"), ToJson(flags), new System.Text.UTF8Encoding(false));

            int ignored = flags.Keys.Count(k => GetStatus(k) == FlagStatus.Ignored);
            Logger.Info("FastFlags", $"Wrote {flags.Count} flags ({ignored} not on Roblox's allowlist and will be ignored by Roblox)");
        }

        /// <summary>Parses pasted JSON (e.g. from Bloxstrap) into name/value strings.</summary>
        public static Dictionary<string, string> ParseImport(string text, out string error)
        {
            error = null;
            if (!Json.TryParse(text, out var parsed, out error)) return null;
            if (!(parsed is Dictionary<string, object> obj))
            {
                error = "Expected a JSON object like { \"FlagName\": \"Value\" }";
                return null;
            }

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in obj)
            {
                switch (kv.Value)
                {
                    case bool b: result[kv.Key] = b ? "True" : "False"; break;
                    case null: result[kv.Key] = ""; break;
                    case double d: result[kv.Key] = d.ToString("R", CultureInfo.InvariantCulture); break;
                    case Dictionary<string, object> _:
                    case List<object> _:
                        result[kv.Key] = Json.Serialize(kv.Value, false); break;
                    default: result[kv.Key] = Convert.ToString(kv.Value, CultureInfo.InvariantCulture); break;
                }
            }
            return result;
        }

        /// <summary>FastFlags from a Bloxstrap / Fishstrap install, if one exists (for one-click import).</summary>
        public static string FindOtherBootstrapperFlags()
        {
            foreach (var name in new[] { "Bloxstrap", "Fishstrap", "Voidstrap", "Froststrap" })
            {
                string f = Path.Combine(Paths.LocalAppData, name, "Modifications", "ClientSettings", "ClientAppSettings.json");
                if (File.Exists(f)) return f;
            }
            return null;
        }
    }
}
