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

            foreach (var mail in mailConfig.Messages)
            {
                Console.WriteLine($"ID: {mail.Id}");
                Console.WriteLine($"From: {mail.From}");
                Console.WriteLine($"Subject: {mail.Subject}");
                Console.WriteLine($"Date: {mail.Date}");
                Console.WriteLine($"Body: {mail.Body}");
                Console.WriteLine(new string('-', 60));
            }
        }
        // ================= Helper Methods =================
    }
}