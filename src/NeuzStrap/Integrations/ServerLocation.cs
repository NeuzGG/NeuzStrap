using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NeuzStrap.Core;

namespace NeuzStrap.Integrations
{
    public class ServerInfo
    {
        public string Location { get; set; } = "Unknown location";
        public double DistanceKm { get; set; } = -1;

        /// <summary>Plain-language "how laggy will this feel" based on distance from you.</summary>
        public string Hint
        {
            get
            {
                if (DistanceKm < 0) return "";
                if (DistanceKm < 1500) return "Nearby server, should feel smooth";
                if (DistanceKm < 4500) return "Medium distance, a little delay";
                return "Far away server, expect some delay";
            }
        }

        public string Summary => DistanceKm >= 0
            ? $"{Location} (about {DistanceKm.ToString("#,0", CultureInfo.InvariantCulture)} km away)"
            : Location;
    }

    /// <summary>
    /// Roughly where a Roblox server is. For players far from the US (like the Philippines),
    /// knowing you landed on a far server explains the lag and tells you to rejoin.
    /// </summary>
    public static class ServerLocation
    {
        static readonly ConcurrentDictionary<string, ServerInfo> Cache = new ConcurrentDictionary<string, ServerInfo>();
        static (double Lat, double Lon)? _me;
        static bool _meLooked;

        public static async Task<ServerInfo> LookupAsync(string ip, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(ip)) return null;
            if (Cache.TryGetValue(ip, out var hit)) return hit;
            if (ip.StartsWith("10.") || ip.StartsWith("192.168.") || ip.StartsWith("127."))
                return Cache[ip] = new ServerInfo { Location = "Private network" };

            var json = await Http.GetJsonAsync($"https://ipinfo.io/{ip}/json", ct, 10).ConfigureAwait(false);
            var info = new ServerInfo { Location = Describe(json) };

            var server = ParseLoc(Json.GetString(json, "loc"));
            var me = await GetMyLocationAsync(ct).ConfigureAwait(false);
            if (server != null && me != null)
                info.DistanceKm = Haversine(me.Value.Lat, me.Value.Lon, server.Value.Lat, server.Value.Lon);

            return Cache[ip] = info;
        }

        static async Task<(double Lat, double Lon)?> GetMyLocationAsync(CancellationToken ct)
        {
            if (_meLooked) return _me;
            try
            {
                var json = await Http.GetJsonAsync("https://ipinfo.io/json", ct, 10).ConfigureAwait(false);
                _me = ParseLoc(Json.GetString(json, "loc"));
            }
            catch (Exception ex) { Logger.Warn("ServerLocation", "Couldn't get your approximate location: " + ex.Message); }
            _meLooked = true;
            return _me;
        }

        static string Describe(object json)
        {
            string city = Json.GetString(json, "city");
            string region = Json.GetString(json, "region");
            string country = Json.GetString(json, "country");
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(city)) parts.Add(city);
            if (!string.IsNullOrEmpty(region) && region != city) parts.Add(region);
            if (!string.IsNullOrEmpty(country)) parts.Add(country);
            return parts.Count > 0 ? string.Join(", ", parts) : "Unknown location";
        }

        static (double Lat, double Lon)? ParseLoc(string loc)
        {
            if (string.IsNullOrEmpty(loc)) return null;
            var p = loc.Split(',');
            if (p.Length == 2
                && double.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                && double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                return (lat, lon);
            return null;
        }

        static double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            double dLat = (lat2 - lat1) * Math.PI / 180, dLon = (lon2 - lon1) * Math.PI / 180;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }
}
