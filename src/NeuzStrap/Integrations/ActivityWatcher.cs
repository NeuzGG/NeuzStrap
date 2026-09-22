using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NeuzStrap.Core;

namespace NeuzStrap.Integrations
{
    public class GameSession
    {
        public long PlaceId { get; set; }
        public long UniverseId { get; set; }
        public string JobId { get; set; } = "";
        public string ServerIp { get; set; } = "";
        public bool IsPrivateServer { get; set; }
        public bool IsReservedServer { get; set; }
        public bool IsTeleport { get; set; }
        public DateTime JoinedUtc { get; set; }

        public string DeepLink => $"roblox://experiences/start?placeId={PlaceId}&gameInstanceId={JobId}";
    }

    /// <summary>
    /// Follows the Roblox log file to know when you join / leave a game. This is the same approach
    /// Bloxstrap uses - it only reads a text file Roblox writes anyway, nothing is injected.
    /// Polls once a second so it costs basically no CPU.
    /// </summary>
    public sealed class ActivityWatcher
    {
        const string JoiningEntry = "[FLog::Output] ! Joining game";
        const string PrivateServerEntry = "[FLog::GameJoinUtil] GameJoinUtil::joinGamePostPrivateServer";
        const string ReservedServerEntry = "[FLog::GameJoinUtil] GameJoinUtil::initiateTeleportToReservedServer";
        const string UniverseEntry = "[FLog::GameJoinLoadTime] Report game_join_loadtime:";
        const string UdmuxEntry = "[FLog::Network] UDMUX Address = ";
        const string JoinedEntry = "[FLog::Network] serverId:";
        const string DisconnectedEntry = "[FLog::Network] Time to disconnect replication data:";
        const string TeleportEntry = "[FLog::SingleSurfaceApp] initiateTeleport";
        const string LeavingEntry = "[FLog::SingleSurfaceApp] leaveUGCGameInternal";

        static readonly Regex JoiningRx = new Regex(@"! Joining game '([0-9a-f\-]{36})' place ([0-9]+) at ([0-9\.]+)", RegexOptions.Compiled);
        static readonly Regex UniverseRx = new Regex(@"universeid:([0-9]+)", RegexOptions.Compiled);
        static readonly Regex UdmuxRx = new Regex(@"UDMUX Address = ([0-9\.]+), Port = [0-9]+ \| RCC Server Address = ([0-9\.]+)", RegexOptions.Compiled);
        static readonly Regex JoinedRx = new Regex(@"serverId: ([0-9\.]+)\|[0-9]+", RegexOptions.Compiled);

        public event Action<GameSession> GameJoined;
        public event Action<GameSession> GameLeft;

        public GameSession Current { get; private set; }
        GameSession _pending;
        bool _nextIsPrivate, _nextIsReserved, _nextIsTeleport;

        /// <summary>Runs until Roblox exits or the token is cancelled.</summary>
        public async Task RunAsync(Func<bool> robloxAlive, DateTime launchedUtc, CancellationToken ct)
        {
            string logFile = await FindLogFileAsync(robloxAlive, launchedUtc, ct).ConfigureAwait(false);
            if (logFile == null)
            {
                Logger.Warn("Activity", "Couldn't find Roblox's log file; activity tracking is off for this session");
                return;
            }
            Logger.Info("Activity", "Following " + Path.GetFileName(logFile));

            using (var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(fs, Encoding.UTF8))
            {
                var partial = new StringBuilder();
                while (!ct.IsCancellationRequested)
                {
                    string chunk = await reader.ReadToEndAsync().ConfigureAwait(false);
                    if (chunk.Length > 0)
                    {
                        partial.Append(chunk);
                        string text = partial.ToString();
                        int lastNewline = text.LastIndexOf('\n');
                        if (lastNewline >= 0)
                        {
                            foreach (var line in text.Substring(0, lastNewline).Split('\n'))
                                HandleLine(line.TrimEnd('\r'));
                            partial.Clear().Append(text.Substring(lastNewline + 1));
                        }
                        continue;
                    }

                    if (!robloxAlive()) break;
                    try { await Task.Delay(1000, ct).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
                }
            }

            if (Current != null)
            {
                var left = Current;
                Current = null;
                GameLeft?.Invoke(left);
            }
        }

        static async Task<string> FindLogFileAsync(Func<bool> robloxAlive, DateTime launchedUtc, CancellationToken ct)
        {
            // Roblox creates the log a moment after starting; give it up to ~40 seconds on slow PCs.
            for (int i = 0; i < 80 && !ct.IsCancellationRequested; i++)
            {
                try
                {
                    if (Directory.Exists(Paths.RobloxLogs))
                    {
                        var file = new DirectoryInfo(Paths.RobloxLogs).GetFiles("*_Player_*.log")
                            .Where(f => f.CreationTimeUtc >= launchedUtc.AddSeconds(-10))
                            .OrderByDescending(f => f.CreationTimeUtc)
                            .FirstOrDefault();
                        if (file != null) return file.FullName;
                    }
                }
                catch { }
                if (!robloxAlive()) return null;
                try { await Task.Delay(500, ct).ConfigureAwait(false); } catch (OperationCanceledException) { return null; }
            }
            return null;
        }

        internal void HandleLine(string line)
        {
            if (line.Length < 20) return;

            if (line.Contains(PrivateServerEntry)) { _nextIsPrivate = true; return; }
            if (line.Contains(ReservedServerEntry)) { _nextIsReserved = true; return; }
            if (line.Contains(TeleportEntry)) { _nextIsTeleport = true; return; }

            if (line.Contains(JoiningEntry))
            {
                var m = JoiningRx.Match(line);
                if (!m.Success) return;
                LeaveCurrent();
                _pending = new GameSession
                {
                    JobId = m.Groups[1].Value,
                    PlaceId = long.Parse(m.Groups[2].Value),
                    ServerIp = m.Groups[3].Value,
                    IsPrivateServer = _nextIsPrivate,
                    IsReservedServer = _nextIsReserved,
                    IsTeleport = _nextIsTeleport,
                };
                _nextIsPrivate = _nextIsReserved = _nextIsTeleport = false;
                return;
            }

            if (_pending != null && line.Contains(UniverseEntry))
            {
                var m = UniverseRx.Match(line);
                if (m.Success) _pending.UniverseId = long.Parse(m.Groups[1].Value);
                return;
            }

            if (_pending != null && line.Contains(UdmuxEntry))
            {
                var m = UdmuxRx.Match(line);
                if (m.Success) _pending.ServerIp = m.Groups[1].Value;
                return;
            }

            if (_pending != null && line.Contains(JoinedEntry))
            {
                var m = JoinedRx.Match(line);
                if (m.Success && string.IsNullOrEmpty(_pending.ServerIp)) _pending.ServerIp = m.Groups[1].Value;
                _pending.JoinedUtc = DateTime.UtcNow;
                Current = _pending;
                _pending = null;
                Logger.Info("Activity", $"Joined place {Current.PlaceId} (universe {Current.UniverseId})");
                GameJoined?.Invoke(Current);
                return;
            }

            if (line.Contains(DisconnectedEntry) || line.Contains(LeavingEntry))
                LeaveCurrent();
        }

        void LeaveCurrent()
        {
            if (Current == null) return;
            var left = Current;
            Current = null;
            Logger.Info("Activity", $"Left place {left.PlaceId}");
            GameLeft?.Invoke(left);
        }
    }
}
