using System;
using NeuzStrap.Core;
using NeuzStrap.UI.Controls;

namespace NeuzStrap.UI.Pages
{
    public sealed class ActivityPage : Page
    {
        readonly TextInput _appId;

        public override string Title => "Activity & Discord";
        public override string Subtitle => "What NeuzStrap does in the background while you play. It uses about as much RAM as a small browser tab.";

        public ActivityPage(MainForm main) : base(main)
        {
            Section("While you play");
            ToggleRow("Game history", "Remembers the games you play so you can rejoin from the Home page.",
                () => S.ActivityTracking, v => S.ActivityTracking = v, Glyph.History);
            ToggleRow("Server location", "Shows a small notice with where your server is and how far away it is. Far server = more lag, so rejoin!",
                () => S.ServerLocationNotice, v => S.ServerLocationNotice = v, Glyph.Globe);
            ToggleRow("Rejoin after a crash", "If Roblox closes unexpectedly while you're in a game, NeuzStrap offers to put you back in the same server.",
                () => S.RejoinAfterCrash, v => S.RejoinAfterCrash = v, Glyph.Refresh);

            Section("Discord Rich Presence", "Show the game you're playing on your Discord profile.");
            ToggleRow("Show my game on Discord", "Friends see \"Playing Roblox\" with the game's name and icon. Works out of the box; just keep the Discord app open on this PC.",
                () => S.DiscordRichPresence, v => S.DiscordRichPresence = v, Glyph.Game);
            ToggleRow("Add a \"Join server\" button", "Friends can click it to land in the exact server you're playing in. It's hidden automatically in private servers.",
                () => S.DiscordShowJoinButton, v => S.DiscordShowJoinButton = v, Glyph.Link);
            ToggleRow("Add a \"View game\" button", "Lets friends open the game's page from your profile.",
                () => S.DiscordShowGameButton, v => S.DiscordShowGameButton = v);

            _appId = new TextInput(220, true) { Text = S.DiscordApplicationId, Placeholder = "Built-in (NeuzStrap)" };
            _appId.TextChanged += (_, __) =>
            {
                S.DiscordApplicationId = _appId.Text.Trim();
                Settings.Save();
            };
            Add(new SettingRow("Custom application ID (optional)",
                "Advanced: use your own app from discord.com/developers. Leave empty to use NeuzStrap's.",
                _appId, Glyph.Code));

            Section("Privacy");
            Note("NeuzStrap only reads the log file Roblox writes on your PC. It never logs into your account. " +
                 "Server locations come from ipinfo.io, and game names and icons from Roblox's public APIs.");
            var clear = new NButton("Clear history", ButtonKind.Danger, Glyph.Delete).FitToText();
            clear.Click += (_, __) =>
            {
                if (!Dialog.Confirm(Main, "Clear game history?", "The Home page will forget the games you've played.", "Clear", danger: true)) return;
                State.Reload();
                State.Current.History.Clear();
                State.Save();
                Toast.Show("History cleared", "Your recently played list is empty now.", null, 3);
            };
            ButtonRow("Game history", "Forget every game in your recent list.", clear, Glyph.History);
        }
    }
}
