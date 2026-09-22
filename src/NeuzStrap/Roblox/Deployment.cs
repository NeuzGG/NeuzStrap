using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NeuzStrap.Core;

namespace NeuzStrap.Roblox
{
    public class ClientVersion
    {
        public string Version { get; set; }     // e.g. 0.739.0.7390687
        public string Guid { get; set; }        // e.g. version-4310300497aa4917
        public string Channel { get; set; }
    }

    public class Package
    {
        public string Name { get; set; }
        public string Md5 { get; set; }
        public long PackedSize { get; set; }
        public long Size { get; set; }
        public override string ToString() => Name;
    }

    /// <summary>Talks to Roblox's official version API and setup CDN.</summary>
    public static class Deployment
    {
        public const string DefaultChannel = "LIVE";

        /// <summary>Official Roblox setup mirrors, in order of preference.</summary>
        public static readonly string[] Mirrors =
        {
            "https://setup.rbxcdn.com",
            "https://setup-aws.rbxcdn.com",
            "https://setup-ak.rbxcdn.com",
            "https://s3.amazonaws.com/setup.roblox.com",
        };

        static readonly string[] VersionApiHosts =
        {
            "https://clientsettingscdn.roblox.com",
            "https://clientsettings.roblox.com",
        };

        static string _fastestMirror;

        public static bool IsLive(string channel) =>
            string.IsNullOrWhiteSpace(channel) || channel.Trim().Equals(DefaultChannel, StringComparison.OrdinalIgnoreCase);

        static string ChannelPath(string channel) =>
            IsLive(channel) ? "" : "/channel/" + channel.Trim().ToLowerInvariant();

        /// <summary>Asks Roblox which client version is current. Falls back to LIVE if a custom channel is locked.</summary>
        public static async Task<ClientVersion> GetLatestAsync(string channel, CancellationToken ct)
        {
            if (!IsLive(channel))
            {
                try { return await QueryVersion(channel, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    Logger.Warn("Deployment", $"Channel '{channel}' isn't available ({ex.Message}); using LIVE instead");
                }
            }
            return await QueryVersion(DefaultChannel, ct).ConfigureAwait(false);
        }

        static async Task<ClientVersion> QueryVersion(string channel, CancellationToken ct)
        {
            Exception last = null;
            foreach (var host in VersionApiHosts)
            {
                string url = host + "/v2/client-version/WindowsPlayer" + (IsLive(channel) ? "" : "/channel/" + channel.Trim());
                try
                {
                    var json = await Http.GetJsonAsync(url, ct, 12).ConfigureAwait(false);
                    var v = new ClientVersion
                    {
                        Version = Json.GetString(json, "version"),
                        Guid = Json.GetString(json, "clientVersionUpload"),
                        Channel = IsLive(channel) ? DefaultChannel : channel.Trim(),
                    };
                    if (string.IsNullOrEmpty(v.Guid) || !v.Guid.StartsWith("version-", StringComparison.Ordinal))
                        throw new FormatException("Roblox returned an unexpected version answer");
                    Logger.Info("Deployment", $"Latest {v.Channel} client: {v.Version} ({v.Guid}) via {host}");
                    return v;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    last = ex;
                    Logger.Warn("Deployment", $"Version check via {host} failed: {ex.Message}");
                }
            }
            throw new Exception("Couldn't reach Roblox to check for updates.", last);
        }

        /// <summary>Picks the first mirror that answers quickly. Cached for the rest of the session.</summary>
        public static async Task<string> GetMirrorAsync(CancellationToken ct)
        {
            if (_fastestMirror != null) return _fastestMirror;
            foreach (var m in Mirrors)
            {
                try
                {
                    string v = await Http.GetStringAsync(m + "/version", ct, 6).ConfigureAwait(false);
                    if (v.StartsWith("version-", StringComparison.Ordinal))
                    {
                        Logger.Info("Deployment", $"Using mirror {m}");
                        return _fastestMirror = m;
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) { Logger.Warn("Deployment", $"Mirror {m} unavailable: {ex.Message}"); }
            }
            return _fastestMirror = Mirrors[0];
        }

        public static string PackageUrl(string mirror, ClientVersion v, string file) =>
            $"{mirror}{ChannelPath(v.Channel)}/{v.Guid}-{file}";

        public static async Task<List<Package>> GetManifestAsync(ClientVersion v, CancellationToken ct)
        {
            Exception last = null;
            string preferred = await GetMirrorAsync(ct).ConfigureAwait(false);
            var order = new List<string> { preferred };
            foreach (var m in Mirrors) if (m != preferred) order.Add(m);

            foreach (var mirror in order)
            {
                try
                {
                    string text = await Http.GetStringAsync(PackageUrl(mirror, v, "rbxPkgManifest.txt"), ct, 20).ConfigureAwait(false);
                    return ParseManifest(text);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    last = ex;
                    Logger.Warn("Deployment", $"Manifest from {mirror} failed: {ex.Message}");
                }
            }
            throw new Exception("Couldn't download the Roblox package list.", last);
        }

        /// <summary>
        /// rbxPkgManifest.txt format: first line "v0", then 4 lines per package:
        /// file name, MD5, packed (download) size, unpacked size.
        /// </summary>
        public static List<Package> ParseManifest(string text)
        {
            var lines = text.Replace("\r", "").Split('\n');
            if (lines.Length < 1 || lines[0].Trim() != "v0")
                throw new FormatException("Unknown Roblox package manifest version: " + (lines.Length > 0 ? lines[0] : "(empty)"));

            var result = new List<Package>();
            for (int i = 1; i + 3 < lines.Length; i += 4)
            {
                string name = lines[i].Trim();
                if (name.Length == 0) break;
                result.Add(new Package
                {
                    Name = name,
                    Md5 = lines[i + 1].Trim().ToLowerInvariant(),
                    PackedSize = long.Parse(lines[i + 2].Trim(), CultureInfo.InvariantCulture),
                    Size = long.Parse(lines[i + 3].Trim(), CultureInfo.InvariantCulture),
                });
            }
            if (result.Count == 0) throw new FormatException("The Roblox package manifest was empty");
            return result;
        }
    }
}
