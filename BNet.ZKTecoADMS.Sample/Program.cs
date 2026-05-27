using System;
using System.Threading.Tasks;

namespace BNet.ZKTecoADMS.Sample
{
    internal class Program
    {
        // ZKTeco ADMS — Model [MB460 Plus]
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
                Console.WriteLine(
                    "[" + e.Timestamp.ToString("HH:mm:ss") + "] " +
                    "Device connected: " + e.SN);

            server.OnAttendance += (sender, e) =>
                Console.WriteLine(
                    "[" + e.Timestamp.ToString("HH:mm:ss") + "] " +
                    "Punch:" +
                    " User=" + e.UserId +
                    " Time=" + e.PunchTime.ToString("yyyy-MM-dd HH:mm:ss") +
                    " Verify=" + ZKTecoHelper.VerifyLabel(e.VerifyMode) +
                    " Type=" + e.PunchType);

            server.OnPhotoReceived += (sender, e) =>
            {
                if (e.Success)
                    Console.WriteLine(
                        "[" + e.Timestamp.ToString("HH:mm:ss") + "] " +
                        "Photo saved: " + e.SavedPath);
                else
                    Console.WriteLine(
                        "[" + e.Timestamp.ToString("HH:mm:ss") + "] " +
                        "Photo FAILED for user: " + e.UserId);
            };
            server.OnRawRequest += (sender, e) =>
            {
                if (e.Table == "ATTLOG")
                    Console.WriteLine("[RAW ATTLOG]\n" + e.Body);
            };

            server.OnHeartbeat += (sender, e) =>
                Console.WriteLine(
                    "[" + e.Timestamp.ToString("HH:mm:ss") + "] " +
                    "Heartbeat: " + e.SN +
                    " pending photos=" + e.PhotoCount);

            server.OnError += (sender, e) =>
                Console.WriteLine(
                    "[" + e.Timestamp.ToString("HH:mm:ss") + "] " +
                    "ERROR [" + e.Source + "]: " + e.Exception?.Message);

            Console.WriteLine("ZKTeco ADMS server starting on port " + server.Port + "...");
            await server.StartAsync();
            Console.WriteLine("Press ENTER to stop.");
            Console.ReadLine();
            await server.StopAsync();
        }
    }
}