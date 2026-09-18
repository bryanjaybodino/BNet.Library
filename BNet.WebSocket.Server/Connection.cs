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
    internal class MyClients
    {
        public Stream Stream { get; set; }
        public HashSet<string> Rooms { get; } = new HashSet<string>();
        public SemaphoreSlim WriteLock { get; } = new SemaphoreSlim(1, 1);
        public bool IsAlive { get; set; } = true;
    }

    public class Connection : EventHandlers
    {
        private TcpListener _listener;
        private readonly int _port;
        private readonly ConcurrentDictionary<TcpClient, MyClients> _clients = new ConcurrentDictionary<TcpClient, MyClients>();

        public bool IsRunning { get; private set; }
        private X509Certificate2 _serverCertificate;

        public DateTime StartTime { get; private set; }
        public int TotalConnectionsHandled { get; private set; } = 0;

        public Connection(int port)
        {
            _port = port;
            _listener = new TcpListener(IPAddress.Any, _port);
            StartTime = DateTime.Now;
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
                Console.WriteLine($"🚀 WebSocket Server started on port {_port}. Waiting for clients...");

                while (IsRunning)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    client.NoDelay = true;
                    _ = Task.Run(async () => await HandleClientAsync(client));
                }
            }
            catch (Exception ex)
            {
                await SetOnError($"StartAsync: {ex.Message}");
            }
        }

        public async Task StopAsync()
        {
            try
            {
                IsRunning = false;
                _listener?.Stop();

                var tasks = _clients.Keys.Select(async client =>
                {
                    await RemoveClientAsync(client);
                }).ToArray();

                await Task.WhenAll(tasks);
                Console.WriteLine("✅ Server stopped gracefully");
            }
            catch (Exception ex)
            {
                await SetOnError($"StopAsync: {ex.Message}");
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
                        if (secureStream == null) return;

                        var myClient = new MyClients { Stream = secureStream };
                        if (!_clients.TryAdd(client, myClient)) return;

                        await HandleStartupAsync(client, myClient);
                    }
                }
            }
            catch { }
            finally
            {
                await RemoveClientAsync(client);
            }
        }

        private async Task<Stream> HandleSecurityAsync(Stream stream)
        {
            if (_serverCertificate == null) return stream;

            try
            {
                var sslStream = new SslStream(stream, false);
                await sslStream.AuthenticateAsServerAsync(
                    _serverCertificate,
                    false,
                    SslProtocols.Tls12,
                    false
                );
                return sslStream;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SSL/TLS Error: {ex.Message}");
                return null;
            }
        }

        private async Task HandleStartupAsync(TcpClient client, MyClients myClient)
        {
            Stream stream = myClient.Stream;
            string handshakeRequest = await ReadRequestAsync(stream);

            if (IsWebSocketHandshake(handshakeRequest, out string key))
            {
                await SendHandshakeResponseAsync(stream, key);

                string roomId = ExtractRoomIdFromRequest(handshakeRequest);
                if (!string.IsNullOrEmpty(roomId))
                {
                    myClient.Rooms.Add(roomId);
                }

                await SetOnConnectedClient(_clients.Count);

                while (client.Connected && myClient.IsAlive)
                {
                    var result = await ReadFrameAsync(stream);

                    if (result.IsClosed) break;
                    if (result.Payload == null || result.Payload.Length == 0) continue;

                    if (result.Opcode == 1) // Text
                    {
                        string message = Encoding.UTF8.GetString(result.Payload);
                        await SetOnReceived(message);

                        if (string.IsNullOrEmpty(roomId))
                            await SendMessageAsync(message);
                        else
                            await SendMessageToRoomAsync(roomId, message);
                    }
                    else if (result.Opcode == 2) // Binary
                    {
                        await SetOnBinaryReceived(result.Payload);

                        if (string.IsNullOrEmpty(roomId))
                            await SendBinaryAsync(result.Payload);
                        else
                            await SendBinaryToRoomAsync(roomId, result.Payload);
                    }
                }
            }
            else if (IsHttpRequest(handshakeRequest))
            {
                await SendHttpStatusResponseAsync(stream, handshakeRequest);
            }
        }

        #region Frame Handling & Protocols

        private class FrameResult
        {
            public byte Opcode { get; set; }
            public byte[] Payload { get; set; }
            public bool IsClosed { get; set; }
        }

        private async Task<FrameResult> ReadFrameAsync(Stream stream)
        {
            try
            {
                byte[] header = await ReadExactBytesAsync(stream, 2);
                if (header == null) return new FrameResult { IsClosed = true };

                byte opcode = (byte)(header[0] & 0x0F);
                bool isMasked = (header[1] & 0x80) != 0;
                long payloadLength = header[1] & 0x7F;

                if (payloadLength == 126)
                {
                    byte[] extLen = await ReadExactBytesAsync(stream, 2);
                    if (extLen == null) return new FrameResult { IsClosed = true };
                    payloadLength = (extLen[0] << 8) | extLen[1];
                }
                else if (payloadLength == 127)
                {
                    byte[] extLen = await ReadExactBytesAsync(stream, 8);
                    if (extLen == null) return new FrameResult { IsClosed = true };
                    payloadLength = BitConverter.ToInt64(extLen.Reverse().ToArray(), 0);
                }

                byte[] maskingKey = null;
                if (isMasked)
                {
                    maskingKey = await ReadExactBytesAsync(stream, 4);
                    if (maskingKey == null) return new FrameResult { IsClosed = true };
                }

                byte[] payload = new byte[0];
                if (payloadLength > 0)
                {
                    payload = await ReadExactBytesAsync(stream, (int)payloadLength);
                    if (payload == null) return new FrameResult { IsClosed = true };
                }

                if (isMasked && payloadLength > 0)
                {
                    for (int i = 0; i < payload.Length; i++)
                    {
                        payload[i] ^= maskingKey[i % 4];
                    }
                }

                if (opcode == 8) // Close
                    return new FrameResult { IsClosed = true };

                if (opcode == 9) // Ping -> Respond with Pong
                {
                    byte[] pongFrame = BuildFrame(0x8A, payload);
                    await stream.WriteAsync(pongFrame, 0, pongFrame.Length);
                    await stream.FlushAsync();
                    return new FrameResult { Opcode = opcode, Payload = new byte[0] };
                }

                return new FrameResult { Opcode = opcode, Payload = payload };
            }
            catch
            {
                return new FrameResult { IsClosed = true };
            }
        }

        private async Task<byte[]> ReadExactBytesAsync(Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await stream.ReadAsync(buffer, totalRead, count - totalRead);
                if (read == 0) return null;
                totalRead += read;
            }
            return buffer;
        }

        #endregion

        #region Broadcast Methods

        public async Task SendMessageAsync(string message)
        {
            byte[] payload = Encoding.UTF8.GetBytes(message);
            byte[] frame = BuildFrame(0x81, payload);
            var tasks = _clients.Values.Select(c => WriteFrameAsync(c, frame));
            await Task.WhenAll(tasks);
        }

        public async Task SendMessageToRoomAsync(string roomId, string message)
        {
            byte[] payload = Encoding.UTF8.GetBytes(message);
            byte[] frame = BuildFrame(0x81, payload);
            var tasks = _clients.Values
                .Where(c => c.Rooms.Contains(roomId))
                .Select(c => WriteFrameAsync(c, frame));
            await Task.WhenAll(tasks);
        }

        public async Task SendBinaryAsync(byte[] data)
        {
            byte[] frame = BuildFrame(0x82, data);
            var tasks = _clients.Values.Select(c => WriteFrameAsync(c, frame));
            await Task.WhenAll(tasks);
        }

        public async Task SendBinaryToRoomAsync(string roomId, byte[] data)
        {
            byte[] frame = BuildFrame(0x82, data);
            var tasks = _clients.Values
                .Where(c => c.Rooms.Contains(roomId))
                .Select(c => WriteFrameAsync(c, frame));
            await Task.WhenAll(tasks);
        }

        private async Task WriteFrameAsync(MyClients client, byte[] frame)
        {
            await client.WriteLock.WaitAsync();
            try
            {
                if (client.IsAlive)
                {
                    await client.Stream.WriteAsync(frame, 0, frame.Length);
                    await client.Stream.FlushAsync();
                }
            }
            catch
            {
                client.IsAlive = false;
            }
            finally
            {
                client.WriteLock.Release();
            }
        }

        private byte[] BuildFrame(byte opcodeByte, byte[] payload)
        {
            int len = payload.Length;
            byte[] frame;

            if (len <= 125)
            {
                frame = new byte[2 + len];
                frame[1] = (byte)len;
            }
            else if (len <= 65535)
            {
                frame = new byte[4 + len];
                frame[1] = 126;
                frame[2] = (byte)(len >> 8);
                frame[3] = (byte)len;
            }
            else
            {
                frame = new byte[10 + len];
                frame[1] = 127;
                frame[2] = (byte)(len >> 56);
                frame[3] = (byte)(len >> 48);
                frame[4] = (byte)(len >> 40);
                frame[5] = (byte)(len >> 32);
                frame[6] = (byte)(len >> 24);
                frame[7] = (byte)(len >> 16);
                frame[8] = (byte)(len >> 8);
                frame[9] = (byte)len;
            }

            frame[0] = opcodeByte;
            Array.Copy(payload, 0, frame, frame.Length - len, len);
            return frame;
        }

        #endregion

        #region Helpers & Handshaking

        private async Task<string> ReadRequestAsync(Stream stream)
        {
            var sb = new StringBuilder();
            var buffer = new byte[8192];
            int read;

            try
            {
                do
                {
                    read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (read > 0)
                    {
                        sb.Append(Encoding.UTF8.GetString(buffer, 0, read));
                    }
                } while (read > 0 && !sb.ToString().EndsWith("\r\n\r\n"));
            }
            catch { }

            return sb.ToString();
        }

        private bool IsWebSocketHandshake(string request, out string key)
        {
            key = null;
            if (string.IsNullOrEmpty(request)) return false;

            var lines = request.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                {
                    key = line.Substring("Sec-WebSocket-Key:".Length).Trim();
                    return true;
                }
            }
            return false;
        }

        private async Task SendHandshakeResponseAsync(Stream stream, string key)
        {
            string acceptKey;
            using (var sha1 = SHA1.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11");
                acceptKey = Convert.ToBase64String(sha1.ComputeHash(bytes));
            }

            string response =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Accept: {acceptKey}\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "\r\n";

            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
            await stream.FlushAsync();
        }

        private bool IsHttpRequest(string request)
        {
            if (string.IsNullOrEmpty(request)) return false;
            return request.StartsWith("GET") || request.StartsWith("POST") || request.StartsWith("HEAD");
        }

        private async Task SendHttpStatusResponseAsync(Stream stream, string request)
        {
            try
            {
                var uptime = DateTime.Now - StartTime;
                string html = $"<!DOCTYPE html><html><head><meta charset='UTF-8'><title>Server</title></head><body><h2>✅ ONLINE</h2><p><b>Connections:</b> {_clients.Count}</p><p><b>Uptime:</b> {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m</p></body></html>";

                string response =
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: text/html; charset=UTF-8\r\n" +
                    $"Content-Length: {Encoding.UTF8.GetByteCount(html)}\r\n" +
                    "Connection: close\r\n" +
                    "Access-Control-Allow-Origin: *\r\n" +
                    "\r\n" + html;

                byte[] b = Encoding.UTF8.GetBytes(response);
                await stream.WriteAsync(b, 0, b.Length);
                await stream.FlushAsync();
            }
            catch { }
        }

        private static string ExtractRoomIdFromRequest(string request)
        {
            var lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);
            var firstLine = lines.FirstOrDefault();
            if (firstLine == null) return null;

            var parts = firstLine.Split(' ');
            if (parts.Length < 2 || !parts[1].Contains("?")) return null;

            string query = parts[1].Substring(parts[1].IndexOf('?') + 1);
            foreach (var part in query.Split('&'))
            {
                var kv = part.Split(new[] { '=' }, 2);
                if (kv.Length == 2 && kv[0].Equals("room", StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(kv[1]);
                }
            }
            return null;
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
                await SetOnDisconnectedClient(_clients.Count);
            }
        }

        #endregion
    }
}