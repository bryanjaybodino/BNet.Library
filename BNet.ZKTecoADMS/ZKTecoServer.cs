using System;
using System.Collections.Generic;
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
    /// Targets .NET Framework 4.5 - 4.8 and .NET 5 - 8 without any third-party DLLs.
    /// Subscribe to events and call StartAsync() / Start() to begin receiving data.
    ///
    /// MB460 Plus firmware note:
    ///   This device always sends PunchState=4 via ADMS Push regardless of what
    ///   punch type is configured on the device. The actual punch type is carried
    ///   in the VerifyMode field (column [2]):
    ///     0 = Check-In
    ///     1 = Check-Out
    ///   The server resolves this via ResolvePunchType() and caches the result
    ///   per user so that the attendance photo filename includes the punch type.
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

        /// <summary>Fired when a photo is received (success or failure).</summary>
        public event EventHandler<PhotoEventArgs> OnPhotoReceived;

        /// <summary>Fired on every device heartbeat.</summary>
        public event EventHandler<HeartbeatEventArgs> OnHeartbeat;

        /// <summary>Fired when any internal error occurs (non-fatal).</summary>
        public event EventHandler<ErrorEventArgs> OnError;

        /// <summary>Fired for every raw request — useful for debugging.</summary>
        public event EventHandler<RawRequestEventArgs> OnRawRequest;

        // ── Private state ──────────────────────────────────────────────────────

        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private Task _listenTask;

        // ── Last punch type cache ──────────────────────────────────────────────
        // Key: userId
        // Stores the most recently resolved PunchType per user so that the
        // attendance photo filename can include the punch type label.

        private readonly Dictionary<string, PunchType> _lastPunchType = new Dictionary<string, PunchType>();
        private readonly object _punchTypeLock = new object();

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
                    HttpListenerContext ctx = await _listener
                        .GetContextAsync()
                        .ConfigureAwait(false);

                    _ = Task.Run(async () =>
                    {
                        using (var requestCts =
                            CancellationTokenSource.CreateLinkedTokenSource(ct))
                        {
                            requestCts.CancelAfter(TimeSpan.FromSeconds(60));
                            try
                            {
                                await HandleRequestAsync(ctx, requestCts.Token)
                                    .ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                                when (!ct.IsCancellationRequested)
                            {
                                RaiseError("RequestTimeout", new TimeoutException(
                                    "Request timed out after 60s"));
                                try { ctx.Response.Abort(); } catch { }
                            }
                            catch (Exception ex)
                            {
                                // ← THIS was missing — unhandled exceptions here
                                // were silently leaving contexts open
                                RaiseError("RequestHandler", ex);
                                try { ctx.Response.Abort(); } catch { }
                            }
                        }
                    }, ct);
                }
                catch (HttpListenerException)
                    when (ct.IsCancellationRequested)
                { break; }
                catch (ObjectDisposedException)
                    when (ct.IsCancellationRequested)
                { break; }
                catch (Exception ex)
                {
                    RaiseError("ListenLoop", ex);
                    // ← Add a small delay so a repeated error doesn't
                    // spin the CPU at 100% if the listener goes bad
                    await Task.Delay(1000, ct).ConfigureAwait(false);
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

            byte[] rawBody;
            using (var ms = new MemoryStream())
            {
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

            // INFO-less /iclock/getrequest is the device's normal keep-alive poll
            // (sent every few seconds while idle). We must go through RespondAsync
            // so the response is properly flushed+closed; a half-open TCP socket
            // here causes the device to show the double-arrows reconnect icon and
            // drop the first punch that arrives after an idle period.
            if (string.IsNullOrEmpty(info))
            {
                await RespondAsync(ctx, "OK", ct).ConfigureAwait(false);
                return;
            }

            // INFO format (MB460 Plus, 13 comma-separated fields):
            //  [0]  Firmware version  e.g. ZMM501-NF28VF-Ver2.2.1
            //  [1]  Unknown
            //  [2]  Unknown
            //  [3]  Pending photo count
            //  [4]  Device IP
            //  [5-12] Unknown
            string[] parts = info.Split(',');
            int photoCount = 0;
            if (parts.Length > 3)
                int.TryParse(parts[3], out photoCount);

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

            string responseBody = string.Format(
                "GET OPTION FROM: {0}\r\n" +
                "ATTLOGSTAMP=0\r\n" +
                "OPERLOGSTAMP=0\r\n" +
                "ATTPHOTOSTAMP={1}\r\n" +
                "ATTPHOTO=1\r\n" +
                "ErrorDelay={2}\r\n" +
                "Delay={3}\r\n" +
                "TransTimes=00:00;14:05\r\n" +
                "TransInterval=1\r\n" +
                "TransFlag=TransData AttLog OpLog AttPhoto\r\n" +
                "TimeZone={4}\r\n" +
                "Realtime=1\r\n" +
                "Encrypt=None\r\n" +
                "ServerVer=2.4.1 2015-04-14\r\n" +
                "PushProtVer=2.4.1\r\n" +
                "PushOptionsFlag=1\r\n",
                sn, PhotoStamp, ErrorDelay, Delay, TimeZone);

            await RespondAsync(ctx, responseBody, ct).ConfigureAwait(false);
        }

        // ── Attendance log ─────────────────────────────────────────────────────

        private async Task HandleAttLogAsync(
            HttpListenerContext ctx, string sn, string body, CancellationToken ct)
        {
            // ATTLOG tab-delimited column layout — MB460 Plus firmware (11 columns):
            //
            //  [0]  UserID      — enrolled user / employee ID
            //  [1]  DateTime    — punch timestamp (yyyy-MM-dd HH:mm:ss)
            //  [2]  VerifyMode  — on MB460 Plus this carries the punch type:
            //                       0 = Check-In
            //                       1 = Check-Out
            //                       4 = Overtime-In
            //                       5 = Overtime-Out
            //                     On standard firmware it is the auth method:
            //                       1 = Fingerprint  4 = Password
            //                       5 = Palm        15 = Face  255 = Face(FF)
            //  [3]  PunchState  — MB460 Plus ALWAYS sends 4 here via ADMS Push.
            //                     Standard firmware: 0=In 1=Out 2=OTIn 3=OTOut
            //  [4]  WorkCode    — optional (0 when absent)
            //  [5-9] Reserved
            //  [10] SeqNo       — incrementing punch sequence counter

            foreach (string line in body.Split('\n'))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                string[] parts = trimmed.Split('\t');

                string userId = parts[0].Trim();
                string time = parts[1].Trim();

                // [2] VerifyMode
                int vm = -1;
                int verifyMode = (parts.Length > 2 && int.TryParse(parts[2], out vm)) ? vm : -1;

                // [3] PunchState — raw value from device
                int ps = -1;
                int punchState = (parts.Length > 3 && int.TryParse(parts[3], out ps)) ? ps : -1;

                // [4] WorkCode
                int wc = 0;
                int workCode = (parts.Length > 4 && int.TryParse(parts[4], out wc)) ? wc : 0;

                DateTime punchTime;
                DateTime.TryParse(time, out punchTime);

                // Resolve the exact punch type from the device.
                // MB460 Plus: PunchState is always 4; actual type is in VerifyMode.
                // Standard firmware: PunchState carries the type directly.
                PunchType resolvedType = ResolvePunchType(verifyMode);

                // Cache the resolved type so HandleAttPhotoAsync can use it for the filename.
                lock (_punchTypeLock)
                    _lastPunchType[userId] = resolvedType;

                OnAttendance?.Invoke(this, new AttendanceEventArgs
                {
                    SN = sn,
                    UserId = userId,
                    PunchTime = punchTime,
                    RawTime = time,
                    VerifyMode = verifyMode,
                    PunchState = punchState,
                    PunchType = resolvedType,
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
            // PIN filename format from MB460 Plus: yyyyMMddHHmmss-userId.jpg
            // e.g. PIN=20260527095423-1.jpg  →  userId = "1"
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

            // Locate JPEG by scanning for the SOI marker (FF D8 FF) after "CMD=uploadphoto"
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

                // Look up the last resolved punch type for this user.
                PunchType punchType = PunchType.Unknown;
                lock (_punchTypeLock)
                {
                    PunchType cached;
                    if (_lastPunchType.TryGetValue(uid, out cached))
                        punchType = cached;
                }

                // Filename: userId_yyyy-MM-dd_HHmmss_PunchType.jpg
                // e.g. 1_2026-05-27_162006_CheckOut.jpg
                string filename = string.Format(
                    "{0}_{1:yyyy-MM-dd}_{2}.jpg",
                    uid, DateTime.Now, punchType);

                savedPath = Path.Combine(PhotoSaveDirectory, filename);
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

        // ── Punch type resolver ────────────────────────────────────────────────

        /// <summary>
        /// Resolves the exact logical punch type from what the device sends.
        ///
        /// MB460 Plus firmware always sends PunchState=4 via ADMS Push.
        /// On this device the actual punch type selected by the employee is
        /// carried in the VerifyMode field (column [2]):
        ///   0 = Check-In
        ///   1 = Check-Out
        ///   4 = Overtime-In
        ///   5 = Overtime-Out
        ///
        /// Standard firmware sends the punch type directly in PunchState (0-3)
        /// and uses VerifyMode for the authentication method.
        /// </summary>
        private PunchType ResolvePunchType(int verifyMode)
        {
            switch (verifyMode)
            {
                case 0: return PunchType.CheckIn;
                case 1: return PunchType.CheckOut;
                case 4: return PunchType.OvertimeIn;
                case 5: return PunchType.OvertimeOut;
                default: return PunchType.Unknown;
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static async Task WriteAllBytesAsync(
            string path, byte[] bytes, CancellationToken ct)
        {
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
                    if (haystack[i + j] != needle[j]) { match = false; break; }
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

            ctx.Response.Headers["Connection"] = "keep-alive";

            await ctx.Response.OutputStream
                .WriteAsync(buf, 0, buf.Length, ct)
                .ConfigureAwait(false);

            ctx.Response.OutputStream.Flush();
            ctx.Response.OutputStream.Close();
            ctx.Response.Close();
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