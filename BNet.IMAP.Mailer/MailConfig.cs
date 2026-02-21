using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace BNet.IMAP.Mailer
{
    public class MailMessage
    {
        public string Id;
        public string From;
        public string Subject;
        public DateTime Date;
        public string HtmlBody;
        public string PlainTextBody;
    }

    public class MailConfig
    {
        private TcpClient tcpClient;
        private SslStream sslStream;
        private StreamReader reader;
        private StreamWriter writer;
        private int tagCounter = 1;

        public List<MailMessage> Messages = new List<MailMessage>();

        public enum ImapFlags
        {
            ALL,
            SEEN,
            UNSEEN,
            DELETED,
            UNDELETED,
        }
        public void GetInbox(string username, string password, ImapFlags imapFlags = ImapFlags.UNSEEN, string host = "imap.gmail.com", int port = 993)
        {
            tcpClient = new TcpClient(host, port);
            sslStream = new SslStream(tcpClient.GetStream(), false,
                (sender, cert, chain, errors) => true);
            sslStream.AuthenticateAsClient(host, null, SslProtocols.Tls12, false);

            reader = new StreamReader(sslStream);
            writer = new StreamWriter(sslStream) { AutoFlush = true };

            ReadLine(); // server greeting

            // LOGIN
            string tagLogin = GetTag();
            writer.WriteLine($"{tagLogin} LOGIN {username} {password}");
            EnsureOk(ReadResponse(tagLogin));

            // SELECT INBOX
            string tagSelect = GetTag();
            writer.WriteLine($"{tagSelect} SELECT INBOX");
            EnsureOk(ReadResponse(tagSelect));

            // SEARCH UNSEEN
            string tagSearch = GetTag();
            writer.WriteLine($"{tagSearch} UID SEARCH {imapFlags.ToString()}");
            string searchResponse = ReadResponse(tagSearch);

            string[] uids = ParseMessageIds(searchResponse);

            foreach (var uid in uids)
            {
                if (string.IsNullOrWhiteSpace(uid)) continue;

                var mail = new MailMessage { Id = uid };

                string tagFetch = GetTag();
                writer.WriteLine($"{tagFetch} UID FETCH {uid} (BODY.PEEK[])"); // ✅ Correct parentheses

                string fullMessage = ReadResponse(tagFetch);


                mail.From = GetHeaderValue(fullMessage, "From");
                mail.Subject = DecodeMimeEncodedWords(GetHeaderValue(fullMessage, "Subject"));
                mail.Date = ParseDate(GetHeaderValue(fullMessage, "Date"));

                ExtractBodies(fullMessage, out string html);

                mail.PlainTextBody = HtmlToPlainText(html);
                mail.HtmlBody = html;

                Messages.Add(mail);
            }
        }
        public void Logout()
        {
            if (writer != null)
            {
                string tag = GetTag();
                writer.WriteLine($"{tag} LOGOUT");
                ReadResponse(tag);
            }
            reader?.Close();
            writer?.Close();
            sslStream?.Close();
            tcpClient?.Close();
        }
        public bool MarkAsSeen(string id)
        {
            if (writer == null)
                throw new Exception("Not connected.");

            string tag = GetTag();
            writer.WriteLine($"{tag} UID STORE {id} +FLAGS (\\Seen)");

            string response = ReadResponse(tag);
            return EnsureOk(response);
        }
        public bool DeleteMessage(string id)
        {
            if (writer == null)
                throw new Exception("Not connected.");

            string tag = GetTag();
            writer.WriteLine($"{tag} UID STORE {id} +FLAGS (\\Deleted)");

            string response = ReadResponse(tag);
            EnsureOk(response);

            // Permanently remove messages marked as \Deleted
            tag = GetTag();
            writer.WriteLine($"{tag} EXPUNGE");

            response = ReadResponse(tag);
            return EnsureOk(response);
        }
        public bool MoveToFolder(string id, string folder)
        {
            if (writer == null)
                throw new Exception("Not connected.");

            string tag = GetTag();

            writer.WriteLine($"{tag} UID MOVE {id} \"{folder}\"");

            string response = ReadResponse(tag);
            return EnsureOk(response);
        }
        public List<string> ListMailboxes()
        {
            if (writer == null)
                throw new Exception("Not connected.");

            string tag = GetTag();
            writer.WriteLine($"{tag} LIST \"\" \"*\"");

            string response = ReadResponse(tag);
            EnsureOk(response);

            var mailboxes = new List<string>();
            var lines = response.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.StartsWith("* LIST"))
                {
                    int lastQuote = line.LastIndexOf("\"");
                    int firstQuote = line.LastIndexOf("\"", lastQuote - 1);

                    if (firstQuote >= 0 && lastQuote > firstQuote)
                    {
                        string mailbox = line.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                        mailboxes.Add(mailbox);
                    }
                }
            }

            return mailboxes;
        }
        #region Body Extraction

        private void ExtractBodies(string message, out string html)
        {

            html = null;

            // Remove IMAP FETCH headers
            var fetchMatch = Regex.Match(message, @"\* \d+ FETCH .*?BODY\[\].*?\r?\n", RegexOptions.Singleline);
            if (fetchMatch.Success)
                message = message.Substring(fetchMatch.Length);

            // Remove final OK line
            message = Regex.Replace(message, @"\r?\nA\d+ OK.*$", "", RegexOptions.Multiline).Trim();

            string boundary = GetBoundary(message);

            if (!string.IsNullOrEmpty(boundary))
            {
                var parts = message.Split(new[] { "--" + boundary }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in parts)
                {
                    if (part.StartsWith("--")) continue;

                    string contentType = GetHeaderValue(part, "Content-Type")?.ToLower();

                    html = DecodePart(part);

                }
            }
            else
            {
                // Single-part fallback
                string singleType = GetHeaderValue(message, "Content-Type")?.ToLower();
                html = DecodePart(message);
            }

            html = html.Replace("\r\n\r\n)", "");
            try
            {

                byte[] bytes = Convert.FromBase64String(html);
                html = Encoding.UTF8.GetString(bytes);
            }
            catch
            {
              
            }

        }

        public string DecodePart(string part)
        {
            if (string.IsNullOrWhiteSpace(part))
                return string.Empty;

            // --- Split headers and body safely ---
            int bodyIndex = part.IndexOf("\r\n\r\n");
            int separatorLength = 4;

            if (bodyIndex < 0)
            {
                bodyIndex = part.IndexOf("\n\n");
                separatorLength = 2;
            }

            string headers = bodyIndex >= 0
                ? part.Substring(0, bodyIndex)
                : "";

            string body = bodyIndex >= 0
                ? part.Substring(bodyIndex + separatorLength)
                : part;

            body = body.Trim();

            string encoding = GetHeaderValue(headers, "Content-Transfer-Encoding")?.ToLower() ?? "";
            string charset = GetCharset(headers) ?? "UTF-8";

            byte[] bytes;

            if (encoding.Contains("base64"))
            {
                string cleaned = Regex.Replace(body, @"\s", "");

                try
                {
                    bytes = Convert.FromBase64String(cleaned);
                }
                catch
                {
                    // Not valid base64 — return raw body
                    return body;
                }
            }
            else if (encoding.Contains("quoted-printable"))
            {
                bytes = DecodeQuotedPrintable(body);
            }
            else
            {
                bytes = Encoding.UTF8.GetBytes(body);
            }

            Encoding enc;
            try
            {
                enc = Encoding.GetEncoding(charset);
            }
            catch
            {
                enc = Encoding.UTF8;
            }

            return enc.GetString(bytes).Trim();
        }
        private string HtmlToPlainText(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            string text = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<[^>]+>", "", RegexOptions.IgnoreCase);
            text = System.Net.WebUtility.HtmlDecode(text);
            return text.Trim();
        }

        private string GetBoundary(string message)
        {
            var match = Regex.Match(message, @"boundary=""?([^""\r\n;]+)""?", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        #endregion

        #region Header & Utils

        private string GetHeaderValue(string headers, string name)
        {
            if (string.IsNullOrEmpty(headers))
                return null;

            var lines = headers.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            string currentHeader = null;

            foreach (var line in lines)
            {
                // Continuation line
                if ((line.StartsWith(" ") || line.StartsWith("\t")) && currentHeader != null)
                {
                    currentHeader += line.Trim();
                    continue;
                }

                int colonIndex = line.IndexOf(':');
                if (colonIndex <= 0)
                    continue;

                string headerName = line.Substring(0, colonIndex).Trim();
                string headerValue = line.Substring(colonIndex + 1).Trim();

                if (headerName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    currentHeader = headerValue;
                    return currentHeader;
                }
            }

            return null;
        }

        private DateTime ParseDate(string dateHeader)
        {
            DateTime.TryParse(dateHeader, out var dt);
            return dt;
        }

        private string[] ParseMessageIds(string response)
        {
            var match = Regex.Match(response, @"\* SEARCH(.*)", RegexOptions.IgnoreCase);
            if (!match.Success)
                return new string[0];

            var ids = match.Groups[1].Value
                .Trim()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            return ids;
        }
        private byte[] DecodeQuotedPrintable(string input)
        {
            if (string.IsNullOrEmpty(input))
                return new byte[0];

            // Remove soft line breaks
            input = input.Replace("=\r\n", "").Replace("=\n", "");

            var bytes = new List<byte>();

            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '=' && i + 2 < input.Length)
                {
                    string hex = input.Substring(i + 1, 2);

                    if (byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out byte b))
                    {
                        bytes.Add(b);
                        i += 2;
                    }
                }
                else
                {
                    bytes.Add((byte)input[i]);
                }
            }

            return bytes.ToArray();
        }
        private string DecodeMimeEncodedWords(string input)
        {
            try
            {
                var match = Regex.Match(input, @"=\?(.+?)\?(B|Q)\?(.+?)\?=", RegexOptions.IgnoreCase);
                if (!match.Success) return input;

                string charset = match.Groups[1].Value;
                string method = match.Groups[2].Value;
                string encoded = match.Groups[3].Value;

                if (method.ToUpper() == "B")
                {
                    var bytes = Convert.FromBase64String(encoded);
                    return Encoding.GetEncoding(charset).GetString(bytes);
                }

                if (method.ToUpper() == "Q")
                {
                    encoded = encoded.Replace('_', ' ');
                    return Encoding.GetEncoding(charset)
                        .GetString(DecodeQuotedPrintable(encoded));
                }
            }
            catch { }

            return input;
        }

        private string GetCharset(string headers)
        {
            var contentType = GetHeaderValue(headers, "Content-Type");
            if (contentType == null)
                return null;

            var match = Regex.Match(contentType, @"charset\s*=\s*[""']?(?<charset>[^;""']+)");
            return match.Success ? match.Groups["charset"].Value : null;
        }


        private string GetTag() => "A" + tagCounter++;

        private string ReadLine() => reader.ReadLine();

        private string ReadResponse(string tag)
        {
            var sb = new StringBuilder();
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                sb.AppendLine(line);

                var literalMatch = Regex.Match(line, @"\{(\d+)\}$");
                if (literalMatch.Success)
                {
                    int bytesToRead = int.Parse(literalMatch.Groups[1].Value);
                    char[] buffer = new char[bytesToRead];
                    int read = 0;
                    while (read < bytesToRead)
                    {
                        int r = reader.Read(buffer, read, bytesToRead - read);
                        if (r <= 0) break;
                        read += r;
                    }
                    sb.Append(buffer);
                }

                if (line.StartsWith(tag + " ")) break;
            }

            return sb.ToString();
        }
        private bool EnsureOk(string response)
        {
            if (!response.Contains("OK"))
            {
                throw new Exception("IMAP Error:\n" + response);
            }
            else
            {
                return true;
            }
        }
        public static string SafeDecodeBase64(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
                return string.Empty;

            base64 = Regex.Replace(base64, @"\s", "");

            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return base64; // return original if invalid
            }
        }
        #endregion
    }
}