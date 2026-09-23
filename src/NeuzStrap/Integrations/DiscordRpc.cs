using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NeuzStrap.Core;

namespace NeuzStrap.Integrations
{
    /// <summary>
    /// Minimal Discord Rich Presence client (Discord's local IPC pipe). No extra DLLs needed.
    /// Needs a Discord application ID - see the README for the 2-minute setup.
    /// </summary>
    public sealed class DiscordRpc : IDisposable
    {
        const int OpHandshake = 0, OpFrame = 1, OpClose = 2, OpPing = 3, OpPong = 4;

        readonly string _clientId;
        readonly object _writeLock = new object();
        NamedPipeClientStream _pipe;
        CancellationTokenSource _readCts;

        public DiscordRpc(string clientId) { _clientId = clientId?.Trim(); }

        public bool IsConnected => _pipe != null && _pipe.IsConnected;

        public async Task<bool> ConnectAsync()
        {
            if (string.IsNullOrEmpty(_clientId)) return false;
            Close();
            for (int i = 0; i < 10; i++)
            {
                NamedPipeClientStream pipe = null;
                try
                {
                    pipe = new NamedPipeClientStream(".", "discord-ipc-" + i, PipeDirection.InOut, PipeOptions.Asynchronous);
                    pipe.Connect(250);
                    _pipe = pipe;
                    Send(OpHandshake, new Dictionary<string, object> { ["v"] = 1L, ["client_id"] = _clientId });

                    var (op, payload) = await ReadFrameAsync(pipe, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                    if (op == OpFrame && Json.GetString(payload, "evt") == "READY")
                    {
                        Logger.Info("Discord", $"Connected to Discord (pipe {i})");
                        _readCts = new CancellationTokenSource();
                        _ = Task.Run(() => ReadLoop(pipe, _readCts.Token));
                        return true;
                    }
                    Logger.Warn("Discord", "Discord refused the connection: " + Json.GetString(payload, "message"));
                    Close();
                    return false;
                }
                catch (TimeoutException) { pipe?.Dispose(); _pipe = null; }
                catch (IOException) { pipe?.Dispose(); _pipe = null; }
                catch (Exception ex)
                {
                    Logger.Warn("Discord", $"Pipe {i}: {ex.Message}");
                    pipe?.Dispose();
                    _pipe = null;
                }
            }
            Logger.Info("Discord", "Discord doesn't seem to be running");
            return false;
        }

        public void SetActivity(Dictionary<string, object> activity)
        {
            if (!IsConnected) return;
            try
            {
                Send(OpFrame, new Dictionary<string, object>
                {
                    ["cmd"] = "SET_ACTIVITY",
                    ["args"] = new Dictionary<string, object>
                    {
                        ["pid"] = (long)Process.GetCurrentProcess().Id,
                        ["activity"] = activity,
                    },
                    ["nonce"] = Guid.NewGuid().ToString(),
                });
            }
            catch (Exception ex)
            {
                Logger.Warn("Discord", "Couldn't update presence: " + ex.Message);
                Close();
            }
        }

        public void ClearActivity() => SetActivity(null);

        void Send(int op, object payload)
        {
            byte[] body = Encoding.UTF8.GetBytes(Json.Serialize(payload, false));
            byte[] frame = new byte[8 + body.Length];
            BitConverter.GetBytes(op).CopyTo(frame, 0);
            BitConverter.GetBytes(body.Length).CopyTo(frame, 4);
            body.CopyTo(frame, 8);
            lock (_writeLock)
            {
                _pipe.Write(frame, 0, frame.Length);
                _pipe.Flush();
            }
        }

        async Task ReadLoop(NamedPipeClientStream pipe, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && pipe.IsConnected)
                {
                    var (op, payload) = await ReadFrameAsync(pipe, Timeout.InfiniteTimeSpan).ConfigureAwait(false);
                    if (op == OpPing) Send(OpPong, payload);
                    else if (op == OpClose) { Logger.Info("Discord", "Discord closed the connection"); break; }
                    else if (Json.GetString(payload, "evt") == "ERROR")
                        Logger.Warn("Discord", "Discord error: " + Json.GetString(Json.Get(payload, "data"), "message"));
                }
            }
            catch { /* pipe closed */ }
        }

        static async Task<(int Op, object Payload)> ReadFrameAsync(Stream s, TimeSpan timeout)
        {
            byte[] header = await ReadExactAsync(s, 8, timeout).ConfigureAwait(false);
            int op = BitConverter.ToInt32(header, 0);
            int len = BitConverter.ToInt32(header, 4);
            if (len < 0 || len > 1 << 20) throw new IOException("Bad Discord frame");
            byte[] body = await ReadExactAsync(s, len, timeout).ConfigureAwait(false);
            object payload = null;
            try { payload = Json.Parse(Encoding.UTF8.GetString(body)); } catch { }
            return (op, payload);
        }

        static async Task<byte[]> ReadExactAsync(Stream s, int count, TimeSpan timeout)
        {
            var buf = new byte[count];
            int got = 0;
            while (got < count)
            {
                var read = s.ReadAsync(buf, got, count - got);
                if (timeout != Timeout.InfiniteTimeSpan && await Task.WhenAny(read, Task.Delay(timeout)).ConfigureAwait(false) != read)
                    throw new TimeoutException("Discord didn't answer");
                int n = await read.ConfigureAwait(false);
                if (n <= 0) throw new EndOfStreamException();
                got += n;
            }
            return buf;
        }

        void Close()
        {
            try { _readCts?.Cancel(); } catch { }
            try { _pipe?.Dispose(); } catch { }
            _pipe = null;
        }

        public void Dispose()
        {
            try { if (IsConnected) ClearActivity(); } catch { }
            Close();
        }

        // ------------------------------------------------------------------ activity builder

        public static Dictionary<string, object> BuildActivity(GameSession session, GameDetails game, bool showButton, bool showJoinButton = false)
        {
            string name = string.IsNullOrEmpty(game?.Name) ? "a Roblox game" : game.Name;
            var activity = new Dictionary<string, object>
            {
                ["details"] = Trim("Playing " + name, 128),
                ["state"] = session.IsPrivateServer ? "In a private server"
                          : !string.IsNullOrEmpty(game?.Creator) ? Trim("by " + game.Creator, 128)
                          : "In a public server",
                ["timestamps"] = new Dictionary<string, object> { ["start"] = Utils.UnixSeconds(session.JoinedUtc) },
                ["instance"] = false,
            };

            var assets = new Dictionary<string, object> { ["large_text"] = Trim(name, 128) };
            if (!string.IsNullOrEmpty(game?.IconUrl)) assets["large_image"] = game.IconUrl;
            activity["assets"] = assets;

            // Discord allows at most two buttons, and they have to be https links.
            var buttons = new List<object>();
            bool canJoin = !session.IsPrivateServer && !session.IsReservedServer && session.JobId.Length > 0;
            if (showJoinButton && session.PlaceId > 0 && canJoin)
                buttons.Add(new Dictionary<string, object>
                {
                    ["label"] = "Join server",
                    ["url"] = $"https://www.roblox.com/games/start?placeId={session.PlaceId}&gameInstanceId={session.JobId}",
                });
            if (showButton && session.PlaceId > 0)
                buttons.Add(new Dictionary<string, object> { ["label"] = "View game", ["url"] = $"https://www.roblox.com/games/{session.PlaceId}" });
            if (buttons.Count > 0) activity["buttons"] = buttons;
            return activity;
        }

        static string Trim(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "\u2026";
    }
}
