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
            string username = "zionstrategicnoreply@gmail.com";
            string password = "izlwwvhpeqvhbdrk";
            MailConfig mailConfig = new MailConfig(username, password);
            var Inboxes = await mailConfig.GetInboxAsync(MailConfig.ImapFlags.UNSEEN);

            //var list = mailConfig.ListMailboxes();
            //for (int i =0; i < list.Count; i++)
            //{
            //    Console.WriteLine($"Mailbox {i + 1}: {list[i]}");
            //}

            foreach (var mail in Inboxes)
            {
                Console.WriteLine($"ID: {mail.Id}");
                Console.WriteLine($"From: {mail.From}");
                Console.WriteLine($"Subject: {mail.Subject}");
                Console.WriteLine($"Date: {mail.Date}");


                //var a = await mailConfig.MoveToFolderAsync(mail.Id, "[Gmail]/Trash");
                //Console.WriteLine(a);

                //var message = mailConfig.GetFullMessage(mail.Id);

                //Console.WriteLine($"BodyText: {message.PlainTextBody}");
                //Console.WriteLine($"BodyHTML: {message.HtmlBody}");
                Console.WriteLine(new string('-', 60));

                //[Gmail]/Trash
                //mailConfig.DeleteMessage(mail.Id);
            }
        }

    }
}