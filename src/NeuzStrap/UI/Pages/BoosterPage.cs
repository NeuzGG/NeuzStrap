using System;
using System.Linq;
using System.Threading.Tasks;
using NeuzStrap.Boost;
using NeuzStrap.Core;
using NeuzStrap.UI.Controls;

namespace NeuzStrap.UI.Pages
{
    public sealed class BoosterPage : Page, IProfileAware
    {
        readonly SettingRow _gpuRow;
        readonly SettingRow _dvrRow;
        readonly NButton _dvrButton;

        public override string Title => "Game Booster";
        public override string Subtitle => "Windows-side tweaks while Roblox runs. Everything is undone when you close Roblox.";

        public BoosterPage(MainForm main) : base(main)
        {
            Section("Roblox");
            DropdownRow("CPU priority", "Tells Windows to give Roblox the CPU first when other apps want it too.",
                new Dropdown(200).Add("Normal", PriorityLevel.Normal).Add("Above normal", PriorityLevel.AboveNormal).Add("High", PriorityLevel.High),
                () => S.RobloxPriority, v => S.RobloxPriority = (PriorityLevel)v, Glyph.Speed, true);
            _gpuRow = DropdownRow("Graphics chip", "Laptops with two GPUs sometimes run games on the weaker one.",
                new Dropdown(220).Add("Let Windows decide", GpuPreference.WindowsDefault).Add("High performance GPU", GpuPreference.HighPerformance).Add("Power saving GPU", GpuPreference.PowerSaving),
                () => S.GpuPreference, v => S.GpuPreference = (GpuPreference)v, Glyph.Chip, true);
            ToggleRow("Disable fullscreen optimizations", "Can fix stutter and input lag in fullscreen on some PCs (the same checkbox as in Windows' Compatibility tab).",
                () => S.DisableFullscreenOptimizations, v => S.DisableFullscreenOptimizations = v, Glyph.Monitor, true);

            Section("Your PC while playing");
            ToggleRow("Free up RAM before launch", "Asks background apps to release memory they aren't using, so Roblox has more room to load. Great on 4-8 GB PCs.",
                () => S.FreeRamBeforeLaunch, v => S.FreeRamBeforeLaunch = v, Glyph.Memory, true);
            ToggleRow("Calm background apps", "Lowers the priority of browsers, game launchers and cloud sync (Chrome, Steam, OneDrive...) while you play. Discord is left alone.",
                () => S.CalmBackgroundApps, v => S.CalmBackgroundApps = v, Glyph.Shield, true);
            ToggleRow("Power boost", "Switches Windows to High performance (or the Best performance power mode) while playing, then back. Uses more battery.",
                () => S.PowerBoost, v => S.PowerBoost = v, Glyph.Battery, true);

            Section("One-time Windows tweaks");
            _dvrButton = new NButton("Turn off", ButtonKind.Secondary).FitToText(Theme.S(96));
            _dvrButton.Click += (_, __) =>
            {
                try { GameBooster.SetGameDvr(!GameBooster.IsGameDvrEnabled()); }
                catch (Exception ex) { Dialog.Error(Main, "Couldn't change Game Bar", ex.Message); }
                RefreshDvr();
            };
            _dvrRow = ButtonRow("Xbox Game Bar background recording", "", _dvrButton, Glyph.Game);
        }

        public override void OnNavigatedTo()
        {
            RefreshDvr();
            _ = DescribeGpusAsync();
        }

        void RefreshDvr()
        {
            bool on = GameBooster.IsGameDvrEnabled();
            _dvrRow.Description = on
                ? "On. It quietly records gameplay in the background, which costs FPS on weak PCs."
                : "Off. Nothing is recorded in the background.";
            _dvrButton.Text = on ? "Turn off" : "Turn on";
            _dvrButton.Kind = on ? ButtonKind.Primary : ButtonKind.Secondary;
        }

        async Task DescribeGpusAsync()
        {
            var sys = await Task.Run(() => SystemInfo.Get());
            if (IsDisposed) return;
            string gpus = sys.Gpus.Count == 0 ? "unknown" : string.Join(" and ", sys.Gpus.Select(g => g.Name + (g.IsIntegrated ? " (built-in)" : "")));
            _gpuRow.Description = sys.HasHybridGraphics
                ? $"This laptop has two GPUs: {gpus}. Pick \"High performance\" so Roblox uses the stronger one."
                : $"Detected: {gpus}. Only matters on laptops with two GPUs.";
        }

        public void ProfileChanged() => RefreshRows();
    }
}
