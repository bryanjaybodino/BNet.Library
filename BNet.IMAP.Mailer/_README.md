# BNet.IMAP.Mailer

Lightweight, dependency-free IMAP client for .NET to fetch, read, move, and manage emails over secure SSL/TLS. Supports Gmail, Outlook, and any standard IMAP server.

---

## ✨ Features

- 🔐 Secure SSL/TLS connection — username/password **and** OAuth2/XOAUTH2
- 📥 Get unread, seen, flagged, deleted, or all messages with pagination
- 📊 Email counts by flag (All, Seen, Unseen, Answered, Flagged, Deleted, Draft…)
- 📄 Fetch full email (HTML + Plain Text)
- 📎 Extract file attachments and inline embedded images
- 🧵 Email thread grouping by subject
- 👤 Sender profile — `FromName`, `FromEmail`, `FromImage` (colored initials avatar)
- 🖼 Inline `cid:` images auto-replaced with `data:` URIs (renders without extra requests)
- 🏷 Per-message IMAP flags (`Seen`, `Answered`, `Flagged`, `Deleted`, `Draft`)
- ✅ Mark read / unread / important / answered / draft
- 🗑 Delete and expunge
- 🧹 Empty folder in one command
- 📂 Move and Copy messages to another folder
- 📋 List, Create, Delete, Rename, Subscribe, Unsubscribe mailboxes
- 🔍 Fluent search query builder (`ImapSearchQuery`)
- 📬 Real-time new mail via IDLE (RFC 2177) with automatic polling fallback
- 🚫 Report Spam / Phishing — auto-detects Junk folder per provider
- ✅ Not Spam — move back to INBOX
- 💾 Append raw message to folder (save sent mail)
- ⚡ Full async/await, thread-safe `SemaphoreSlim`
- 🔄 Auto-reconnect on dropped connections
- 📦 Zero third-party dependencies
- 🎯 Targets .NET 4.5 → .NET 8.0

---

## 📦 Installation

### NuGet Package Manager
```powershell
Install-Package BNet.IMAP.Mailer
```

### .NET CLI
```bash
dotnet add package BNet.IMAP.Mailer
```

---

## 📐 Data Models

### MailAttachment

```csharp
public class MailAttachment
{
    public string FileName    { get; set; }  // "invoice.pdf"
    public string ContentType { get; set; }  // "application/pdf"
    public string ContentId   { get; set; }  // CID for inline images (null if not inline)
    public bool   IsInline    { get; set; }  // true = embedded image in HTML body
    public long   SizeBytes   { get; set; }  // decoded file size in bytes
    public byte[] Data        { get; set; }  // raw decoded bytes
    public string Base64Data  { get; }       // base64 string of Data
    public string DataUri     { get; }       // "data:image/png;base64,…" ready for <img src>
}
```

---

### MailMessage

```csharp
public class MailMessage
{
    public string       Id              { get; set; }  // IMAP UID e.g. "124"
    public string       Folder          { get; set; }  // "INBOX", "Sent", etc.
    public List<string> Flags           { get; set; }  // ["Seen", "Answered"]

    public string       From            { get; set; }  // "John Doe <john@example.com>"
    public string       FromName        { get; set; }  // "John Doe"
    public string       FromEmail       { get; set; }  // "john@example.com"
    public string       FromImage       { get; set; }  // <span> colored initials avatar

    public List<string> To              { get; set; }
    public List<string> CC              { get; set; }
    public List<string> BCC             { get; set; }
    public string       ReplyTo         { get; set; }
    public string       MessageId       { get; set; }
    public string       InReplyTo       { get; set; }
    public string       Subject         { get; set; }
    public DateTime     Date            { get; set; }

    public string       HtmlBody        { get; set; }  // inline images auto-replaced with data URIs
    public string       PlainTextBody   { get; set; }

    public List<MailAttachment> Attachments       { get; set; }  // all (inline + file)
    public List<MailAttachment> FileAttachments   { get; }       // downloadable only
    public List<MailAttachment> InlineAttachments { get; }       // embedded images only
    public bool                 HasAttachments    { get; }       // true if FileAttachments.Count > 0
}
```

---

### MailInboxes
Lightweight list-view entry — headers only, no body content.

```csharp
public class MailInboxes
{
    public int          TotalEmail      { get; set; }  // total matching emails
    public int          TotalPagination { get; set; }  // total pages

    public string       Id              { get; set; }  // IMAP UID
    public string       Folder          { get; set; }  // "INBOX", "Sent", etc.
    public List<string> Flags           { get; set; }  // ["Seen", "Flagged"]

    public string       From            { get; set; }
    public string       FromName        { get; set; }
    public string       FromEmail       { get; set; }
    public string       FromImage       { get; set; }  // <span> colored initials avatar

    public List<string> To              { get; set; }
    public List<string> CC              { get; set; }
    public List<string> BCC             { get; set; }
    public string       Subject         { get; set; }
    public DateTime     Date            { get; set; }
    public bool         HasAttachment   { get; set; }

    public List<MailMessage> Submail    { get; set; }  // thread replies — from GetThreadAsync
}
```

---

### NewMailEventArgs
Passed to `OnNewMailAsync` when a new message arrives.

```csharp
public class NewMailEventArgs
{
    public MailMessage Message    { get; }  // fully fetched — body, attachments, flags all ready
    public string      Folder     { get; }  // folder being watched e.g. "INBOX"
    public DateTime    ReceivedAt { get; }  // UTC time the event was raised
}
```

---

## 🚀 Quick Start

### Connect — Username / Password
```csharp
var mail = new MailConfig();
bool ok = await mail.ConnectAsync("you@gmail.com", "your-app-password");
```

### Connect — OAuth2 / XOAUTH2
```csharp
var mail = new MailConfig();
bool ok = await mail.XOAuth2Async("you@outlook.com", accessToken, "outlook.office365.com");
```

---

## 📥 Get Inbox

### Unread Messages
```csharp
var inbox = await mail.GetInboxAsync("INBOX", ImapFlags.UNSEEN);
foreach (var msg in inbox)
{
    Console.WriteLine(msg.Id);
    Console.WriteLine(msg.FromName);
    Console.WriteLine(msg.FromEmail);
    Console.WriteLine(msg.Subject);
    Console.WriteLine(msg.Date);
    Console.WriteLine(msg.Folder);
    Console.WriteLine(string.Join(", ", msg.Flags));
}
```

### Pagination
```csharp
// Page 1 — 20 per page
var page1 = await mail.GetInboxAsync("INBOX", ImapFlags.ALL, pageSize: 20, pageIndex: 0);

// Page 2
var page2 = await mail.GetInboxAsync("INBOX", ImapFlags.ALL, pageSize: 20, pageIndex: 1);

Console.WriteLine("Total emails : " + page1[0].TotalEmail);
Console.WriteLine("Total pages  : " + page1[0].TotalPagination);
```

### Filter by Sender
```csharp
var filtered = await mail.GetInboxAsync(
    folder:      "INBOX",
    imapFlags:   ImapFlags.ALL,
    emailFilter: "boss@company.com"
);
```

---

## 📊 Email Counts by Flag
```csharp
var counts = await mail.GetEmailCountsByFlagsAsync("INBOX");

Console.WriteLine("All:        " + counts[ImapFlags.ALL]);
Console.WriteLine("Unread:     " + counts[ImapFlags.UNSEEN]);
Console.WriteLine("Read:       " + counts[ImapFlags.SEEN]);
Console.WriteLine("Answered:   " + counts[ImapFlags.ANSWERED]);
Console.WriteLine("Unanswered: " + counts[ImapFlags.UNANSWERED]);
Console.WriteLine("Flagged:    " + counts[ImapFlags.FLAGGED]);
Console.WriteLine("Unflagged:  " + counts[ImapFlags.UNFLAGGED]);
Console.WriteLine("Deleted:    " + counts[ImapFlags.DELETED]);
Console.WriteLine("Undeleted:  " + counts[ImapFlags.UNDELETED]);
Console.WriteLine("Draft:      " + counts[ImapFlags.DRAFT]);
Console.WriteLine("Undraft:    " + counts[ImapFlags.UNDRAFT]);
```

Returns `Dictionary<ImapFlags, int>` keyed by flag.

---

## 📄 Full Message
```csharp
var msg = await mail.GetFullMessageAsync("MESSAGE_UID", "INBOX");

Console.WriteLine(msg.Subject);
Console.WriteLine(msg.FromName);
Console.WriteLine(msg.FromEmail);
Console.WriteLine(string.Join(", ", msg.To));
Console.WriteLine(string.Join(", ", msg.CC));
Console.WriteLine(msg.Date);
Console.WriteLine(string.Join(", ", msg.Flags));
Console.WriteLine(msg.PlainTextBody);
Console.WriteLine(msg.HtmlBody);
```

---

## 📎 Attachments
```csharp
var msg = await mail.GetFullMessageAsync("MESSAGE_UID", "INBOX");

if (msg.HasAttachments)
{
    foreach (var file in msg.FileAttachments)
    {
        Console.WriteLine(file.FileName + "  (" + (file.SizeBytes / 1024.0).ToString("0.0") + " KB)");

        // Save to disk
        File.WriteAllBytes(file.FileName, file.Data);

        // Use DataUri in HTML:
        // <a href="@file.DataUri" download="@file.FileName">Download</a>
    }
}
```

> Inline `cid:` images in `HtmlBody` are automatically replaced with `data:` URIs — render `HtmlBody` directly and images appear with no extra HTTP requests.

---

## 🧵 Email Thread
```csharp
var thread = await mail.GetThreadAsync("MESSAGE_UID", "INBOX");

Console.WriteLine(thread.Subject);
Console.WriteLine(thread.FromName);
Console.WriteLine(thread.Date);
Console.WriteLine(string.Join(", ", thread.Flags));
Console.WriteLine("Replies: " + thread.Submail.Count);

foreach (var reply in thread.Submail)
{
    Console.WriteLine("[" + reply.Id + "] " + reply.Subject);
    Console.WriteLine("  From  : " + reply.FromName + " <" + reply.FromEmail + ">");
    Console.WriteLine("  Date  : " + reply.Date);
    Console.WriteLine("  Flags : " + string.Join(", ", reply.Flags));

    foreach (var file in reply.FileAttachments)
        Console.WriteLine("  Attachment: " + file.FileName);
}
```

Thread subject normalization — all map to the same thread:

| Raw Subject | Normalized |
|---|---|
| `Meeting tomorrow` | `Meeting tomorrow` |
| `Re: Meeting tomorrow` | `Meeting tomorrow` |
| `Re: Re: Meeting tomorrow` | `Meeting tomorrow` |
| `Fwd: Meeting tomorrow` | `Meeting tomorrow` |
| `Fw: Re: Meeting tomorrow` | `Meeting tomorrow` |

---

## 🔍 Search
```csharp
// Fluent query — unread from a sender since a date
var query = ImapSearchQuery.Unseen()
    .From("boss@company.com")
    .Since(new DateTime(2025, 1, 1));

var uids = await mail.SearchMessagesAsync(query, "INBOX");

// Body text search
var uids = await mail.SearchMessagesAsync(
    ImapSearchQuery.All().Body("invoice"), "INBOX");

// Date range
var uids = await mail.SearchMessagesAsync(
    ImapSearchQuery.All().Since(DateTime.Today.AddDays(-7)), "INBOX");

// OR / NOT
var query = ImapSearchQuery.All()
    .Or(ImapSearchQuery.Flagged(), ImapSearchQuery.Unseen())
    .Not(ImapSearchQuery.Deleted());
```

### ImapSearchQuery Reference

| Method | IMAP Criteria |
|---|---|
| `All()` | `ALL` |
| `Unseen()` | `UNSEEN` |
| `Seen()` | `SEEN` |
| `Flagged()` | `FLAGGED` |
| `Unflagged()` | `UNFLAGGED` |
| `Answered()` | `ANSWERED` |
| `Unanswered()` | `UNANSWERED` |
| `Deleted()` | `DELETED` |
| `Draft()` | `DRAFT` |
| `.From(addr)` | `FROM "addr"` |
| `.To(addr)` | `TO "addr"` |
| `.Subject(text)` | `SUBJECT "text"` |
| `.Body(text)` | `BODY "text"` |
| `.Text(text)` | `TEXT "text"` |
| `.Header(name, val)` | `HEADER "name" "val"` |
| `.Since(date)` | `SINCE dd-MMM-yyyy` |
| `.Before(date)` | `BEFORE dd-MMM-yyyy` |
| `.On(date)` | `ON dd-MMM-yyyy` |
| `.Larger(bytes)` | `LARGER n` |
| `.Smaller(bytes)` | `SMALLER n` |
| `.Or(a, b)` | `OR (a) (b)` |
| `.Not(q)` | `NOT (q)` |

---

## 🏷 Flag Operations

```csharp
await mail.MarkAsSeenAsync("UID");           // mark read
await mail.MarkAsUnseenAsync("UID");         // mark unread
await mail.MarkAsImportantAsync("UID");      // star / flag
await mail.MarkAsUnimportantAsync("UID");    // remove star
await mail.MarkAsAnsweredAsync("UID");       // mark answered
await mail.MarkAsDraftAsync("UID");          // mark draft

// All accept optional folder parameter (default: "INBOX")
await mail.MarkAsSeenAsync("UID", "INBOX");
```

---

## 🗑 Delete
```csharp
await mail.DeleteMessageAsync("UID");
await mail.DeleteMessageAsync("UID", "INBOX");  // explicit folder
```

> Permanently removes the message. Move to Trash first for recoverable deletion.

---

## 🧹 Empty Folder
```csharp
// Deletes all messages in a folder in one shot
await mail.EmptyFolderAsync("INBOX");
await mail.EmptyFolderAsync("[Gmail]/Trash");
```

---

## 📂 Move and Copy
```csharp
// Move
await mail.MoveToFolderAsync("UID", sourceFolder: "INBOX", destinationFolder: "Archive");

// Copy
await mail.CopyToFolderAsync("UID", sourceFolder: "INBOX", destinationFolder: "Archive");

// Gmail examples
await mail.MoveToFolderAsync("UID", "INBOX", "[Gmail]/Trash");
await mail.MoveToFolderAsync("UID", "INBOX", "[Gmail]/All Mail");
```

---

## 🚫 Spam and Phishing

IMAP has no native spam command. The standard approach used by Gmail, Outlook, and Apple Mail is to move the message to the Junk folder with appropriate flags. The Junk folder name is **auto-detected** per provider — no hardcoding needed.

```csharp
// Report as spam — moves to Junk, marks Seen
await mail.ReportSpamAsync("UID", sourceFolder: "INBOX");

// Report as phishing — moves to Junk, marks Seen + Flagged
await mail.ReportPhishingAsync("UID", sourceFolder: "INBOX");

// Not spam — moves back to INBOX, removes Flagged
await mail.NotSpamAsync("UID");

// Override spam folder explicitly
await mail.ReportSpamAsync("UID", spamFolder: "[Gmail]/Spam");   // Gmail
await mail.ReportSpamAsync("UID", spamFolder: "Junk Email");     // Outlook
```

**Auto-detected Junk folder names (tried in order):**
`Junk` → `Junk Email` → `[Gmail]/Spam` → `Spam` → `Bulk Mail` → `Bulk`

If none exist, the folder is created automatically.

| Method | Flags Set | Folder |
|---|---|---|
| `ReportSpamAsync` | `\Seen` | Junk (auto-detected) |
| `ReportPhishingAsync` | `\Seen` + `\Flagged` | Junk (auto-detected) |
| `NotSpamAsync` | removes `\Flagged` | Back to INBOX |

---

## 📋 Folder Management

```csharp
// List all mailboxes
var folders = await mail.ListMailboxesAsync();
// INBOX, [Gmail]/Sent Mail, [Gmail]/Trash, [Gmail]/All Mail ...

// Create
await mail.CreateFolderAsync("MyFolder");

// Rename
await mail.RenameFolderAsync("MyFolder", "Archive");

// Delete
await mail.DeleteFolderAsync("Archive");

// Subscribe / Unsubscribe
await mail.SubscribeFolderAsync("MyFolder");
await mail.UnsubscribeFolderAsync("MyFolder");
```

---

## 💾 Append Message
Save a raw message to a folder (e.g. record sent mail):

```csharp
byte[] rawMessage = Encoding.UTF8.GetBytes("From: ...\r\nTo: ...\r\n\r\nBody");

await mail.AppendMessageAsync(
    folder:       "Sent",
    rawMessage:   rawMessage,
    flags:        new[] { "\\Seen" },
    internalDate: DateTime.UtcNow
);
```

---

## 📬 Real-Time New Mail — ListenAsync

`ListenAsync` uses **IMAP IDLE** (RFC 2177) for true server push. Automatically falls back to 30-second polling if the server does not support IDLE.

```csharp
var mail = new MailConfig();
await mail.ConnectAsync("you@gmail.com", "your-app-password");

mail.OnNewMailAsync += async (args) =>
{
    Console.WriteLine("📬 New mail in : " + args.Folder);
    Console.WriteLine("From           : " + args.Message.FromName);
    Console.WriteLine("Subject        : " + args.Message.Subject);
    Console.WriteLine("Has attachment : " + args.Message.HasAttachments);
    Console.WriteLine("Received at    : " + args.ReceivedAt);

    // Full body is already fetched — no extra call needed
    Console.WriteLine(args.Message.PlainTextBody);

    // Safe to call any mail method from inside the handler
    await mail.MarkAsSeenAsync(args.Message.Id);
    var inbox = await mail.GetInboxAsync();
};

// Blocks here — fires OnNewMailAsync each time a new message arrives
await mail.ListenAsync("INBOX");
```

### Stop Listening
```csharp
var cts = new CancellationTokenSource();

mail.OnNewMailAsync += async (args) => { ... };

// Pass the token — ListenAsync returns when cancelled
await mail.ListenAsync("INBOX", cts.Token);

// To stop from another thread:
cts.Cancel();
```

### NewMailEventArgs Properties

| Property | Type | Description |
|---|---|---|
| `Message` | `MailMessage` | Fully fetched — body, attachments, flags all ready |
| `Folder` | `string` | Folder being watched e.g. `"INBOX"` |
| `ReceivedAt` | `DateTime` | UTC time the event was raised |

---

## 🚪 Logout
```csharp
await mail.LogoutAsync();
```

---

## 🏳 ImapFlags Enum

| Flag | Description |
|---|---|
| `ALL` | All emails |
| `SEEN` | Read |
| `UNSEEN` | Unread |
| `ANSWERED` | Replied to |
| `UNANSWERED` | Not replied to |
| `FLAGGED` | Starred / flagged |
| `UNFLAGGED` | Not starred |
| `DELETED` | Marked for deletion |
| `UNDELETED` | Not marked for deletion |
| `DRAFT` | Drafts |
| `UNDRAFT` | Not drafts |

---

## 🏷 Message Flags Reference

Flags are returned as `List<string>` on both `MailMessage` and `MailInboxes`.

| Flag | Meaning |
|---|---|
| `Seen` | Email has been read |
| `Answered` | Email has been replied to |
| `Flagged` | Starred / important |
| `Deleted` | Marked for deletion |
| `Draft` | Draft email |
| `Recent` | Newly arrived (set by server) |

```csharp
bool isUnread  = !msg.Flags.Contains("Seen");
bool isStarred =  msg.Flags.Contains("Flagged");
```

---

## 💻 Full Sample App

```csharp
using BNet.IMAP.Mailer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

var mail = new MailConfig();
bool ok = await mail.ConnectAsync("you@gmail.com", "your-app-password");
if (!ok) { Console.WriteLine("Connection failed."); return; }
Console.WriteLine("Connected.\n");

// ── Mailboxes ──────────────────────────────────────────────────────────────────
var folders = await mail.ListMailboxesAsync();
Console.WriteLine("=== Mailboxes ===");
foreach (var f in folders) Console.WriteLine("  " + f);

// ── Counts ─────────────────────────────────────────────────────────────────────
var counts = await mail.GetEmailCountsByFlagsAsync("INBOX");
Console.WriteLine("\n=== Counts ===");
Console.WriteLine("  All:     " + counts[ImapFlags.ALL]);
Console.WriteLine("  Unread:  " + counts[ImapFlags.UNSEEN]);
Console.WriteLine("  Read:    " + counts[ImapFlags.SEEN]);
Console.WriteLine("  Flagged: " + counts[ImapFlags.FLAGGED]);

// ── Inbox ──────────────────────────────────────────────────────────────────────
var inbox = await mail.GetInboxAsync("INBOX", ImapFlags.UNSEEN, pageSize: 10, pageIndex: 0);
Console.WriteLine("\n=== Inbox ===");
Console.WriteLine("  Total : " + (inbox.Count > 0 ? inbox[0].TotalEmail.ToString() : "0"));
Console.WriteLine("  Pages : " + (inbox.Count > 0 ? inbox[0].TotalPagination.ToString() : "0"));

foreach (var msg in inbox)
{
    Console.WriteLine("\n  [" + msg.Id + "] " + msg.Subject);
    Console.WriteLine("    From  : " + msg.FromName + " <" + msg.FromEmail + ">");
    Console.WriteLine("    Date  : " + msg.Date);
    Console.WriteLine("    Flags : " + string.Join(", ", msg.Flags));
}

// ── Full message ───────────────────────────────────────────────────────────────
if (inbox.Count > 0)
{
    string uid  = inbox[0].Id;
    var    full = await mail.GetFullMessageAsync(uid, "INBOX");

    Console.WriteLine("\n=== Full Message [" + uid + "] ===");
    Console.WriteLine("  Subject : " + full.Subject);
    Console.WriteLine("  From    : " + full.FromName + " <" + full.FromEmail + ">");
    Console.WriteLine("  Date    : " + full.Date);
    Console.WriteLine("  Flags   : " + string.Join(", ", full.Flags));

    if (full.HasAttachments)
        foreach (var file in full.FileAttachments)
        {
            Console.WriteLine("  File    : " + file.FileName + " (" + (file.SizeBytes / 1024.0).ToString("0.0") + " KB)");
            File.WriteAllBytes(file.FileName, file.Data);
        }

    // Flag operations
    await mail.MarkAsSeenAsync(uid);
    await mail.MarkAsUnseenAsync(uid);
    await mail.MarkAsImportantAsync(uid);

    // Thread
    var thread = await mail.GetThreadAsync(uid, "INBOX");
    Console.WriteLine("\n=== Thread — " + thread.Subject + " (" + thread.Submail.Count + " replies) ===");
    foreach (var reply in thread.Submail)
        Console.WriteLine("  [" + reply.Id + "] " + reply.FromName + " — " + reply.Date);

    // Spam / Phishing
    // await mail.ReportSpamAsync(uid);
    // await mail.ReportPhishingAsync(uid);
    // await mail.NotSpamAsync(uid);

    // Move / Delete
    // await mail.MoveToFolderAsync(uid, "INBOX", "[Gmail]/All Mail");
    // await mail.DeleteMessageAsync(uid);
}

// ── Real-time new mail ─────────────────────────────────────────────────────────
Console.WriteLine("\nListening for new mail… (Ctrl+C to stop)");
mail.OnNewMailAsync += async (args) =>
{
    Console.WriteLine("📬 New mail from : " + args.Message.FromName);
    Console.WriteLine("   Subject       : " + args.Message.Subject);
    await mail.MarkAsSeenAsync(args.Message.Id);
};
await mail.ListenAsync("INBOX");

await mail.LogoutAsync();
Console.WriteLine("Logged out.");
```

---

## 🐛 Changelog

| # | Area | Fix / Enhancement |
|---|---|---|
| 1 | **Date Parsing** | Fixed `ParseDate` to handle timezone offsets, parenthetical comments like `(PST)`, and malformed strings. Uses `DateTimeOffset.TryParse` with `AssumeUniversal` before falling back to `DateTime.TryParse`. |
| 2 | **Mark as Unread** | Added `MarkAsUnseenAsync` — uses `UID STORE -FLAGS (\Seen)`. |
| 3 | **Action Methods** | All action methods now correctly issue `SELECT` before any `UID STORE` or `UID MOVE`. Folder names with spaces are automatically quoted. |
| 4 | **Flags** | `Flags` (`List<string>`) added to both `MailMessage` and `MailInboxes`, fetched via `UID FETCH (UID FLAGS …)`. |
| 5 | **IDLE / ListenAsync** | `ListenAsync` replaces two-step setup. Uses IDLE (RFC 2177) for server push with automatic 30s polling fallback. Fires `OnNewMailAsync` with fully fetched `MailMessage`. Fixed 60s timeout bug that silently suppressed events. |
| 6 | **Spam / Phishing** | Added `ReportSpamAsync`, `ReportPhishingAsync`, `NotSpamAsync`. Junk folder auto-detected per provider. |
| 7 | **Folder Management** | Added `CreateFolderAsync`, `DeleteFolderAsync`, `RenameFolderAsync`, `SubscribeFolderAsync`, `UnsubscribeFolderAsync`, `EmptyFolderAsync`. |
| 8 | **Copy / Append** | Added `CopyToFolderAsync`, `AppendMessageAsync`. |
| 9 | **Fluent Search** | Added `ImapSearchQuery` builder with `From`, `To`, `Subject`, `Body`, `Since`, `Before`, `Or`, `Not` and more. |
| 10 | **Literal Read** | Literal bodies now read from `StreamReader.ReadAsync` instead of `SslStream.ReadAsync` — fixes `TimeoutException` on large messages. |
| 11 | **BOM Fix** | `StreamWriter` uses `UTF8Encoding(false)` (no BOM). The default `Encoding.UTF8` emitted a 3-byte BOM that servers rejected as an invalid tag. |
| 12 | **MIME Decoding** | Fixed multi-word encoded headers `=?UTF-8?B?…?= =?UTF-8?B?…?=` — previously only the first word was decoded. Fixed RFC 5987 `filename*=UTF-8''…` extended filenames. |
| 13 | **.NET 4.5 Support** | Replaced all C# 6–8 syntax (`?.`, `switch` expressions, `using var`, `Array.Empty`, index-from-end `[^1]`) with compatible equivalents. `LangVersion` locked to 7.3. |

---

## 🔐 Notes

- Enable IMAP in your provider settings before connecting.
- For **Gmail** with 2FA, use an **App Password** — [myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords).
- Default port is **993** (implicit SSL/TLS).
- `MailConfig` is **thread-safe** — a single instance serializes all operations via `SemaphoreSlim`. You can safely call any method from inside `OnNewMailAsync`.
- `ListenAsync` uses IDLE (RFC 2177) for instant push. Falls back to 30-second polling automatically if the server does not advertise the IDLE capability.
- `UID MOVE` uses RFC 6851 when available; falls back to `COPY + DELETE + EXPUNGE` automatically.
- Spam folder is **auto-detected** per provider — no hardcoded folder names required.

---

## 📚 Repository

https://github.com/bryanjaybodino/BNet.Library/tree/master/BNet.IMAP.Mailer

---

## 📄 License

MIT License