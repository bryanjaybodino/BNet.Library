using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection.Metadata;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using static System.Net.Mime.MediaTypeNames;


namespace BNet.WebSocket.Server
{
    public class Connection : EventHandlers
    {
        ////private EventHandlers _eventHandlers = new EventHandlers();
        ////public EventHandlers EventHandlers => _eventHandlers; // Expose the event handlers


        #region Constructors
        public Connection(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
        }
        #endregion

        #region Setup
        public void Setup(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
        }
        #endregion


        #region Private Components
        private ConcurrentDictionary<TcpClient, Task> _clients = new ConcurrentDictionary<TcpClient, Task>();
        private ConcurrentDictionary<Stream, Task> _streams = new ConcurrentDictionary<Stream, Task>();
        private readonly ConcurrentDictionary<Task, CancellationTokenSource> _clientCancellationTokens = new ConcurrentDictionary<Task, CancellationTokenSource>();
        private TcpListener _listener { get; set; }
        #endregion

        #region Public Components
        public Dictionary<string, string> UserCredentials = new Dictionary<string, string>();
        public bool isRunning { get; private set; }
        #endregion

        #region Certificates
        private X509Certificate2 _serverCertificate { get; set; }
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
        #endregion

        #region StartAsync
        public async Task StartAsync()
        {
            try
            {
                isRunning = true;
                _listener.Start();
                Console.WriteLine("Server started. Waiting for clients...");

                while (isRunning)
                {
                    try
                    {
                        // Accept a new client
                        var client = await _listener.AcceptTcpClientAsync();

                        var clientCancellationTokenSource = new CancellationTokenSource();
                        // Handle the new client connection
                        var clientTask = Task.Run(() => HandleClientAsync(client), clientCancellationTokenSource.Token);


                        // Store the task in the dictionary
                        _clients[client] = clientTask;
                    }
                    catch (Exception ex)
                    {
                        SetOnError($"StartAsync : {ex.Message}");
                    }
                }
            }
            catch { }
        }
        #endregion

        #region StopAsync
        public async Task StopAsync()
        {
            try
            {
                isRunning = false;
                _listener.Stop();

                // Cancel all client tasks and await their completion
                var cancellationTasks = new List<Task>();
                foreach (var cancellationTokenSource in _clientCancellationTokens.Values)
                {
                    cancellationTokenSource.Cancel();
                }
                foreach (var clientTask in _clients.Values)
                {
                    cancellationTasks.Add(clientTask);
                }
                foreach (var streamTask in _streams.Values)
                {
                    cancellationTasks.Add(streamTask);
                }
                try
                {
                    await Task.WhenAll(cancellationTasks);
                }
                catch (OperationCanceledException ex)
                {
                    SetOnError($"StopAsync : {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                SetOnError($"StopAsync : {ex.Message}");
            }
        }

        #endregion
        private async Task<Stream> HandleSecurityAsync(Stream stream)
        {
            bool isSecure = _serverCertificate != null;
            if (isSecure)
            {
                SslStream sslStream = new SslStream(stream, false);
                await sslStream.AuthenticateAsServerAsync(_serverCertificate);
                return sslStream;
            }
            else
            {
                return stream;
            }
        }
        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                Stream NewStream = await HandleSecurityAsync(stream);

                //SAVE STREAM TO LIST TO SEND SERVER INFORMATION TO ALL CLIENTS
                var clientCancellationTokenSource = new CancellationTokenSource();
                var streamTask = Task.Run(() => clientCancellationTokenSource.Token);
                _streams[NewStream] = streamTask;

                //START WEBSOCKET
                await HandleStartupAsync(client, NewStream);
            }
            catch (Exception ex)
            {
                SetOnError($"HandleClientAsync : {ex.Message}");
            }
            finally
            {
                client.Close();
                dictionaryTCPClientRemove(client);
            }
        }
        private async Task HandleStartupAsync(TcpClient client, Stream stream)
        {
            // Perform the WebSocket handshake
            string handshakeRequest = await ReadRequestAsync(client, stream);
            if (IsWebSocketHandshake(handshakeRequest, out string key))
            {
                await SendHandshakeResponseAsync(stream, key);
                SetOnConnectedClient(_clients.Count);




                // Enter WebSocket communication loop
                while (client.Connected)
                {
                    string message = await ReadMessageAsync(client, stream);
                    if (message != null)
                    {
                        SetOnReceived(message); 
                        //SEND TO ALL CLIENT
                        await SendMessageAsync(message);
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
                // Extract Sec-WebSocket-Key
                var lines = request.Split("\r\n");
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
                using (var sha1 = System.Security.Cryptography.SHA1.Create())
                {
                    var keyBytes = Encoding.UTF8.GetBytes(key + magicGuid);
                    var hashBytes = sha1.ComputeHash(keyBytes);
                    return Convert.ToBase64String(hashBytes);
                }
            }
            StringBuilder responseBuilder = new StringBuilder();
            responseBuilder.AppendLine("HTTP/1.1 101 Switching Protocols");
            responseBuilder.AppendLine("Upgrade: websocket");
            responseBuilder.AppendLine("Connection: Upgrade");
            responseBuilder.AppendLine($"Sec-WebSocket-Accept: {CalculateWebSocketAcceptKey(key)}");
            responseBuilder.AppendLine(); // Adds the \r\n\rn at the end of the response
            byte[] responseBytes = Encoding.UTF8.GetBytes(responseBuilder.ToString());
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
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
                throw new NotImplementedException("Only final text frames are supported.");
            }

            // Payload length
            int payloadLength = buffer[1] & 0x7F;
            if (payloadLength == 126)
            {
                payloadLength = (buffer[2] << 8) | buffer[3];
            }
            else if (payloadLength == 127)
            {
                throw new NotImplementedException("Payload length 127 (64-bit length) is not supported.");
            }

            // Masking key
            byte[] maskingKey = new byte[4];
            Array.Copy(buffer, 2, maskingKey, 0, 4);
            byte[] payload = new byte[payloadLength];
            Array.Copy(buffer, 6, payload, 0, payloadLength);

            // Unmask payload
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] ^= maskingKey[i % 4];
            }

            return Encoding.UTF8.GetString(payload);
        }

        private async Task SendMessageAsync(Stream stream, string message)
        {
            byte[] payload = Encoding.UTF8.GetBytes(message);
            int payloadLength = payload.Length;

            // Frame header
            byte[] frame = new byte[1 + 1 + payloadLength];
            frame[0] = 0x81; // Final fragment + text frame opcode
            if (payloadLength <= 125)
            {
                frame[1] = (byte)payloadLength;
            }
            else if (payloadLength <= 65535)
            {
                frame[1] = 126;
                frame[2] = (byte)(payloadLength >> 8);
                frame[3] = (byte)payloadLength;
            }
            else
            {
                // Handle extended payload length if needed
            }
            Array.Copy(payload, 0, frame, frame.Length - payloadLength, payloadLength);
            await stream.WriteAsync(frame, 0, frame.Length);
        }




        public async Task SendMessageAsync(string message)
        {
            foreach (var stream in _streams.Keys.ToList())
            {
                try
                {
                    await SendMessageAsync(stream, message);
                }
                catch (Exception ex)
                {
                    SetOnError($"SendMessageAsync : {ex.Message}");
                }
            }
        }

        private void dictionaryTCPClientRemove(TcpClient client)
        {
            for (int i = 0; i < _clients.Keys.Count; i++)
            {
                if (_clients.Keys.ElementAt(i) == client)
                {
                    var stream = _streams.ElementAt(i).Key;
                    _streams.TryRemove(stream, out _);
                    break;
                }
            }
            _clients.TryRemove(client, out _);
            SetOnDisconnectedClient(_clients.Count);
        }
    }
}