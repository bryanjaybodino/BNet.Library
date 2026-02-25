using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
// X509Certificates not needed - removed with SslClientAuthenticationOptions
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BNet.IMAP.Mailer
{
    // ─────────────────────────────────────────────────────────────────────────
    //  MAIL CONFIG  — full IMAP client
    //
    //  Features:
    //    • SSL/TLS with proper certificate validation
    //    • XOAUTH2 (Office 365, Gmail)
    //    • CAPABILITY negotiation
    //    • BODYSTRUCTURE-based selective fetch (bandwidth-efficient)
    //    • IDLE push notifications (RFC 2177)
    //    • Full attachment + inline image extraction
    //    • Thread-safe SemaphoreSlim lock
    //    • Auto-reconnect
    //    • Create/Delete/Rename folders
    //    • Full flag management (Seen, Flagged, Answered, Deleted, Draft)
    //    • Move / Copy messages
    //    • Fluent search queries
    //    • Pagination
    //    • Initials avatar builder
    // ─────────────────────────────────────────────────────────────────────────
    public class MailConfig : IDisposable
    {
        // ── State ─────────────────────────────────────────────────────────────
        private TcpClient _tcp;
        private SslStream _ssl;
        private StreamReader _reader;
        private StreamWriter _writer;
        private int _tag = 1;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private HashSet<string> _capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private string _host;
        private int _port;
        private string _username;
        private string _password;
        private string _accessToken;
        private AuthMethod _authMethod;
        private bool _validateCertificate = true;

        private CancellationTokenSource _idleCts;
        private Task _idleTask;

        public bool IsConnected { get; private set; }

        public enum AuthMethod { Plain, XOAuth2 }

        // ── Events ────────────────────────────────────────────────────────────
        /// <summary>
        /// Fired when a new message arrives in the monitored folder.
        /// The event args contain the full MailMessage so you can read
        /// From, Subject, Body, Attachments immediately.
        /// </summary>
        public event Func<NewMailEventArgs, Task> OnNewMailAsync;

        // ─────────────────────────────────────────────────────────────────────
        //  CONNECT  — plain LOGIN
        // ─────────────────────────────────────────────────────────────────────
        public async Task<bool> ConnectAsync(
            string username,
            string password,
            string host = "imap.gmail.com",
            int port = 993,
            bool validateSslCert = true,
            CancellationToken ct = default)
        {
            _host = host;
            _port = port;
            _username = username;
            _password = password;
            _authMethod = AuthMethod.Plain;
            _validateCertificate = validateSslCert;

            return await ConnectCoreAsync(ct);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CONNECT  — XOAUTH2 (Office 365 / Gmail OAuth)
        // ─────────────────────────────────────────────────────────────────────
        public async Task<bool> XOAuth2Async(
            string username,
            string accessToken,
            string host = "outlook.office365.com",
            int port = 993,
            bool validateSslCert = true,
            CancellationToken ct = default)
        {
            _host = host;
            _port = port;
            _username = username;
            _accessToken = accessToken;
            _authMethod = AuthMethod.XOAuth2;
            _validateCertificate = validateSslCert;

            return await ConnectCoreAsync(ct);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CORE CONNECT
        // ─────────────────────────────────────────────────────────────────────
        private async Task<bool> ConnectCoreAsync(CancellationToken ct)
        {
            try
            {
                _tcp = new TcpClient();
                await _tcp.ConnectAsync(_host, _port);

                _ssl = new SslStream(
                    _tcp.GetStream(),
                    false,
                    (sender, cert, chain, errors) =>
                        _validateCertificate ? errors == SslPolicyErrors.None : true);

                await _ssl.AuthenticateAsClientAsync(_host, null, SslProtocols.Tls12, false);

                _reader = new StreamReader(_ssl, new UTF8Encoding(false), false, 65536);
                _writer = new StreamWriter(_ssl, new UTF8Encoding(false)) { AutoFlush = true };

                // Read greeting
                string greeting = await ReadLineAsync(ct);
                Console.WriteLine($"[IMAP] {greeting}");

                if (greeting.IndexOf("OK", StringComparison.OrdinalIgnoreCase) < 0)
                    return false;

                // Negotiate capabilities
                await NegotiateCapabilitiesAsync(ct);

                // Authenticate
                if (_authMethod == AuthMethod.XOAuth2)
                    await AuthenticateXOAuth2Async(ct);
                else
                    await AuthenticatePlainAsync(ct);

                IsConnected = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Connect failed: {ex.Message}");
                IsConnected = false;
                return false;
            }
        }

        private async Task NegotiateCapabilitiesAsync(CancellationToken ct)
        {
            string tag = Tag();
            await SendAsync($"{tag} CAPABILITY", ct);
            var resp = await ReadTaggedAsync(tag, ct);
            foreach (var line in resp.Lines)
            {
                var m = Regex.Match(line, @"^\* CAPABILITY (.+)$", RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                foreach (var cap in m.Groups[1].Value.Split(' '))
                    _capabilities.Add(cap.Trim());
            }
        }

        private async Task AuthenticatePlainAsync(CancellationToken ct)
        {
            string tag = Tag();
            await SendAsync($"{tag} LOGIN {_username} {_password}", ct);
            var resp = await ReadTaggedAsync(tag, ct);
            resp.ThrowIfFailed("LOGIN");
        }

        private async Task AuthenticateXOAuth2Async(CancellationToken ct)
        {
            string authStr = $"user={_username}\x01auth=Bearer {_accessToken}\x01\x01";
            string b64 = Convert.ToBase64String(Encoding.ASCII.GetBytes(authStr));
            string tag = Tag();
            await SendAsync($"{tag} AUTHENTICATE XOAUTH2 {b64}", ct);
            var resp = await ReadTaggedAsync(tag, ct);
            resp.ThrowIfFailed("XOAUTH2");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  AUTO-RECONNECT
        // ─────────────────────────────────────────────────────────────────────
        private async Task<bool> EnsureConnectedAsync(CancellationToken ct)
        {
            if (IsConnected && await NoOpAsync(ct)) return true;
            Console.WriteLine("[IMAP] Reconnecting…");
            IsConnected = false;
            return await ConnectCoreAsync(ct);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  NOOP  — keep-alive / connectivity check
        // ─────────────────────────────────────────────────────────────────────
        public async Task<bool> NoOpAsync(CancellationToken ct = default)
        {
            try
            {
                string tag = Tag();
                await SendAsync($"{tag} NOOP", ct);
                var resp = await ReadTaggedAsync(tag, ct);
                return resp.IsOk;
            }
            catch { return false; }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  LOGOUT
        // ─────────────────────────────────────────────────────────────────────
        public async Task LogoutAsync(CancellationToken ct = default)
        {
            await StopIdleAsync();
            await _lock.WaitAsync(ct);
            try
            {
                if (_writer != null)
                {
                    string tag = Tag();
                    await SendAsync($"{tag} LOGOUT", ct);
                    await ReadTaggedAsync(tag, ct);
                }
            }
            catch { }
            finally
            {
                _lock.Release();
                Cleanup();
            }
        }

        private void Cleanup()
        {
            IsConnected = false;
            if (_reader != null) { _reader.Dispose(); _reader = null; }
            if (_writer != null) { _writer.Dispose(); _writer = null; }
            if (_ssl != null) { _ssl.Dispose(); _ssl = null; }
            _tcp = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GET INBOX  — paginated list with BODYSTRUCTURE-based has-attachment flag
        // ─────────────────────────────────────────────────────────────────────
        public async Task<List<MailInboxes>> GetInboxAsync(
            string folder = "INBOX",
            ImapFlags imapFlags = ImapFlags.UNSEEN,
            string emailFilter = "",
            int pageSize = 50,
            int pageIndex = 0,
            CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);
                await SelectFolderAsync(folder, ct);

                string search = BuildFlagSearch(imapFlags, emailFilter);
                string[] uids = await SearchAsync(search, ct);

                if (uids.Length == 0) return new List<MailInboxes>();
                Array.Reverse(uids);

                int total = uids.Length;
                int totalPages = (int)Math.Ceiling(total / (double)pageSize);
                int start = pageIndex * pageSize;
                if (start >= total) return new List<MailInboxes>();

                var pageUids = uids.Skip(start).Take(pageSize).ToArray();
                string uidSet = string.Join(",", pageUids);

                // Fetch headers + FLAGS in one round-trip
                string tagH = Tag();
                await SendAsync(
                    $"{tagH} UID FETCH {uidSet} (UID FLAGS BODYSTRUCTURE BODY.PEEK[HEADER.FIELDS (FROM TO CC BCC SUBJECT DATE MESSAGE-ID)])", ct);
                var headerResp = await ReadTaggedAsync(tagH, ct);

                var result = new List<MailInboxes>();
                foreach (string uid in pageUids)
                {
                    string block = ExtractFetchBlock(headerResp.Raw, uid);
                    if (string.IsNullOrEmpty(block)) continue;

                    string rawHeaders = ExtractLiteralFromBlock(block, "HEADER.FIELDS");
                    var headers = HeaderParser.Parse(rawHeaders);

                    string rawFrom = HeaderParser.GetRaw(headers, "From");
                    AddressParser.ParseSingle(rawFrom, out string fromName, out string fromEmail);

                    string bsRaw = ExtractField(block, "BODYSTRUCTURE");
                    BodyPart bs = string.IsNullOrEmpty(bsRaw) ? null : BodyStructureParser.Parse(bsRaw);
                    bool hasAtt = bs != null && BodyStructureParser.Flatten(bs).Any(p => p.IsAttachment);

                    result.Add(new MailInboxes
                    {
                        Id = uid,
                        Folder = folder,
                        From = rawFrom,
                        FromName = fromName,
                        FromEmail = fromEmail,
                        FromImage = BuildInitialsSpan(fromName ?? fromEmail),
                        To = AddressParser.ParseList(HeaderParser.GetRaw(headers, "To")),
                        CC = AddressParser.ParseList(HeaderParser.GetRaw(headers, "CC")),
                        BCC = AddressParser.ParseList(HeaderParser.GetRaw(headers, "BCC")),
                        Subject = HeaderParser.Get(headers, "Subject"),
                        Date = MimeDecoder.ParseDate(HeaderParser.GetRaw(headers, "Date")) ?? DateTime.MinValue,
                        Flags = ExtractFlagsFromBlock(block),
                        HasAttachment = hasAtt,
                        TotalEmail = total,
                        TotalPagination = totalPages,
                        Submail = new List<MailMessage>()
                    });
                }

                return result;
            }
            finally { _lock.Release(); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GET FULL MESSAGE  — uses BODYSTRUCTURE to fetch only needed parts
        // ─────────────────────────────────────────────────────────────────────
        public async Task<MailMessage> GetFullMessageAsync(
            string id,
            string folder = "INBOX",
            CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);
                return await FetchFullMessageAsync(id, folder, false, ct);
            }
            finally { _lock.Release(); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GET THREAD
        // ─────────────────────────────────────────────────────────────────────
        public async Task<MailInboxes> GetThreadAsync(
            string id,
            string folder = "INBOX",
            CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);
                await SelectFolderAsync(folder, ct);

                // Fetch primary headers
                string tagH = Tag();
                await SendAsync(
                    $"{tagH} UID FETCH {id} (UID FLAGS BODY.PEEK[HEADER.FIELDS (FROM TO CC BCC SUBJECT DATE)])", ct);
                var hResp = await ReadTaggedAsync(tagH, ct);

                string block = ExtractFetchBlock(hResp.Raw, id);
                string rawHeaders = ExtractLiteralFromBlock(block, "HEADER.FIELDS");
                var headers = HeaderParser.Parse(rawHeaders);

                string rawFrom = HeaderParser.GetRaw(headers, "From");
                AddressParser.ParseSingle(rawFrom, out string fromName, out string fromEmail);

                string rawSubject = HeaderParser.Get(headers, "Subject") ?? "";
                string normSubject = NormalizeSubject(rawSubject);

                var primary = new MailInboxes
                {
                    Id = id,
                    Folder = folder,
                    From = rawFrom,
                    FromName = fromName,
                    FromEmail = fromEmail,
                    FromImage = BuildInitialsSpan(fromName ?? fromEmail),
                    To = AddressParser.ParseList(HeaderParser.GetRaw(headers, "To")),
                    CC = AddressParser.ParseList(HeaderParser.GetRaw(headers, "CC")),
                    BCC = AddressParser.ParseList(HeaderParser.GetRaw(headers, "BCC")),
                    Subject = rawSubject,
                    Date = MimeDecoder.ParseDate(HeaderParser.GetRaw(headers, "Date")) ?? DateTime.MinValue,
                    Flags = ExtractFlagsFromBlock(block),
                    Submail = new List<MailMessage>()
                };

                // Search for related
                string[] related = await SearchAsync($"SUBJECT \"{EscapeImap(normSubject)}\"", ct);
                var others = related.Where(u => u != id)
                                          .OrderBy(u => long.TryParse(u, out long n) ? n : 0)
                                          .ToArray();

                foreach (string uid in others)
                {
                    var msg = await FetchFullMessageAsync(uid, folder, true, ct);
                    if (msg != null) primary.Submail.Add(msg);
                }

                return primary;
            }
            finally { _lock.Release(); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  SEARCH  — fluent query support
        // ─────────────────────────────────────────────────────────────────────
        public async Task<string[]> SearchMessagesAsync(
            ImapSearchQuery query,
            string folder = "INBOX",
            CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);
                await SelectFolderAsync(folder, ct);
                return await SearchAsync(query.Build(), ct);
            }
            finally { _lock.Release(); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EMAIL COUNTS BY FLAG
        // ─────────────────────────────────────────────────────────────────────
        public async Task<Dictionary<ImapFlags, int>> GetEmailCountsByFlagsAsync(
            string folder = "INBOX",
            CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);
                await SelectFolderAsync(folder, ct);

                var counts = new Dictionary<ImapFlags, int>();
                foreach (ImapFlags flag in Enum.GetValues(typeof(ImapFlags)))
                {
                    string cmd = BuildFlagSearch(flag, "");
                    string[] uids = await SearchAsync(cmd, ct);
                    counts[flag] = uids.Length;
                }
                return counts;
            }
            finally { _lock.Release(); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  FLAG OPERATIONS
        // ─────────────────────────────────────────────────────────────────────
        public Task<bool> MarkAsSeenAsync(string id, string folder = "INBOX", CancellationToken ct = default)
            => StoreFlagAsync(id, folder, "+FLAGS", "\\Seen", ct);

        public Task<bool> MarkAsUnseenAsync(string id, string folder = "INBOX", CancellationToken ct = default)
            => StoreFlagAsync(id, folder, "-FLAGS", "\\Seen", ct);

        public Task<bool> MarkAsImportantAsync(string id, string folder = "INBOX", CancellationToken ct = default)
            => StoreFlagAsync(id, folder, "+FLAGS", "\\Flagged", ct);

        public Task<bool> MarkAsUnimportantAsync(string id, string folder = "INBOX", CancellationToken ct = default)
            => StoreFlagAsync(id, folder, "-FLAGS", "\\Flagged", ct);

        public Task<bool> MarkAsAnsweredAsync(string id, string folder = "INBOX", CancellationToken ct = default)
            => StoreFlagAsync(id, folder, "+FLAGS", "\\Answered", ct);

        public Task<bool> MarkAsDraftAsync(string id, string folder = "INBOX", CancellationToken ct = default)
            => StoreFlagAsync(id, folder, "+FLAGS", "\\Draft", ct);

        private async Task<bool> StoreFlagAsync(
            string id, string folder, string op, string flag, CancellationToken ct)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);
                await SelectFolderAsync(folder, ct);
                string tag = Tag();
                await SendAsync($"{tag} UID STORE {id} {op} ({flag})", ct);
                var resp = await ReadTaggedAsync(tag, ct);
                return resp.IsOk;
            }
            finally { _lock.Release(); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  DELETE
        // ─────────────────────────────────────────────────────────────────────
        public async Task<bool> DeleteMessageAsync(
            string id,
            string folder = "INBOX",
            CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);
                await SelectFolderAsync(folder, ct);

                string tagStore = Tag();
                await SendAsync($"{tagStore} UID STORE {id} +FLAGS (\\Deleted)", ct);
                (await ReadTaggedAsync(tagStore, ct)).ThrowIfFailed("STORE \\Deleted");

                string tagExp = Tag();
                await SendAsync($"{tagExp} EXPUNGE", ct);
                var resp = await ReadTaggedAsync(tagExp, ct);
                return resp.IsOk;
            }
            finally { _lock.Release(); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EMPTY FOLDER  — marks all messages deleted then expunges
        // ─────────────────────────────────────────────────────────────────────
        public async Task<bool> EmptyFolderAsync(
            string folder = "INBOX",
            CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);
                await SelectFolderAsync(folder, ct);

                // Search all messages
                string[] uids = await SearchAsync("ALL", ct);
                if (uids.Length == 0) return true;

                string uidSet = string.Join(",", uids);

                // Mark all \Deleted in one command
                string tagStore = Tag();
                await SendAsync($"{tagStore} UID STORE {uidSet} +FLAGS (\\Deleted)", ct);
                (await ReadTaggedAsync(tagStore, ct)).ThrowIfFailed("STORE \\Deleted");

                // Expunge all at once
                string tagExp = Tag();
                await SendAsync($"{tagExp} EXPUNGE", ct);
                var resp = await ReadTaggedAsync(tagExp, ct);
                return resp.IsOk;
            }
            finally { _lock.Release(); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  REPORT SPAM  — moves to Junk/Spam and marks \Flagged + \Seen
        //
        //  IMAP has no native spam command. The universal standard used by
        //  Gmail, Outlook, Apple Mail is: move to Spam folder + mark Seen.
        //
        //  Common spam folder names by provider:
        //    Gmail        → [Gmail]/Spam
        //    Outlook/365  → Junk Email
        //    Yahoo        → Bulk Mail
        //    Generic IMAP → Junk  (RFC 6154 \Junk special-use attribute)
        // ─────────────────────────────────────────────────────────────────────
        public async Task<bool> ReportSpamAsync(
            string id,
            string sourceFolder = "INBOX",
            string spamFolder = "Junk",
            CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);

                // Auto-detect spam folder name if not overridden
                string resolvedSpam = await ResolveSpecialFolderAsync(
                    spamFolder,
                    new[] { "Junk", "Junk Email", "[Gmail]/Spam", "Spam", "Bulk Mail", "Bulk" },
                    ct);

                await SelectFolderAsync(sourceFolder, ct);

                // Mark as Seen (spam shouldn't stay unread)
                string tagSeen = Tag();
                await SendAsync(tagSeen + " UID STORE " + id + " +FLAGS (\\Seen)", ct);
                await ReadTaggedAsync(tagSeen, ct);

                // Move to spam folder
                string dest = SafeFolder(resolvedSpam);
                if (_capabilities.Contains("MOVE"))
                {
                    string tagMove = Tag();
                    await SendAsync(tagMove + " UID MOVE " + id + " " + dest, ct);
                    return (await ReadTaggedAsync(tagMove, ct)).IsOk;
                }

                // Fallback: COPY + DELETE + EXPUNGE
                string tagCopy = Tag();
                await SendAsync(tagCopy + " UID COPY " + id + " " + dest, ct);
                (await ReadTaggedAsync(tagCopy, ct)).ThrowIfFailed("COPY to spam");

                string tagDel = Tag();
                await SendAsync(tagDel + " UID STORE " + id + " +FLAGS (\\Deleted)", ct);
                (await ReadTaggedAsync(tagDel, ct)).ThrowIfFailed("STORE \\Deleted");

                string tagExp = Tag();
                await SendAsync(tagExp + " EXPUNGE", ct);
                return (await ReadTaggedAsync(tagExp, ct)).IsOk;
            }
            finally { _lock.Release(); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  REPORT PHISHING  — same as spam but moves to Phishing/Junk folder
        //  and additionally marks with \Flagged so it stands out in the folder
        // ─────────────────────────────────────────────────────────────────────
        public async Task<bool> ReportPhishingAsync(
            string id,
            string sourceFolder = "INBOX",
            string spamFolder = "Junk",
            CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);

                string resolvedSpam = await ResolveSpecialFolderAsync(
                    spamFolder,
                    new[] { "Junk", "Junk Email", "[Gmail]/Spam", "Spam", "Bulk Mail", "Bulk" },
                    ct);

                await SelectFolderAsync(sourceFolder, ct);

                // Mark Seen + Flagged (phishing is high priority to review)
                string tagFlags = Tag();
                await SendAsync(tagFlags + " UID STORE " + id + " +FLAGS (\\Seen \\Flagged)", ct);
                await ReadTaggedAsync(tagFlags, ct);

                // Move to spam/junk folder
                string dest = SafeFolder(resolvedSpam);
                if (_capabilities.Contains("MOVE"))
                {
                    string tagMove = Tag();
                    await SendAsync(tagMove + " UID MOVE " + id + " " + dest, ct);
                    return (await ReadTaggedAsync(tagMove, ct)).IsOk;
                }

                string tagCopy = Tag();
                await SendAsync(tagCopy + " UID COPY " + id + " " + dest, ct);
                (await ReadTaggedAsync(tagCopy, ct)).ThrowIfFailed("COPY to junk");

                string tagDel = Tag();
                await SendAsync(tagDel + " UID STORE " + id + " +FLAGS (\\Deleted)", ct);
                (await ReadTaggedAsync(tagDel, ct)).ThrowIfFailed("STORE \\Deleted");

                string tagExp = Tag();
                await SendAsync(tagExp + " EXPUNGE", ct);
                return (await ReadTaggedAsync(tagExp, ct)).IsOk;
            }
            finally { _lock.Release(); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  NOT SPAM  — moves message back to INBOX and removes \Flagged
        // ─────────────────────────────────────────────────────────────────────
        public async Task<bool> NotSpamAsync(
            string id,
            string spamFolder = "Junk",
            string destFolder = "INBOX",
            CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);

                string resolvedSpam = await ResolveSpecialFolderAsync(
                    spamFolder,
                    new[] { "Junk", "Junk Email", "[Gmail]/Spam", "Spam", "Bulk Mail", "Bulk" },
                    ct);

                await SelectFolderAsync(resolvedSpam, ct);

                // Remove \Flagged (in case it was marked as phishing)
                string tagFlag = Tag();
                await SendAsync(tagFlag + " UID STORE " + id + " -FLAGS (\\Flagged)", ct);
                await ReadTaggedAsync(tagFlag, ct);

                // Move back to INBOX (or specified destination)
                string dest = SafeFolder(destFolder);
                if (_capabilities.Contains("MOVE"))
                {
                    string tagMove = Tag();
                    await SendAsync(tagMove + " UID MOVE " + id + " " + dest, ct);
                    return (await ReadTaggedAsync(tagMove, ct)).IsOk;
                }

                string tagCopy = Tag();
                await SendAsync(tagCopy + " UID COPY " + id + " " + dest, ct);
                (await ReadTaggedAsync(tagCopy, ct)).ThrowIfFailed("COPY to inbox");

                string tagDel = Tag();
                await SendAsync(tagDel + " UID STORE " + id + " +FLAGS (\\Deleted)", ct);
                (await ReadTaggedAsync(tagDel, ct)).ThrowIfFailed("STORE \\Deleted");

                string tagExp = Tag();
                await SendAsync(tagExp + " EXPUNGE", ct);
                return (await ReadTaggedAsync(tagExp, ct)).IsOk;
            }
            finally { _lock.Release(); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  RESOLVE SPECIAL FOLDER  — finds the actual folder name on the server
        //  Tries the preferred name first, then falls back through candidates
        // ─────────────────────────────────────────────────────────────────────
        private async Task<string> ResolveSpecialFolderAsync(
            string preferred,
            string[] candidates,
            CancellationToken ct)
        {
            // List all mailboxes once
            string tagList = Tag();
            await SendAsync(tagList + " LIST \"\" \"*\"", ct);
            var resp = await ReadTaggedAsync(tagList, ct);

            var existing = new List<string>();
            foreach (string line in resp.Lines)
            {
                var m = Regex.Match(line, @"^\* LIST \([^)]*\) ""[^""]*"" (.+)$", RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                string name = m.Groups[1].Value.Trim().Trim('"');
                existing.Add(name);
            }

            // Preferred name first
            foreach (string e in existing)
                if (e.Equals(preferred, StringComparison.OrdinalIgnoreCase))
                    return e;

            // Then try each candidate
            foreach (string candidate in candidates)
                foreach (string e in existing)
                    if (e.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                        return e;

            // Nothing found — create preferred folder and use it
            string tagCreate = Tag();
            await SendAsync(tagCreate + " CREATE " + SafeFolder(preferred), ct);
            await ReadTaggedAsync(tagCreate, ct);
            return preferred;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  MOVE  — uses MOVE extension if available, falls back to COPY+DELETE
        // ─────────────────────────────────────────────────────────────────────
        public async Task<bool> MoveToFolderAsync(
            string id,
            string sourceFolder = "INBOX",
            string destinationFolder = "INBOX",
            CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);
                await SelectFolderAsync(sourceFolder, ct);
                string dest = SafeFolder(destinationFolder);

                if (_capabilities.Contains("MOVE"))
                {
                    string tag = Tag();
                    await SendAsync($"{tag} UID MOVE {id} {dest}", ct);
                    return (await ReadTaggedAsync(tag, ct)).IsOk;
                }

                // Fallback: COPY + STORE \Deleted + EXPUNGE
                string tagCopy = Tag();
                await SendAsync($"{tagCopy} UID COPY {id} {dest}", ct);
                (await ReadTaggedAsync(tagCopy, ct)).ThrowIfFailed("COPY");

                string tagDel = Tag();
                await SendAsync($"{tagDel} UID STORE {id} +FLAGS (\\Deleted)", ct);
                (await ReadTaggedAsync(tagDel, ct)).ThrowIfFailed("STORE \\Deleted");

                string tagExp = Tag();
                await SendAsync($"{tagExp} EXPUNGE", ct);
                return (await ReadTaggedAsync(tagExp, ct)).IsOk;
            }
            finally { _lock.Release(); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  COPY
        // ─────────────────────────────────────────────────────────────────────
        public async Task<bool> CopyToFolderAsync(
            string id,
            string sourceFolder = "INBOX",
            string destinationFolder = "INBOX",
            CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);
                await SelectFolderAsync(sourceFolder, ct);

                string tag = Tag();
                await SendAsync($"{tag} UID COPY {id} {SafeFolder(destinationFolder)}", ct);
                return (await ReadTaggedAsync(tag, ct)).IsOk;
            }
            finally { _lock.Release(); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  FOLDER MANAGEMENT
        // ─────────────────────────────────────────────────────────────────────
        public async Task<List<string>> ListMailboxesAsync(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);
                string tag = Tag();
                await SendAsync($"{tag} LIST \"\" \"*\"", ct);
                var resp = await ReadTaggedAsync(tag, ct);
                resp.ThrowIfFailed("LIST");

                var mailboxes = new List<string>();
                var regex = new Regex(@"^\* LIST \([^)]*\) ""[^""]*"" (.+)$", RegexOptions.IgnoreCase);

                foreach (string line in resp.Lines)
                {
                    var m = regex.Match(line);
                    if (!m.Success) continue;
                    string mb = m.Groups[1].Value.Trim().Trim('"');
                    mailboxes.Add(mb);
                }
                return mailboxes;
            }
            finally { _lock.Release(); }
        }

        public async Task<bool> CreateFolderAsync(string folderName, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);
                string tag = Tag();
                await SendAsync($"{tag} CREATE {SafeFolder(folderName)}", ct);
                return (await ReadTaggedAsync(tag, ct)).IsOk;
            }
            finally { _lock.Release(); }
        }

        public async Task<bool> DeleteFolderAsync(string folderName, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);
                string tag = Tag();
                await SendAsync($"{tag} DELETE {SafeFolder(folderName)}", ct);
                return (await ReadTaggedAsync(tag, ct)).IsOk;
            }
            finally { _lock.Release(); }
        }

        public async Task<bool> RenameFolderAsync(string oldName, string newName, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);
                string tag = Tag();
                await SendAsync($"{tag} RENAME {SafeFolder(oldName)} {SafeFolder(newName)}", ct);
                return (await ReadTaggedAsync(tag, ct)).IsOk;
            }
            finally { _lock.Release(); }
        }

        public async Task<bool> SubscribeFolderAsync(string folderName, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);
                string tag = Tag();
                await SendAsync($"{tag} SUBSCRIBE {SafeFolder(folderName)}", ct);
                return (await ReadTaggedAsync(tag, ct)).IsOk;
            }
            finally { _lock.Release(); }
        }

        public async Task<bool> UnsubscribeFolderAsync(string folderName, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);
                string tag = Tag();
                await SendAsync($"{tag} UNSUBSCRIBE {SafeFolder(folderName)}", ct);
                return (await ReadTaggedAsync(tag, ct)).IsOk;
            }
            finally { _lock.Release(); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  APPEND  — save a raw message to a folder (e.g. Sent)
        // ─────────────────────────────────────────────────────────────────────
        public async Task<bool> AppendMessageAsync(
            string folder,
            byte[] rawMessage,
            string[] flags = null,
            DateTime? internalDate = null,
            CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);

                string flagStr = flags != null && flags.Length > 0
                    ? $"({string.Join(" ", flags)}) "
                    : "";

                string dateStr = internalDate.HasValue
                    ? $"\"{internalDate.Value.ToString("dd-MMM-yyyy HH:mm:ss zzz")}\" "
                    : "";

                string tag = Tag();
                await SendAsync(
                    $"{tag} APPEND {SafeFolder(folder)} {flagStr}{dateStr}{{{rawMessage.Length}}}", ct);

                // Server responds with + (continue)
                string cont = await ReadLineAsync(ct);
                if (!cont.StartsWith("+")) return false;

                await _writer.BaseStream.WriteAsync(rawMessage, 0, rawMessage.Length, ct);
                await _writer.WriteLineAsync();
                await _writer.FlushAsync();

                return (await ReadTaggedAsync(tag, ct)).IsOk;
            }
            finally { _lock.Release(); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  IDLE  — RFC 2177 push notifications
        //
        //  Usage:
        //    mail.OnNewMailAsync += async (args) => {
        //        Console.WriteLine("New mail from: " + args.Message.FromName);
        //        Console.WriteLine("Subject: "       + args.Message.Subject);
        //    };
        //    await mail.StartIdleAsync("INBOX");
        //
        //  Falls back to polling (every 30s) if server has no IDLE support.
        // ─────────────────────────────────────────────────────────────────────
        private string _idleFolder = "INBOX";

        private async Task StartIdleAsync(string folder = "INBOX", CancellationToken ct = default)
        {
            _idleFolder = folder;
            await StopIdleAsync();

            Console.WriteLine("[IDLE] Capabilities: " + string.Join(", ", _capabilities));
            Console.WriteLine("[IDLE] IDLE supported: " + _capabilities.Contains("IDLE"));

            // Seed the last known UID before starting the loop
            string lastKnownUid = await GetHighestUidAsync(folder, ct);
            Console.WriteLine("[IDLE] Starting from lastKnownUid=" + lastKnownUid);

            _idleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            if (_capabilities.Contains("IDLE"))
            {
                Console.WriteLine("[IDLE] Starting IDLE loop for folder: " + folder);
                _idleTask = RunIdleLoopAsync(lastKnownUid, _idleCts.Token);
            }
            else
            {
                Console.WriteLine("[IDLE] Server has no IDLE — falling back to 30s polling.");
                _idleTask = RunPollingLoopAsync(lastKnownUid, _idleCts.Token);
            }

            Console.WriteLine("[IDLE] Background task started. Waiting for new mail…");
        }

        private async Task StopIdleAsync()
        {
            if (_idleCts == null) return;
            _idleCts.Cancel();
            try { if (_idleTask != null) await _idleTask; } catch { }
            _idleCts = null;
            _idleTask = null;
        }

        /// <summary>
        /// Awaits the IDLE background loop — call this to keep your app alive
        /// while waiting for new mail. Returns when StopIdleAsync() is called.
        ///
        /// Example:
        ///   await mail.StartIdleAsync("INBOX");
        ///   await mail.WaitForIdleAsync();  // blocks here until stopped
        /// </summary>
        public async Task WaitForIdleAsync()
        {
            if (_idleTask != null)
                try { await _idleTask; } catch { }
        }

        /// <summary>
        /// Combines StartIdleAsync + WaitForIdleAsync in one call.
        /// Blocks until StopIdleAsync() is called or ct is cancelled.
        ///
        /// Usage:
        ///   mail.OnNewMailAsync += async (args) => { ... };
        ///   await mail.ListenAsync("INBOX");
        /// </summary>
        public async Task ListenAsync(string folder = "INBOX", CancellationToken ct = default)
        {
            await StartIdleAsync(folder, ct);
            await WaitForIdleAsync();
        }

        // ── IDLE loop ─────────────────────────────────────────────────────────
        private async Task RunIdleLoopAsync(string lastKnownUid, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Enter IDLE — send command while holding lock
                    await _lock.WaitAsync(ct);
                    bool lockHeld = true;
                    try
                    {
                        string tag = Tag();
                        await SendAsync(tag + " IDLE", ct);

                        // Read the + continuation (still under lock)
                        string cont = await ReadLineRawAsync(ct);
                        Console.WriteLine("[IDLE] Server: " + cont);

                        if (!cont.StartsWith("+"))
                        {
                            Console.WriteLine("[IDLE] Server rejected IDLE.");
                            return;
                        }

                        // Release lock BEFORE blocking on server push lines
                        _lock.Release();
                        lockHeld = false;

                        // ── Listen for server push — NO lock, NO timeout ──────
                        bool gotExists = false;
                        while (!ct.IsCancellationRequested)
                        {
                            // ReadLineRawAsync has NO internal timeout — reads until \n
                            string line;
                            try { line = await ReadLineRawAsync(ct); }
                            catch (OperationCanceledException) { break; }

                            Console.WriteLine("[IDLE PUSH] " + line);

                            if (Regex.IsMatch(line, @"^\* \d+ EXISTS", RegexOptions.IgnoreCase))
                            {
                                gotExists = true;
                                break; // respond immediately
                            }

                            // Server keepalive or other untagged — keep listening
                        }

                        // Re-acquire lock to send DONE
                        await _lock.WaitAsync(ct);
                        lockHeld = true;

                        await _writer.WriteLineAsync("DONE");
                        await _writer.FlushAsync();

                        // Read until we get the tagged OK for IDLE
                        await ReadTaggedAsync(tag, ct);

                        if (gotExists)
                        {
                            await SelectFolderInternalAsync(_idleFolder, ct);
                            lastKnownUid = await FetchAndFireNewMessagesInternalAsync(
                                _idleFolder, lastKnownUid, ct);
                        }
                    }
                    finally
                    {
                        if (lockHeld) _lock.Release();
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine("[IDLE] Error: " + ex.Message + " — retry in 5s…");
                    try { await Task.Delay(5000, ct); } catch { break; }

                    await _lock.WaitAsync(ct);
                    try
                    {
                        await EnsureConnectedAsync(ct);
                        await SelectFolderInternalAsync(_idleFolder, ct);
                    }
                    finally { _lock.Release(); }
                }
            }
        }

        // ── Polling fallback ──────────────────────────────────────────────────
        private async Task RunPollingLoopAsync(string lastKnownUid, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);

                    if (OnNewMailAsync != null)
                    {
                        await _lock.WaitAsync(ct);
                        try
                        {
                            await EnsureConnectedAsync(ct);
                            await SelectFolderInternalAsync(_idleFolder, ct);
                            lastKnownUid = await FetchAndFireNewMessagesInternalAsync(
                                _idleFolder, lastKnownUid, ct);
                        }
                        finally { _lock.Release(); }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine("[POLL] Error: " + ex.Message + " — reconnecting…");
                    try { await Task.Delay(5000, ct); } catch { break; }

                    await _lock.WaitAsync(ct);
                    try { await EnsureConnectedAsync(ct); }
                    finally { _lock.Release(); }
                }
            }
        }

        // ── Internal: fetch new messages and fire event — lock already held ───
        private async Task<string> FetchAndFireNewMessagesInternalAsync(
            string folder, string lastKnownUid, CancellationToken ct)
        {
            long lastLong;
            if (!long.TryParse(lastKnownUid, out lastLong)) lastLong = 0;

            // Search for ALL UIDs and filter client-side
            // More reliable than UID N:* which some servers handle inconsistently
            string[] allUids = await SearchAsync("ALL", ct);

            var fresh = new List<string>();
            foreach (string uid in allUids)
            {
                long u;
                if (long.TryParse(uid, out u) && u > lastLong)
                    fresh.Add(uid);
            }

            Console.WriteLine("[IDLE] Found " + fresh.Count + " new message(s). lastKnownUid=" + lastKnownUid);

            string newHighest = lastKnownUid;

            foreach (string uid in fresh)
            {
                try
                {
                    Console.WriteLine("[IDLE] Fetching new message UID=" + uid);
                    var msg = await FetchFullMessageAsync(uid, folder, true, ct);

                    if (msg != null)
                    {
                        // Update highest before firing so handler sees correct state
                        long u, h;
                        if (long.TryParse(uid, out u) &&
                            (string.IsNullOrEmpty(newHighest) ||
                             (long.TryParse(newHighest, out h) && u > h)))
                            newHighest = uid;

                        if (OnNewMailAsync != null)
                        {
                            // Release lock so user can call GetInbox() etc. in handler
                            _lock.Release();
                            try
                            {
                                await OnNewMailAsync.Invoke(new NewMailEventArgs(msg, folder));
                            }
                            finally
                            {
                                await _lock.WaitAsync(ct);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[IDLE] Failed to fetch UID " + uid + ": " + ex.Message);
                }
            }

            return newHighest;
        }

        // ── Get highest UID — uses lock normally ──────────────────────────────
        private async Task<string> GetHighestUidAsync(string folder, CancellationToken ct)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await EnsureConnectedAsync(ct);
                await SelectFolderInternalAsync(folder, ct);
                string[] uids = await SearchAsync("ALL", ct);
                if (uids.Length == 0) return "0";

                string highest = "0";
                foreach (string uid in uids)
                {
                    long u, h;
                    if (long.TryParse(uid, out u) &&
                        long.TryParse(highest, out h) && u > h)
                        highest = uid;
                }
                return highest;
            }
            finally { _lock.Release(); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  INTERNAL FULL MESSAGE FETCH  (BODYSTRUCTURE-based)
        // ─────────────────────────────────────────────────────────────────────
        private async Task<MailMessage> FetchFullMessageAsync(
            string id, string folder, bool skipSelect, CancellationToken ct)
        {
            if (!skipSelect)
                await SelectFolderAsync(folder, ct);

            // 1 — Fetch BODYSTRUCTURE + headers
            string tagBS = Tag();
            await SendAsync(
                $"{tagBS} UID FETCH {id} (UID FLAGS BODYSTRUCTURE BODY.PEEK[HEADER])", ct);
            var bsResp = await ReadTaggedAsync(tagBS, ct);

            string rawHeaders = ExtractLiteralFromBlock(bsResp.Raw, "HEADER");
            var headers = HeaderParser.Parse(rawHeaders ?? "");
            var flags = ExtractFlagsFromResponse(bsResp.Raw, id);

            string bsRaw = ExtractField(bsResp.Raw, "BODYSTRUCTURE");
            BodyPart root = string.IsNullOrEmpty(bsRaw)
                ? null
                : BodyStructureParser.Parse(bsRaw);

            var mail = new MailMessage
            {
                Id = id,
                Folder = folder,
                Flags = flags,
                MessageId = HeaderParser.GetRaw(headers, "Message-ID"),
                InReplyTo = HeaderParser.GetRaw(headers, "In-Reply-To"),
                ReplyTo = HeaderParser.GetRaw(headers, "Reply-To"),
                Subject = HeaderParser.Get(headers, "Subject"),
                Date = MimeDecoder.ParseDate(HeaderParser.GetRaw(headers, "Date")) ?? DateTime.MinValue,
                To = AddressParser.ParseList(HeaderParser.GetRaw(headers, "To")),
                CC = AddressParser.ParseList(HeaderParser.GetRaw(headers, "CC")),
                BCC = AddressParser.ParseList(HeaderParser.GetRaw(headers, "BCC")),
            };

            string rawFrom = HeaderParser.GetRaw(headers, "From");
            AddressParser.ParseSingle(rawFrom, out string fromName, out string fromEmail);
            mail.From = rawFrom;
            mail.FromName = fromName;
            mail.FromEmail = fromEmail;
            mail.FromImage = BuildInitialsSpan(fromName ?? fromEmail);

            // 2 — If no BODYSTRUCTURE, fall back to fetching BODY[]
            if (root == null)
            {
                string tagFull = Tag();
                await SendAsync($"{tagFull} UID FETCH {id} (BODY[])", ct);
                var fullResp = await ReadTaggedAsync(tagFull, ct);
                ParseRawBody(fullResp.Raw, mail);
                return mail;
            }

            // 3 — Selectively fetch only needed parts
            await FetchPartsAsync(id, root, mail, ct);
            mail.HtmlBody = InjectImportant(ReplaceCidWithDataUri(mail.HtmlBody, mail.Attachments));
            mail.PlainTextBody = !string.IsNullOrEmpty(mail.PlainTextBody)
                ? mail.PlainTextBody
                : MimeDecoder.HtmlToPlainText(mail.HtmlBody);

            return mail;
        }

        private async Task FetchPartsAsync(string id, BodyPart root, MailMessage mail, CancellationToken ct)
        {
            var parts = BodyStructureParser.Flatten(root)
                        .Where(p => !p.IsMultipart)
                        .ToList();

            foreach (var part in parts)
            {
                string section = string.IsNullOrEmpty(part.Section) ? "1" : part.Section;
                string tagP = Tag();
                await SendAsync($"{tagP} UID FETCH {id} (BODY.PEEK[{section}])", ct);
                var resp = await ReadTaggedAsync(tagP, ct);

                string body = ExtractSectionBody(resp.Raw, section);
                if (body == null) continue;

                string mimeType = $"{part.Type}/{part.SubType}".ToLowerInvariant();

                if (part.IsAttachment || (part.Type != "text" && !string.IsNullOrEmpty(part.FileName)))
                {
                    byte[] data = MimeDecoder.DecodeAttachmentBytes(body, part.Encoding);
                    mail.Attachments.Add(new MailAttachment
                    {
                        FileName = part.FileName ?? $"attachment_{Guid.NewGuid():N}.bin",
                        ContentType = mimeType,
                        ContentId = part.ContentId,
                        IsInline = part.IsInline && !part.IsAttachment,
                        SizeBytes = data.LongLength,
                        Data = data
                    });
                }
                else if (part.IsInline && part.Type != "text")
                {
                    byte[] data = MimeDecoder.DecodeAttachmentBytes(body, part.Encoding);
                    mail.Attachments.Add(new MailAttachment
                    {
                        FileName = part.FileName ?? $"inline_{Guid.NewGuid():N}.bin",
                        ContentType = mimeType,
                        ContentId = part.ContentId,
                        IsInline = true,
                        SizeBytes = data.LongLength,
                        Data = data
                    });
                }
                else if (mimeType == "text/html" && mail.HtmlBody == null)
                {
                    mail.HtmlBody = MimeDecoder.DecodePart(body, part.Encoding, part.Charset);
                }
                else if (mimeType == "text/plain" && mail.PlainTextBody == null)
                {
                    mail.PlainTextBody = MimeDecoder.DecodePart(body, part.Encoding, part.Charset);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  INTERNAL HELPERS
        // ─────────────────────────────────────────────────────────────────────
        private async Task SelectFolderAsync(string folder, CancellationToken ct)
        {
            string tag = Tag();
            await SendAsync($"{tag} SELECT {SafeFolder(folder)}", ct);
            (await ReadTaggedAsync(tag, ct)).ThrowIfFailed($"SELECT {folder}");
        }

        private async Task SelectFolderInternalAsync(string folder, CancellationToken ct)
            => await SelectFolderAsync(folder, ct);

        private async Task<string[]> SearchAsync(string criteria, CancellationToken ct)
        {
            string tag = Tag();
            await SendAsync($"{tag} UID SEARCH {criteria}", ct);
            var resp = await ReadTaggedAsync(tag, ct);

            foreach (string line in resp.Lines)
            {
                var m = Regex.Match(line, @"^\* SEARCH(.*)$", RegexOptions.IgnoreCase);
                if (m.Success)
                    return m.Groups[1].Value.Trim()
                           .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            }
            return new string[0];
        }

        private string BuildFlagSearch(ImapFlags flag, string emailFilter)
        {
            string search;
            if (flag == ImapFlags.SEEN) search = "SEEN";
            else if (flag == ImapFlags.UNSEEN) search = "UNSEEN";
            else if (flag == ImapFlags.ANSWERED) search = "ANSWERED";
            else if (flag == ImapFlags.UNANSWERED) search = "UNANSWERED";
            else if (flag == ImapFlags.FLAGGED) search = "FLAGGED";
            else if (flag == ImapFlags.UNFLAGGED) search = "UNFLAGGED";
            else if (flag == ImapFlags.DELETED) search = "DELETED";
            else if (flag == ImapFlags.UNDELETED) search = "UNDELETED";
            else if (flag == ImapFlags.DRAFT) search = "DRAFT";
            else if (flag == ImapFlags.UNDRAFT) search = "UNDRAFT";
            else search = "ALL";

            if (!string.IsNullOrEmpty(emailFilter))
                search = $"FROM \"{emailFilter}\" {search}";

            return search;
        }

        // ── Send ──────────────────────────────────────────────────────────────
        private async Task SendAsync(string command, CancellationToken ct)
        {
            Console.WriteLine("[SEND] " + command);
            await _writer.WriteLineAsync(command);
        }

        // ── Read tagged response ──────────────────────────────────────────────
        private async Task<ImapResponse> ReadTaggedAsync(string tag, CancellationToken ct)
        {
            var resp = new ImapResponse { Tag = tag };

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                string line = await ReadLineAsync(ct);
                Console.WriteLine($"[IMAP] {line}");

                // Literal expansion
                var litMatch = Regex.Match(line, @"\{(\d+)\}$");
                if (litMatch.Success)
                {
                    int count = int.Parse(litMatch.Groups[1].Value);
                    string data = await ReadLiteralAsync(count, ct);
                    line = line + data;
                }

                resp.Lines.Add(line);

                if (line.StartsWith(tag + " ", StringComparison.Ordinal))
                {
                    string status = line.Substring(tag.Length + 1);
                    resp.IsOk = status.StartsWith("OK", StringComparison.OrdinalIgnoreCase);
                    resp.IsNo = status.StartsWith("NO", StringComparison.OrdinalIgnoreCase);
                    resp.IsBad = status.StartsWith("BAD", StringComparison.OrdinalIgnoreCase);
                    resp.Raw = string.Join("\r\n", resp.Lines);
                    if (resp.IsNo || resp.IsBad) IsConnected = false;
                    return resp;
                }

                if (line.StartsWith("* BYE", StringComparison.OrdinalIgnoreCase))
                {
                    resp.IsBye = true;
                    resp.Raw = string.Join("\r\n", resp.Lines);
                    IsConnected = false;
                    return resp;
                }
            }
        }

        // ── Read line WITH 60s timeout  (normal commands) ────────────────────
        private async Task<string> ReadLineAsync(CancellationToken ct)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(60));
            try
            {
                return await ReadLineRawAsync(cts.Token);
            }
            finally { cts.Dispose(); }
        }

        // ── Read line with NO timeout  (IDLE push — server may be silent for mins) ──
        private async Task<string> ReadLineRawAsync(CancellationToken ct)
        {
            var sb = new StringBuilder(256);
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                char[] buf = new char[1];
                int n = await _reader.ReadAsync(buf, 0, 1);
                if (n == 0) return sb.ToString();
                if (buf[0] == '\r') continue;
                if (buf[0] == '\n') return sb.ToString();
                sb.Append(buf[0]);
            }
        }

        private async Task<string> ReadLiteralAsync(int count, CancellationToken ct)
        {
            char[] buf = new char[count];
            int total = 0;
            while (total < count)
            {
                ct.ThrowIfCancellationRequested();
                int n = await _reader.ReadAsync(buf, total, count - total);
                if (n <= 0) break;
                total += n;
            }
            return new string(buf, 0, total);
        }

        // ── Extract FETCH block for a specific UID ────────────────────────────
        private string ExtractFetchBlock(string response, string uid)
        {
            // Find "* NNN FETCH (…)" that contains UID uid
            foreach (Match m in Regex.Matches(response,
                @"\* \d+ FETCH \((.+?)(?=\n\* |\n[A-Z]\d+ |\z)",
                RegexOptions.Singleline))
            {
                string block = m.Groups[1].Value;
                if (Regex.IsMatch(block, $@"\bUID\s+{Regex.Escape(uid)}\b"))
                    return block;
            }
            return null;
        }

        private string ExtractLiteralFromBlock(string block, string fieldName)
        {
            if (string.IsNullOrEmpty(block)) return null;
            var m = Regex.Match(block,
                $@"BODY\[{Regex.Escape(fieldName)}[^\]]*\]\s*\{{(\d+)\}}(.+)",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            int len = int.Parse(m.Groups[1].Value);
            string d = m.Groups[2].Value;
            return d.Length >= len ? d.Substring(0, len) : d;
        }

        private string ExtractSectionBody(string response, string section)
        {
            var m = Regex.Match(response,
                $@"BODY\[{Regex.Escape(section)}\]\s*\{{(\d+)\}}([\s\S]+)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!m.Success) return null;
            int len = int.Parse(m.Groups[1].Value);
            string d = m.Groups[2].Value;
            return d.Length >= len ? d.Substring(0, len) : d;
        }

        private string ExtractField(string block, string fieldName)
        {
            if (string.IsNullOrEmpty(block)) return null;

            var m = Regex.Match(block,
                $@"{Regex.Escape(fieldName)}\s+(\(.+\)|\S+)",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!m.Success) return null;

            string val = m.Groups[1].Value.Trim();

            // If it's a paren group, find the balanced end
            if (val.StartsWith("("))
                return ExtractBalancedParens(block, m.Groups[1].Index);

            return val;
        }

        private string ExtractBalancedParens(string s, int start)
        {
            int depth = 0;
            bool inQ = false;
            var sb = new StringBuilder();
            for (int i = start; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"') inQ = !inQ;
                if (!inQ)
                {
                    if (c == '(') depth++;
                    else if (c == ')') { depth--; sb.Append(c); if (depth == 0) break; continue; }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        private List<string> ExtractFlagsFromResponse(string response, string uid)
        {
            foreach (string line in response.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                if (!line.Contains($"UID {uid}")) continue;
                var m = Regex.Match(line, @"FLAGS\s*\(([^)]*)\)");
                if (m.Success)
                    return m.Groups[1].Value
                           .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(f => f.TrimStart('\\').ToUpperInvariant())
                           .ToList();
            }
            return new List<string>();
        }

        private List<string> ExtractFlagsFromBlock(string block)
        {
            if (string.IsNullOrEmpty(block)) return new List<string>();
            var m = Regex.Match(block, @"FLAGS\s*\(([^)]*)\)");
            if (!m.Success) return new List<string>();
            return m.Groups[1].Value
                   .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                   .Select(f => f.TrimStart('\\').ToUpperInvariant())
                   .ToList();
        }

        // ── Fallback raw-body parser (used when BODYSTRUCTURE is unavailable) ─
        private void ParseRawBody(string raw, MailMessage mail)
        {
            // Strip FETCH wrapper
            raw = Regex.Replace(raw, @"^\* \d+ FETCH \(.*?\r?\n", "", RegexOptions.Singleline);
            raw = Regex.Replace(raw, @"\)\r?\n[A-Z]\d+ OK.*$", "", RegexOptions.Multiline).Trim();

            string html = null;
            string plainText = null;
            var attaches = new List<MailAttachment>();

            ParseMimePart(raw, ref html, ref plainText, attaches);

            mail.HtmlBody = InjectImportant(ReplaceCidWithDataUri(html, attaches));
            mail.PlainTextBody = plainText ?? MimeDecoder.HtmlToPlainText(html);
            mail.Attachments = attaches;
        }

        private void ParseMimePart(
            string part, ref string html, ref string plainText,
            List<MailAttachment> attaches, int depth = 0)
        {
            if (depth > 10) return;

            int hEnd = part.IndexOf("\r\n\r\n");
            if (hEnd < 0) hEnd = part.IndexOf("\n\n");
            string headerSection = hEnd >= 0 ? part.Substring(0, hEnd) : "";
            var partHeaders = HeaderParser.Parse(headerSection);

            string ct = HeaderParser.GetRaw(partHeaders, "Content-Type") ?? "";
            string enc = HeaderParser.GetRaw(partHeaders, "Content-Transfer-Encoding") ?? "";
            string disp = HeaderParser.GetRaw(partHeaders, "Content-Disposition") ?? "";
            string cid = (HeaderParser.GetRaw(partHeaders, "Content-ID") ?? "").Trim('<', '>');
            string boundary = MimeDecoder.ExtractBoundary(ct);

            if (!string.IsNullOrEmpty(boundary))
            {
                string body = hEnd >= 0 ? part.Substring(hEnd + (part[hEnd] == '\r' ? 4 : 2)) : part;
                var subParts = body.Split(new[] { "--" + boundary }, StringSplitOptions.None);
                foreach (var sub in subParts)
                {
                    if (sub.TrimStart().StartsWith("--")) continue;
                    if (string.IsNullOrWhiteSpace(sub)) continue;
                    ParseMimePart(sub, ref html, ref plainText, attaches, depth + 1);
                }
                return;
            }

            string bodyText = hEnd >= 0
                ? part.Substring(hEnd + (part[hEnd] == '\r' ? 4 : 2)).Trim()
                : part.Trim();

            bool isAtt = disp.IndexOf("attachment", StringComparison.OrdinalIgnoreCase) >= 0
                      || (!string.IsNullOrEmpty(MimeDecoder.ExtractFilename(disp, ct)));

            if (isAtt || (ct.IndexOf("text/", StringComparison.OrdinalIgnoreCase) < 0 && !string.IsNullOrEmpty(ct)))
            {
                byte[] data = MimeDecoder.DecodeAttachmentBytes(bodyText, enc);
                attaches.Add(new MailAttachment
                {
                    FileName = MimeDecoder.ExtractFilename(disp, ct) ?? $"file_{Guid.NewGuid():N}.bin",
                    ContentType = ct.Split(';')[0].Trim(),
                    ContentId = string.IsNullOrEmpty(cid) ? null : cid,
                    IsInline = !string.IsNullOrEmpty(cid) || disp.Contains("inline"),
                    SizeBytes = data.LongLength,
                    Data = data
                });
            }
            else if (ct.IndexOf("text/html", StringComparison.OrdinalIgnoreCase) >= 0 && html == null)
            {
                html = MimeDecoder.DecodePart(bodyText, enc, MimeDecoder.ExtractCharset(ct));
            }
            else if (ct.IndexOf("text/plain", StringComparison.OrdinalIgnoreCase) >= 0 && plainText == null)
            {
                plainText = MimeDecoder.DecodePart(bodyText, enc, MimeDecoder.ExtractCharset(ct));
            }
        }

        // ── HTML post-processing ──────────────────────────────────────────────
        private string ReplaceCidWithDataUri(string html, List<MailAttachment> attachments)
        {
            if (string.IsNullOrEmpty(html) || attachments == null) return html;
            return Regex.Replace(html, @"cid:([^\s""'>]+)", m =>
            {
                string cid = m.Groups[1].Value.Trim();
                var match = attachments.FirstOrDefault(a =>
                    a.ContentId != null &&
                    (a.ContentId.Equals(cid, StringComparison.OrdinalIgnoreCase) ||
                     a.ContentId.Equals($"<{cid}>", StringComparison.OrdinalIgnoreCase)));
                return match != null ? match.DataUri : m.Value;
            }, RegexOptions.IgnoreCase);
        }

        private string InjectImportant(string html)
        {
            if (string.IsNullOrEmpty(html)) return html;
            return Regex.Replace(html, @"style=""([^""]*)""", m =>
            {
                var sb = new StringBuilder();
                foreach (var prop in m.Groups[1].Value.Split(';'))
                {
                    string p = prop.Trim();
                    if (string.IsNullOrEmpty(p)) continue;
                    sb.Append(p.EndsWith("!important", StringComparison.OrdinalIgnoreCase)
                        ? p + ";"
                        : p + " !important;");
                }
                return $"style=\"{sb}\"";
            }, RegexOptions.IgnoreCase);
        }

        // ── Subject normalization ─────────────────────────────────────────────
        private string NormalizeSubject(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string result = s;
            while (Regex.IsMatch(result, @"^\s*(Re|Fwd?)\s*:\s*", RegexOptions.IgnoreCase))
                result = Regex.Replace(result, @"^\s*(Re|Fwd?)\s*:\s*", "", RegexOptions.IgnoreCase);
            return result.Trim();
        }

        // ── Avatar builder ────────────────────────────────────────────────────
        private string BuildInitialsSpan(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) input = "?";
            var words = input.Trim().Split(new[] { ' ', '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            string initials = words.Length >= 2
                ? string.Concat(words[0][0].ToString(), words[words.Length - 1][0].ToString())
                : words[0].Substring(0, Math.Min(2, words[0].Length));
            initials = initials.ToUpper();
            string color = GetColorForInitial(initials[0]);
            return $"<span class=\"rounded-circle text-white fw-bold d-inline-flex align-items-center " +
                   $"justify-content-center flex-shrink-0\" " +
                   $"style=\"width:34px;height:34px;font-size:.7rem;background-color:{color}\">" +
                   $"{initials}</span>";
        }

        private string GetColorForInitial(char c)
        {
            string[] colors = { "#e74c3c", "#3498db", "#2ecc71", "#f1c40f", "#9b59b6", "#e67e22", "#1abc9c", "#34495e" };
            return colors[char.ToUpper(c) % colors.Length];
        }

        // ── Misc ──────────────────────────────────────────────────────────────
        private string Tag() => "A" + _tag++;
        private string SafeFolder(string f) => f.Contains(' ') ? $"\"{f}\"" : f;
        private string EscapeImap(string s) => s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        public void Dispose()
        {
            LogoutAsync().GetAwaiter().GetResult();
            _lock.Dispose();
        }
    }
}