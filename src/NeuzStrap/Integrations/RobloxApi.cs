using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NeuzStrap.Core;

namespace NeuzStrap.Integrations
{
    public class GameDetails
    {
        public long UniverseId { get; set; }
        public long RootPlaceId { get; set; }
        public string Name { get; set; } = "";
        public string Creator { get; set; } = "";
        public string IconUrl { get; set; } = "";
    }

    /// <summary>Public, no-login Roblox web APIs for game names and icons.</summary>
    public static class RobloxApi
    {
        static readonly ConcurrentDictionary<long, GameDetails> Cache = new ConcurrentDictionary<long, GameDetails>();

        public static async Task<long> GetUniverseIdAsync(long placeId, CancellationToken ct = default)
        {
            var json = await Http.GetJsonAsync($"https://apis.roblox.com/universes/v1/places/{placeId}/universe", ct).ConfigureAwait(false);
            return Json.GetLong(json, "universeId");
        }

        public static async Task<GameDetails> GetGameAsync(long universeId, long placeId = 0, CancellationToken ct = default)
        {
            if (universeId == 0 && placeId != 0)
                universeId = await GetUniverseIdAsync(placeId, ct).ConfigureAwait(false);
            if (universeId == 0) return null;
            if (Cache.TryGetValue(universeId, out var cached)) return cached;

            var details = new GameDetails { UniverseId = universeId };

            var games = await Http.GetJsonAsync($"https://games.roblox.com/v1/games?universeIds={universeId}", ct).ConfigureAwait(false);
            if (Json.Get(games, "data") is List<object> data && data.Count > 0)
            {
                details.Name = Json.GetString(data[0], "name") ?? "";
                details.RootPlaceId = Json.GetLong(data[0], "rootPlaceId");
                details.Creator = Json.GetString(Json.Get(data[0], "creator"), "name") ?? "";
            }

            try
            {
                var icons = await Http.GetJsonAsync($"https://thumbnails.roblox.com/v1/games/icons?universeIds={universeId}&returnPolicy=PlaceHolder&size=512x512&format=Png&isCircular=false", ct).ConfigureAwait(false);
                if (Json.Get(icons, "data") is List<object> iconData && iconData.Count > 0)
                    details.IconUrl = Json.GetString(iconData[0], "imageUrl") ?? "";
            }
            catch (Exception ex) { Logger.Warn("RobloxApi", "Icon lookup failed: " + ex.Message); }

            Cache[universeId] = details;
            return details;
        }
    }
}
