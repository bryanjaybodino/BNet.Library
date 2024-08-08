using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace BNet.WebSocket.Server
{
    public class Connection
    {
        #region Constructors
        public Connection(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
        }
        #endregion

        #region Private Components
        private System.Text.Encoding encoding = System.Text.Encoding.UTF8;
        private ConcurrentDictionary<TcpClient, Task> _clients = new ConcurrentDictionary<TcpClient, Task>();
        private readonly object _lock = new object(); //Thread Safety: Use the _lock object to synchronize access to _isRunning and _listener.
        private readonly ConcurrentDictionary<Task, CancellationTokenSource> _clientCancellationTokens = new ConcurrentDictionary<Task, CancellationTokenSource>();

        private TcpListener _listener { get; set; }
        private TcpListener _dataListener { get; set; }
        private TcpClient _dataClient { get; set; }
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
            //System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Ssl3;
            _serverCertificate = new X509Certificate2(path, password);
        }
        public void LoadCertificate(byte[] rawData, string password)
        {
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            //System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Ssl3;
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
                        var clientTask = Task.Run(() => HandleClientAsync(client, _serverCertificate), clientCancellationTokenSource.Token);


                        // Store the task in the dictionary
                        _clients[client] = clientTask;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception: {ex.Message}");
                        // You might want to log exceptions and continue accepting new clients
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

                try
                {
                    await Task.WhenAll(cancellationTasks);
                }
                catch (OperationCanceledException ex)
                {
                    Console.WriteLine(ex.Message);
                }
                Console.WriteLine("Server stopped.");
            }
            catch { }
        }

        #endregion

        private async Task HandleClientAsync(TcpClient client, X509Certificate2 certificate)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                bool isSecure = certificate != null;

                if (isSecure)
                {
                    SslStream sslStream = new SslStream(stream, false);
                    await sslStream.AuthenticateAsServerAsync(_serverCertificate);
                    await HandleSecurityAsync(client, sslStream);
                }
                else
                {
                    await HandleSecurityAsync(client, stream);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }

        private async Task HandleSecurityAsync(TcpClient client, Stream stream)
        {
            // Perform the WebSocket handshake
            string handshakeRequest = await ReadRequestAsync(stream);
            if (IsWebSocketHandshake(handshakeRequest, out string key))
            {
                await SendHandshakeResponseAsync(stream, key);
                Console.WriteLine("WebSocket handshake successful.");

                // Enter WebSocket communication loop
                while (client.Connected)
                {
                    string message = await ReadMessageAsync(stream);
                    if (message != null)
                    {
                        Console.WriteLine($"Received: {message}");
                        await SendMessageAsync(stream, message + " from server"); // Echo back
                    }
                }
            }
        }



        private async Task<string> ReadRequestAsync(Stream stream)
        {
            byte[] buffer = new byte[1024];
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
            responseBuilder.AppendLine(); // Adds the \r\n\r\n at the end of the response
            byte[] responseBytes = Encoding.UTF8.GetBytes(responseBuilder.ToString());
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
        }

        private async Task<string> ReadMessageAsync(Stream stream)
        {
            byte[] buffer = new byte[1024];
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
                // Handle extended payload length if needed
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
    }
}