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
            string userEmail = "";
            string userPassword = "";
            string hostname = "";

            var mail = new MailConfig();
            await mail.ConnectAsync(userEmail, userPassword, hostname);


            var inbox = await mail.GetInboxAsync("INBOX", MailConfig.ImapFlags.UNSEEN);

            var count = await mail.GetEmailCountsByFlagsAsync("INBOX");

            int TotalAll = count[MailConfig.ImapFlags.ALL];
            int TotalSeen = count[MailConfig.ImapFlags.SEEN];
            int TotalUnseen = count[MailConfig.ImapFlags.UNSEEN];
            int TotalAnswered = count[MailConfig.ImapFlags.ANSWERED];
            int TotalUnanswered = count[MailConfig.ImapFlags.UNANSWERED];
            int TotalFlagged = count[MailConfig.ImapFlags.FLAGGED];
            int TotalUnflagged = count[MailConfig.ImapFlags.UNFLAGGED];
            int TotalDeleted = count[MailConfig.ImapFlags.DELETED];
            int TotalUndeleted = count[MailConfig.ImapFlags.UNDELETED];
            int TotalDraft = count[MailConfig.ImapFlags.DRAFT];
            int TotalUndraft = count[MailConfig.ImapFlags.UNDRAFT];

            Console.WriteLine("\n=== Individual Counts ===");
            Console.WriteLine($"TotalAll: {TotalAll}");
            Console.WriteLine($"TotalSeen: {TotalSeen}");
            Console.WriteLine($"TotalUnseen: {TotalUnseen}");
            Console.WriteLine($"TotalAnswered: {TotalAnswered}");
            Console.WriteLine($"TotalUnanswered: {TotalUnanswered}");
            Console.WriteLine($"TotalFlagged: {TotalFlagged}");
            Console.WriteLine($"TotalUnflagged: {TotalUnflagged}");
            Console.WriteLine($"TotalDeleted: {TotalDeleted}");
            Console.WriteLine($"TotalUndeleted: {TotalUndeleted}");
            Console.WriteLine($"TotalDraft: {TotalDraft}");
            Console.WriteLine($"TotalUndraft: {TotalUndraft}");





            //GET ALL FOLDERS
            var folders = await mail.ListMailboxesAsync();

            foreach (var folder in folders)
            {
                Console.WriteLine(folder);
            }

            foreach (var message in inbox)
            {
                string id = message.Id;
                Console.WriteLine($"Total: {message.TotalEmail}");
                Console.WriteLine($"Pagination: {message.TotalPagination}");
                Console.WriteLine($"From: {message.From}");
                Console.WriteLine($"Subject: {message.Subject}");
                Console.WriteLine($"Date: {message.Date}");


                //// GET FULL MESSAGE
                //var fullMessage = await mail.GetFullMessageAsync(id);
                //Console.WriteLine(fullMessage.Subject);
                //Console.WriteLine(fullMessage.PlainTextBody);

                //// MARK AS READ MESSAGE
                //bool isSuccess1 = await mail.MarkAsSeenAsync(id);


                //// DELETE MESSAGE
                //bool isSuccess2 = await mail.DeleteMessageAsync(id);


                //// MOVE TO FOLDER
                //bool isSuccess3 = await mail.MoveToFolderAsync(id, folders[0]);

            }

            await mail.Logout();
        }


    }
}