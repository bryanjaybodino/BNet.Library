using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static BNet.ZKTecoADMS.ZKTecoEventArgs;
using ErrorEventArgs = BNet.ZKTecoADMS.ZKTecoEventArgs.ErrorEventArgs;

namespace BNet.ZKTecoADMS
{
    /// <summary>
    /// Async, event-driven HTTP server that speaks the ZKTeco ADMS/Push protocol.
    /// Targets .NET Framework 4.5 – 4.8 and .NET 5 – 8 without any third-party DLLs.
    /// Subscribe to events and call StartAsync() / Start() to begin receiving data.
    /// </summary>
    public class ZKTecoServer :
        #if NET5_0_OR_GREATER || NETCOREAPP3_0_OR_GREATER
                IAsyncDisposable,
        #endif
        IDisposable
    {
        // ── Configuration ──────────────────────────────────────────────────────

        /// <summary>TCP port the server listens on (default 4780).</summary>
        public int Port { get; set; } = 4780;

        /// <summary>
        /// Directory where attendance photos are saved.
        /// Adjustable at any time before or after Start().
        /// </summary>
        public string PhotoSaveDirectory { get; set; } = @"C:\ZKPhotos";

        /// <summary>
        /// ATTPHOTOSTAMP sent during handshake.
        /// 0 = send all photos from the beginning.
        /// </summary>
        public int PhotoStamp { get; set; } = 0;

        /// <summary>Delay (seconds) between device push cycles (default 10).</summary>
        public int Delay { get; set; } = 10;

        /// <summary>Error delay (seconds) on failure (default 30).</summary>
        public int ErrorDelay { get; set; } = 30;

        /// <summary>Device timezone offset (default 8 = UTC+8).</summary>
        public int TimeZone { get; set; } = 8;

        // ── Events ─────────────────────────────────────────────────────────────

        /// <summary>Fired when a device completes its initial handshake.</summary>
        public event EventHandler<HandshakeEventArgs> OnHandshake;

        /// <summary>Fired for every attendance punch record received.</summary>
        public event EventHandler<AttendanceEventArgs> OnAttendance;

        /// <summary>Fired when a photo is successfully saved to disk.</summary>
        public event EventHandler<PhotoEventArgs> OnPhotoReceived;

        /// <summary>Fired on every device heartbeat.</summary>
        public event EventHandler<HeartbeatEventArgs> OnHeartbeat;

        /// <summary>Fired when any internal error occurs (non-fatal).</summary>
        public event EventHandler<ErrorEventArgs> OnError;

        /// <summary>Fired for raw request logging / debugging.</summary>
        public event EventHandler<RawRequestEventArgs> OnRawRequest;

        // ── Private state ──────────────────────────────────────────────────────

        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private Task _listenTask;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        /// <summary>
        /// Start listening asynchronously.
        /// The returned Task represents the listen loop — await it if you want
        /// to keep the host alive, or fire-and-forget if you manage lifetime yourself.
        /// </summary>
        public Task StartAsync(CancellationToken externalToken = default(CancellationToken))
        {
            if (_listenTask != null) return _listenTask;

            Directory.CreateDirectory(PhotoSaveDirectory);

            _listener = new HttpListener();
            _listener.Prefixes.Add(string.Format("http://+:{0}/", Port));
            _listener.Start();

            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            _listenTask = ListenLoopAsync(_cts.Token);
            return _listenTask;
        }

        /// <summary>
        /// Synchronous convenience wrapper — spawns the async loop on a background Task.
        /// </summary>
        public void Start(CancellationToken externalToken = default(CancellationToken))
        {
            _ = StartAsync(externalToken);
        }

        /// <summary>Stop the server and wait for the listen loop to exit.</summary>
        public async Task StopAsync()
        {
            _cts?.Cancel();
            try { _listener?.Stop(); } catch { /* ignore */ }
            if (_listenTask != null)
                await _listenTask.ConfigureAwait(false);
        }

        /// <summary>Synchronous stop (blocks until loop exits).</summary>
        public void Stop() => StopAsync().GetAwaiter().GetResult();

        // IAsyncDisposable is only available on .NET Core 3+ / .NET 5+
#if NET5_0_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
        }
#endif

        public void Dispose() => Stop();

        // ── Main accept loop ───────────────────────────────────────────────────

        private async Task ListenLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // GetContextAsync has no CancellationToken overload on any TFM;
                    // cancellation arrives via _listener.Stop() called from StopAsync().
                    HttpListenerContext ctx = await _listener.GetContextAsync().ConfigureAwait(false);

                    // Fire-and-forget each connection so Accept() is never blocked.
                    _ = Task.Run(() => HandleRequestAsync(ctx, ct), ct);
                }
                catch (HttpListenerException) when (ct.IsCancellationRequested)
                {
                    break; // normal shutdown
                }
                catch (ObjectDisposedException) when (ct.IsCancellationRequested)
                {
                    break; // normal shutdown
                }
                catch (Exception ex)
                {
                    RaiseError("ListenLoop", ex);
                }
            }
        }

        // ── Request dispatcher ─────────────────────────────────────────────────

        private async Task HandleRequestAsync(HttpListenerContext ctx, CancellationToken ct)
        {
            string method = ctx.Request.HttpMethod;
            string url = ctx.Request.RawUrl ?? "";
            string sn = ctx.Request.QueryString["SN"] ?? "unknown";
            string table = ctx.Request.QueryString["table"] ?? "";

            // Read raw body — binary-safe, works on all target TFMs
            byte[] rawBody;
            using (var ms = new MemoryStream())
            {
                // The 3-arg overload (stream, bufferSize, ct) was added in .NET 4.5,
                // so this is safe across the whole supported range.
                await ctx.Request.InputStream
                    .CopyToAsync(ms, 81920, ct)
                    .ConfigureAwait(false);
                rawBody = ms.ToArray();
            }
            string body = Encoding.UTF8.GetString(rawBody);

            OnRawRequest?.Invoke(this, new RawRequestEventArgs
            {
                Method = method,
                Url = url,
                SN = sn,
                Table = table,
                RawBody = rawBody,
                Body = body,
                Timestamp = DateTime.Now
            });

            try
            {
                if (url.StartsWith("/iclock/getrequest", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleHeartbeatAsync(ctx, sn, ct).ConfigureAwait(false);
                    return;
                }

                if (url.StartsWith("/iclock/cdata", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrEmpty(table))
                {
                    await HandleHandshakeAsync(ctx, sn, ct).ConfigureAwait(false);
                    return;
                }

                if (url.StartsWith("/iclock/cdata", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(table, "ATTLOG", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleAttLogAsync(ctx, sn, body, ct).ConfigureAwait(false);
                    return;
                }

                if (url.StartsWith("/iclock/cdata", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(table, "ATTPHOTO", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleAttPhotoAsync(ctx, sn, body, rawBody, ct).ConfigureAwait(false);
                    return;
                }

                await RespondAsync(ctx, "OK", ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                RaiseError(string.Format("HandleRequest [{0}]", url), ex);
                try { await RespondAsync(ctx, "OK", ct).ConfigureAwait(false); } catch { /* ignore */ }
            }
        }

        // ── Heartbeat ──────────────────────────────────────────────────────────

        private async Task HandleHeartbeatAsync(HttpListenerContext ctx, string sn, CancellationToken ct)
        {
            string info = ctx.Request.QueryString["INFO"] ?? "";

            if (string.IsNullOrEmpty(info))
            {
                await RespondAsync(ctx, "OK", ct).ConfigureAwait(false);
                return;
            }

            string[] parts = info.Split(',');
            int photoCount = 0;
            if (parts.Length > 3) int.TryParse(parts[3], out photoCount);

            OnHeartbeat?.Invoke(this, new HeartbeatEventArgs
            {
                SN = sn,
                Info = info,
                PhotoCount = photoCount,
                Timestamp = DateTime.Now
            });

            string response = photoCount > 0 ? "C:DATA UPDATE ATTPHOTO" : "OK";
            await RespondAsync(ctx, response, ct).ConfigureAwait(false);
        }

        // ── Handshake ──────────────────────────────────────────────────────────

        private async Task HandleHandshakeAsync(HttpListenerContext ctx, string sn, CancellationToken ct)
        {
            OnHandshake?.Invoke(this, new HandshakeEventArgs
            {
                SN = sn,
                Timestamp = DateTime.Now
            });

            // Use string.Format (not $ interpolation) for .NET 4.5 compatibility —
            // although $ strings are a C# 6 language feature (VS 2015+) and DO compile
            // fine on .NET 4.5 with a modern compiler, Format is universally safe.
            string responseBody = string.Format(
                "GET OPTION FROM: {0}\n" +
                "ATTLOGSTAMP=0\n" +
                "OPERLOGSTAMP=0\n" +
                "ATTPHOTOSTAMP={1}\n" +
                "ATTPHOTO=1\n" +
                "ErrorDelay={2}\n" +
                "Delay={3}\n" +
                "TransTimes=00:00;14:05\n" +
                "TransInterval=1\n" +
                "TransFlag=TransData AttLog OpLog AttPhoto\n" +
                "TimeZone={4}\n" +
                "Realtime=1\n" +
                "Encrypt=None\n" +
                "ServerVer=2.4.1 2015-04-14\n" +
                "PushProtVer=2.4.1\n" +
                "PushOptionsFlag=1\n",
                sn, PhotoStamp, ErrorDelay, Delay, TimeZone);

            await RespondAsync(ctx, responseBody, ct).ConfigureAwait(false);
        }

        // ── Attendance log ─────────────────────────────────────────────────────

        private async Task HandleAttLogAsync(
            HttpListenerContext ctx, string sn, string body, CancellationToken ct)
        {
            foreach (string line in body.Split('\n'))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                string[] parts = trimmed.Split('\t');
                if (parts.Length < 2) continue;

                string userId = parts[0].Trim();
                string time = parts[1].Trim();

                int vm = -1;
                int verifyMode = (parts.Length > 3 && int.TryParse(parts[3], out vm)) ? vm : -1;

                int wc = 0;
                int workCode = (parts.Length > 6 && int.TryParse(parts[6], out wc)) ? wc : 0;

                DateTime punchTime;
                DateTime.TryParse(time, out punchTime);

                OnAttendance?.Invoke(this, new AttendanceEventArgs
                {
                    SN = sn,
                    UserId = userId,
                    PunchTime = punchTime,
                    RawTime = time,
                    VerifyMode = verifyMode,
                    WorkCode = workCode,
                    RawLine = trimmed,
                    Timestamp = DateTime.Now
                });
            }

            await RespondAsync(ctx, "OK", ct).ConfigureAwait(false);
        }

        // ── Attendance photo ───────────────────────────────────────────────────

        private async Task HandleAttPhotoAsync(
            HttpListenerContext ctx,
            string sn,
            string body,
            byte[] rawBody,
            CancellationToken ct)
        {
            string uid = "unknown";

            foreach (string line in body.Split('\n'))
            {
                if (!line.StartsWith("PIN=", StringComparison.Ordinal)) continue;

                string pin = line.Substring(4).Trim();
                int dashIdx = pin.LastIndexOf('-');
                int dotIdx = pin.LastIndexOf('.');
                if (dashIdx >= 0 && dotIdx > dashIdx)
                    uid = pin.Substring(dashIdx + 1, dotIdx - dashIdx - 1);
                break;
            }

            // Locate the CMD=uploadphoto marker then scan for JPEG SOI (FF D8 FF)
            byte[] imageBytes = null;
            byte[] marker = Encoding.UTF8.GetBytes("CMD=uploadphoto");
            int markerPos = FindBytes(rawBody, marker);

            if (markerPos >= 0)
            {
                int searchFrom = markerPos + marker.Length;
                for (int i = searchFrom; i < rawBody.Length - 2; i++)
                {
                    if (rawBody[i] == 0xFF && rawBody[i + 1] == 0xD8 && rawBody[i + 2] == 0xFF)
                    {
                        imageBytes = new byte[rawBody.Length - i];
                        Array.Copy(rawBody, i, imageBytes, 0, imageBytes.Length);
                        break;
                    }
                }
            }

            string savedPath = null;
            if (imageBytes != null && imageBytes.Length > 0)
            {
                Directory.CreateDirectory(PhotoSaveDirectory);

                string filename = string.Format(
                    "{0}_{1:yyyy-MM-dd}.jpg", uid, DateTime.Now);
                savedPath = Path.Combine(PhotoSaveDirectory, filename);

                // File.WriteAllBytesAsync was added in .NET Core 2.0 / .NET Standard 2.1.
                // On .NET 4.x we use a FileStream with async write instead.
                await WriteAllBytesAsync(savedPath, imageBytes, ct).ConfigureAwait(false);
            }

            OnPhotoReceived?.Invoke(this, new PhotoEventArgs
            {
                SN = sn,
                UserId = uid,
                SavedPath = savedPath,
                ImageBytes = imageBytes,
                Success = savedPath != null,
                Timestamp = DateTime.Now
            });

            await RespondAsync(ctx, "OK", ct).ConfigureAwait(false);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Polyfill for File.WriteAllBytesAsync that works on all TFMs from 4.5 onwards.
        /// On .NET 5+ the BCL version is available but using our own is equally correct.
        /// </summary>
        private static async Task WriteAllBytesAsync(
            string path, byte[] bytes, CancellationToken ct)
        {
            // FileOptions.Asynchronous tells Windows to use overlapped I/O,
            // which is important for truly async writes on .NET 4.x.
            using (var fs = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await fs.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
            }
        }

        private static int FindBytes(byte[] haystack, byte[] needle)
        {
            int limit = haystack.Length - needle.Length;
            for (int i = 0; i <= limit; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }

        private static async Task RespondAsync(
            HttpListenerContext ctx, string body, CancellationToken ct)
        {
            byte[] buf = Encoding.UTF8.GetBytes(body);
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            ctx.Response.ContentLength64 = buf.Length;
            await ctx.Response.OutputStream
                .WriteAsync(buf, 0, buf.Length, ct)
                .ConfigureAwait(false);
            ctx.Response.OutputStream.Close();
        }

        private void RaiseError(string source, Exception ex) =>
            OnError?.Invoke(this, new ErrorEventArgs
            {
                Source = source,
                Exception = ex,
                Timestamp = DateTime.Now
            });
    }
}