using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BNet.WebSocket.Server
{
    public class Connection : EventHandlers
    {
        private TcpListener _listener;
        private ConcurrentDictionary<TcpClient, Stream> _clients = new ConcurrentDictionary<TcpClient, Stream>();

        private ConcurrentDictionary<Task, CancellationTokenSource> _clientCancellationTokens = new ConcurrentDictionary<Task, CancellationTokenSource>();
        private ConcurrentDictionary<string, HashSet<TcpClient>> _rooms = new ConcurrentDictionary<string, HashSet<TcpClient>>();
        public Dictionary<string, string> UserCredentials { get; } = new Dictionary<string, string>();
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
                    try
                    {
                        var client = await _listener.AcceptTcpClientAsync();
                        // Use a lock to ensure single client handling
                        lock (_clients)
                        {
                            if (_clients.ContainsKey(client))
                            {
                                RemoveClient(client);
                                continue;
                            }
                        }

                        var clientCancellationTokenSource = new CancellationTokenSource();
                        var clientTask = Task.Run(() => HandleClientAsync(client, clientCancellationTokenSource.Token), clientCancellationTokenSource.Token);
                        // Track the task and cancellation token if needed
                        _clientCancellationTokens[clientTask] = clientCancellationTokenSource;
                    }
                    catch (Exception ex)
                    {
                        SetOnError($"StartAsync: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                SetOnError($"StartAsync: {ex.Message}");
            }
        }

        public async Task SendMessageToRoomAsync(string roomId, string message)
        {
            if (_rooms.TryGetValue(roomId, out var clientsInRoom))
            {
                var tasks = new List<Task>();

                foreach (var client in clientsInRoom)
                {
                    if (_clients.TryGetValue(client, out var stream))
                    {
                        tasks.Add(WriteMessageAsync(stream, message));
                    }
                }

                await Task.WhenAll(tasks);
            }
        }
        private void JoinRoom(string roomId, TcpClient client)
        {
            var clientsInRoom = _rooms.GetOrAdd(roomId, _ => new HashSet<TcpClient>());
            lock (clientsInRoom)
            {
                clientsInRoom.Add(client);
            }
        }

        public async Task StopAsync()
        {
            try
            {
                IsRunning = false;
                _listener.Stop();

                var tasks = new List<Task>();
                foreach (var client in _clients.Keys.ToList()) // ToList to avoid modification during iteration
                {
                    RemoveClient(client);
                }

                // If you have ongoing operations/tasks, cancel them
                foreach (var cts in _clientCancellationTokens.Values)
                {
                    cts.Cancel();
                }

                // Await tasks if you have asynchronous operations to wait on
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                SetOnError($"StopAsync: {ex.Message}");
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

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            try
            {
                NetworkStream networkStream = client.GetStream();
                Stream secureStream = await HandleSecurityAsync(networkStream);

                // Check and handle existing connections
                if (_clients.ContainsKey(client))
                {
                    Console.WriteLine($"Client {client.Client.RemoteEndPoint} already connected.");
                    RemoveClient(client);
                    return;
                }

                // Try adding the client to the dictionary
                if (!_clients.TryAdd(client, secureStream))
                {
                    // Failed to add client, likely a duplicate
                    Console.WriteLine($"Failed to add client {client.Client.RemoteEndPoint}. Removing existing client.");
                    RemoveClient(client);
                    return;
                }
                //Console.WriteLine($"Client {client.Client.RemoteEndPoint} connected.");

                await HandleStartupAsync(client, secureStream);
            }
            catch (Exception ex)
            {
                SetOnError($"HandleClientAsync: {ex.Message}");
            }
            finally
            {
                //Console.WriteLine($"Client {client.Client.RemoteEndPoint} disconnected.");
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

                SetOnConnectedClient(_clients.Count);

                while (client.Connected)
                {
                    string message = await ReadMessageAsync(client, stream);
                    if (message != null)
                    {
                        SetOnReceived(message);
                        if (roomId == null)
                        {
                            await SendMessageAsync(message);
                        }
                        else
                        {
                            await SendMessageToRoomAsync(roomId, message); // Send message to the room
                        }

                    }
                }
            }
        }

        private async Task<string> ReadRequestAsync(TcpClient client, Stream stream)
        {
            byte[] buffer = new byte[client.ReceiveBufferSize];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer, 0, bytesRead);
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
            string CalculateWebSocketAcceptKey(string key)
            {
                var magicGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                using (var sha1 = SHA1.Create())
                {
                    var keyBytes = Encoding.UTF8.GetBytes(key + magicGuid);
                    var hashBytes = sha1.ComputeHash(keyBytes);
                    return Convert.ToBase64String(hashBytes);
                }
            }

            var response = new StringBuilder()
                .AppendLine("HTTP/1.1 101 Switching Protocols")
                .AppendLine("Upgrade: websocket")
                .AppendLine("Connection: Upgrade")
                .AppendLine($"Sec-WebSocket-Accept: {CalculateWebSocketAcceptKey(key)}")
                .AppendLine()
                .ToString();

            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
        }

        private string ExtractRoomIdFromRequest(string request)
        {
            // Split request into lines
            var lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);

            // Extract Host header
            string hostHeader = lines.FirstOrDefault(line => line.StartsWith("Host:"));
            if (hostHeader != null)
            {
                var hostParts = hostHeader.Substring("Host:".Length).Trim().Split(':');
                string hostname = hostParts[0];
                int port = 80; // Default HTTP port

                if (hostParts.Length > 1 && int.TryParse(hostParts[1], out int parsedPort))
                {
                    port = parsedPort;
                }

                // Extract the request line
                var requestLine = lines.FirstOrDefault();
                if (requestLine != null)
                {
                    // Split the request line into components
                    var requestParts = requestLine.Split(' ');

                    if (requestParts.Length > 1)
                    {
                        // The URL is the second part of the request line
                        var url = requestParts[1];

                        // Create a full URI with the hostname and port
                        var uri = new Uri($"http://{hostname}:{port}{url}");
                        var query = uri.Query;

                        if (!string.IsNullOrEmpty(query))
                        {
                            // Parse query parameters
                            var queryParams = query.TrimStart('?').Split('&');
                            foreach (var param in queryParams)
                            {
                                var keyValue = param.Split('=');
                                if (keyValue.Length == 2 && keyValue[0] == "room")
                                {
                                    return keyValue[1];
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }




        private async Task<string> ReadMessageAsync(TcpClient client, Stream stream)
        {
            byte[] buffer = new byte[client.ReceiveBufferSize];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0) return null;

            // Frame header
            byte b0 = buffer[0];
            bool isFinalFragment = (b0 & 0x80) != 0;
            bool isTextFrame = (b0 & 0x0F) == 1;

            if (!isFinalFragment || !isTextFrame)
            {

                RemoveClient(client, "Disconnected");
                return null;
                //throw new NotImplementedException("Only final text frames are supported.");
            }

            // Payload length
            int payloadLength = buffer[1] & 0x7F;
            int offset = 2;

            if (payloadLength == 126)
            {
                // 16-bit length
                payloadLength = (buffer[2] << 8) | buffer[3];
                offset += 2;
            }
            else if (payloadLength == 127)
            {
                // 64-bit length (not supported)
                throw new NotImplementedException("Payload length 127 (64-bit length) is not supported.");
            }

            // Masking key
            byte[] maskingKey = new byte[4];
            Array.Copy(buffer, offset, maskingKey, 0, 4);
            offset += 4;

            // Extract payload
            byte[] payload = new byte[payloadLength];
            Array.Copy(buffer, offset, payload, 0, payloadLength);

            // Unmask payload
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] ^= maskingKey[i % 4];
            }

            return Encoding.UTF8.GetString(payload);
        }



        private async Task WriteMessageAsync(Stream stream, string message)
        {
            byte[] payload = Encoding.UTF8.GetBytes(message);
            int payloadLength = payload.Length;
            byte[] frame;


            // Determine the length of the frame
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

            // Set the frame header
            frame[0] = 0x81; // Final fragment + text frame opcode

            // Copy the payload to the frame
            Array.Copy(payload, 0, frame, frame.Length - payloadLength, payloadLength);


            try
            {
                await stream.WriteAsync(frame, 0, frame.Length);
                await stream.FlushAsync(); // Ensure data is flushed to the network
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WriteMessageAsync: {ex.Message}");
                throw;
            }
        }



        public async Task SendMessageAsync(string message)
        {
            var streamTasks = _clients.Values.ToList();
            foreach (var stream in streamTasks)
            {
                try
                {
                    await WriteMessageAsync(stream, message);
                }
                catch (Exception ex)
                {
                    SetOnError($"SendMessageAsync: {ex.Message}");
                }
            }
        }

        private async void RemoveClient(TcpClient client, string closeReason = null)
        {
            if (_clients.TryRemove(client, out Stream stream))
            {
                try
                {
                    // Send a close frame to the client
                    await SendCloseFrameAsync(stream, reason: closeReason);
                }
                catch (Exception ex)
                {
                    SetOnError($"RemoveClient: Error sending close frame. {ex.Message}");
                }

                try
                {
                    // Close and dispose of the stream
                    stream.Close();
                    stream.Dispose();
                }
                catch (Exception ex)
                {
                    SetOnError($"RemoveClient: {ex.Message} while closing stream.");
                }

                try
                {
                    // Close the TcpClient
                    client.Close();
                }
                catch (Exception ex)
                {
                    SetOnError($"RemoveClient: {ex.Message} while closing TcpClient.");
                }

                // Log or update the state
                SetOnDisconnectedClient(_clients.Count);
            }
        }

        private async Task SendCloseFrameAsync(Stream stream, ushort closeCode = 1000, string reason = null)
        {
            var reasonBytes = string.IsNullOrEmpty(reason) ? new byte[0] : Encoding.UTF8.GetBytes(reason);
            var closeFrameLength = 2 + reasonBytes.Length;
            var closeFrame = new byte[closeFrameLength + 2]; // 2 bytes for the frame header

            closeFrame[0] = 0x88; // Final frame + Close frame opcode
            closeFrame[1] = (byte)closeFrameLength; // Payload length

            // Write the close code
            closeFrame[2] = (byte)(closeCode >> 8);
            closeFrame[3] = (byte)closeCode;

            // Write the reason (if any)
            Array.Copy(reasonBytes, 0, closeFrame, 4, reasonBytes.Length);

            try
            {
                await stream.WriteAsync(closeFrame, 0, closeFrame.Length);
                await stream.FlushAsync(); // Ensure the frame is sent immediately
            }
            catch (Exception ex)
            {
                SetOnError($"SendCloseFrameAsync: {ex.Message}");
            }
        }
    }
}
