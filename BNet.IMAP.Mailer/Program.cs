using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;

namespace BNet.IMAP.Mailer
{


    internal class Program
    {
        static void Main(string[] args)
        {
            string username = "zionstrategicnoreply@gmail.com";
            string password = "izlwwvhpeqvhbdrk";

            MailConfig mailConfig = new MailConfig();
            mailConfig.GetInbox(username, password);

            //var list = mailConfig.ListMailboxes();
            //for (int i =0; i < list.Count; i++)
            //{
            //    Console.WriteLine($"Mailbox {i + 1}: {list[i]}");
            //}

            foreach (var mail in mailConfig.Messages)
            {
                Console.WriteLine($"ID: {mail.Id}");
                Console.WriteLine($"From: {mail.From}");
                Console.WriteLine($"Subject: {mail.Subject}");
                Console.WriteLine($"Date: {mail.Date}");
                Console.WriteLine($"BodyText: {mail.PlainTextBody}");
                Console.WriteLine($"BodyHTML: {mail.HtmlBody}");
                Console.WriteLine(new string('-', 60));

                //[Gmail]/Trash
                //mailConfig.DeleteMessage(mail.Id);
            }
        }

    }
}