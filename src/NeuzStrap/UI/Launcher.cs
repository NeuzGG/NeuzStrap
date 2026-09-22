using System;
using System.Diagnostics;
using NeuzStrap.Core;
using NeuzStrap.Setup;

namespace NeuzStrap.UI
{
    /// <summary>Starts NeuzStrap in another mode (a separate process keeps the settings window's RAM out of the game).</summary>
    public static class Launcher
    {
        public static string SelfExe => !Paths.IsPortable && AppInstaller.IsInstalled ? Paths.InstalledExe : Paths.CurrentExe;

        public static bool Start(string args)
        {
            try
            {
                Process.Start(new ProcessStartInfo(SelfExe, args) { UseShellExecute = false });
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Launcher", ex, "Couldn't start " + args);
                return false;
            }
        }

        public static bool PlayRoblox(string uri = null) =>
            Start(string.IsNullOrEmpty(uri) ? "-player" : $"-player \"{uri}\"");

        public static bool PlayPlace(long placeId) =>
            PlayRoblox($"roblox://experiences/start?placeId={placeId}");

        public static bool UpdateRoblox() => Start("-update");
        public static bool RepairRoblox() => Start("-repair");
    }
}
