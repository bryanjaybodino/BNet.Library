using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BNet.WebSocket.Server
{
    class MyClients
    {
        public Stream Stream { get; set; }
        public HashSet<string> Rooms { get; set; } = new HashSet<string>();
        public SemaphoreSlim WriteLock { get; } = new SemaphoreSlim(1, 1);
        public DateTime LastPingTime { get; set; } = DateTime.Now;
        public bool IsAlive { get; set; } = true;
    }

    public class Connection : EventHandlers
    {
        private TcpListener _listener;
        private ConcurrentDictionary<TcpClient, MyClients> _clients = new ConcurrentDictionary<TcpClient, MyClients>();
        public bool IsRunning { get; private set; }
        private X509Certificate2 _serverCertificate;

        // Server stats
        public DateTime StartTime { get; private set; }
        public int TotalConnectionsHandled { get; private set; } = 0;

        // Cloudflare Tunnel optimization
        private const int PingIntervalSeconds = 30;
        private const int ConnectionTimeoutSeconds = 120;
        private CancellationTokenSource _healthCheckCts;

        public Connection(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            StartTime = DateTime.Now;
            _healthCheckCts = new CancellationTokenSource();
        }

        public void LoadCertificate(string path, string password)
        {
            _serverCertificate = new X509Certificate2(
                path,
                password,
                X509KeyStorageFlags.MachineKeySet |
                X509KeyStorageFlags.PersistKeySet |
                X509KeyStorageFlags.Exportable
            );
        }

        public void LoadCertificate(byte[] rawData, string password)
        {
            _serverCertificate = new X509Certificate2(
                rawData,
                password,
                X509KeyStorageFlags.MachineKeySet |
                X509KeyStorageFlags.PersistKeySet |
                X509KeyStorageFlags.Exportable
            );
        }

        public async Task StartAsync()
        {
            try
            {
                IsRunning = true;
                _listener.Start();
                Console.WriteLine("🚀 WebSocket Server started. Waiting for clients...");

                _ = Task.Run(HealthCheckLoop, _healthCheckCts.Token);

                while (IsRunning)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    client.NoDelay = true; // Disable Nagle's algorithm for faster WebSocket response times
                    _ = Task.Run(async () => await HandleClientAsync(client));
                }
            }
            catch (Exception ex)
            {
                await SetOnError($"StartAsync: {ex.Message}");
            }
        }

        private async Task HealthCheckLoop()
        {
            while (IsRunning)
            {
                try
                {
                    await Task.Delay(10000, _healthCheckCts.Token);

                    var deadClients = new List<TcpClient>();

                    foreach (var kvp in _clients)
                    {
                        var client = kvp.Key;
                        var myClient = kvp.Value;

                        if (!client.Connected || !myClient.IsAlive)
                        {
                            deadClients.Add(client);
                            continue;
                        }

                        if ((DateTime.Now - myClient.LastPingTime).TotalSeconds > ConnectionTimeoutSeconds)
                        {
                            Console.WriteLine($"⏱️ Connection timeout after {ConnectionTimeoutSeconds}s");
                            deadClients.Add(client);
                        }
                    }

                    foreach (var client in deadClients)
                    {
                        await RemoveClientAsync(client);
                    }
                }
                catch (TaskCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Health check error: {ex.Message}");
                }
            }
        }

        public async Task SendMessageAsync(string message)
        {
            var clients = _clients.Values.Distinct().ToList();
            var tasks = clients.Select(client => WriteTextAsync(client, message)).ToArray();
            await Task.WhenAll(tasks);
        }

        public Task SendMessageToRoomAsync(string roomId, string message)
        {
            var clients = _clients.Values
                .Where(c => c.Rooms.Contains(roomId))
                .Distinct()
                .ToList();

            var tasks = clients.Select(client => WriteTextAsync(client, message)).ToArray();
            return Task.WhenAll(tasks);
        }

        public async Task SendBinaryAsync(byte[] data)
        {
            var clients = _clients.Values.Distinct().ToList();
            var tasks = clients.Select(client => WriteBinaryAsync(client, data)).ToArray();
            await Task.WhenAll(tasks);
        }

        public Task SendBinaryToRoomAsync(string roomId, byte[] data)
        {
            var clients = _clients.Values
                .Where(c => c.Rooms.Contains(roomId))
                .Distinct()
                .ToList();

            var tasks = clients.Select(client => WriteBinaryAsync(client, data)).ToArray();
            return Task.WhenAll(tasks);
        }

        private void JoinRoom(string roomId, TcpClient client)
        {
            if (_clients.TryGetValue(client, out var myClient))
            {
                myClient.Rooms.Add(roomId);
                Console.WriteLine($"📍 Client joined room: {roomId}");
            }
        }

        public async Task StopAsync()
        {
            try
            {
                IsRunning = false;
                _healthCheckCts.Cancel();
                _listener.Stop();

                var tasks = _clients.Keys.Select(async client =>
                {
                    await RemoveClientAsync(client);
                    return Task.FromResult(0);
                }).ToArray();

                await Task.WhenAll(tasks);
                Console.WriteLine("✅ Server stopped gracefully");
            }
            catch (Exception ex)
            {
                await SetOnError($"StopAsync: {ex.Message}");
            }
        }

        private async Task<Stream> HandleSecurityAsync(Stream stream)
        {
            if (_serverCertificate == null)
            {
                return stream;
            }

            try
            {
                var sslStream = new SslStream(stream, false);
                await sslStream.AuthenticateAsServerAsync(
                    _serverCertificate,
                    clientCertificateRequired: false,
                    enabledSslProtocols: SslProtocols.Tls12 | (SslProtocols)3072,
                    checkCertificateRevocation: false
                );
                return sslStream;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SSL/TLS Error: {ex.Message}");
                return null;
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (NetworkStream networkStream = client.GetStream())
                {
                    using (Stream secureStream = await HandleSecurityAsync(networkStream))
                    {
                        if (secureStream == null)
                            return;

                        if (_clients.ContainsKey(client))
                            return;

                        var myClient = new MyClients { Stream = secureStream };
                        if (!_clients.TryAdd(client, myClient))
                            return;

                        await HandleStartupAsync(client, myClient);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Client error: {ex.Message}");
            }
            finally
            {
                await RemoveClientAsync(client);
            }
        }

        private async Task HandleStartupAsync(TcpClient client, MyClients myClient)
        {
            Stream stream = myClient.Stream;
            string handshakeRequest = await ReadRequestAsync(client, stream);

            var firstLine = handshakeRequest.Split(new[] { "\r\n" }, StringSplitOptions.None).FirstOrDefault() ?? "";
            Console.WriteLine($"📨 Incoming request: {firstLine}");

            if (IsWebSocketHandshake(handshakeRequest, out string key))
            {
                Console.WriteLine("🔗 ✅ WebSocket handshake recognized!");
                await SendHandshakeResponseAsync(stream, key);

                string roomId = ExtractRoomIdFromRequest(handshakeRequest);
                if (!string.IsNullOrEmpty(roomId))
                {
                    JoinRoom(roomId, client);
                }

                var clients = _clients.Values.Distinct().ToList();
                await SetOnConnectedClient(clients.Count);

                _ = Task.Run(() => PingLoopAsync(client, myClient));

                const string BinaryMarker = " BIN ";

                while (client.Connected && myClient.IsAlive)
                {
                    string message = await ReadMessageAsync(client, stream);

                    if (message == null)
                    {
                        continue;
                    }
                    else if (message == string.Empty)
                    {
                        return;
                    }
                    else if (message == "Unexpected frame type received")
                    {
                        continue;
                    }
                    else if (message.StartsWith(BinaryMarker))
                    {
                        string json = message.Substring(BinaryMarker.Length);
                        byte[] raw = Encoding.UTF8.GetBytes(json);

                        if (string.IsNullOrEmpty(roomId))
                        {
                            await SendBinaryAsync(raw);
                        }
                        else
                        {
                            await SendBinaryToRoomAsync(roomId, raw);
                        }

                        _ = Task.Run(() => SetOnBinaryReceived(raw));
                    }
                    else if (!string.IsNullOrWhiteSpace(message))
                    {
                        await SetOnReceived(message);

                        if (string.IsNullOrEmpty(roomId))
                        {
                            await SendMessageAsync(message);
                        }
                        else
                        {
                            await SendMessageToRoomAsync(roomId, message);
                        }
                    }
                }
            }
            else if (IsHttpRequest(handshakeRequest))
            {
                Console.WriteLine("🌐 HTTP request detected - serving status page");
                await SendHttpStatusResponseAsync(stream, handshakeRequest);
            }
            else
            {
                Console.WriteLine("❌ Invalid request - not WebSocket or HTTP");
                throw new Exception("Invalid request - not WebSocket or HTTP.");
            }
        }

        private async Task PingLoopAsync(TcpClient client, MyClients myClient)
        {
            try
            {
                while (client.Connected && myClient.IsAlive && _clients.ContainsKey(client))
                {
                    await Task.Delay(PingIntervalSeconds * 1000);

                    if (!client.Connected || !myClient.IsAlive)
                        break;

                    byte[] pingFrame = CreatePingFrame();

                    // Fixed: Secure stream writes using WriteLock
                    await myClient.WriteLock.WaitAsync();
                    try
                    {
                        await myClient.Stream.WriteAsync(pingFrame, 0, pingFrame.Length);
                        await myClient.Stream.FlushAsync();
                    }
                    finally
                    {
                        myClient.WriteLock.Release();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ping loop error: {ex.Message}");
                myClient.IsAlive = false;
            }
        }

        private byte[] CreatePingFrame()
        {
            byte[] frame = new byte[2];
            frame[0] = 0x89; // FIN=1, Opcode=9 (Ping)
            frame[1] = 0x00; // Mask=0, Payload=0
            return frame;
        }

        private bool IsHttpRequest(string request)
        {
            var lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0) return false;

            var firstLine = lines[0];
            return firstLine.StartsWith("GET") || firstLine.StartsWith("POST") || firstLine.StartsWith("HEAD");
        }

        private async Task SendHttpStatusResponseAsync(Stream stream, string request)
        {
            try
            {
                var uptime = DateTime.Now - StartTime;
                var clientCount = _clients.Count;

                var lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);
                var requestLine = lines[0];

                string hostHeader = lines.FirstOrDefault(line =>
                    line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase));

                string host = hostHeader != null ? hostHeader.Substring("Host:".Length).Trim() : "localhost";
                string wsUrl = $"wss://{host}";

                string htmlBody = "<!DOCTYPE html><html><head><meta charset='UTF-8'><meta name='viewport' content='width=device-width'><title>Server</title><style>body{margin:0;padding:10px;background:#f5f5f5;font-family:Arial;font-size:14px}.c{max-width:400px;margin:0 auto;background:#fff;padding:15px;border-radius:4px;box-shadow:0 1px 2px rgba(0,0,0,.1)}.s{color:#16a34a;font-weight:bold;font-size:18px;text-align:center;margin:5px 0}.d{padding:5px 0;border-bottom:1px solid #eee}</style></head><body><div class='c'><div class='s'>✅ ONLINE</div><div class='d'><b>Connections:</b> " + clientCount + "</div><div class='d'><b>Uptime:</b> " + uptime.Days + "d " + uptime.Hours + "h " + uptime.Minutes + "m</div><div style='margin-top:10px;font-size:12px;color:#666'>" + wsUrl + "</div><div style='text-align:center;margin-top:10px;font-size:11px;color:#999'>Ping: " + PingIntervalSeconds + "s</div></div></body></html>";

                string response =
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: text/html; charset=UTF-8\r\n" +
                    $"Content-Length: {Encoding.UTF8.GetByteCount(htmlBody)}\r\n" +
                    "Connection: close\r\n" +
                    "Cache-Control: no-cache\r\n" +
                    "Access-Control-Allow-Origin: *\r\n" +
                    "\r\n";

                byte[] responseBytes = Encoding.UTF8.GetBytes(response + htmlBody);
                await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                await stream.FlushAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error sending status: {ex.Message}");
            }
        }

        private async Task<string> ReadRequestAsync(TcpClient client, Stream stream)
        {
            var requestBuilder = new StringBuilder();
            var buffer = new byte[8192];
            int bytesRead;

            try
            {
                do
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        requestBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                    }
                } while (bytesRead > 0 && !requestBuilder.ToString().EndsWith("\r\n\r\n"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading request: {ex.Message}");
            }

            return requestBuilder.ToString();
        }

        private bool IsWebSocketHandshake(string request, out string key)
        {
            key = null;
            string lowerRequest = request.ToLower();
            if (!lowerRequest.Contains("upgrade:") || !lowerRequest.Contains("websocket"))
                return false;

            var lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (line.IndexOf("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    key = line.Substring("Sec-WebSocket-Key:".Length).Trim();
                    return true;
                }
            }
            return false;
        }

        private async Task SendHandshakeResponseAsync(Stream stream, string key)
        {
            string CalculateWebSocketAcceptKey()
            {
                var magicGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                using (var sha1 = SHA1.Create())
                {
                    var keyBytes = Encoding.UTF8.GetBytes(key + magicGuid);
                    var hashBytes = sha1.ComputeHash(keyBytes);
                    return Convert.ToBase64String(hashBytes);
                }
            }

            string response =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Accept: {CalculateWebSocketAcceptKey()}\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "\r\n";

            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
            await stream.FlushAsync();
        }

        private string ExtractRoomIdFromRequest(string request)
        {
            var lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);
            var requestLine = lines.FirstOrDefault();
            if (requestLine == null) return null;

            var requestParts = requestLine.Split(' ');
            if (requestParts.Length < 2) return null;

            var url = requestParts[1];
            if (!url.Contains("?")) return null;

            string queryContent = url.Substring(url.IndexOf('?') + 1);
            foreach (var part in queryContent.Split('&'))
            {
                var kv = part.Split(new[] { '=' }, 2);
                if (kv.Length == 2 && kv[0].Equals("room", StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(kv[1]);
                }
            }

            return null;
        }

        private async Task<byte[]> ReadExactBytesAsync(Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            int totalBytesRead = 0;
            while (totalBytesRead < count)
            {
                int read = await stream.ReadAsync(buffer, totalBytesRead, count - totalBytesRead);
                if (read == 0) return null;
                totalBytesRead += read;
            }
            return buffer;
        }

        private async Task<string> ReadMessageAsync(TcpClient client, Stream stream)
        {
            try
            {
                byte[] header = await ReadExactBytesAsync(stream, 2);
                if (header == null) return string.Empty;

                bool isFinalFragment = (header[0] & 0x80) != 0;
                byte opcode = (byte)(header[0] & 0x0F);
                bool isMasked = (header[1] & 0x80) != 0;
                long payloadLength = header[1] & 0x7F;

                if (payloadLength == 126)
                {
                    byte[] extLen = await ReadExactBytesAsync(stream, 2);
                    if (extLen == null) return string.Empty;
                    payloadLength = (extLen[0] << 8) | extLen[1];
                }
                else if (payloadLength == 127)
                {
                    byte[] extLen = await ReadExactBytesAsync(stream, 8);
                    if (extLen == null) return string.Empty;
                    payloadLength = BitConverter.ToInt64(extLen.Reverse().ToArray(), 0);
                }

                byte[] maskingKey = null;
                if (isMasked)
                {
                    maskingKey = await ReadExactBytesAsync(stream, 4);
                    if (maskingKey == null) return string.Empty;
                }

                byte[] payload = payloadLength > 0 ? await ReadExactBytesAsync(stream, (int)payloadLength) : new byte[0];
                if (payload == null && payloadLength > 0) return string.Empty;

                if (isMasked && payloadLength > 0)
                {
                    for (int i = 0; i < payload.Length; i++)
                    {
                        payload[i] ^= maskingKey[i % 4];
                    }
                }

                switch (opcode)
                {
                    case 1: // Text
                        return Encoding.UTF8.GetString(payload);
                    case 2: // Binary
                        return " BIN " + Encoding.UTF8.GetString(payload);
                    case 8: // Close
                        if (_clients.TryGetValue(client, out var myClientClose))
                            myClientClose.IsAlive = false;
                        return string.Empty;
                    case 9: // Ping
                        byte[] pongFrame = CreatePongFrame(payload);
                        if (_clients.TryGetValue(client, out var myClientPing))
                        {
                            await myClientPing.WriteLock.WaitAsync();
                            try
                            {
                                await stream.WriteAsync(pongFrame, 0, pongFrame.Length);
                                await stream.FlushAsync();
                            }
                            finally { myClientPing.WriteLock.Release(); }
                        }
                        return null;
                    case 10: // Pong
                        if (_clients.TryGetValue(client, out var myClientPong))
                        {
                            // Fixed: Correctly register client responses
                            myClientPong.LastPingTime = DateTime.Now;
                        }
                        return null;
                    default:
                        return "Unexpected frame type received";
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private byte[] CreatePongFrame(byte[] payload)
        {
            byte[] frame = new byte[2 + payload.Length];
            frame[0] = 0x8A; // FIN=1, Opcode=10 (Pong)
            frame[1] = (byte)payload.Length;
            Array.Copy(payload, 0, frame, 2, payload.Length);
            return frame;
        }

        private async Task WriteTextAsync(MyClients client, string message)
        {
            byte[] payload = Encoding.UTF8.GetBytes(message);
            byte[] frame = BuildFrame(0x81, payload);
            await client.WriteLock.WaitAsync();
            try
            {
                await client.Stream.WriteAsync(frame, 0, frame.Length);
                await client.Stream.FlushAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing text: {ex.Message}");
                client.IsAlive = false;
            }
            finally
            {
                client.WriteLock.Release();
            }
        }

        private async Task WriteBinaryAsync(MyClients client, byte[] payload)
        {
            byte[] frame = BuildFrame(0x82, payload);
            await client.WriteLock.WaitAsync();
            try
            {
                await client.Stream.WriteAsync(frame, 0, frame.Length);
                await client.Stream.FlushAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing binary: {ex.Message}");
                client.IsAlive = false;
            }
            finally
            {
                client.WriteLock.Release();
            }
        }

        private byte[] BuildFrame(byte opcodeByte, byte[] payload)
        {
            int payloadLength = payload.Length;
            byte[] frame;

            if (payloadLength <= 125)
            {
                frame = new byte[2 + payloadLength];
                frame[1] = (byte)payloadLength;
            }
            else if (payloadLength <= 65535)
            {
                frame = new byte[4 + payloadLength];
                frame[1] = 126;
                frame[2] = (byte)(payloadLength >> 8);
                frame[3] = (byte)payloadLength;
            }
            else
            {
                frame = new byte[10 + payloadLength];
                frame[1] = 127;
                frame[2] = (byte)(payloadLength >> 56);
                frame[3] = (byte)(payloadLength >> 48);
                frame[4] = (byte)(payloadLength >> 40);
                frame[5] = (byte)(payloadLength >> 32);
                frame[6] = (byte)(payloadLength >> 24);
                frame[7] = (byte)(payloadLength >> 16);
                frame[8] = (byte)(payloadLength >> 8);
                frame[9] = (byte)payloadLength;
            }

            frame[0] = opcodeByte;
            Array.Copy(payload, 0, frame, frame.Length - payloadLength, payloadLength);
            return frame;
        }

        private async Task RemoveClientAsync(TcpClient client)
        {
            if (_clients.TryRemove(client, out var myClient))
            {
                try
                {
                    myClient.IsAlive = false;
                    myClient.Stream?.Dispose();
                    client?.Close();
                }
                catch { }
                TotalConnectionsHandled++;
            }
            var clients = _clients.Values.Distinct().ToList();
            await SetOnDisconnectedClient(clients.Count);
        }
    }
}