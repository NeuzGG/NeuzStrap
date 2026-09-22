using System;
using System.Collections.Generic;
using NeuzStrap.Core;

namespace NeuzStrap.Roblox
{
    /// <summary>
    /// Where each Roblox package gets extracted, relative to the version folder.
    /// The zips don't contain their own folder prefix, so this map is required.
    /// </summary>
    public static class PackageMap
    {
        static readonly Dictionary<string, string> Map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RobloxApp.zip"] = @"",
            ["Libraries.zip"] = @"",
            ["redist.zip"] = @"",
            ["shaders.zip"] = @"shaders\",
            ["ssl.zip"] = @"ssl\",

            ["WebView2.zip"] = @"",
            ["WebView2RuntimeInstaller.zip"] = @"WebView2RuntimeInstaller\",

            ["content-avatar.zip"] = @"content\avatar\",
            ["content-configs.zip"] = @"content\configs\",
            ["content-fonts.zip"] = @"content\fonts\",
            ["content-sky.zip"] = @"content\sky\",
            ["content-sounds.zip"] = @"content\sounds\",
            ["content-textures2.zip"] = @"content\textures\",
            ["content-models.zip"] = @"content\models\",

            ["content-platform-fonts.zip"] = @"PlatformContent\pc\fonts\",
            ["content-platform-dictionaries.zip"] = @"PlatformContent\pc\shared_compression_dictionaries\",
            ["content-terrain.zip"] = @"PlatformContent\pc\terrain\",
            ["content-textures3.zip"] = @"PlatformContent\pc\textures\",

            ["extracontent-luapackages.zip"] = @"ExtraContent\LuaPackages\",
            ["extracontent-translations.zip"] = @"ExtraContent\translations\",
            ["extracontent-models.zip"] = @"ExtraContent\models\",
            ["extracontent-textures.zip"] = @"ExtraContent\textures\",
            ["extracontent-places.zip"] = @"ExtraContent\places\",
        };

        /// <summary>
        /// Returns the extraction folder. Packages Roblox adds in the future get a best-guess
        /// location from their name instead of being silently dropped.
        /// </summary>
        public static string GetFolder(string packageName)
        {
            if (Map.TryGetValue(packageName, out var folder)) return folder;

            string stem = packageName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                ? packageName.Substring(0, packageName.Length - 4)
                : packageName;

            string guess;
            if (stem.StartsWith("extracontent-", StringComparison.OrdinalIgnoreCase))
                guess = @"ExtraContent\" + stem.Substring("extracontent-".Length) + @"\";
            else if (stem.StartsWith("content-platform-", StringComparison.OrdinalIgnoreCase))
                guess = @"PlatformContent\pc\" + stem.Substring("content-platform-".Length) + @"\";
            else if (stem.StartsWith("content-", StringComparison.OrdinalIgnoreCase))
                guess = @"content\" + stem.Substring("content-".Length) + @"\";
            else
                guess = @"";

            Logger.Warn("PackageMap", $"Unknown package '{packageName}', extracting to '{guess}' (best guess)");
            return guess;
        }
    }
}
