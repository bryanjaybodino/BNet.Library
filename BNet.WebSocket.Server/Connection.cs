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

        // FIX #3: per-client write lock prevents concurrent stream corruption
        public SemaphoreSlim WriteLock { get; } = new SemaphoreSlim(1, 1);
    }

    public class Connection : EventHandlers
    {
        private TcpListener _listener;
        private ConcurrentDictionary<TcpClient, MyClients> _clients = new ConcurrentDictionary<TcpClient, MyClients>();
        public bool IsRunning { get; private set; }

        private X509Certificate2 _serverCertificate;

        public Connection(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
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
                Console.WriteLine("Server started. Waiting for clients...");

                while (IsRunning)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    _ = Task.Run(async () => await HandleClientAsync(client));
                }
            }
            catch (Exception ex)
            {
                await SetOnError($"StartAsync: {ex.Message}");
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
            }
        }

        public Task StopAsync()
        {
            try
            {
                IsRunning = false;
                _listener.Stop();

                var tasks = _clients.Keys.Select(async client =>
                {
                    await RemoveClientAsync(client);
                    return Task.FromResult(0);
                }).ToArray();

                return Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                return SetOnError($"StopAsync: {ex.Message}");
            }
        }

        private async Task<Stream> HandleSecurityAsync(Stream stream)
        {
            if (_serverCertificate == null)
            {
                return stream;
            }

            var sslStream = new SslStream(stream, false);
            await sslStream.AuthenticateAsServerAsync(
                _serverCertificate,
                clientCertificateRequired: false,
                enabledSslProtocols: SslProtocols.Tls12,
                checkCertificateRevocation: false
            );
            return sslStream;
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
                            throw new NotSupportedException("Failed to secure the stream for client.");

                        if (_clients.ContainsKey(client))
                            throw new Exception("Client already connected.");

                        if (!_clients.TryAdd(client, new MyClients { Stream = secureStream }))
                            throw new Exception("Failed to add client to the dictionary.");

                        await HandleStartupAsync(client, secureStream);
                    }
                }
            }
            catch (Exception ex)
            {
                await SetOnError($"{ex.Message}");
            }
            finally
            {
                await RemoveClientAsync(client);
            }
        }

        private async Task HandleStartupAsync(TcpClient client, Stream stream)
        {
            string handshakeRequest = await ReadRequestAsync(client, stream);
            if (IsWebSocketHandshake(handshakeRequest, out string key))
            {
                await SendHandshakeResponseAsync(stream, key);

                // FIX #1: now returns clean "Room_ABC", not "?room=Room_ABC"
                string roomId = ExtractRoomIdFromRequest(handshakeRequest);
                if (!string.IsNullOrEmpty(roomId))
                {
                    JoinRoom(roomId, client);
                }

                var clients = _clients.Values.Distinct().ToList();
                await SetOnConnectedClient(clients.Count);

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
                        throw new Exception("Force close client due to abnormal activity");
                    }
                    else if (message == "Unexpected frame type received")
                    {
                        // ignore unknown opcodes
                    }
                    else if (message.StartsWith(BinaryMarker))
                    {
                        // Binary frame — broadcast to room immediately (fast, same as text path),
                        // then notify Setup.cs in the background for DB save / email / push.
                        string json = message.Substring(BinaryMarker.Length);
                        byte[] raw = Encoding.UTF8.GetBytes(json);

                        // 1. Instant room broadcast (no round-trip through Setup.cs)
                        if (string.IsNullOrEmpty(roomId))
                            await SendBinaryAsync(raw);
                        else
                            await SendBinaryToRoomAsync(roomId, raw);

                        // 2. Background side-effects (DB save, email, notifications)
                        _ = Task.Run(() => SetOnBinaryReceived(raw));
                    }
                    else if (message.Replace(" ", "") != "")
                    {
                        await SetOnReceived(message);
                        if (string.IsNullOrEmpty(roomId))
                            await SendMessageAsync(message);
                        else
                            await SendMessageToRoomAsync(roomId, message);
                    }
                }

                throw new Exception("Client Disconnected");
            }
            else
            {
                throw new Exception("Invalid WebSocket handshake.");
            }
        }

        private async Task<string> ReadRequestAsync(TcpClient client, Stream stream)
        {
            var requestBuilder = new StringBuilder();
            var buffer = new byte[client.ReceiveBufferSize];
            int bytesRead;

            do
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    requestBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                }
            }
            while (bytesRead > 0 && !requestBuilder.ToString().EndsWith("\r\n\r\n"));

            return requestBuilder.ToString();
        }

        private bool IsWebSocketHandshake(string request, out string key)
        {
            key = null;
            if (request.Contains("Upgrade: websocket") && request.Contains("Connection: Upgrade"))
            {
                var lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);
                foreach (var line in lines)
                {
                    if (line.StartsWith("Sec-WebSocket-Key:"))
                    {
                        key = line.Substring("Sec-WebSocket-Key:".Length).Trim();
                        return true;
                    }
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
                "\r\n";

            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
        }

        // FIX #1: was returning raw "?room=Room_ABC" — now returns "Room_ABC"
        //
        // Root cause: the old code returned uri.Query directly, which includes
        // the leading "?" and the "room=" key name. So JoinRoom stored the key
        // "?room=Room_ABC", but SendBinaryToRoomAsync looked for "Room_ABC".
        // No client ever matched → all real-time messages were silently dropped.
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

            var query = uri.Query; // e.g. "?room=Room_ABC"
            if (string.IsNullOrEmpty(query)) return null;

            // Parse key=value pairs — extract only the "room" value
            string queryContent = query.TrimStart('?');
            foreach (var part in queryContent.Split('&'))
            {
                var kv = part.Split(new[] { '=' }, 2);
                if (kv.Length == 2 && kv[0].Equals("room", StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(kv[1]); // returns "Room_ABC"
                }
            }

            return null;
        }

        // Returns:
        //   non-empty string  → decoded text frame
        //   null              → binary frame (OnBinaryReceived already fired)
        //   ""                → force-close signal
        //   "Unexpected..."   → unknown opcode
        private async Task<string> ReadMessageAsync(TcpClient client, Stream stream)
        {
            var messageBuilder = new List<byte>();
            bool isFinalFragment = false;

            while (!isFinalFragment)
            {
                byte[] buffer = new byte[client.ReceiveBufferSize];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                    return string.Empty;

                int offset = 0;
                while (offset < bytesRead)
                {
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
                                    payloadLength = (buffer[offset + 2] << 8) | buffer[offset + 3];
                                    headerSize += 2;
                                }
                                else if (payloadLength == 127)
                                {
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
                                    // Return binary payload decoded as string with a marker prefix.
                                    // The message loop broadcasts it immediately (fast path) then
                                    // fires OnBinaryReceived in the background for side-effects.
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
                                await stream.WriteAsync(responseCloseFrame, 0, responseCloseFrame.Length);
                                throw new InvalidOperationException("Received close frame.");
                            }

                        case 9: // Ping frame
                            {
                                byte[] pongFrame = CreatePongFrame(buffer, bytesRead, offset);
                                await stream.WriteAsync(pongFrame, 0, pongFrame.Length);
                                offset += 2 + (buffer[offset + 1] & 0x7F);
                                break;
                            }

                        case 10: // Pong frame
                            offset += 2 + (buffer[offset + 1] & 0x7F);
                            break;

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

        // FIX #3: all writes go through a per-client SemaphoreSlim(1,1)
        // Without this, two concurrent broadcasts (e.g. typing + chat) could
        // interleave bytes on the same stream, producing corrupted WS frames
        // that the browser silently drops or that crash the connection.
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
                myClient.Stream?.Dispose();
                client?.Close();
            }
            var clients = _clients.Values.Distinct().ToList();
            await SetOnDisconnectedClient(clients.Count);
        }
    }
}
