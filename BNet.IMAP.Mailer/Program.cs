using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BNet.IMAP.Mailer
{


    internal class Program
    {
        static async Task Main(string[] args)
        {
            var mail = new MailConfig("your-email@gmail.com", "your-password");

            //GET ALL FOLDERS
            var folders = await mail.ListMailboxesAsync();

            foreach (var folder in folders)
            {
                Console.WriteLine(folder);
            }


            var inbox = await mail.GetInboxAsync(MailConfig.ImapFlags.UNSEEN);

            foreach (var message in inbox)
            {
                string id = message.Id;
                Console.WriteLine($"From: {message.From}");
                Console.WriteLine($"Subject: {message.Subject}");
                Console.WriteLine($"Date: {message.Date}");


                // GET FULL MESSAGE
                var fullMessage = await mail.GetFullMessageAsync(id);
                Console.WriteLine(fullMessage.Subject);
                Console.WriteLine(fullMessage.PlainTextBody);

                // MARK AS READ MESSAGE
                bool isSuccess1 = await mail.MarkAsSeenAsync(id);


                // DELETE MESSAGE
                bool isSuccess2 = await mail.DeleteMessageAsync(id);


                // MOVE TO FOLDER
                bool isSuccess3 = await mail.MoveToFolderAsync(id, folders[0]);

            }

            await mail.Logout();
        }


    }
}