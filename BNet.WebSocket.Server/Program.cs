using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using System.Net.Security;
using BNet.WebSocket.Server;

class Program
{
    static Connection connection = new Connection(8080);

    static async Task Main(string[] args)
    {
        //connection.Setup(8080);
        //connection.LoadCertificate("C:\\cert.pfx", "123123");

        connection.OnReceived += Connection_OnReceived;
        connection.OnBinaryReceived += Connection_OnBinaryReceived;
        connection.OnConnectedClient += Connection_OnConnectedClient;
        connection.OnDisconnectedClient += Connection_OnDisconnectedClient;
        connection.OnError += Connection_OnError;

        // Start the TCP server
        Task serverTask = connection.StartAsync();

        // Start listening for key presses
        Task keyPressTask = Task.Run(() => ListenForKeyPress());

        // Await both tasks
        await Task.WhenAll(serverTask, keyPressTask);


        // ── Text examples ────────────────────────────────────────────────────
        // Send text message to all connected clients
        await connection.SendMessageAsync("Hello everyone!");

        // Send text message to a specific room
        await connection.SendMessageToRoomAsync("Room1", "Hello Room1");
        // ws://localhost:8080?room=Room1

        // ── Binary examples ──────────────────────────────────────────────────
        // Send a JSON payload as binary to all clients
        byte[] binaryPayload = Encoding.UTF8.GetBytes("{\"type\":\"ping\"}");
        await connection.SendBinaryAsync(binaryPayload);

        // Send raw binary to a specific room
        await connection.SendBinaryToRoomAsync("Room1", binaryPayload);
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private static void Connection_OnError(object sender, EventHandlers.ErrorEventArgs e)
    {
        Console.WriteLine("Error: " + e.Message);
    }

    private static void Connection_OnDisconnectedClient(object sender, EventHandlers.DisconnectedClientEventArgs e)
    {
        Console.WriteLine("Total Clients: " + e.Count);
    }

    private static void Connection_OnConnectedClient(object sender, EventHandlers.ConnectedClientEventArgs e)
    {
        Console.WriteLine("Total Clients: " + e.Count);
    }

    private static void Connection_OnReceived(object sender, EventHandlers.ReceivedEventArgs e)
    {
        Console.WriteLine("Received Text: " + e.Message);
    }

    private static void Connection_OnBinaryReceived(object sender, EventHandlers.BinaryReceivedEventArgs e)
    {
        // e.Data is the raw byte[] from the binary WebSocket frame.
        // Here we decode it as UTF-8 JSON — adjust to your protocol as needed.
        string decoded = Encoding.UTF8.GetString(e.Data);
        Console.WriteLine($"Received Binary ({e.Data.Length} bytes): {decoded}");

        // Optional: echo the binary back to all clients
        // _ = connection.SendBinaryAsync(e.Data);
    }

    private static async void ListenForKeyPress()
    {
        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true);
            Console.WriteLine($"Key Pressed: {keyInfo.KeyChar}");

            if (keyInfo.Key == ConsoleKey.T)
            {
                // Send text frame
                await connection.SendMessageAsync("Text broadcast from server");
            }
            else if (keyInfo.Key == ConsoleKey.B)
            {
                // Send binary frame
                byte[] data = Encoding.UTF8.GetBytes("{\"type\":\"server-push\",\"msg\":\"binary broadcast\"}");
                await connection.SendBinaryAsync(data);
            }
        }
    }
}