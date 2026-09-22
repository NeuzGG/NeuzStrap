using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace NeuzStrap.Core
{
    /// <summary>One shared HttpClient for the whole app (sockets are precious on a potato).</summary>
    public static class Http
    {
        public static HttpClient Client { get; private set; }

        public static void Init()
        {
            // .NET Framework defaults can be old; make sure modern TLS is on and allow a few parallel downloads.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | (SecurityProtocolType)12288 /* Tls13 */;
            ServicePointManager.DefaultConnectionLimit = 16;
            ServicePointManager.Expect100Continue = false;

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseCookies = false,
            };
            Client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            Client.DefaultRequestHeaders.UserAgent.ParseAdd(AppInfo.UserAgent);
        }

        /// <summary>GET a small text/JSON response with a per-request timeout.</summary>
        public static async Task<string> GetStringAsync(string url, CancellationToken ct = default, int timeoutSeconds = 15)
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                using (var resp = await Client.GetAsync(url, HttpCompletionOption.ResponseContentRead, cts.Token).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode)
                        throw new HttpStatusException(resp.StatusCode, url);
                    return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
        }

        public static async Task<object> GetJsonAsync(string url, CancellationToken ct = default, int timeoutSeconds = 15) =>
            Json.Parse(await GetStringAsync(url, ct, timeoutSeconds).ConfigureAwait(false));

        public static async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct) =>
            await Client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        public static void AddRange(HttpRequestMessage req, long from) =>
            req.Headers.Range = new RangeHeaderValue(from, null);
    }

    public class HttpStatusException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public HttpStatusException(HttpStatusCode code, string url)
            : base($"Server answered {(int)code} {code} for {url}")
        {
            StatusCode = code;
        }
    }
}
