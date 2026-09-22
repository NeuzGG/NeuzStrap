using System;
using System.Runtime.InteropServices;
using NeuzStrap.Core;

namespace NeuzStrap.Boost
{
    /// <summary>
    /// "Power boost": switches Windows to its High performance plan (or the "Best performance" power
    /// mode on laptops that only have Balanced) while Roblox runs, then switches back.
    /// </summary>
    public static class PowerPlan
    {
        public static readonly Guid HighPerformance = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        public static readonly Guid PowerSaver = new Guid("a1841308-3541-4fab-bc81-f71556f20b4a");
        static readonly Guid BestPerformanceOverlay = new Guid("ded574b5-45a0-4f42-8737-46345c09c238");

        public static Guid? GetActiveScheme()
        {
            try
            {
                if (PowerGetActiveScheme(IntPtr.Zero, out IntPtr ptr) != 0) return null;
                try { return (Guid)Marshal.PtrToStructure(ptr, typeof(Guid)); }
                finally { LocalFree(ptr); }
            }
            catch { return null; }
        }

        public static bool IsPowerSaverActive() => GetActiveScheme() == PowerSaver;

        /// <summary>Returns true if something was changed (and recorded in State for restoring).</summary>
        public static bool Boost()
        {
            try
            {
                var active = GetActiveScheme();
                if (active == null || active == HighPerformance) return false;

                var target = HighPerformance;
                if (PowerSetActiveScheme(IntPtr.Zero, ref target) == 0)
                {
                    State.Current.PendingPowerSchemeRestore = active.Value.ToString();
                    State.Save();
                    Logger.Info("PowerPlan", "Switched to High performance while playing");
                    return true;
                }

                // Modern laptops hide the High performance plan; use the power-mode slider instead.
                if (PowerGetEffectiveOverlayScheme(out Guid overlay) == 0 && overlay != BestPerformanceOverlay)
                {
                    var best = BestPerformanceOverlay;
                    if (PowerSetActiveOverlayScheme(ref best) == 0)
                    {
                        State.Current.PendingPowerOverlayRestore = overlay.ToString();
                        State.Save();
                        Logger.Info("PowerPlan", "Switched power mode to Best performance while playing");
                        return true;
                    }
                }
            }
            catch (Exception ex) // older Windows builds don't have the overlay APIs
            {
                Logger.Warn("PowerPlan", "Power boost unavailable: " + ex.Message);
            }
            return false;
        }

        /// <summary>Puts back whatever Boost() changed. Also called on startup in case NeuzStrap was killed mid-game.</summary>
        public static void Restore()
        {
            var st = State.Current;
            bool changed = false;
            try
            {
                if (Guid.TryParse(st.PendingPowerSchemeRestore, out var scheme))
                {
                    PowerSetActiveScheme(IntPtr.Zero, ref scheme);
                    Logger.Info("PowerPlan", "Power plan restored");
                }
                if (Guid.TryParse(st.PendingPowerOverlayRestore, out var overlay))
                {
                    PowerSetActiveOverlayScheme(ref overlay);
                    Logger.Info("PowerPlan", "Power mode restored");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("PowerPlan", "Couldn't restore power settings: " + ex.Message);
            }
            if (st.PendingPowerSchemeRestore.Length > 0 || st.PendingPowerOverlayRestore.Length > 0) changed = true;
            st.PendingPowerSchemeRestore = "";
            st.PendingPowerOverlayRestore = "";
            if (changed) State.Save();
        }

        [DllImport("powrprof.dll")]
        static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

        [DllImport("powrprof.dll")]
        static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

        [DllImport("powrprof.dll")]
        static extern uint PowerGetEffectiveOverlayScheme(out Guid overlaySchemeGuid);

        [DllImport("powrprof.dll")]
        static extern uint PowerSetActiveOverlayScheme(ref Guid overlaySchemeGuid);

        [DllImport("kernel32.dll")]
        static extern IntPtr LocalFree(IntPtr hMem);
    }
}
