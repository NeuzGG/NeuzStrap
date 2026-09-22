using NeuzStrap.Core;

namespace NeuzStrap.Boost
{
    /// <summary>One-click performance presets. Each sets the Performance and Game Booster options.</summary>
    public static class Profiles
    {
        public static string Title(PerformanceProfile p)
        {
            switch (p)
            {
                case PerformanceProfile.RobloxDefault: return "Roblox Default";
                case PerformanceProfile.Balanced: return "Balanced";
                case PerformanceProfile.Potato: return "Potato";
                case PerformanceProfile.UltraPotato: return "Ultra Potato";
                default: return "Custom";
            }
        }

        public static string Emoji(PerformanceProfile p)
        {
            switch (p)
            {
                case PerformanceProfile.RobloxDefault: return "\u2728";      // sparkles
                case PerformanceProfile.Balanced: return "\u2696";           // scales
                case PerformanceProfile.Potato: return "\U0001F954";         // potato
                case PerformanceProfile.UltraPotato: return "\U0001F525";    // fire
                default: return "\U0001F527";                                // wrench
            }
        }

        public static string Description(PerformanceProfile p)
        {
            switch (p)
            {
                case PerformanceProfile.RobloxDefault: return "No changes. Roblox exactly as it ships.";
                case PerformanceProfile.Balanced: return "Stable Direct3D 11, balanced mode and a small priority boost. Looks normal, runs steadier.";
                case PerformanceProfile.Potato: return "Low textures, no grass or anti-aliasing, graphics level 3, 60 FPS cap, high priority and RAM clean-up.";
                case PerformanceProfile.UltraPotato: return "Everything off: lowest quality, blocky meshes, frozen lighting, gray sky and power boost. Max FPS on very weak PCs.";
                default: return "Your own mix of settings.";
            }
        }

        public static void Apply(PerformanceProfile p, Settings s, SystemInfo sys)
        {
            // start from Roblox defaults, then layer the profile on top
            s.GraphicsQualityLock = 0;
            s.OptimizationMode = -1;
            s.FramerateCap = 0;
            s.RenderingApi = RenderingApi.Automatic;
            s.TextureQuality = TextureQuality.Automatic;
            s.AntiAliasing = 0;
            s.RenderQualityOverride = 0;
            s.MeshDetail = MeshDetail.Automatic;
            s.RemoveGrass = false;
            s.FreezeLighting = false;
            s.GraySky = false;
            s.RenderResolutionKilopixels = 0;

            s.RobloxPriority = PriorityLevel.Normal;
            s.FreeRamBeforeLaunch = false;
            s.CalmBackgroundApps = false;
            s.PowerBoost = false;
            s.GpuPreference = GpuPreference.WindowsDefault;

            switch (p)
            {
                case PerformanceProfile.Balanced:
                    s.OptimizationMode = 1;
                    s.RenderingApi = RenderingApi.Direct3D11;
                    s.RobloxPriority = PriorityLevel.AboveNormal;
                    break;

                case PerformanceProfile.Potato:
                    s.OptimizationMode = 0;
                    s.GraphicsQualityLock = 3;
                    s.FramerateCap = 60;
                    s.RenderingApi = RenderingApi.Direct3D11;
                    s.TextureQuality = TextureQuality.Low;
                    s.AntiAliasing = 1;
                    s.RemoveGrass = true;
                    s.RobloxPriority = PriorityLevel.High;
                    s.FreeRamBeforeLaunch = true;
                    s.CalmBackgroundApps = true;
                    break;

                case PerformanceProfile.UltraPotato:
                    s.OptimizationMode = 0;
                    s.GraphicsQualityLock = 1;
                    s.RenderQualityOverride = 1;
                    s.FramerateCap = 60;
                    s.RenderingApi = RenderingApi.Direct3D11;
                    s.TextureQuality = TextureQuality.Lowest;
                    s.AntiAliasing = 1;
                    s.MeshDetail = MeshDetail.Lowest;
                    s.RemoveGrass = true;
                    s.FreezeLighting = true;
                    s.GraySky = true;
                    s.RobloxPriority = PriorityLevel.High;
                    s.FreeRamBeforeLaunch = true;
                    s.CalmBackgroundApps = true;
                    s.PowerBoost = true;
                    break;
            }

            // Laptops with two GPUs often run games on the weak one. Always prefer the strong one.
            if (p != PerformanceProfile.RobloxDefault && sys != null && sys.HasHybridGraphics)
                s.GpuPreference = GpuPreference.HighPerformance;

            s.Profile = p;
        }
    }
}
