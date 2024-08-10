using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;

namespace BNet.FTPServer
{

    //https://www.serv-u.com/resources/tutorial/cwd-cdup-pwd-rmd-dele-smnt-site-ftp-command
    public class Commands
    {
        private TcpListener _listener;
        private bool _isRunning;
        private string _currentDirectory;
        private string _rootFolder;
        private TcpListener _dataListener;
        private TcpClient _dataClient;
        private string _renameFrom;
        private System.Text.Encoding encoding = System.Text.Encoding.UTF8;
        private string currentUser { get; set; }
        public Dictionary<string, string> UserCredentials = new Dictionary<string, string>();
        public bool isRunning { get; private set; }
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


        public void Setup(string rootFolder, int port)
        {
            _rootFolder = Path.GetFullPath(rootFolder);
            _listener = new TcpListener(IPAddress.Any, port);
        }

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

        public async Task StartAsync()
        {
            Console.WriteLine("\nStarting FTP server...");
            _isRunning = true;
            _listener.Start();
            while (_isRunning)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();

                    Console.WriteLine("Client connected.");
                    await HandleClientAsync(client);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accepting client: {ex.Message}");
                }
            }
        }

        public async Task StopAsync()
        {
            try
            {
                isRunning = false;
                _listener.Stop();
            }
            catch { }
        }
        private async Task HandleClientAsync(TcpClient client)
        {
            _currentDirectory = Path.GetFullPath(_rootFolder);
            var networkStream = client.GetStream();
            var reader = new StreamReader(networkStream, encoding);
            var writer = new StreamWriter(networkStream) { AutoFlush = true };

            await ReplyAsync(networkStream, writer, 220, "Welcome to Simple FTP Server");

            while (_isRunning)
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
                            await HandlePwdCommandAsync(networkStream, writer);
                            Console.ResetColor();
                            break;
                        case "CWD":
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            await HandleCwdCommandAsync(networkStream, writer, argument);
                            Console.ResetColor();
                            break;
                        case "PASV":
                            Console.ForegroundColor = ConsoleColor.Blue;
                            await HandlePasvCommandAsync(networkStream, writer, client);
                            Console.ResetColor();
                            break;
                        case "LIST":
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            await HandleListCommandAsync(networkStream, writer);
                            Console.ResetColor();
                            break;
                        case "STOR":
                            Console.ForegroundColor = ConsoleColor.DarkBlue;
                            await HandleStorCommandAsync(networkStream, writer, argument);
                            Console.ResetColor();
                            break;
                        case "RETR":
                            Console.ForegroundColor = ConsoleColor.DarkCyan;
                            await HandleRetrCommandAsync(networkStream, writer, argument);
                            Console.ResetColor();
                            break;
                        case "DELE":
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            await HandleDeleCommandAsync(networkStream, writer, argument);
                            Console.ResetColor();
                            break;
                        case "RMD":
                            Console.ForegroundColor = ConsoleColor.DarkGreen;
                            await HandleRmdCommandAsync(networkStream, writer, argument);
                            Console.ResetColor();
                            break;
                        case "MKD":
                            Console.ForegroundColor = ConsoleColor.DarkMagenta;
                            await HandleMkdCommandAsync(networkStream, writer, argument);
                            Console.ResetColor();
                            break;
                        case "RNFR":
                            Console.ForegroundColor = ConsoleColor.DarkRed;
                            await HandleRnfrCommandAsync(networkStream, writer, argument);
                            Console.ResetColor();
                            break;
                        case "RNTO":
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            await HandleRntoCommandAsync(networkStream, writer, argument);
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
                            await HandleTypeCommandAsync(networkStream, writer, argument);
                            break;
                        case "SITE":
                            await HandleSiteCommandAsync(networkStream, writer, argument);
                            break;
                        case "OPTS":
                            await HandleOptsCommandAsync(networkStream, writer, argument);
                            break;
                        case "PORT":
                            await HandlePortCommandAsync(networkStream, writer, argument);
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
                    await ReplyAsync(networkStream, writer, 500, "Internal error");
                }
            }
        }

        private async Task HandlePortCommandAsync(NetworkStream networkStream, StreamWriter writer, string argument)
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
                _dataListener = new TcpListener(address, port);
                _dataListener.Start();

                // Store the port for later use in data connection
                await ReplyAsync(networkStream, writer, 200, "PORT command successful.");
            }
            else
            {
                await ReplyAsync(networkStream, writer, 501, "501 Invalid IP address.");
            }
        }



        private async Task HandlePwdCommandAsync(NetworkStream stream, StreamWriter writer)
        {
            string path = "";
            if (_currentDirectory.Replace(_rootFolder, "") == "")
            {
                path = "/";
            }
            else
            {
                path = _currentDirectory.Replace(_rootFolder, "");
            }
            await ReplyAsync(stream, writer, 257, $"\"{path}\" is current directory");
        }

        private async Task HandleTypeCommandAsync(NetworkStream stream, StreamWriter writer, string argument)
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

        private async Task HandleOptsCommandAsync(NetworkStream stream, StreamWriter writer, string argument)
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

        private async Task HandleCwdCommandAsync(NetworkStream stream, StreamWriter writer, string directoryName)
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
                newDirectory = Path.Combine(_currentDirectory, directoryName);
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
                _currentDirectory = newDirectory;
                await ReplyAsync(stream, writer, 250, "Directory successfully changed.");
            }
            else
            {
                await ReplyAsync(stream, writer, 550, "Directory not found.");
                return;
            }
        }

        private async Task HandleMkdCommandAsync(NetworkStream stream, StreamWriter writer, string directoryName)
        {
            if (string.IsNullOrWhiteSpace(directoryName))
            {
                await ReplyAsync(stream, writer, 501, "Syntax error in parameters or arguments.");
                return;
            }

            string fullPath = Path.IsPathRooted(directoryName) ? Path.GetFullPath(directoryName) : Path.GetFullPath(Path.Combine(_currentDirectory, directoryName));

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

        private async Task HandlePasvCommandAsync(NetworkStream stream, StreamWriter writer, TcpClient client)
        {
            try
            {
                var port = new Random().Next(2022, 7000);
                _dataListener = new TcpListener(IPAddress.Any, 0);
                _dataListener.ExclusiveAddressUse = true;
                _dataListener.Server.NoDelay = true;
                _dataListener.Server.UseOnlyOverlappedIO = true;
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
                await ReplyAsync(stream, writer, 500, "Failed to enter passive mode.");
            }
        }

        private async Task HandleListCommandAsync(NetworkStream stream, StreamWriter writer)
        {
            await ReplyAsync(stream, writer, 150, "Here comes the directory listing");

            try
            {
                if (_dataListener == null)
                {
                    await ReplyAsync(stream, writer, 425, "Can't open data connection.");
                    return;
                }

                Console.WriteLine("Waiting for data connection...");
                _dataClient = await _dataListener.AcceptTcpClientAsync();
                var dataStream = _dataClient.GetStream();
                var dataWriter = new StreamWriter(dataStream) { AutoFlush = true };

                if (!Directory.Exists(_currentDirectory))
                {
                    await ReplyAsync(stream, writer, 550, "Directory not found.");
                    return;
                }

                await ListDirectoryContents(dataWriter, _currentDirectory);

                await dataWriter.FlushAsync();
                dataWriter.Close();
                dataStream.Close();
                _dataClient.Close();

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
        private async Task HandleStorCommandAsync(NetworkStream stream, StreamWriter writer, string fileName)
        {
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

                var filePath = Path.Combine(_currentDirectory, fileName);
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

        private async Task HandleRetrCommandAsync(NetworkStream stream, StreamWriter writer, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                await ReplyAsync(stream, writer, 501, "Syntax error in parameters or arguments.");
                return;
            }

            var filePath = Path.Combine(_currentDirectory, fileName);

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

        private async Task HandleRnfrCommandAsync(NetworkStream stream, StreamWriter writer, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                await ReplyAsync(stream, writer, 501, "Syntax error in parameters or arguments.");
                return;
            }

            var filePath = Path.Combine(_currentDirectory, fileName);

            if (!File.Exists(filePath) && !Directory.Exists(filePath))
            {
                await ReplyAsync(stream, writer, 550, "File or directory not found.");
                return;
            }

            _renameFrom = filePath;
            await ReplyAsync(stream, writer, 350, "Requested file action pending further information.");
        }

        private async Task HandleRntoCommandAsync(NetworkStream stream, StreamWriter writer, string newFileName)
        {
            if (string.IsNullOrWhiteSpace(_renameFrom))
            {
                await ReplyAsync(stream, writer, 503, "Bad sequence of commands.");
                return;
            }

            var newFilePath = Path.Combine(_currentDirectory, newFileName);

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

        private async Task HandleDeleCommandAsync(NetworkStream stream, StreamWriter writer, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                await ReplyAsync(stream, writer, 501, "Syntax error in parameters or arguments.");
                return;
            }

            var filePath = Path.Combine(_currentDirectory, fileName);

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

        private async Task HandleRmdCommandAsync(NetworkStream stream, StreamWriter writer, string directoryName)
        {
            if (string.IsNullOrWhiteSpace(directoryName))
            {
                await ReplyAsync(stream, writer, 501, "Syntax error in parameters or arguments.");
                return;
            }

            var dirPath = Path.Combine(_currentDirectory, directoryName);

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

        private async Task HandleSiteCommandAsync(NetworkStream stream, StreamWriter writer, string argument)
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
        private async Task ReplyAsync(NetworkStream stream, StreamWriter writer, int code, string message)
        {
            var response = $"{code} {message}\r\n";
            var responseBytes = System.Text.Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
            await stream.FlushAsync();
            await writer.FlushAsync();
            Console.WriteLine("Server Reply : " + message + "\n");
        }
    }
}