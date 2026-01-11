using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Security.Authentication;
namespace BNet.FTPServer
{
    public class Commands
    {
        //https://www.serv-u.com/resources/tutorial/cwd-cdup-pwd-rmd-dele-smnt-site-ftp-command
        #region Private Components
        private System.Text.Encoding encoding = System.Text.Encoding.UTF8;
        private ConcurrentDictionary<TcpClient, Task> _clients = new ConcurrentDictionary<TcpClient, Task>();
        private Dictionary<TcpClient, string> TcpClientDictionary = new Dictionary<TcpClient, string>();
        private readonly object _lock = new object(); //Thread Safety: Use the _lock object to synchronize access to _isRunning and _listener.
        private readonly ConcurrentDictionary<Task, CancellationTokenSource> _clientCancellationTokens = new ConcurrentDictionary<Task, CancellationTokenSource>();

        private TcpListener _listener { get; set; }
        private TcpListener _dataListener { get; set; }
        private TcpClient _dataClient { get; set; }
        private string _rootFolder { get; set; }
        private string _renameFrom { get; set; }
        private string currentUser { get; set; }
        #endregion

        #region Public Components
        public Dictionary<string, string> UserCredentials = new Dictionary<string, string>();
        public bool isRunning { get; private set; }
        #endregion

        #region Constructors
        public Commands()
        {
            _rootFolder = string.Empty;
            _listener = new TcpListener(IPAddress.Any, 21); // Default port 0 for demonstration                                             
        }
        public Commands(string rootFolder, int port = 21)
        {
            _rootFolder = Path.GetFullPath(rootFolder);
            _listener = new TcpListener(IPAddress.Any, port);
        }
        #endregion

        #region Setup
        public void Setup(string rootFolder, int port)
        {
            _rootFolder = Path.GetFullPath(rootFolder);
            _listener = new TcpListener(IPAddress.Any, port);
        }
        #endregion

        #region Certificates
        private X509Certificate2 _serverCertificate;
        public void LoadCertificate(string path, string password)
        {
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Ssl3;
            _serverCertificate = new X509Certificate2(path, password);
        }
        public void LoadCertificate(byte[] rawData, string password)
        {
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Ssl3;
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

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                TcpClientDictionary.Add(client, Path.GetFullPath(_rootFolder));
                var networkStream = client.GetStream();
                var reader = new StreamReader(networkStream, encoding);
                var writer = new StreamWriter(networkStream) { AutoFlush = true };

                await ReplyAsync(networkStream, writer, 220, "Welcome to Simple FTP Server");

                while (isRunning)
                {
                    try
                    {
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrEmpty(line)) continue;

                        var command = line.Split(' ')[0].ToUpperInvariant();
                        var argument = line.Length > command.Length ? line.Substring(command.Length + 1).Trim() : string.Empty;

                        Console.WriteLine($"Received command: {command} {argument}");

                        switch (command)
                        {
                            case "USER":
                                Console.ForegroundColor = ConsoleColor.White;
                                await HandleUserCommandAsync(client, networkStream, writer, argument);
                                Console.ResetColor();
                                break;
                            case "PASS":
                                Console.ForegroundColor = ConsoleColor.White;
                                await HandlePassCommandAsync(client, networkStream, writer, argument);
                                Console.ResetColor();
                                break;
                            case "PWD":
                                Console.ForegroundColor = ConsoleColor.Green;
                                await HandlePwdCommandAsync(client, networkStream, writer);
                                Console.ResetColor();
                                break;
                            case "CWD":
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                await HandleCwdCommandAsync(client, networkStream, writer, argument);
                                Console.ResetColor();
                                break;
                            case "PASV":
                                Console.ForegroundColor = ConsoleColor.Blue;
                                await HandlePasvCommandAsync(client, networkStream, writer);
                                Console.ResetColor();
                                break;
                            case "LIST":
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                await HandleListCommandAsync(client, networkStream, writer);
                                Console.ResetColor();
                                break;
                            case "STOR":
                                Console.ForegroundColor = ConsoleColor.DarkBlue;
                                await HandleStorCommandAsync(client, networkStream, writer, argument);
                                Console.ResetColor();
                                break;
                            case "RETR":
                                Console.ForegroundColor = ConsoleColor.DarkCyan;
                                await HandleRetrCommandAsync(client, networkStream, writer, argument);
                                Console.ResetColor();
                                break;
                            case "DELE":
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                await HandleDeleCommandAsync(client, networkStream, writer, argument);
                                Console.ResetColor();
                                break;
                            case "RMD":
                                Console.ForegroundColor = ConsoleColor.DarkGreen;
                                await HandleRmdCommandAsync(client, networkStream, writer, argument);
                                Console.ResetColor();
                                break;
                            case "MKD":
                                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                                await HandleMkdCommandAsync(client, networkStream, writer, argument);
                                Console.ResetColor();
                                break;
                            case "RNFR":
                                Console.ForegroundColor = ConsoleColor.DarkRed;
                                await HandleRnfrCommandAsync(client, networkStream, writer, argument);
                                Console.ResetColor();
                                break;
                            case "RNTO":
                                Console.ForegroundColor = ConsoleColor.DarkYellow;
                                await HandleRntoCommandAsync(client, networkStream, writer, argument);
                                Console.ResetColor();
                                break;
                            case "QUIT":
                                Console.ForegroundColor = ConsoleColor.Gray;
                                await ReplyAsync(networkStream, writer, 221, "Goodbye");
                                Console.ResetColor();
                                return;
                            case "AUTH": //CANT SEPERATE BECAUSE WE ARE UPDATING THE reader and writer
                                Console.ForegroundColor = ConsoleColor.White;
                                if (_serverCertificate != null && _serverCertificate.HasPrivateKey)
                                {
                                    bool isValid = _serverCertificate.Verify();
                                    if (argument == "TLS" && isValid)
                                    {
                                        await ReplyAsync(networkStream, writer, 234, "Enabling TLS Connection");
                                        // Ensure that no data is sent/received in plaintext after AUTH TLS
                                        var sslStream = new SslStream(networkStream, false, (sender, certificate, chain, sslPolicyErrors) => true);
                                        await sslStream.AuthenticateAsServerAsync(_serverCertificate);
                                        reader = new StreamReader(sslStream);
                                        writer = new StreamWriter(sslStream) { AutoFlush = true };
                                    }
                                    else
                                    {
                                        await ReplyAsync(networkStream, writer, 502, "Certificate is invalid");
                                    }
                                }
                                else
                                {
                                    await ReplyAsync(networkStream, writer, 502, "Command not implemented");
                                }
                                Console.ResetColor();
                                break;
                            case "NOOP":
                                Console.ForegroundColor = ConsoleColor.Magenta;
                                await ReplyAsync(networkStream, writer, 200, "NOOP command successful.");
                                Console.ResetColor();
                                break;
                            case "TYPE":
                                Console.ForegroundColor = ConsoleColor.Red;
                                await HandleTypeCommandAsync(client, networkStream, writer, argument);
                                Console.ResetColor();
                                break;
                            case "SITE":
                                Console.ForegroundColor = ConsoleColor.Green;
                                await HandleSiteCommandAsync(client, networkStream, writer, argument);
                                Console.ResetColor();
                                break;
                            case "OPTS":
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                await HandleOptsCommandAsync(client, networkStream, writer, argument);
                                Console.ResetColor();
                                break;
                            case "PORT":
                                Console.ForegroundColor = ConsoleColor.Blue;
                                await HandlePortCommandAsync(client, networkStream, writer, argument);
                                Console.ResetColor();
                                break;
                            case "SYST":
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                await ReplyAsync(networkStream, writer, 215, "UNIX Type: L8");
                                Console.ResetColor();
                                break;
                            default:
                                await ReplyAsync(networkStream, writer, 502, "Command not implemented");
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        await ReplyAsync(networkStream, writer, 500, "Internal error " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception while handling client: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("Client disconnected.");
                // Optionally remove the client from the dictionary
                dictionaryTCPClientRemove(client);

            }
        }


        #region HandlePortCommandAsync
        private async Task HandlePortCommandAsync(TcpClient client, NetworkStream networkStream, StreamWriter writer, string argument)
        {
            try
            {
                var parts = argument.Split(',');

                // Check if the PORT command has the correct number of arguments
                if (parts.Length != 6)
                {
                    await ReplyAsync(networkStream, writer, 501, "Syntax error in parameters or arguments.");
                    return;
                }

                // Extract IP address and port components
                var ipAddress = string.Join(".", parts.Take(4));
                var portHigh = int.Parse(parts[4]);
                var portLow = int.Parse(parts[5]);

                // Calculate the port number
                var port = (portHigh * 256) + portLow;

                // Validate and parse the IP address
                if (IPAddress.TryParse(ipAddress, out var address))
                {
                    // Set up the data listener on the specified IP address and port
                    _dataListener = new TcpListener(address, 0);
                    _dataListener.Start();

                    // Store the port for later use in data connection
                    await ReplyAsync(networkStream, writer, 200, "PORT command successful.");
                }
                else
                {
                    await ReplyAsync(networkStream, writer, 501, "501 Invalid IP address.");
                }
            }
            catch (Exception ex)
            {
                await ReplyAsync(networkStream, writer, 501, "501 Invalid IP address. " + ex.Message);
            }
            finally
            {
                _dataListener?.Stop();
                _dataListener = null;

            }
        }
        #endregion

        #region HandlePwdCommandAsync
        private async Task HandlePwdCommandAsync(TcpClient client, NetworkStream stream, StreamWriter writer)
        {
            string path = "";
            if (dictionaryCurrentDirectory(client).Replace(_rootFolder, "") == "")
            {
                path = "/";
            }
            else
            {
                path = dictionaryCurrentDirectory(client).Replace(_rootFolder, "");
            }
            await ReplyAsync(stream, writer, 257, $"\"{path}\" is current directory");
        }
        #endregion

        #region HandleTypeCommandAsync
        private async Task HandleTypeCommandAsync(TcpClient client, NetworkStream stream, StreamWriter writer, string argument)
        {
            if (argument == "I")
            {
                await ReplyAsync(stream, writer, 200, "Type set to I");
            }
            else if (argument == "A")
            {
                await ReplyAsync(stream, writer, 200, "Type set to A");
            }
            else
            {
                await ReplyAsync(stream, writer, 504, "Command not implemented for that argument");
            }
        }
        #endregion

        #region HandleOptsCommandAsync
        private async Task HandleOptsCommandAsync(TcpClient client, NetworkStream stream, StreamWriter writer, string argument)
        {
            if (argument.Equals("UTF8 ON", StringComparison.OrdinalIgnoreCase))
            {
                encoding = System.Text.Encoding.UTF8;
                await ReplyAsync(stream, writer, 200, "UTF8 encoding enabled");
            }
            else if (argument.Equals("UTF8 OFF", StringComparison.OrdinalIgnoreCase))
            {
                encoding = System.Text.Encoding.ASCII;
                await ReplyAsync(stream, writer, 200, "UTF8 encoding disabled");
            }
            else
            {
                await ReplyAsync(stream, writer, 501, "Unsupported option");
            }
        }
        #endregion

        #region HandleCwdCommandAsync
        private async Task HandleCwdCommandAsync(TcpClient client, NetworkStream stream, StreamWriter writer, string directoryName)
        {
            if (string.IsNullOrWhiteSpace(directoryName))
            {
                await ReplyAsync(stream, writer, 501, "Syntax error in parameters or arguments.");
                return;
            }

            string newDirectory;

            // Normalize and combine paths
            if (directoryName.Equals("/"))
            {
                newDirectory = Path.Combine(_rootFolder, directoryName.TrimStart('/'));
            }
            else
            {
                newDirectory = Path.Combine(dictionaryCurrentDirectory(client), directoryName);
            }

            // Ensure newDirectory is within the root folder
            if (!newDirectory.StartsWith(_rootFolder, StringComparison.OrdinalIgnoreCase))
            {
                newDirectory = _rootFolder + "" + newDirectory;
                //await ReplyAsync(stream, writer, 550, "Directory change not allowed.");
                //return;
            }

            if (Directory.Exists(newDirectory))
            {
                dictionaryTCPClientUpdate(client, newDirectory);
                await ReplyAsync(stream, writer, 250, "Directory successfully changed.");
            }
            else
            {
                await ReplyAsync(stream, writer, 550, "Directory not found.");
                return;
            }
        }
        #endregion

        #region HandleMkdCommandAsync
        private async Task HandleMkdCommandAsync(TcpClient client, NetworkStream stream, StreamWriter writer, string directoryName)
        {
            if (string.IsNullOrWhiteSpace(directoryName))
            {
                await ReplyAsync(stream, writer, 501, "Syntax error in parameters or arguments.");
                return;
            }

            string fullPath = Path.IsPathRooted(directoryName) ? Path.GetFullPath(directoryName) : Path.GetFullPath(Path.Combine(dictionaryCurrentDirectory(client), directoryName));

            try
            {
                Directory.CreateDirectory(fullPath);
                await ReplyAsync(stream, writer, 257, $"\"{directoryName}\" directory created.");
            }
            catch (Exception ex)
            {
                await ReplyAsync(stream, writer, 550, "Failed to create directory.");
            }
        }
        #endregion

        #region HandlePasvCommandAsync
        private async Task HandlePasvCommandAsync(TcpClient client, NetworkStream stream, StreamWriter writer)
        {
            try
            {

                const int minPort = 20022;
                const int maxPort = 49151;
                var port = new System.Random().Next(minPort, maxPort);
                _dataListener = new TcpListener(IPAddress.Any, 0);
                _dataListener.ExclusiveAddressUse = true;
                _dataListener.Server.NoDelay = true;
                _dataListener.Start();


                int _dataPort = ((IPEndPoint)_dataListener.LocalEndpoint).Port;


                var clientIpLAN = client.Client.LocalEndPoint; // GET CURRENT HOST  KUNG WIFI OR HOTSPOT IP ADDRESS GAMIT NI USER
                var clientIpWAN = client.Client.RemoteEndPoint;
                string _IPAddress = clientIpLAN.ToString().Split(':')[0];


                IPAddress ipAddress = IPAddress.Parse(_IPAddress);
                if (ipAddress == null)
                {
                    throw new Exception("No network adapters with an IPv4 address in the system!");
                }

                var ipAddressBytes = ipAddress.GetAddressBytes();
                var p1 = _dataPort / 256;
                var p2 = _dataPort % 256;

                var pasvResponse = $"Entering Passive Mode ({ipAddressBytes[0]},{ipAddressBytes[1]},{ipAddressBytes[2]},{ipAddressBytes[3]},{p1},{p2})";
                await ReplyAsync(stream, writer, 227, pasvResponse);
            }
            catch (Exception ex)
            {
                await ReplyAsync(stream, writer, 500, "Failed to enter passive mode. " + ex.Message);
            }
        }
        #endregion

        #region HandleListCommandAsync
        private async Task HandleListCommandAsync(TcpClient client, NetworkStream stream, StreamWriter writer)
        {
            await ReplyAsync(stream, writer, 150, "Here comes the directory listing");

            try
            {
                CheckConnection();
                Console.WriteLine("Waiting for data connection...");
                _dataClient = await _dataListener.AcceptTcpClientAsync();
                var dataStream = _dataClient.GetStream();
                var dataWriter = new StreamWriter(dataStream) { AutoFlush = true };

                if (!Directory.Exists(dictionaryCurrentDirectory(client)))
                {
                    await ReplyAsync(stream, writer, 550, "Directory not found.");
                    return;
                }

                await ListDirectoryContents(dataWriter, dictionaryCurrentDirectory(client));

                await dataWriter.FlushAsync();
                dataWriter.Close();
                dataStream.Close();
                _dataClient?.Close();
                await ReplyAsync(stream, writer, 226, "Directory send OK");
            }
            catch (IOException ioEx)
            {
                await ReplyAsync(stream, writer, 550, $"I/O error: {ioEx.Message}");
            }
            catch (UnauthorizedAccessException uaEx)
            {
                await ReplyAsync(stream, writer, 550, $"Access denied: {uaEx.Message}");
            }
            catch (Exception ex)
            {
                await ReplyAsync(stream, writer, 550, $"Failed to list directory: {ex.Message}");
            }
            finally
            {
                _dataClient?.Close();
                _dataClient = null;
                Console.WriteLine("Data connection closed\n");
            }
        }
        #endregion

        #region HandleStorCommandAsync
        private async Task HandleStorCommandAsync(TcpClient client, NetworkStream stream, StreamWriter writer, string fileName)
        {
            CheckConnection();
            if (string.IsNullOrWhiteSpace(fileName))
            {
                await ReplyAsync(stream, writer, 501, "Syntax error in parameters or arguments.");
                return;
            }

            await ReplyAsync(stream, writer, 150, "Opening data connection for file transfer");

            try
            {
                _dataClient = await _dataListener.AcceptTcpClientAsync();
                var dataStream = _dataClient.GetStream();

                var filePath = Path.Combine(dictionaryCurrentDirectory(client), fileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    await dataStream.CopyToAsync(fileStream);
                }

                await ReplyAsync(stream, writer, 226, "Transfer complete");
            }
            catch (Exception ex)
            {
                await ReplyAsync(stream, writer, 550, $"Failed to save file: {ex.Message}");
            }
            finally
            {
                _dataClient?.Close();
                _dataClient = null;
                Console.WriteLine("Data connection closed\n");
            }
        }
        #endregion

        #region HandleRetrCommandAsync
        private async Task HandleRetrCommandAsync(TcpClient client, NetworkStream stream, StreamWriter writer, string fileName)
        {
            CheckConnection();
            if (string.IsNullOrWhiteSpace(fileName))
            {
                await ReplyAsync(stream, writer, 501, "Syntax error in parameters or arguments.");
                return;
            }

            var filePath = Path.Combine(dictionaryCurrentDirectory(client), fileName);

            if (!File.Exists(filePath))
            {
                await ReplyAsync(stream, writer, 550, "File not found.");
                return;
            }


            await ReplyAsync(stream, writer, 150, "Opening data connection for file transfer");

            try
            {
                _dataClient = await _dataListener.AcceptTcpClientAsync();
                var dataStream = _dataClient.GetStream();

                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    await fileStream.CopyToAsync(dataStream);
                }

                await ReplyAsync(stream, writer, 226, "Transfer complete");
            }
            catch (Exception ex)
            {
                await ReplyAsync(stream, writer, 550, $"Failed to retrieve file: {ex.Message}");
            }
            finally
            {
                _dataClient?.Close();
                _dataClient = null;
                Console.WriteLine("Data connection closed\n");
            }
        }
        #endregion

        #region HandleRnfrCommandAsync
        private async Task HandleRnfrCommandAsync(TcpClient client, NetworkStream stream, StreamWriter writer, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                await ReplyAsync(stream, writer, 501, "Syntax error in parameters or arguments.");
                return;
            }

            var filePath = Path.Combine(dictionaryCurrentDirectory(client), fileName);

            if (!File.Exists(filePath) && !Directory.Exists(filePath))
            {
                await ReplyAsync(stream, writer, 550, "File or directory not found.");
                return;
            }

            _renameFrom = filePath;
            await ReplyAsync(stream, writer, 350, "Requested file action pending further information.");
        }
        #endregion

        #region HandleRntoCommandAsync
        private async Task HandleRntoCommandAsync(TcpClient client, NetworkStream stream, StreamWriter writer, string newFileName)
        {
            if (string.IsNullOrWhiteSpace(_renameFrom))
            {
                await ReplyAsync(stream, writer, 503, "Bad sequence of commands.");
                return;
            }

            var newFilePath = Path.Combine(dictionaryCurrentDirectory(client), newFileName);

            try
            {
                if (File.Exists(_renameFrom))
                {
                    File.Move(_renameFrom, newFilePath);
                }
                else if (Directory.Exists(_renameFrom))
                {
                    Directory.Move(_renameFrom, newFilePath);
                }
                else
                {
                    await ReplyAsync(stream, writer, 550, "File or directory not found.");
                    return;
                }

                _renameFrom = null;
                await ReplyAsync(stream, writer, 250, "Requested file action okay, completed.");
            }
            catch (Exception ex)
            {
                await ReplyAsync(stream, writer, 550, $"Failed to rename file or directory: {ex.Message}");
            }
        }
        #endregion

        #region HandleDeleCommandAsync
        private async Task HandleDeleCommandAsync(TcpClient client, NetworkStream stream, StreamWriter writer, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                await ReplyAsync(stream, writer, 501, "Syntax error in parameters or arguments.");
                return;
            }

            var filePath = Path.Combine(dictionaryCurrentDirectory(client), fileName);

            if (!File.Exists(filePath))
            {
                await ReplyAsync(stream, writer, 550, "File not found.");
                return;
            }

            try
            {
                File.Delete(filePath);
                await ReplyAsync(stream, writer, 250, "File deleted successfully.");
            }
            catch (Exception ex)
            {
                await ReplyAsync(stream, writer, 550, $"Failed to delete file: {ex.Message}");
            }
        }
        #endregion

        #region HandleRmdCommandAsync
        private async Task HandleRmdCommandAsync(TcpClient client, NetworkStream stream, StreamWriter writer, string directoryName)
        {
            if (string.IsNullOrWhiteSpace(directoryName))
            {
                await ReplyAsync(stream, writer, 501, "Syntax error in parameters or arguments.");
                return;
            }

            var dirPath = Path.Combine(dictionaryCurrentDirectory(client), directoryName);

            if (!Directory.Exists(dirPath))
            {
                await ReplyAsync(stream, writer, 550, "Directory not found.");
                return;
            }

            try
            {
                Directory.Delete(dirPath, true);
                await ReplyAsync(stream, writer, 250, "Directory removed successfully.");
            }
            catch (Exception ex)
            {
                await ReplyAsync(stream, writer, 550, $"Failed to remove directory: {ex.Message}");
            }
        }
        #endregion

        #region HandleSiteCommandAsync
        private async Task HandleSiteCommandAsync(TcpClient client, NetworkStream stream, StreamWriter writer, string argument)
        {
            if (argument.Equals("CHMOD 777", StringComparison.OrdinalIgnoreCase))
            {
                await ReplyAsync(stream, writer, 200, "SITE command executed successfully.");
            }
            else
            {
                await ReplyAsync(stream, writer, 501, "Unsupported SITE command.");
            }
        }
        #endregion

        #region ListDirectoryContents
        private async Task ListDirectoryContents(StreamWriter dataWriter, string directory)
        {
            var dirs = Directory.GetDirectories(directory);
            var files = Directory.GetFiles(directory);

            foreach (var dir in dirs)
            {
                dataWriter.AutoFlush = true;
                var dirInfo = new DirectoryInfo(dir);
                var date = dirInfo.LastWriteTime.ToString("MMM dd yyyy");
                await dataWriter.WriteLineAsync($"drwxr-xr-x 1 owner group 0 {date} {dirInfo.Name}");
                await dataWriter.FlushAsync();
            }

            foreach (var file in files)
            {
                dataWriter.AutoFlush = true;
                var fileInfo = new FileInfo(file);
                var date = fileInfo.LastWriteTime.ToString("MMM dd yyyy");
                await dataWriter.WriteLineAsync($"-rw-r--r-- 1 owner group {fileInfo.Length} {date} {fileInfo.Name}");
                await dataWriter.FlushAsync();
            }
        }
        #endregion

        #region HandleUserCommandAsync
        private async Task HandleUserCommandAsync(TcpClient client, NetworkStream stream, StreamWriter writer, string username)
        {
            if (UserCredentials.Count > 0)
            {
                if (string.IsNullOrEmpty(username))
                {
                    await ReplyAsync(stream, writer, 501, "Syntax error in parameters or arguments.");
                    return;
                }

                if (UserCredentials.ContainsKey(username))
                {
                    currentUser = username;
                    await ReplyAsync(stream, writer, 331, "User name okay, need password.");
                }
                else
                {
                    await ReplyAsync(stream, writer, 430, "Invalid username.");
                }
            }
            else
            {
                await ReplyAsync(stream, writer, 331, "Login as Anonymous");
            }
        }
        #endregion

        #region HandlePassCommandAsync
        private async Task HandlePassCommandAsync(TcpClient client, NetworkStream stream, StreamWriter writer, string password)
        {
            if (UserCredentials.Count > 0)
            {
                if (currentUser == null)
                {
                    await ReplyAsync(stream, writer, 503, "Login with USER first.");
                    return;
                }

                if (string.IsNullOrEmpty(password))
                {
                    await ReplyAsync(stream, writer, 501, "Syntax error in parameters or arguments.");
                    return;
                }

                if (UserCredentials.TryGetValue(currentUser, out var storedPassword) && storedPassword == password)
                {
                    await ReplyAsync(stream, writer, 230, "User logged in, proceed.");
                }
                else
                {
                    await ReplyAsync(stream, writer, 430, "Invalid password.");
                }
            }
            else
            {
                await ReplyAsync(stream, writer, 230, "Login in proceed");
            }
        }
        #endregion

        #region ReplyAsync
        private async Task ReplyAsync(NetworkStream stream, StreamWriter writer, int code, string message)
        {
            var response = $"{code} {message}\r\n";
            var responseBytes = System.Text.Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
            await stream.FlushAsync();
            await writer.FlushAsync();
            Console.WriteLine("Server Reply : " + message + "\n");
        }
        #endregion


        private void CheckConnection()
        {
            if (_dataListener == null)
            {
                _dataListener = new TcpListener(IPAddress.Any, 0);
                _dataListener.Start();
            }
        }

        #region THIS CODES IS FOR LISTING ALL TCP CLIENTS TO GET CURRENT DIRECTORY
        private string dictionaryCurrentDirectory(TcpClient client)
        {
            return TcpClientDictionary[client].ToString();
        }
        private void dictionaryTCPClientRemove(TcpClient client)
        {
            TcpClientDictionary.Remove(client);
            _clients.TryRemove(client, out _);
        }
        private void dictionaryTCPClientUpdate(TcpClient client, string newDirectory)
        {
            TcpClientDictionary[client] = newDirectory;
        }
        #endregion

    }
}