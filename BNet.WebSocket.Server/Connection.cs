﻿using System;
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
        private ConcurrentDictionary<TcpClient, Task> _clients = new ConcurrentDictionary<TcpClient, Task>();
        private ConcurrentDictionary<Stream, TcpClient> _streams = new ConcurrentDictionary<Stream, TcpClient>();
        private readonly ConcurrentDictionary<Task, CancellationTokenSource> _clientCancellationTokens = new ConcurrentDictionary<Task, CancellationTokenSource>();
        private TcpListener _listener { get; set; }
        public Dictionary<string, string> UserCredentials = new Dictionary<string, string>();
        public bool isRunning { get; private set; }
        private X509Certificate2 _serverCertificate { get; set; }

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
                isRunning = true;
                _listener.Start();
                Console.WriteLine("Server started. Waiting for clients...");

                while (isRunning)
                {
                    try
                    {
                        var client = await _listener.AcceptTcpClientAsync();
                        var clientCancellationTokenSource = new CancellationTokenSource();
                        var clientTask = Task.Run(() => HandleClientAsync(client), clientCancellationTokenSource.Token);

                        _clients[client] = clientTask;
                        _clientCancellationTokens[clientTask] = clientCancellationTokenSource;
                    }
                    catch (Exception ex)
                    {
                        SetOnError($"StartAsync : {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                SetOnError($"StartAsync : {ex.Message}");
            }
        }

        public async Task StopAsync()
        {
            try
            {
                isRunning = false;
                _listener.Stop();

                foreach (var tokenSource in _clientCancellationTokens.Values)
                {
                    tokenSource.Cancel();
                }

                foreach (var clientTask in _clients.Values)
                {
                    try
                    {
                        await clientTask;
                    }
                    catch (OperationCanceledException) { }
                }

                foreach (var stream in _streams.Keys)
                {
                    stream.Close();
                }
            }
            catch (Exception ex)
            {
                SetOnError($"StopAsync : {ex.Message}");
            }
        }

        private async Task<Stream> HandleSecurityAsync(Stream stream)
        {
            if (_serverCertificate != null)
            {
                var sslStream = new SslStream(stream, false);
                await sslStream.AuthenticateAsServerAsync(_serverCertificate);
                return sslStream;
            }
            return stream;
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            Stream secureStream = await HandleSecurityAsync(stream);

            var clientCancellationTokenSource = new CancellationTokenSource();
            var streamTask = Task.Run(() => clientCancellationTokenSource.Token);
            _streams[secureStream] = client;
            try
            {
                await HandleStartupAsync(client, secureStream);
            }
            catch (Exception ex)
            {
                SetOnError($"HandleClientAsync : {ex.Message}");
            }
            finally
            {
                if (client.Connected)
                {
                    client.Close();
                }
                dictionaryTCPClientRemove(client);
            }
        }

        private async Task HandleStartupAsync(TcpClient client, Stream stream)
        {
            string handshakeRequest = await ReadRequestAsync(client, stream);
            if (IsWebSocketHandshake(handshakeRequest, out string key))
            {
                await SendHandshakeResponseAsync(stream, key);
                SetOnConnectedClient(_streams.Count);

                while (client.Connected)
                {
                    string message = await ReadMessageAsync(client, stream);
                    if (message != null)
                    {
                        SetOnReceived(message);
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
                using (var sha1 = SHA1.Create())
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
            responseBuilder.AppendLine();
            byte[] responseBytes = Encoding.UTF8.GetBytes(responseBuilder.ToString());
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
        }

        private async Task<string> ReadMessageAsync(TcpClient client, Stream stream)
        {
            var messageBuilder = new List<byte>();
            bool isFinalFragment = false;

            int bufferSize = client.ReceiveBufferSize;

            try
            {
                while (!isFinalFragment)
                {
                    var buffer = new byte[bufferSize];
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead == 0)
                    {
                        return null;
                    }

                    var b0 = buffer[0];
                    isFinalFragment = (b0 & 0x80) != 0;
                    var isTextFrame = (b0 & 0x0F) == 1;

                    if (!isTextFrame)
                    {
                        throw new NotImplementedException("Only text frames are supported.");
                    }

                    int payloadLength = buffer[1] & 0x7F;
                    int headerSize = 2;

                    if (payloadLength == 126)
                    {
                        payloadLength = (buffer[2] << 8) | buffer[3];
                        headerSize += 2;
                    }
                    else if (payloadLength == 127)
                    {
                        payloadLength = (int)(
                            ((long)buffer[2] << 56) |
                            ((long)buffer[3] << 48) |
                            ((long)buffer[4] << 40) |
                            ((long)buffer[5] << 32) |
                            ((long)buffer[6] << 24) |
                            ((long)buffer[7] << 16) |
                            ((long)buffer[8] << 8) |
                            ((long)buffer[9])
                        );
                        headerSize += 8;
                    }

                    if (headerSize + 4 > bytesRead)
                    {
                        throw new InvalidOperationException("Header size exceeds bytes read.");
                    }

                    var maskingKey = new byte[4];
                    Array.Copy(buffer, headerSize, maskingKey, 0, 4);

                    var payloadOffset = headerSize + 4;
                    var payloadSize = bytesRead - payloadOffset;

                    if (payloadSize < 0)
                    {
                        throw new InvalidOperationException("Payload size calculation error.");
                    }

                    var payload = new byte[payloadSize];
                    Array.Copy(buffer, payloadOffset, payload, 0, payloadSize);

                    for (var i = 0; i < payload.Length; i++)
                    {
                        payload[i] ^= maskingKey[i % 4];
                    }

                    messageBuilder.AddRange(payload);

                    if (isFinalFragment)
                    {
                        break;
                    }
                }

                return Encoding.UTF8.GetString(messageBuilder.ToArray());
            }
            catch (Exception ex)
            {
                SetOnError($"ReadMessageAsync : {ex.Message}");
                return null;
            }
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

            frame[0] = 0x81;
            Array.Copy(payload, 0, frame, frame.Length - payloadLength, payloadLength);

            try
            {
                await stream.WriteAsync(frame, 0, frame.Length);
            }
            catch (Exception ex)
            {
                SetOnError($"WriteMessageAsync : {ex.Message}");
                throw;
            }
        }

        public async Task SendMessageAsync(string message)
        {
            var tasks = _streams.Keys.Select(stream => WriteMessageAsync(stream, message));
            await Task.WhenAll(tasks);
        }

        private void dictionaryTCPClientRemove(TcpClient client)
        {
            if (_clients.TryRemove(client, out var clientTask))
            {
                _clientCancellationTokens.TryRemove(clientTask, out var tokenSource);
                tokenSource?.Cancel();

                if (_streams.TryRemove(_streams.FirstOrDefault(pair => pair.Value == client).Key, out _))
                {
                    _streams.Values.FirstOrDefault(value => value == client)?.Close();
                }
                SetOnDisconnectedClient(_streams.Count);
            }
        }
    }
}
