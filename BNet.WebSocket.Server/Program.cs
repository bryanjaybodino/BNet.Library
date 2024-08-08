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
       
        connection.LoadCertificate("C:\\MobileFTp.pfx", "123123");
        connection.EventHandlers.Received += EventHandlers_Received;
        connection.EventHandlers.ConnectedClient += EventHandlers_ConnectedClient;
        connection.EventHandlers.DisconnectedClient += EventHandlers_DisconnectedClient;

        // Start the TCP server
        Task serverTask = connection.StartAsync();

        // Start listening for key presses
        Task keyPressTask = Task.Run(() => ListenForKeyPress());

        // Await both tasks
        await Task.WhenAll(serverTask, keyPressTask);
    }

    private static void EventHandlers_DisconnectedClient(EventHandlers.DisconnectedClientEventArgs e)
    {
        Console.WriteLine("Total Clients : " + e.Count.ToString());
    }

    private static void EventHandlers_ConnectedClient(EventHandlers.ConnectedClientEventArgs e)
    {
        Console.WriteLine("Total Clients : "+e.Count.ToString());
    }

    private static void EventHandlers_Received(EventHandlers.ReceivedEventArgs e)
    {
        Console.WriteLine("Received Message: " + e.Message);
    }





    private static async void ListenForKeyPress()
    {
        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true); // Read the key without displaying it
            Console.WriteLine($"Key Pressed: {keyInfo.KeyChar}");

          await  connection.SendMessageAsync("Hellooooooo");

        }
    }

}
