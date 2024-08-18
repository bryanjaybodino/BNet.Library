using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Security;

namespace BNet.WebSocket.Server
{
    public class Connection : EventHandlers
    {
        private TcpListener _listener;
        private ConcurrentDictionary<TcpClient, Stream> _clients = new ConcurrentDictionary<TcpClient, Stream>();
        private ConcurrentDictionary<string, HashSet<TcpClient>> _rooms = new ConcurrentDictionary<string, HashSet<TcpClient>>();
        public bool IsRunning { get; private set; }

        private X509Certificate2 _serverCertificate;

        public Connection(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
        }

        public void LoadCertificate(string path, string password)
        {
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            _serverCertificate = new X509Certificate2(path, password);
        }

        public void LoadCertificate(byte[] rawData, string password)
        {
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            _serverCertificate = new X509Certificate2(rawData, password);
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
                    await Task.Run(() => HandleClientAsync(client));
                }
            }
            catch (Exception ex)
            {
                await SetOnError($"StartAsync: {ex.Message}");
            }
        }
        public Task SendMessageToRoomAsync(string roomId, string message)
        {
            if (_rooms.TryGetValue(roomId, out var clientsInRoom))
            {
                var tasks = clientsInRoom.Select(client =>
                {
                    if (_clients.TryGetValue(client, out var stream))
                    {
                        return WriteMessageAsync(stream, message);
                    }
                    return Task.FromResult(0); // Return a completed task
                }).ToArray();

                // Use Task.WhenAll to await the completion of all tasks
                return Task.WhenAll(tasks);
            }

            // Return a completed task if no clients found
            return Task.FromResult(0);
        }


        private void JoinRoom(string roomId, TcpClient client)
        {
            var clientsInRoom = _rooms.GetOrAdd(roomId, _ => new HashSet<TcpClient>());
        }
        public Task StopAsync()
        {
            try
            {
                IsRunning = false;
                _listener.Stop();

                var tasks = _clients.Keys.Select(client =>
                {
                    RemoveClient(client); // Assuming RemoveClient is synchronous
                    return Task.FromResult(0); // Return a completed task for each client
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
            await sslStream.AuthenticateAsServerAsync(_serverCertificate);
            return sslStream;
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {

                var endpoint = client.Client.RemoteEndPoint as IPEndPoint;
                if (endpoint == null)
                {
                    throw new Exception("Unable to retrieve client endpoint.");
                }

                string clientKey = $"{endpoint.Address}:{endpoint.Port}";

                using (NetworkStream networkStream = client.GetStream())
                {
                    using (Stream secureStream = await HandleSecurityAsync(networkStream))
                    {
                        if (secureStream == null)
                        {
                            throw new Exception("Failed to secure the stream for client.");
                        }


                        lock (_clients)
                        {
                            if (_clients.ContainsKey(client))
                            {
                                throw new Exception("Client already connected.");
                            }
                            else
                            {
                                if (!_clients.TryAdd(client, secureStream))
                                {
                                    throw new Exception("Failed to add client to the dictionary.");
                                }
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
                // Ensure client is removed from dictionary when done
                RemoveClient(client);
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
                await SetOnConnectedClient(_clients.Count);
                while (client.Connected)
                {

                    string message = await ReadMessageAsync(client, stream);
                    if (message != null && message != "BNet.WebSocket.Server = Image")
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
                    else if (message == "BNet.WebSocket.Server = Image") // This is my placeholder
                    {
                        //Do Nothing
                    }
                    else
                    {
                        throw new Exception("Message Null");
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
        private string CalculateWebSocketAcceptKey(string key)
        {
            var magicGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            using (var sha1 = SHA1.Create())
            {
                var keyBytes = Encoding.UTF8.GetBytes(key + magicGuid);
                var hashBytes = sha1.ComputeHash(keyBytes);
                return Convert.ToBase64String(hashBytes);
            }
        }
        private async Task SendHandshakeResponseAsync(Stream stream, string key)
        {
            string response =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Accept: {CalculateWebSocketAcceptKey(key)}\r\n" +
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
                            var queryParams = query.TrimStart('?').Split('&');
                            foreach (var param in queryParams)
                            {
                                var keyValue = param.Split('=');
                                if (keyValue.Length == 2 && keyValue[0].ToLower() == "room")
                                {
                                    return keyValue[1];
                                }
                            }
                        }
                    }
                }
                return null;
            }
            else
            {
                throw new Exception("Room ID not found in request.");
            }

        }
        private async Task<string> ReadMessageAsync(TcpClient client, Stream stream)
        {
            var messageBuilder = new List<byte>();
            bool isFinalFragment = false;
            int payloadLength = 0;
            int payloadRead = 0;

            while (!isFinalFragment)
            {
                byte[] buffer = new byte[client.ReceiveBufferSize];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    // Connection closed
                    return null;
                }

                int offset = 0;

                while (offset < bytesRead)
                {
                    byte b0 = buffer[offset];
                    isFinalFragment = (b0 & 0x80) != 0;
                    bool isTextFrame = (b0 & 0x0F) == 1;

                    if (!isTextFrame)
                    {
                        // Handle non-text frames as needed
                        return "BNet.WebSocket.Server = Image";
                    }

                    if (payloadLength == 0)
                    {
                        payloadLength = buffer[offset + 1] & 0x7F;
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

                        payloadRead = Math.Min(payloadLength, remainingBytes);
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

                        // Reset payload length for next fragment
                        payloadLength = 0;
                        payloadRead = 0;
                    }
                    else
                    {
                        // Handle continuation frames
                        int payloadOffset = offset + 4;
                        int remainingBytes = bytesRead - payloadOffset;
                        int payloadSize = Math.Min(payloadLength - messageBuilder.Count, remainingBytes);

                        byte[] payload = new byte[payloadSize];
                        Array.Copy(buffer, payloadOffset, payload, 0, payloadSize);

                        messageBuilder.AddRange(payload);

                        offset += payloadSize;

                        if (messageBuilder.Count >= payloadLength)
                        {
                            isFinalFragment = true;
                        }
                    }
                }
            }

            return Encoding.UTF8.GetString(messageBuilder.ToArray());
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

        public Task SendMessageAsync(string message)
        {
            var tasks = _clients.Values.Select(stream => WriteMessageAsync(stream, message)).ToArray();
            return Task.WhenAll(tasks);
        }

        private async void RemoveClient(TcpClient client)
        {
            lock (_clients)
            {
                if (_clients.TryRemove(client, out Stream stream))
                {
                    stream?.Dispose();
                    client?.Close();
                }
            }
            await SetOnConnectedClient(_clients.Count);
        }

    }
}