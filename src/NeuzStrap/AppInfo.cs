using System;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NeuzStrap.Tests")]

namespace NeuzStrap
{
    /// <summary>Static facts about this build of NeuzStrap.</summary>
    public static class AppInfo
    {
        public const string Name = "NeuzStrap";
        public const string Tagline = "The potato-friendly Roblox launcher";
        public const string Author = "Neuz";

        /// <summary>
        /// GitHub "owner/repo" used for the About page and the self-updater.
        /// Change this once the project is pushed to GitHub (e.g. "neuz/NeuzStrap").
        /// While it still contains "YOUR-", update checks are skipped.
        /// </summary>
        public const string GitHubRepo = "NeuzGG/NeuzStrap";

        /// <summary>
        /// NeuzStrap's own Discord application (named "Roblox", so profiles show "Playing Roblox").
        /// Users can still enter their own ID under Activity &amp; Discord.
        /// </summary>
        public const string DiscordApplicationId = "1551904246084145172";

        public static bool HasRepo => !GitHubRepo.StartsWith("YOUR-", StringComparison.Ordinal);
        public static string RepoUrl => "https://github.com/" + GitHubRepo;

        public static readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;
        public static string VersionString => $"{Version.Major}.{Version.Minor}.{Version.Build}";
        public static string UserAgent => $"{Name}/{VersionString}";
    }
}
