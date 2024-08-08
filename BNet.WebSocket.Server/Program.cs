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
    static async Task Main(string[] args)
    {

        Connection connection = new Connection(8080);
        connection.LoadCertificate("C:\\MobileFTp.pfx", "123123");
        await connection.StartAsync();

    }


}
