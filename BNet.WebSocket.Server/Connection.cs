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
        private const int PingIntervalSeconds = 45;          // Increased from 25 to 45 (less aggressive)
        private const int ConnectionTimeoutSeconds = 180;    // Increased from 90 to 180 (more forgiving)
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

                // Start health check task
                _ = Task.Run(HealthCheckLoop, _healthCheckCts.Token);

                while (IsRunning)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    client.ReceiveTimeout = 60000;
                    client.SendTimeout = 60000;
                    _ = Task.Run(async () => await HandleClientAsync(client));
                }
            }
            catch (Exception ex)
            {
                await SetOnError($"StartAsync: {ex.Message}");
            }
        }

        // Health check - monitors connections and removes dead ones
        private async Task HealthCheckLoop()
        {
            while (IsRunning)
            {
                try
                {
                    await Task.Delay(10000); // Check every 10 seconds (was 5)

                    var deadClients = new List<TcpClient>();

                    foreach (var kvp in _clients)
                    {
                        var client = kvp.Key;
                        var myClient = kvp.Value;

                        // Only remove if CLEARLY dead - not just on timeout
                        if (!client.Connected || !myClient.IsAlive)
                        {
                            deadClients.Add(client);
                            continue;
                        }

                        // More lenient timeout - 180 seconds (3 minutes)
                        if ((DateTime.Now - myClient.LastPingTime).TotalSeconds > ConnectionTimeoutSeconds)
                        {
                            Console.WriteLine($"⏱️  Connection timeout after {ConnectionTimeoutSeconds}s");
                            deadClients.Add(client);
                        }
                    }

                    // Remove dead clients
                    foreach (var client in deadClients)
                    {
                        await RemoveClientAsync(client);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Health check error: {ex.Message}");
                }
            }
        }

        // Send text to all clients
        public async Task SendMessageAsync(string message)
        {
            var clients = _clients.Values.Distinct().ToList();
            var tasks = clients.Select(client => WriteTextAsync(client, message)).ToArray();
            await Task.WhenAll(tasks);
        }

        // Send text to a room
        public Task SendMessageToRoomAsync(string roomId, string message)
        {
            var clients = _clients.Values
                .Where(c => c.Rooms.Contains(roomId))
                .Distinct()
                .ToList();

            var tasks = clients.Select(client => WriteTextAsync(client, message)).ToArray();
            return Task.WhenAll(tasks);
        }

        // Send binary to all clients
        public async Task SendBinaryAsync(byte[] data)
        {
            var clients = _clients.Values.Distinct().ToList();
            var tasks = clients.Select(client => WriteBinaryAsync(client, data)).ToArray();
            await Task.WhenAll(tasks);
        }

        // Send binary to a room
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
                    enabledSslProtocols: SslProtocols.Tls12,
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

                        if (!_clients.TryAdd(client, new MyClients { Stream = secureStream }))
                            return;

                        await HandleStartupAsync(client, secureStream);
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

        private async Task HandleStartupAsync(TcpClient client, Stream stream)
        {
            string handshakeRequest = await ReadRequestAsync(client, stream);

            var firstLine = handshakeRequest.Split(new[] { "\r\n" }, StringSplitOptions.None).FirstOrDefault() ?? "";
            Console.WriteLine($"📨 Incoming request: {firstLine}");

            if (IsWebSocketHandshake(handshakeRequest, out string key))
            {
                // Handle WebSocket connection
                Console.WriteLine("🔗 ✅ WebSocket handshake recognized!");
                await SendHandshakeResponseAsync(stream, key);

                string roomId = ExtractRoomIdFromRequest(handshakeRequest);
                if (!string.IsNullOrEmpty(roomId))
                {
                    JoinRoom(roomId, client);
                }

                var clients = _clients.Values.Distinct().ToList();
                await SetOnConnectedClient(clients.Count);

                // Start ping loop for this connection (Cloudflare Tunnel compatibility)
                _ = Task.Run(() => PingLoopAsync(client, stream));

                const string BinaryMarker = " BIN ";

                while (client.Connected)
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
                        // ignore unknown opcodes
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
                    else if (message.Replace(" ", "") != "")
                    {
                        // Only broadcast once, not both to SetOnReceived and SendMessage
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

                throw new Exception("Client Disconnected");
            }
            else if (IsHttpRequest(handshakeRequest))
            {
                // Handle regular HTTP request (status check)
                Console.WriteLine("🌐 HTTP request detected - serving status page");
                await SendHttpStatusResponseAsync(stream, handshakeRequest);
            }
            else
            {
                Console.WriteLine("❌ Invalid request - not WebSocket or HTTP");
                Console.WriteLine($"Request headers:\n{handshakeRequest.Substring(0, Math.Min(500, handshakeRequest.Length))}");
                throw new Exception("Invalid request - not WebSocket or HTTP.");
            }
        }

        // NEW: Ping loop for Cloudflare Tunnel compatibility
        private async Task PingLoopAsync(TcpClient client, Stream stream)
        {
            try
            {
                while (client.Connected && _clients.ContainsKey(client))
                {
                    await Task.Delay(PingIntervalSeconds * 1000);

                    if (!client.Connected || !_clients.ContainsKey(client))
                        break;

                    try
                    {
                        byte[] pingFrame = CreatePingFrame();
                        await stream.WriteAsync(pingFrame, 0, pingFrame.Length);
                        await stream.FlushAsync();

                        if (_clients.TryGetValue(client, out var myClient))
                        {
                            myClient.LastPingTime = DateTime.Now;
                        }

                        // Debug: ping sent
                        // Console.WriteLine($"📍 Ping sent");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️  Ping error: {ex.Message}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ping loop error: {ex.Message}");
            }
        }

        // Create a proper WebSocket ping frame
        private byte[] CreatePingFrame()
        {
            byte[] frame = new byte[2];
            frame[0] = 0x89; // FIN=1, RSV=0, OPCODE=9 (Ping)
            frame[1] = 0x00; // Mask=0, Payload length=0
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
                var path = requestLine.Split(' ').Length > 1 ? requestLine.Split(' ')[1] : "/";

                // Extract the Host header
                string host = "localhost"; // Default fallback
                string hostHeader = lines.FirstOrDefault(line =>
                    line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase));

                if (hostHeader != null)
                {
                    host = hostHeader.Substring("Host:".Length).Trim();
                }

                // Build the WebSocket URL using the actual host
                string wsProtocol = hostHeader?.Contains(":443") ?? false ? "wss" : "wss"; // Default to wss
                string wsUrl = $"{wsProtocol}://{host}";

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

                Console.WriteLine($"📊 Status page served - {clientCount} active connections - Host: {host}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error sending status: {ex.Message}");
            }
        }

        private async Task<string> ReadRequestAsync(TcpClient client, Stream stream)
        {
            var requestBuilder = new StringBuilder();
            var buffer = new byte[client.ReceiveBufferSize];
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

            // Case-insensitive check for WebSocket upgrade (Cloudflare compatibility)
            string lowerRequest = request.ToLower();
            if (!lowerRequest.Contains("upgrade:") || !lowerRequest.Contains("websocket"))
                return false;
            if (!lowerRequest.Contains("connection:") || !lowerRequest.Contains("upgrade"))
                return false;

            var lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                // Case-insensitive header matching
                if (line.IndexOf("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    key = line.Substring("Sec-WebSocket-Key:".Length).Trim();
                    Console.WriteLine($"✅ WebSocket handshake detected - Key: {key.Substring(0, Math.Min(10, key.Length))}...");
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

            string hostHeader = lines.FirstOrDefault(line => line.StartsWith("Host:"));
            if (hostHeader == null)
                throw new NotSupportedException("Host header not found in request.");

            var hostParts = hostHeader.Substring("Host:".Length).Trim().Split(':');
            string hostname = hostParts[0];
            int port = 80;
            if (hostParts.Length > 1 && int.TryParse(hostParts[1], out int parsedPort))
                port = parsedPort;

            var requestLine = lines.FirstOrDefault();
            if (requestLine == null) return null;

            var requestParts = requestLine.Split(' ');
            if (requestParts.Length < 2) return null;

            var url = requestParts[1];
            var uri = new Uri($"http://{hostname}:{port}{url}");

            var query = uri.Query;
            if (string.IsNullOrEmpty(query)) return null;

            string queryContent = query.TrimStart('?');
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

        private async Task<string> ReadMessageAsync(TcpClient client, Stream stream)
        {
            var messageBuilder = new List<byte>();
            bool isFinalFragment = false;

            while (!isFinalFragment)
            {
                byte[] buffer = new byte[client.ReceiveBufferSize];
                int bytesRead = 0;

                try
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                }
                catch
                {
                    return string.Empty;
                }

                if (bytesRead == 0)
                    return string.Empty;

                int offset = 0;
                while (offset < bytesRead)
                {
                    if (offset + 2 > bytesRead)
                        break;

                    byte b0 = buffer[offset];
                    isFinalFragment = (b0 & 0x80) != 0;
                    byte opcode = (byte)(b0 & 0x0F);

                    switch (opcode)
                    {
                        case 1: // Text frame
                        case 2: // Binary frame
                            {
                                int payloadLength = buffer[offset + 1] & 0x7F;
                                int headerSize = 2;

                                if (payloadLength == 126)
                                {
                                    if (offset + 4 > bytesRead) return string.Empty;
                                    payloadLength = (buffer[offset + 2] << 8) | buffer[offset + 3];
                                    headerSize += 2;
                                }
                                else if (payloadLength == 127)
                                {
                                    if (offset + 10 > bytesRead) return string.Empty;
                                    payloadLength = (int)(
                                        ((long)buffer[offset + 2] << 56) |
                                        ((long)buffer[offset + 3] << 48) |
                                        ((long)buffer[offset + 4] << 40) |
                                        ((long)buffer[offset + 5] << 32) |
                                        ((long)buffer[offset + 6] << 24) |
                                        ((long)buffer[offset + 7] << 16) |
                                        ((long)buffer[offset + 8] << 8) |
                                        ((long)buffer[offset + 9])
                                    );
                                    headerSize += 8;
                                }

                                if (offset + headerSize + 4 > bytesRead)
                                    return string.Empty;

                                byte[] maskingKey = new byte[4];
                                Array.Copy(buffer, offset + headerSize, maskingKey, 0, 4);

                                int payloadOffset = offset + headerSize + 4;
                                int remainingBytes = bytesRead - payloadOffset;
                                int payloadRead = Math.Min(payloadLength, remainingBytes);

                                byte[] payload = new byte[payloadRead];
                                Array.Copy(buffer, payloadOffset, payload, 0, payloadRead);

                                for (int i = 0; i < payload.Length; i++)
                                    payload[i] ^= maskingKey[i % 4];

                                if (opcode == 2)
                                {
                                    return " BIN " + Encoding.UTF8.GetString(payload);
                                }

                                messageBuilder.AddRange(payload);
                                offset += headerSize + 4 + payloadRead;

                                if (isFinalFragment) break;
                                payloadLength = 0;
                                break;
                            }

                        case 8: // Close frame
                            {
                                byte[] responseCloseFrame = CreateCloseFrame();
                                try { await stream.WriteAsync(responseCloseFrame, 0, responseCloseFrame.Length); }
                                catch { }
                                if (_clients.TryGetValue(client, out var myClient))
                                    myClient.IsAlive = false;
                                return string.Empty;
                            }

                        case 9: // Ping frame
                            {
                                byte[] pongFrame = CreatePongFrame(buffer, bytesRead, offset);
                                try { await stream.WriteAsync(pongFrame, 0, pongFrame.Length); }
                                catch { }
                                offset += 2 + (buffer[offset + 1] & 0x7F);
                                break;
                            }

                        case 10: // Pong frame
                            {
                                if (_clients.TryGetValue(client, out var myClient))
                                {
                                    myClient.LastPingTime = DateTime.Now;
                                }
                                offset += 2 + (buffer[offset + 1] & 0x7F);
                                break;
                            }

                        default:
                            return "Unexpected frame type received";
                    }
                }
            }

            return Encoding.UTF8.GetString(messageBuilder.ToArray());
        }

        private byte[] CreatePongFrame(byte[] buffer, int bytesRead, int offset)
        {
            byte[] pongFrame = new byte[2 + (buffer[offset + 1] & 0x7F)];
            pongFrame[0] = 0x8A;
            pongFrame[1] = buffer[offset + 1];
            Array.Copy(buffer, offset + 2, pongFrame, 2, pongFrame.Length - 2);
            return pongFrame;
        }

        private byte[] CreateCloseFrame(ushort statusCode = 1000, string reason = "")
        {
            byte[] statusCodeBytes = BitConverter.GetBytes(statusCode);
            Array.Reverse(statusCodeBytes);
            byte[] reasonBytes = Encoding.UTF8.GetBytes(reason);
            byte[] payload = new byte[2 + reasonBytes.Length];
            payload[0] = statusCodeBytes[0];
            payload[1] = statusCodeBytes[1];
            Array.Copy(reasonBytes, 0, payload, 2, reasonBytes.Length);
            byte[] frame = new byte[2 + payload.Length];
            frame[0] = 0x88;
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