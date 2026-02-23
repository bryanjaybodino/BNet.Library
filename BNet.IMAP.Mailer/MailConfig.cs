using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BNet.IMAP.Mailer
{
    public class MailMessage
    {
        public List<string> To { get; set; }
        public List<string> CC { get; set; }
        public List<string> BCC { get; set; }
        public string Id { get; set; }
        public string From { get; set; }
        public string Subject { get; set; }
        public DateTime Date { get; set; }
        public string HtmlBody { get; set; }
        public string PlainTextBody { get; set; }
    }

    public class MailInboxes
    {
        public int TotalEmail { get; set; }
        public int TotalPagination { get; set; }
        public List<string> To { get; set; }
        public List<string> CC { get; set; }
        public List<string> BCC { get; set; }
        public string Id { get; set; }
        public string From { get; set; }
        public string Subject { get; set; }
        public string Folder { get; set; } // INBOX, Sent, etc.
        public DateTime Date { get; set; }
        List<MailInboxes> Submail { get; set; }
    }

    public class MailConfig
    {
        private TcpClient tcpClient;
        private SslStream sslStream;
        private StreamReader reader;
        private StreamWriter writer;
        private int tagCounter = 1;
        private string host = "imap.gmail.com";
        private int port = 993;
        private string username = "";
        private string password = "";
        private bool isConnected = true;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public async Task<bool> ConnectAsync(string username, string password, string host = "imap.gmail.com", int port = 993)
        {
            try
            {
                this.username = username;
                this.password = password;
                this.host = host;
                this.port = port;

                tcpClient = new TcpClient(host, port);

                sslStream = new SslStream(
                    tcpClient.GetStream(),
                    false,
                    (sender, cert, chain, errors) => true);

                await sslStream.AuthenticateAsClientAsync(host, null, SslProtocols.Tls12, false);

                reader = new StreamReader(sslStream, Encoding.UTF8, false, 65536);
                writer = new StreamWriter(sslStream, Encoding.UTF8) { AutoFlush = true };

                // Read server greeting
                await reader.ReadLineAsync();

                string tagLogin = GetTag();
                await writer.WriteLineAsync($"{tagLogin} LOGIN {username} {password}");
                EnsureOk(await ReadResponseAsync(tagLogin));
                return isConnected;
            }
            catch
            {
                return false;
            }
        }

        public enum ImapFlags
        {
            ALL,
            SEEN,
            UNSEEN,
            ANSWERED,
            UNANSWERED,
            FLAGGED,
            UNFLAGGED,
            DELETED,
            UNDELETED,
            DRAFT,
            UNDRAFT
        }

        public async Task<List<MailInboxes>> GetInboxAsync(
            string folder = "INBOX",
            ImapFlags imapFlags = ImapFlags.UNSEEN,
            string emailFilter = "",
            int pageSize = 50,
            int pageIndex = 0)
        {
            await _lock.WaitAsync();
            try
            {
                string safeFolderName = folder.Contains(" ") ? $"\"{folder}\"" : folder;

                string tagSelect = GetTag();
                await writer.WriteLineAsync($"{tagSelect} SELECT {safeFolderName}");
                EnsureOk(await ReadResponseAsync(tagSelect));

                string searchCommand = imapFlags.ToString();
                if (!string.IsNullOrEmpty(emailFilter))
                    searchCommand = $"FROM \"{emailFilter}\" {imapFlags}";

                string tagSearch = GetTag();
                await writer.WriteLineAsync($"{tagSearch} UID SEARCH {searchCommand}");
                string searchResponse = await ReadResponseAsync(tagSearch);

                string[] uids = ParseMessageIds(searchResponse);
                if (uids.Length == 0)
                    return new List<MailInboxes>();

                Array.Reverse(uids);

                int totalEmails = uids.Length;
                int totalPages = (int)Math.Ceiling(totalEmails / (double)pageSize);
                int start = pageIndex * pageSize;

                if (start >= totalEmails)
                    return new List<MailInboxes>();

                var pageUids = uids.Skip(start).Take(pageSize).ToArray();
                if (pageUids.Length == 0)
                    return new List<MailInboxes>();

                string uidSet = string.Join(",", pageUids);
                string tagFetch = GetTag();
                await writer.WriteLineAsync($"{tagFetch} UID FETCH {uidSet} (BODY.PEEK[HEADER.FIELDS (FROM SUBJECT DATE)])");
                string response = await ReadResponseAsync(tagFetch);

                List<MailInboxes> mailInboxes = new List<MailInboxes>();
                foreach (var uid in pageUids)
                {
                    string messageBlock = ExtractHeaderFields(response, uid);
                    var mail = new MailInboxes
                    {
                        Id = uid,
                        From = GetHeaderValue(messageBlock, "From"),
                        Subject = DecodeMimeEncodedWords(GetHeaderValue(messageBlock, "Subject")),
                        Date = ParseDate(GetHeaderValue(messageBlock, "Date")),
                        TotalEmail = totalEmails,
                        TotalPagination = totalPages
                    };
                    mailInboxes.Add(mail);
                }

                return mailInboxes;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<Dictionary<ImapFlags, int>> GetEmailCountsByFlagsAsync(string folder = "INBOX")
        {
            await _lock.WaitAsync();
            try
            {
                string safeFolder = folder.Contains(" ") ? $"\"{folder}\"" : folder;

                string tagSelect = GetTag();
                await writer.WriteLineAsync($"{tagSelect} SELECT {safeFolder}");
                EnsureOk(await ReadResponseAsync(tagSelect));

                var counts = new Dictionary<ImapFlags, int>();

                foreach (ImapFlags flag in Enum.GetValues(typeof(ImapFlags)))
                {
                    string searchCommand = flag == ImapFlags.ALL ? "ALL" : flag.ToString();

                    string tagSearch = GetTag();
                    await writer.WriteLineAsync($"{tagSearch} UID SEARCH {searchCommand}");
                    string searchResponse = await ReadResponseAsync(tagSearch);

                    string[] uids = ParseMessageIds(searchResponse);
                    counts[flag] = uids.Length;
                }

                return counts;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<MailMessage> GetFullMessageAsync(string id, string folder = "INBOX")
        {
            await _lock.WaitAsync();
            try
            {
                // SELECT folder first
                string safeFolderName = folder.Contains(" ") ? $"\"{folder}\"" : folder;
                string tagSelect = GetTag();
                await writer.WriteLineAsync($"{tagSelect} SELECT {safeFolderName}");
                await ReadResponseAsync(tagSelect);

                // FETCH full message body
                string tagFetch = GetTag();
                await writer.WriteLineAsync($"{tagFetch} UID FETCH {id} (BODY[])");
                await writer.FlushAsync();

                string fullMessage = await ReadResponseAsync(tagFetch);

                var mail = new MailMessage { Id = id };
                mail.From = GetHeaderValue(fullMessage, "From");
                mail.Subject = DecodeMimeEncodedWords(GetHeaderValue(fullMessage, "Subject"));
                mail.Date = ParseDate(GetHeaderValue(fullMessage, "Date"));

                ExtractBodies(fullMessage, out string html, out string plainText);
                // ✅ Inject !important to all inline styles so email renders correctly in webform
                mail.HtmlBody = InjectImportant(html);

                mail.PlainTextBody = !string.IsNullOrEmpty(plainText) ? plainText : HtmlToPlainText(html);

                return mail;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task Logout()
        {
            await _lock.WaitAsync();
            try
            {
                if (writer != null)
                {
                    string tag = GetTag();
                    await writer.WriteLineAsync($"{tag} LOGOUT");
                    await ReadResponseAsync(tag);
                }
            }
            finally
            {
                _lock.Release();
                reader?.Close();
                writer?.Close();
                sslStream?.Close();
                tcpClient?.Close();
            }
        }

        public async Task<bool> MarkAsSeenAsync(string id)
        {
            await _lock.WaitAsync();
            try
            {
                if (writer == null) throw new Exception("Not connected.");
                string tag = GetTag();
                await writer.WriteLineAsync($"{tag} UID STORE {id} +FLAGS (\\Seen)");
                string response = await ReadResponseAsync(tag);
                return EnsureOk(response);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> DeleteMessageAsync(string id)
        {
            await _lock.WaitAsync();
            try
            {
                if (writer == null) throw new Exception("Not connected.");

                string tag = GetTag();
                await writer.WriteLineAsync($"{tag} UID STORE {id} +FLAGS (\\Deleted)");
                EnsureOk(await ReadResponseAsync(tag));

                tag = GetTag();
                await writer.WriteLineAsync($"{tag} EXPUNGE");
                return EnsureOk(await ReadResponseAsync(tag));
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> MoveToFolderAsync(string id, string folder)
        {
            await _lock.WaitAsync();
            try
            {
                if (writer == null) throw new Exception("Not connected.");
                string tag = GetTag();
                await writer.WriteLineAsync($"{tag} UID MOVE {id} \"{folder}\"");
                return EnsureOk(await ReadResponseAsync(tag));
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<List<string>> ListMailboxesAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (writer == null) throw new Exception("Not connected.");

                string tag = GetTag();
                await writer.WriteLineAsync($"{tag} LIST \"\" \"*\"");
                string response = await ReadResponseAsync(tag);
                EnsureOk(response);

                var mailboxes = new List<string>();
                var lines = response.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                var regex = new Regex(@"^\* LIST \([^\)]*\) ""[^""]*"" (.+)$");

                foreach (var line in lines)
                {
                    if (!line.StartsWith("* LIST")) continue;

                    var match = regex.Match(line);
                    if (match.Success)
                    {
                        string mailbox = match.Groups[1].Value.Trim();
                        if (mailbox.StartsWith("\"") && mailbox.EndsWith("\""))
                            mailbox = mailbox.Substring(1, mailbox.Length - 2);
                        mailboxes.Add(mailbox);
                    }
                }

                return mailboxes;
            }
            finally
            {
                _lock.Release();
            }
        }

        #region Body Extraction

        private string ExtractHeaderFields(string response, string uid)
        {
            string marker = $"UID {uid} BODY[HEADER.FIELDS";
            int uidIndex = response.IndexOf(marker);
            if (uidIndex == -1) return string.Empty;

            int literalStart = response.IndexOf("{", uidIndex);
            int literalEnd = response.IndexOf("}", literalStart);
            if (literalStart == -1 || literalEnd == -1) return string.Empty;

            string lengthStr = response.Substring(literalStart + 1, literalEnd - literalStart - 1);
            if (!int.TryParse(lengthStr, out int literalLength)) return string.Empty;

            int headerStart = response.IndexOf("\r\n", literalEnd) + 2;
            if (headerStart == -1) return string.Empty;
            if (headerStart + literalLength > response.Length) return string.Empty;

            return response.Substring(headerStart, literalLength).Trim();
        }

        private void ExtractBodies(string message, out string html, out string plainText)
        {
            html = null;
            plainText = null;

            // Remove IMAP FETCH header line
            var fetchMatch = Regex.Match(message, @"\* \d+ FETCH \(.*?\r?\n", RegexOptions.Singleline);
            if (fetchMatch.Success)
                message = message.Substring(fetchMatch.Index + fetchMatch.Length);

            // Remove trailing closing paren + OK line
            message = Regex.Replace(message, @"\)\r?\nA\d+ OK.*$", "", RegexOptions.Multiline).Trim();
            message = Regex.Replace(message, @"\r?\nA\d+ OK.*$", "", RegexOptions.Multiline).Trim();

            // ✅ Recursively extract bodies
            ExtractFromPart(message, ref html, ref plainText);

            if (html == null) html = string.Empty;
            if (plainText == null) plainText = string.Empty;
        }

        private void ExtractFromPart(string part, ref string html, ref string plainText, int depth = 0)
        {
            // ✅ Prevent infinite recursion
            if (depth > 10) return;

            string boundary = GetBoundary(part);

            if (!string.IsNullOrEmpty(boundary))
            {
                var parts = part.Split(new[] { "--" + boundary }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var subPart in parts)
                {
                    if (subPart.TrimStart().StartsWith("--")) continue;
                    if (string.IsNullOrWhiteSpace(subPart)) continue;

                    string contentType = GetRawHeaderValue(subPart, "Content-Type")?.ToLower() ?? "";

                    if (contentType.Contains("multipart/"))
                        ExtractFromPart(subPart, ref html, ref plainText, depth + 1); // ✅ pass depth
                    else if (contentType.Contains("text/html") && html == null)
                        html = DecodePart(subPart);
                    else if (contentType.Contains("text/plain") && plainText == null)
                        plainText = DecodePart(subPart);
                }
            }
            else
            {
                string contentType = GetRawHeaderValue(part, "Content-Type")?.ToLower() ?? "";
                string trimmed = part.Trim();
                string noWhitespace = trimmed.Replace("\r\n", "").Replace("\n", "").Replace("\r", "");

                bool isBase64 = noWhitespace.Length > 0 &&
                                Regex.IsMatch(noWhitespace, @"^[A-Za-z0-9+/]+=*$");

                if (isBase64)
                {
                    try
                    {
                        byte[] bytes = Convert.FromBase64String(noWhitespace);
                        string decoded = Encoding.UTF8.GetString(bytes);

                        if (contentType.Contains("text/plain") && plainText == null)
                            plainText = decoded;
                        else if (html == null)
                            html = decoded;
                    }
                    catch
                    {
                        if (html == null) html = DecodePart(part);
                    }
                }
                else
                {
                    if (contentType.Contains("text/plain") && plainText == null)
                        plainText = DecodePart(part);
                    else if (html == null)
                        html = DecodePart(part);
                }
            }

            if (html == null && plainText != null)
                html = $"<pre>{System.Net.WebUtility.HtmlEncode(plainText)}</pre>";
        }
        private string InjectImportant(string html)
        {
            if (string.IsNullOrEmpty(html)) return html;

            // Find every style="..." attribute and add !important to each property
            return Regex.Replace(html, @"style=""([^""]*)""", m =>
            {
                string styleContent = m.Groups[1].Value;

                // Split into individual CSS properties
                var properties = styleContent.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                var result = new StringBuilder();
                foreach (var prop in properties)
                {
                    string trimmed = prop.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    // Only add !important if not already there
                    if (!trimmed.EndsWith("!important", StringComparison.OrdinalIgnoreCase))
                        result.Append(trimmed + " !important;");
                    else
                        result.Append(trimmed + ";");
                }

                return $"style=\"{result}\"";
            }, RegexOptions.IgnoreCase);
        }
        private string DecodePart(string part)
        {
            if (string.IsNullOrWhiteSpace(part))
                return string.Empty;

            int bodyIndex = part.IndexOf("\r\n\r\n");
            int separatorLength = 4;

            if (bodyIndex < 0)
            {
                bodyIndex = part.IndexOf("\n\n");
                separatorLength = 2;
            }

            string headers = bodyIndex >= 0 ? part.Substring(0, bodyIndex) : "";
            string body = bodyIndex >= 0 ? part.Substring(bodyIndex + separatorLength) : part;
            body = body.Trim();

            string encoding = GetRawHeaderValue(headers, "Content-Transfer-Encoding")?.ToLower() ?? "";
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
            try { enc = Encoding.GetEncoding(charset); }
            catch { enc = Encoding.UTF8; }

            return enc.GetString(bytes).Trim();
        }

        private string HtmlToPlainText(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";

            string text = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<[^>]+>", "", RegexOptions.IgnoreCase);
            text = System.Net.WebUtility.HtmlDecode(text);

            // Remove MIME boundary lines
            text = Regex.Replace(text, @"--[a-f0-9]{20,}.*", "", RegexOptions.IgnoreCase);

            // Remove leftover MIME headers
            text = Regex.Replace(text, @"^Content-\w+:.*$", "", RegexOptions.Multiline | RegexOptions.IgnoreCase);

            // Normalize line endings
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            // Remove multiple blank lines
            text = Regex.Replace(text, @"\n{3,}", "\n\n");

            return text.Trim();
        }

        private string GetBoundary(string message)
        {
            var match = Regex.Match(message, @"boundary=""?([^""\r\n;]+)""?", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        #endregion

        #region Header & Utils

        private string GetRawHeaderValue(string block, string name)
        {
            if (string.IsNullOrEmpty(block)) return null;

            // ✅ Limit block size to headers only — stop at blank line
            int headerEnd = block.IndexOf("\r\n\r\n");
            if (headerEnd < 0) headerEnd = block.IndexOf("\n\n");
            if (headerEnd > 0) block = block.Substring(0, headerEnd);

            // ✅ Hard limit — never parse more than 4KB of headers
            if (block.Length > 4096) block = block.Substring(0, 4096);

            var lines = block.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            string current = null;
            string foundName = null;

            foreach (var line in lines)
            {
                // Continuation line (starts with space or tab)
                if ((line.StartsWith(" ") || line.StartsWith("\t")) && current != null)
                {
                    current += " " + line.Trim();
                    continue;
                }

                int colon = line.IndexOf(':');
                if (colon <= 0)
                {
                    if (current != null && foundName != null) return current;
                    continue;
                }

                string headerName = line.Substring(0, colon).Trim();
                string headerValue = line.Substring(colon + 1).Trim();

                if (headerName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    current = headerValue;
                    foundName = headerName;
                }
                else if (current != null)
                {
                    return current; // past our header
                }
            }

            return current;
        }

        private string GetHeaderValue(string headers, string name)
        {
            if (string.IsNullOrEmpty(headers)) return null;

            var lines = headers.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            string currentHeader = null;

            foreach (var line in lines)
            {
                if ((line.StartsWith(" ") || line.StartsWith("\t")) && currentHeader != null)
                {
                    currentHeader += line.Trim();
                    continue;
                }

                int colonIndex = line.IndexOf(':');
                if (colonIndex <= 0) continue;

                string headerName = line.Substring(0, colonIndex).Trim();
                string headerValue = line.Substring(colonIndex + 1).Trim();

                if (headerName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    currentHeader = headerValue;

                    var match = Regex.Match(currentHeader, @"<([^>]+)>");
                    string extracted = match.Success ? match.Groups[1].Value : currentHeader.Trim();

                    bool isValidEmail = Regex.IsMatch(extracted, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                    return isValidEmail ? extracted : currentHeader;
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
            if (!match.Success) return new string[0];

            return match.Groups[1].Value
                .Trim()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private byte[] DecodeQuotedPrintable(string input)
        {
            if (string.IsNullOrEmpty(input)) return new byte[0];

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
            if (string.IsNullOrEmpty(input)) return input;
            try
            {
                var match = Regex.Match(input, @"=\?(.+?)\?(B|Q)\?(.+?)\?=", RegexOptions.IgnoreCase);
                if (!match.Success) return input;

                string charset = match.Groups[1].Value;
                string method = match.Groups[2].Value.ToUpper();
                string encoded = match.Groups[3].Value;

                if (method == "B")
                {
                    var bytes = Convert.FromBase64String(encoded);
                    return Encoding.GetEncoding(charset).GetString(bytes);
                }

                if (method == "Q")
                {
                    encoded = encoded.Replace('_', ' ');
                    return Encoding.GetEncoding(charset).GetString(DecodeQuotedPrintable(encoded));
                }
            }
            catch { }

            return input;
        }

        private string GetCharset(string headers)
        {
            var contentType = GetRawHeaderValue(headers, "Content-Type");
            if (contentType == null) return null;

            var match = Regex.Match(contentType, @"charset\s*=\s*[""']?(?<charset>[^;""'\s]+)");
            return match.Success ? match.Groups["charset"].Value : null;
        }

        private string GetTag() => "A" + tagCounter++;

        private async Task<string> ReadResponseAsync(string tag)
        {
            var sb = new StringBuilder();
            string line;

            while (true)
            {
                var readTask = reader.ReadLineAsync();
                if (await Task.WhenAny(readTask, Task.Delay(60000)) != readTask)
                    throw new TimeoutException($"IMAP timeout waiting for: {tag}");

                line = await readTask;
                if (line == null) break;

                Console.WriteLine($"[IMAP] {line}");

                if (Regex.IsMatch(line, @"^A\d+ NO", RegexOptions.IgnoreCase))
                    isConnected = false;

                sb.AppendLine(line);

                var literalMatch = Regex.Match(line, @"\{(\d+)\}$");
                if (literalMatch.Success)
                {
                    int bytesToRead = int.Parse(literalMatch.Groups[1].Value);
                    Console.WriteLine($"[IMAP] Reading literal {bytesToRead} bytes...");

                    // ✅ Read directly from SslStream as raw bytes — avoids StreamReader stall
                    byte[] rawBuffer = new byte[bytesToRead];
                    int totalRead = 0;

                    while (totalRead < bytesToRead)
                    {
                        int remaining = bytesToRead - totalRead;
                        int chunkSize = Math.Min(65536, remaining);

                        var readBytesTask = sslStream.ReadAsync(rawBuffer, totalRead, chunkSize);
                        if (await Task.WhenAny(readBytesTask, Task.Delay(60000)) != readBytesTask)
                            throw new TimeoutException($"Timeout reading literal body at {totalRead}/{bytesToRead}");

                        int r = await readBytesTask;
                        if (r <= 0) break;
                        totalRead += r;
                    }

                    string literalContent = Encoding.UTF8.GetString(rawBuffer, 0, totalRead);
                    sb.Append(literalContent);

                    Console.WriteLine($"[IMAP] Literal read complete: {totalRead} bytes");

                    // Read trailing CRLF
                    await reader.ReadLineAsync();
                }

                if (line.StartsWith(tag + " "))
                    break;
            }

            return sb.ToString();
        }

        private bool EnsureOk(string response)
        {
            if (!response.Contains("OK"))
                throw new Exception("IMAP Error:\n" + response);
            return true;
        }

        #endregion
    }
}