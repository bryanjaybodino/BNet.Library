using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BNet.ZKTecoADMS.Sample
{
    internal class Program
    {
        //ZKTeco ADMS : Modal MB460 Plus
        static async Task Main(string[] args)
        {
            var server = new ZKTecoServer
            {
                Port = 4780,
                PhotoSaveDirectory = @"C:\ZKPhotos",
                Delay = 10,
                ErrorDelay = 30,
                TimeZone = 8
            };

            server.OnHandshake += (sender, e) =>
                Console.WriteLine($"Device connected: {e.SN}");

            server.OnAttendance += (sender, e) =>
                Console.WriteLine($"Punch: User={e.UserId} Time={e.PunchTime} Verify={e.VerifyMode}");

            server.OnPhotoReceived += (sender, e) =>
                Console.WriteLine($"Photo saved: {e.SavedPath}");

            server.OnHeartbeat += (sender, e) =>
                Console.WriteLine($"Heartbeat from {e.SN} — pending photos: {e.PhotoCount}");

            server.OnError += (sender, e) =>
                Console.WriteLine($"Error [{e.Source}]: {e.Exception?.Message}");

            await server.StartAsync();
            Console.ReadLine();
        }
    }
}
