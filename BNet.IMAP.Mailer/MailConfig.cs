using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BNet.IMAP.Mailer
{
    public class MailConfig
    {
        private TcpClient tcpClient;
        private SslStream sslStream;
        private StreamReader reader;
        private StreamWriter writer;

        private int tagCounter = 1;

        public List<MailMessage> Messages = new List<MailMessage>();

        // ==============================
        // CONNECT + FETCH UNSEEN
        // ==============================
        public void GetInbox(string username, string password,
                             string host = "imap.gmail.com", int port = 993)
        {
            tcpClient = new TcpClient(host, port);

            sslStream = new SslStream(
                tcpClient.GetStream(),
                false,
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

            // UID SEARCH UNSEEN  (IMPORTANT)
            string tagSearch = GetTag();
            writer.WriteLine($"{tagSearch} UID SEARCH UNSEEN");
            string searchResponse = ReadResponse(tagSearch);

            string[] uids = ParseMessageIds(searchResponse);

            foreach (var uid in uids)
            {
                var mail = new MailMessage { Id = uid };

                // FETCH HEADER
                string tagHeader = GetTag();
                writer.WriteLine($"{tagHeader} UID FETCH {uid} BODY.PEEK[HEADER]");
                string headerResponse = ReadResponse(tagHeader);


                mail.From = GetHeaderValue(headerResponse, "From");
                mail.Subject = CleanSubject(GetHeaderValue(headerResponse, "Subject"));
                mail.Date = ParseDate(GetHeaderValue(headerResponse, "Date"));

                // FETCH BODY
                string tagBody = GetTag();
                writer.WriteLine($"{tagBody} UID FETCH {uid} BODY.PEEK[TEXT]");
                string bodyResponse = ReadResponse(tagBody);

                mail.Body = CleanBody(bodyResponse);

                Messages.Add(mail);
            }
        }

        // ==============================
        // MARK AS SEEN (WORKING)
        // ==============================
        public void MarkAsSeen(string uid)
        {
            if (writer == null)
                throw new Exception("Not connected.");

            string tag = GetTag();
            writer.WriteLine($"{tag} UID STORE {uid} +FLAGS (\\Seen)");

            string response = ReadResponse(tag);
            EnsureOk(response);
        }

        // ==============================
        // LOGOUT
        // ==============================
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

        // ==============================
        // HELPERS
        // ==============================

        private string GetTag()
        {
            return "A" + tagCounter++;
        }

        private string ReadLine()
        {
            return reader.ReadLine();
        }

        private string ReadResponse(string tag)
        {
            StringBuilder sb = new StringBuilder();
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                sb.AppendLine(line);

                if (line.StartsWith(tag + " "))
                    break;
            }

            return sb.ToString();
        }

        private void EnsureOk(string response)
        {
            if (!response.Contains("OK"))
                throw new Exception("IMAP Error:\n" + response);
        }



        private string ReadResponse(StreamReader reader, string tag)
        {
            var sb = new StringBuilder();
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                sb.AppendLine(line);

                if (line.EndsWith("}"))
                {
                    int idx1 = line.LastIndexOf('{');
                    int idx2 = line.LastIndexOf('}');
                    if (idx1 >= 0 && idx2 > idx1)
                    {
                        if (int.TryParse(line.Substring(idx1 + 1, idx2 - idx1 - 1), out int bytesToRead))
                        {
                            char[] buffer = new char[bytesToRead];
                            reader.Read(buffer, 0, bytesToRead);
                            sb.Append(buffer);
                        }
                    }
                }

                if (line.StartsWith(tag))
                    break;
            }

            return sb.ToString();
        }

        private string[] ParseMessageIds(string searchResponse)
        {
            string[] lines = searchResponse.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("* SEARCH"))
                {
                    string idsPart = line.Substring(8).Trim();
                    if (string.IsNullOrEmpty(idsPart))
                        return new string[0];
                    return idsPart.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                }
            }
            return new string[0];
        }

        private string GetHeaderValue(string headers, string headerName)
        {
            foreach (var line in headers.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith(headerName, StringComparison.OrdinalIgnoreCase))
                    return line.Substring(headerName.Length + 1).Trim();
            }
            return "";
        }

        private string CleanSubject(string subject)
        {
            if (string.IsNullOrEmpty(subject)) return "";
            return DecodeMimeEncodedWords(subject);
        }

        private DateTime ParseDate(string dateHeader)
        {
            if (DateTime.TryParse(dateHeader, out var dt))
                return dt;
            return DateTime.MinValue;
        }
        private string CleanBody(string response)
        {
            // Skip IMAP fetch line e.g. "* 6067 FETCH (BODY[TEXT] {123}"
            int fetchIndex = response.IndexOf("* ");
            int firstBlank = -1;
            if (fetchIndex >= 0)
            {
                firstBlank = response.IndexOf("\n", fetchIndex);
                if (firstBlank >= 0) firstBlank++;
            }

            string body = firstBlank >= 0
                ? response.Substring(firstBlank).Trim()
                : response.Trim();

            // Check if multipart
            bool isMultipart = body.Contains("Content-Type: multipart") ||
                               (body.Contains("Content-Type:") && body.Contains("boundary="));

            if (isMultipart)
            {
                string boundary = null;

                var boundaryMatch = Regex.Match(
                    body,
                    @"boundary=""?([^""\r\n;]+)""?",
                    RegexOptions.IgnoreCase);

                if (boundaryMatch.Success)
                    boundary = boundaryMatch.Groups[1].Value.Trim();

                if (boundary == null)
                    return string.Empty;

                string[] parts = body.Split(
                    new[] { "--" + boundary },
                    StringSplitOptions.RemoveEmptyEntries);

                string plainText = null;
                string htmlText = null;

                foreach (var part in parts)
                {
                    string trimmedPart = part.Trim();

                    if (string.IsNullOrWhiteSpace(trimmedPart) ||
                        trimmedPart == "--" ||
                        trimmedPart.StartsWith("--"))
                        continue;

                    int partBodyStart = part.IndexOf("\r\n\r\n");
                    if (partBodyStart < 0)
                        partBodyStart = part.IndexOf("\n\n");

                    if (partBodyStart < 0)
                        continue;

                    string partHeaders = part.Substring(0, partBodyStart);
                    string partBody = part.Substring(partBodyStart).Trim();

                    // Remove hard boundary cut
                    int hardCut = partBody.IndexOf("\n--");
                    if (hardCut >= 0)
                        partBody = partBody.Substring(0, hardCut).Trim();

                    string encoding = GetHeaderValue(partHeaders, "Content-Transfer-Encoding").ToLower();
                    string charset = GetCharset(partHeaders) ?? "UTF-8";

                    string decoded = DecodeBodyPart(partBody, encoding, charset);
                    decoded = RemoveMimeBoundaries(decoded);

                    if (partHeaders.Contains("Content-Type: text/plain") && plainText == null)
                    {
                        if (!string.IsNullOrWhiteSpace(decoded))
                            plainText = decoded;
                    }
                    else if (partHeaders.Contains("Content-Type: text/html") && htmlText == null)
                    {
                        string stripped = StripHtmlToText(decoded);
                        if (!string.IsNullOrWhiteSpace(stripped))
                            htmlText = stripped;
                    }
                }

                if (!string.IsNullOrWhiteSpace(plainText))
                    return plainText;

                if (!string.IsNullOrWhiteSpace(htmlText))
                    return htmlText;

                return string.Empty;
            }
            else
            {
                // Single-part email
                int bodyStart = body.IndexOf("\r\n\r\n");
                if (bodyStart < 0)
                    bodyStart = body.IndexOf("\n\n");

                string headers = bodyStart >= 0
                    ? body.Substring(0, bodyStart)
                    : "";

                string rawBody = bodyStart >= 0
                    ? body.Substring(bodyStart).Trim()
                    : body.Trim();

                string encoding = GetHeaderValue(headers, "Content-Transfer-Encoding").ToLower();
                string charset = GetCharset(headers) ?? "UTF-8";

                bool isHtml = headers.Contains("Content-Type: text/html");
                bool isPlain = headers.Contains("Content-Type: text/plain") ||
                               !headers.Contains("Content-Type:");

                // Remove IMAP status lines
                rawBody = Regex.Replace(
                    rawBody,
                    @"\r?\nA\d+ OK[^\r\n]*$",
                    "",
                    RegexOptions.Multiline).Trim();

                rawBody = rawBody.TrimEnd(')', '\r', '\n', ' ');

                string decoded = DecodeBodyPart(rawBody, encoding, charset);

                decoded = RemoveMimeSections(decoded);

                if (isHtml)
                    return StripHtmlToText(decoded);

                if (isPlain)
                    return decoded;

                return string.Empty;
            }
        }
        private string RemoveMimeSections(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // Find first MIME boundary marker
            var boundaryMatch = Regex.Match(text, @"\r?\n--[A-Za-z0-9]{10,}");

            if (boundaryMatch.Success)
            {
                // Keep only content before boundary
                text = text.Substring(0, boundaryMatch.Index);
            }

            return text.Trim();
        }

        private string RemoveMimeBoundaries(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Remove boundary lines
            text = Regex.Replace(
                text,
                @"(?m)^--[A-Za-z0-9'()+_,-./:=?]{10,}--?\s*$",
                "");

            // Remove stray Content-Type headers
            text = Regex.Replace(
                text,
                @"(?im)^Content-Type:.*\r?\n",
                "");

            return text.Trim();
        }

        private string DecodeBodyPart(string rawBody, string encoding, string charset)
        {
            byte[] bodyBytes;

            if (encoding == "base64")
            {
                string cleaned = Regex.Replace(rawBody, @"[^A-Za-z0-9+/=]", "");
                int pad = cleaned.Length % 4;
                if (pad > 0)
                    cleaned += new string('=', 4 - pad);

                try
                {
                    bodyBytes = Convert.FromBase64String(cleaned);
                }
                catch
                {
                    return string.Empty;
                }
            }
            else if (encoding == "quoted-printable")
            {
                bodyBytes = DecodeQuotedPrintable(rawBody);
            }
            else
            {
                bodyBytes = Encoding.UTF8.GetBytes(rawBody);
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

            string result = enc.GetString(bodyBytes).Trim();
            result = Regex.Replace(result, @"(\r?\n){3,}", "\n\n");

            return result.Trim();
        }
        private string StripHtmlToText(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;

            // Replace block-level closing tags with newlines
            html = System.Text.RegularExpressions.Regex.Replace(
                html, @"<br\s*/?>", "\n",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            html = System.Text.RegularExpressions.Regex.Replace(
                html, @"</(p|div|tr|li|h[1-6])>", "\n",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Strip all remaining tags
            html = System.Text.RegularExpressions.Regex.Replace(html, @"<[^>]+>", string.Empty);

            // Decode common HTML entities
            html = html.Replace("&nbsp;", " ")
                       .Replace("&amp;", "&")
                       .Replace("&lt;", "<")
                       .Replace("&gt;", ">")
                       .Replace("&quot;", "\"")
                       .Replace("&#39;", "'");

            // Collapse excessive blank lines
            html = System.Text.RegularExpressions.Regex.Replace(html, @"(\r?\n){3,}", "\n\n");

            return html.Trim();
        }

        private string GetCharset(string headers)
        {
            var contentType = GetHeaderValue(headers, "Content-Type");
            if (contentType != null && contentType.Contains("charset="))
            {
                var parts = contentType.Split(new[] { "charset=" }, StringSplitOptions.None);
                if (parts.Length > 1)
                    return parts[1].Replace("\"", "").Trim();
            }
            return null;
        }

        private byte[] DecodeQuotedPrintable(string input)
        {
            var bytes = new List<byte>();
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '=')
                {
                    if (i + 2 < input.Length)
                    {
                        string hex = input.Substring(i + 1, 2);
                        try { bytes.Add(Convert.ToByte(hex, 16)); i += 2; continue; } catch { }
                    }
                }
                bytes.Add((byte)input[i]);
            }
            return bytes.ToArray();
        }

        private string DecodeMimeEncodedWords(string input)
        {
            try
            {
                if (input.StartsWith("=?") && input.EndsWith("?="))
                {
                    var parts = input.Split('?');
                    if (parts.Length == 5)
                    {
                        string encoding = parts[1];
                        string method = parts[2];
                        string encodedText = parts[3];

                        if (method.Equals("B", StringComparison.OrdinalIgnoreCase))
                        {
                            byte[] bytes = Convert.FromBase64String(encodedText);
                            return Encoding.GetEncoding(encoding).GetString(bytes);
                        }
                        else if (method.Equals("Q", StringComparison.OrdinalIgnoreCase))
                        {
                            encodedText = encodedText.Replace('_', ' ');
                            var bytes = new List<byte>();
                            for (int i = 0; i < encodedText.Length; i++)
                            {
                                if (encodedText[i] == '=' && i + 2 < encodedText.Length)
                                {
                                    string hex = encodedText.Substring(i + 1, 2);
                                    bytes.Add(Convert.ToByte(hex, 16));
                                    i += 2;
                                }
                                else { bytes.Add((byte)encodedText[i]); }
                            }
                            return Encoding.GetEncoding(encoding).GetString(bytes.ToArray());
                        }
                    }
                }
            }
            catch { }
            return input;
        }
    }
}
