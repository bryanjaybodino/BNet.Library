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
        // Start the TCP connection
        //connection.Setup(8080);
        //connection.LoadCertificate("C:\\cert.pfx", "123123");
        connection.OnReceived += Connection_OnReceived;
        connection.OnConnectedClient += Connection_OnConnectedClient;
        connection.OnDisconnectedClient += Connection_OnDisconnectedClient;
        connection.OnError += Connection_OnError;

        // Start the TCP server
        Task serverTask = connection.StartAsync();

        // Start listening for key presses
        Task keyPressTask = Task.Run(() => ListenForKeyPress());

        // Await both tasks
        await Task.WhenAll(serverTask, keyPressTask);


        //Send Message to all connected clients
        await connection.SendMessageAsync("Test Send Text Message to All Clients Connected");

        //Send Message to Specific Client Room 
        await connection.SendMessageToRoomAsync("Room1", "Hello Room1");
        //ws://localhost:8080?room=Room1
    }

    private static void Connection_OnError(object sender, EventHandlers.ErrorEventArgs e)
    {
        Console.WriteLine(e.Message);
    }

    private static void Connection_OnDisconnectedClient(object sender, EventHandlers.DisconnectedClientEventArgs e)
    {
        Console.WriteLine("Total Clients : " + e.Count.ToString());
    }

    private static void Connection_OnConnectedClient(object sender, EventHandlers.ConnectedClientEventArgs e)
    {
        Console.WriteLine("Total Clients : " + e.Count.ToString());
    }

    private static void Connection_OnReceived(object sender, EventHandlers.ReceivedEventArgs e)
    {
        Console.WriteLine("Received Message: " + e.Message);
    }


    private static async void ListenForKeyPress()
    {
        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true); // Read the key without displaying it
            Console.WriteLine($"Key Pressed: {keyInfo.KeyChar}");
            await connection.SendMessageAsync("Test Send Text Message to All Clients Connected");
        }
    }

}
