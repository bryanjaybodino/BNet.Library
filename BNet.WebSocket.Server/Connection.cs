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
using System.Threading.Tasks;

namespace BNet.WebSocket.Server
{
    class MyClients
    {
        public Stream Stream { get; set; }
        public HashSet<string> Rooms { get; set; } = new HashSet<string>();
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

        // FIX #1: Added X509KeyStorageFlags so the private key is accessible in all environments
        // FIX #3: Removed ServicePointManager callback — it only affects outgoing HTTP clients, not inbound SSL
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

        public Task SendMessageToRoomAsync(string roomId, string message)
        {
            var clients = _clients.Values
                .Where(c => c.Rooms.Contains(roomId))
                .Distinct()
                .ToList();

            var tasks = clients.Select(client =>
                WriteMessageAsync(client.Stream, message)
            ).ToArray();

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

        // FIX #2: Explicitly specify TLS protocols in AuthenticateAsServerAsync
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
                        {
                            throw new NotSupportedException("Failed to secure the stream for client.");
                        }

                        if (_clients.ContainsKey(client))
                        {
                            throw new Exception("Client already connected.");
                        }
                        else
                        {
                            if (!_clients.TryAdd(client, new MyClients { Stream = secureStream }))
                            {
                                throw new Exception("Failed to add client to the dictionary.");
                            }
                        }

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

                string roomId = ExtractRoomIdFromRequest(handshakeRequest);
                if (!string.IsNullOrEmpty(roomId))
                {
                    JoinRoom(roomId, client);
                }

                var clients = _clients.Values.Distinct().ToList();
                await SetOnConnectedClient(clients.Count);

                while (client.Connected)
                {
                    string message = await ReadMessageAsync(client, stream);
                    if (message != null && message != "Unexpected frame type received" && message.Replace(" ", "") != "")
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
                    else if (message == null)
                    {
                        throw new Exception("Force close client due to abnormal activity");
                    }
                    else if (message == "Unexpected frame type received")
                    {
                        // Do Nothing
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

        private string ExtractRoomIdFromRequest(string request)
        {
            var lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);
            string hostHeader = lines.FirstOrDefault(line => line.StartsWith("Host:"));
            if (hostHeader != null)
            {
                var hostParts = hostHeader.Substring("Host:".Length).Trim().Split(':');
                string hostname = hostParts[0];
                int port = 80;

                if (hostParts.Length > 1 && int.TryParse(hostParts[1], out int parsedPort))
                {
                    port = parsedPort;
                }

                var requestLine = lines.FirstOrDefault();
                if (requestLine != null)
                {
                    var requestParts = requestLine.Split(' ');
                    if (requestParts.Length > 1)
                    {
                        var url = requestParts[1];
                        var uri = new Uri($"http://{hostname}:{port}{url}");
                        var query = uri.Query;
                        if (!string.IsNullOrEmpty(query))
                        {
                            return query;
                        }
                    }
                }
                return null;
            }
            else
            {
                throw new NotSupportedException("Room ID not found in request.");
            }
        }

        private async Task<string> ReadMessageAsync(TcpClient client, Stream stream)
        {
            var messageBuilder = new List<byte>();
            bool isFinalFragment = false;

            while (!isFinalFragment)
            {
                byte[] buffer = new byte[client.ReceiveBufferSize];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    return null;
                }

                int offset = 0;
                while (offset < bytesRead)
                {
                    byte b0 = buffer[offset];
                    isFinalFragment = (b0 & 0x80) != 0;
                    byte opcode = (byte)(b0 & 0x0F);

                    switch (opcode)
                    {
                        case 1: // Text Frame
                        case 2: // Binary Frame
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
                            {
                                payload[i] ^= maskingKey[i % 4];
                            }

                            messageBuilder.AddRange(payload);
                            offset += headerSize + 4 + payloadRead;

                            if (isFinalFragment)
                            {
                                break;
                            }

                            payloadLength = 0;
                            break;

                        case 8: // Close frame
                            byte[] closeFrame = new byte[bytesRead];
                            Array.Copy(buffer, offset, closeFrame, 0, bytesRead);

                            if (bytesRead > 2)
                            {
                                ushort closeCode = (ushort)((closeFrame[0] << 8) | closeFrame[1]);
                                string closeReason = Encoding.UTF8.GetString(closeFrame, 2, bytesRead - 2);
                            }

                            byte[] responseCloseFrame = CreateCloseFrame();
                            await stream.WriteAsync(responseCloseFrame, 0, responseCloseFrame.Length);
                            throw new InvalidOperationException("Received close frame.");

                        case 9: // Ping frame
                            byte[] pongFrame = CreatePongFrame(buffer, bytesRead, offset);
                            await stream.WriteAsync(pongFrame, 0, pongFrame.Length);
                            offset += 2 + (buffer[offset + 1] & 0x7F);
                            break;

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
            pongFrame[0] = 0x8A; // FIN + Pong opcode
            pongFrame[1] = buffer[offset + 1];
            Array.Copy(buffer, offset + 2, pongFrame, 2, pongFrame.Length - 2);
            return pongFrame;
        }

        // FIX #4: Corrected byte-packing and added proper WebSocket framing header (0x88)
        private byte[] CreateCloseFrame(ushort statusCode = 1000, string reason = "")
        {
            byte[] statusCodeBytes = BitConverter.GetBytes(statusCode);
            Array.Reverse(statusCodeBytes); // big-endian

            byte[] reasonBytes = Encoding.UTF8.GetBytes(reason);

            // Build payload: 2-byte status code + reason
            byte[] payload = new byte[2 + reasonBytes.Length];
            payload[0] = statusCodeBytes[0];
            payload[1] = statusCodeBytes[1];
            Array.Copy(reasonBytes, 0, payload, 2, reasonBytes.Length);

            // Build WebSocket frame: FIN + close opcode (0x88) + payload length + payload
            byte[] frame = new byte[2 + payload.Length];
            frame[0] = 0x88; // FIN bit + close opcode
            frame[1] = (byte)payload.Length;
            Array.Copy(payload, 0, frame, 2, payload.Length);

            return frame;
        }

        private async Task WriteMessageAsync(Stream stream, string message)
        {
            byte[] payload = Encoding.UTF8.GetBytes(message);
            int payloadLength = payload.Length;
            byte[] frame;

            if (payloadLength <= 125)
            {
                frame = new byte[1 + 1 + payloadLength];
                frame[1] = (byte)payloadLength;
            }
            else if (payloadLength <= 65535)
            {
                frame = new byte[1 + 1 + 2 + payloadLength];
                frame[1] = 126;
                frame[2] = (byte)(payloadLength >> 8);
                frame[3] = (byte)payloadLength;
            }
            else
            {
                frame = new byte[1 + 1 + 8 + payloadLength];
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

            frame[0] = 0x81; // Final fragment + text frame opcode
            Array.Copy(payload, 0, frame, frame.Length - payloadLength, payloadLength);

            await stream.WriteAsync(frame, 0, frame.Length);
            await stream.FlushAsync();
        }

        public async Task SendMessageAsync(string message)
        {
            var clients = _clients.Values.Distinct().ToList();
            var tasks = clients.Select(client => WriteMessageAsync(client.Stream, message)).ToArray();
            await Task.WhenAll(tasks);
        }

        // FIX #5: Fire OnDisconnectedClient (not OnConnectedClient) when a client leaves
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