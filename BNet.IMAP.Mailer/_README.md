# BNet.IMAP.Mailer

Lightweight, dependency-free IMAP client for .NET to fetch, read, move, and manage emails over secure SSL (supports Gmail, Outlook, and other IMAP servers).

---

## ✨ Features

- 🔐 Secure SSL/TLS IMAP connection (username/password **and** OAuth2/XOAUTH2)
- 📥 Get unread, seen, deleted, or all messages with pagination
- 📊 Get email counts by flags (All, Seen, Unseen, Answered, Flagged, Deleted, Draft, etc.)
- 📄 Fetch full email (HTML + Plain Text)
- 📎 Extract file attachments and inline embedded images
- 🧵 Fetch email threads (conversation grouping by subject)
- 👤 Sender profile: `FromName`, `FromEmail`, `FromImage` (colored initials avatar)
- 🖼 Inline images auto-replaced with data URIs (renders without extra requests)
- 🏷 Per-message IMAP flags (`Seen`, `Answered`, `Flagged`, `Deleted`, `Draft`, etc.)
- ✅ Mark message as read / unread
- 🗑 Delete and expunge messages
- 📂 Move messages to another folder
- 📋 List all mailboxes
- ⚡ Async/await support with thread-safe `SemaphoreSlim`
- 📦 No third-party IMAP dependencies

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
Represents a single file attachment or inline embedded image extracted from an email.

```csharp
public class MailAttachment
{
    public string FileName    { get; set; }  // "invoice.pdf"
    public string ContentType { get; set; }  // "application/pdf"
    public string ContentId   { get; set; }  // CID for inline images e.g. "image001@mail" (null if not inline)
    public bool   IsInline    { get; set; }  // true = embedded image in HTML body
    public long   SizeBytes   { get; set; }  // decoded file size in bytes
    public byte[] Data        { get; set; }  // raw decoded bytes — use to save or stream
    public string Base64Data  { get; }       // base64 string of Data
    public string DataUri     { get; }       // "data:image/png;base64,..." ready for <img src> or <a href>
}
```

---

### MailMessage
Represents a full email with body content, attachments, and flags.

```csharp
public class MailMessage
{
    public string       Id            { get; set; }  // IMAP UID e.g. "124"
    public string       From          { get; set; }  // raw header: "John Doe <john@example.com>"
    public string       FromName      { get; set; }  // "John Doe"
    public string       FromEmail     { get; set; }  // "john@example.com"
    public string       FromImage     { get; set; }  // ready-to-render <span> colored initials avatar
    public List<string> To            { get; set; }
    public List<string> CC            { get; set; }
    public List<string> BCC           { get; set; }
    public string       Subject       { get; set; }
    public DateTime     Date          { get; set; }
    public string       HtmlBody      { get; set; }  // full HTML (inline images auto-replaced with data URIs)
    public string       PlainTextBody { get; set; }  // plain text fallback
    public List<string> Flags         { get; set; }  // ✅ NEW — e.g. ["Seen", "Answered"]

    public List<MailAttachment> Attachments       { get; set; }  // all attachments (inline + file)
    public List<MailAttachment> FileAttachments   { get; }       // only downloadable file attachments
    public List<MailAttachment> InlineAttachments { get; }       // only embedded images
    public bool                 HasAttachments    { get; }       // true if FileAttachments.Count > 0
}
```

---

### MailInboxes
Lightweight list-view entry — headers only, no body content.

```csharp
public class MailInboxes
{
    public int          TotalEmail      { get; set; }  // total matching emails in folder
    public int          TotalPagination { get; set; }  // total number of pages
    public string       Id              { get; set; }  // IMAP UID
    public string       From            { get; set; }  // raw header value
    public string       FromName        { get; set; }  // "John Doe"
    public string       FromEmail       { get; set; }  // "john@example.com"
    public string       FromImage       { get; set; }  // <span> colored initials avatar
    public List<string> To              { get; set; }
    public List<string> CC              { get; set; }
    public List<string> BCC             { get; set; }
    public List<string> Flags           { get; set; }  // ✅ NEW — e.g. ["Seen", "Flagged"]
    public string       Subject         { get; set; }
    public string       Folder          { get; set; }  // "INBOX", "Sent", etc.
    public DateTime     Date            { get; set; }
    public List<MailMessage> Submail    { get; set; }  // thread replies — populated by GetThreadAsync
}
```

---

## 🚀 Quick Start

### Connect (Username / Password)
```csharp
var mail = new MailConfig();
bool ok = await mail.ConnectAsync("you@gmail.com", "your-app-password");
```

### Connect (OAuth2 / XOAUTH2)
```csharp
var mail = new MailConfig();
bool ok = await mail.XOAuth2Async("you@outlook.com", accessToken, "outlook.office365.com");
```

---

### 📥 Get Unread Messages
```csharp
var inbox = await mail.GetInboxAsync("INBOX", MailConfig.ImapFlags.UNSEEN);
foreach (var message in inbox)
{
    Console.WriteLine($"Id:         {message.Id}");
    Console.WriteLine($"Total:      {message.TotalEmail}");
    Console.WriteLine($"Pages:      {message.TotalPagination}");
    Console.WriteLine($"FromName:   {message.FromName}");
    Console.WriteLine($"FromEmail:  {message.FromEmail}");
    Console.WriteLine($"Subject:    {message.Subject}");
    Console.WriteLine($"Date:       {message.Date}");
    Console.WriteLine($"Folder:     {message.Folder}");
    Console.WriteLine($"Flags:      {string.Join(", ", message.Flags)}");  // ✅ NEW
}
```

---

### 📥 Get Inbox with Pagination
```csharp
// Page 1 — 20 per page
var page1 = await mail.GetInboxAsync("INBOX", MailConfig.ImapFlags.ALL, pageSize: 20, pageIndex: 0);

// Page 2
var page2 = await mail.GetInboxAsync("INBOX", MailConfig.ImapFlags.ALL, pageSize: 20, pageIndex: 1);

Console.WriteLine($"Total emails: {page1[0].TotalEmail}");
Console.WriteLine($"Total pages:  {page1[0].TotalPagination}");
```

---

### 📥 Filter by Sender
```csharp
var filtered = await mail.GetInboxAsync(
    folder: "INBOX",
    imapFlags: MailConfig.ImapFlags.ALL,
    emailFilter: "boss@company.com"
);
```

---

### 📊 Get Email Counts by Flags
```csharp
var counts = await mail.GetEmailCountsByFlagsAsync("INBOX");

Console.WriteLine($"All:        {counts[MailConfig.ImapFlags.ALL]}");
Console.WriteLine($"Unread:     {counts[MailConfig.ImapFlags.UNSEEN]}");
Console.WriteLine($"Read:       {counts[MailConfig.ImapFlags.SEEN]}");
Console.WriteLine($"Answered:   {counts[MailConfig.ImapFlags.ANSWERED]}");
Console.WriteLine($"Unanswered: {counts[MailConfig.ImapFlags.UNANSWERED]}");
Console.WriteLine($"Flagged:    {counts[MailConfig.ImapFlags.FLAGGED]}");
Console.WriteLine($"Unflagged:  {counts[MailConfig.ImapFlags.UNFLAGGED]}");
Console.WriteLine($"Deleted:    {counts[MailConfig.ImapFlags.DELETED]}");
Console.WriteLine($"Undeleted:  {counts[MailConfig.ImapFlags.UNDELETED]}");
Console.WriteLine($"Draft:      {counts[MailConfig.ImapFlags.DRAFT]}");
Console.WriteLine($"Undraft:    {counts[MailConfig.ImapFlags.UNDRAFT]}");
```

Returns a `Dictionary<ImapFlags, int>` keyed by flag.

---

### 📄 Get Full Message
```csharp
var msg = await mail.GetFullMessageAsync("MESSAGE_UID", "INBOX");

Console.WriteLine($"Id:        {msg.Id}");
Console.WriteLine($"FromName:  {msg.FromName}");
Console.WriteLine($"FromEmail: {msg.FromEmail}");
Console.WriteLine($"To:        {string.Join(", ", msg.To)}");
Console.WriteLine($"CC:        {string.Join(", ", msg.CC)}");
Console.WriteLine($"Subject:   {msg.Subject}");
Console.WriteLine($"Date:      {msg.Date:g}");
Console.WriteLine($"Flags:     {string.Join(", ", msg.Flags)}");  // ✅ NEW
Console.WriteLine($"PlainText: {msg.PlainTextBody}");
Console.WriteLine($"HtmlBody:  {msg.HtmlBody}");
```

---

### 📎 Working with Attachments
```csharp
var msg = await mail.GetFullMessageAsync("MESSAGE_UID", "INBOX");

if (msg.HasAttachments)
{
    foreach (var file in msg.FileAttachments)
    {
        Console.WriteLine($"FileName:    {file.FileName}");
        Console.WriteLine($"ContentType: {file.ContentType}");
        Console.WriteLine($"Size:        {file.SizeBytes / 1024.0:0.0} KB");

        // Save to disk
        File.WriteAllBytes(file.FileName, file.Data);

        // Or use DataUri directly in HTML: file.DataUri
        // e.g. <a href="@file.DataUri" download="@file.FileName">Download</a>
    }
}
```

> **Inline images** in `HtmlBody` with `cid:` references are automatically replaced with `data:` URIs — just render `HtmlBody` and images appear with no extra requests.

---

### 🧵 Get Email Thread
```csharp
var thread = await mail.GetThreadAsync("MESSAGE_UID", "INBOX");

// Primary email (header info — use GetFullMessageAsync for body + attachments)
Console.WriteLine($"Subject: {thread.Subject}");
Console.WriteLine($"From:    {thread.FromName} <{thread.FromEmail}>");
Console.WriteLine($"Date:    {thread.Date:g}");
Console.WriteLine($"Flags:   {string.Join(", ", thread.Flags)}");  // ✅ NEW
Console.WriteLine($"Replies: {thread.Submail?.Count ?? 0}");

// Thread replies — already full MailMessage with body + attachments
foreach (var reply in thread.Submail ?? new())
{
    Console.WriteLine($"  [{reply.Id}] {reply.Subject}");
    Console.WriteLine($"         From  : {reply.FromName} <{reply.FromEmail}>");
    Console.WriteLine($"         Date  : {reply.Date:g}");
    Console.WriteLine($"         Flags : {string.Join(", ", reply.Flags)}");  // ✅ NEW

    if (reply.HasAttachments)
        foreach (var file in reply.FileAttachments)
            Console.WriteLine($"   Attachment: {file.FileName} ({file.SizeBytes / 1024.0:0.0} KB)");
}
```

> **Note:** `GetThreadAsync` returns the primary email as `MailInboxes` (headers only). Call `GetFullMessageAsync` separately for its body and attachments.

Thread subject normalization — all of the below map to the same thread:

| Raw Subject | Normalized |
|---|---|
| `Meeting tomorrow` | `Meeting tomorrow` |
| `Re: Meeting tomorrow` | `Meeting tomorrow` |
| `Re: Re: Meeting tomorrow` | `Meeting tomorrow` |
| `Fwd: Meeting tomorrow` | `Meeting tomorrow` |
| `Fw: Re: Meeting tomorrow` | `Meeting tomorrow` |

---

### ✅ Mark Message as Read
```csharp
await mail.MarkAsSeenAsync("MESSAGE_UID");

// With explicit folder
await mail.MarkAsSeenAsync("MESSAGE_UID", "INBOX");
```

### ✅ Mark Message as Unread ✅ NEW
```csharp
await mail.MarkAsUnseenAsync("MESSAGE_UID");

// With explicit folder
await mail.MarkAsUnseenAsync("MESSAGE_UID", "INBOX");
```

---

### 🗑 Delete Message
```csharp
await mail.DeleteMessageAsync("MESSAGE_UID");

// With explicit folder
await mail.DeleteMessageAsync("MESSAGE_UID", "INBOX");
```

> ⚠️ This permanently removes the email. Move to Trash first if you want recoverable deletion.

---

### 📂 Move Message to Folder
```csharp
await mail.MoveToFolderAsync("MESSAGE_UID", sourceFolder: "INBOX", destinationFolder: "Archive");

// Gmail examples
await mail.MoveToFolderAsync("MESSAGE_UID", "INBOX", "[Gmail]/Trash");
await mail.MoveToFolderAsync("MESSAGE_UID", "INBOX", "[Gmail]/All Mail");
```

> ⚠️ `UID MOVE` requires the MOVE extension (RFC 6851). Supported by Gmail, Outlook, and most modern IMAP servers.

---

### 📋 List All Mailboxes
```csharp
var folders = await mail.ListMailboxesAsync();
foreach (var folder in folders)
    Console.WriteLine(folder);
// INBOX
// [Gmail]/Sent Mail
// [Gmail]/Trash
// [Gmail]/All Mail
// [Gmail]/Drafts
// [Gmail]/Starred
```

---

### 🚪 Logout
```csharp
await mail.Logout();
```

---

## 🏳 ImapFlags Enum

| Flag | Description |
|---|---|
| `ALL` | All emails regardless of status |
| `SEEN` | Read emails |
| `UNSEEN` | Unread emails |
| `ANSWERED` | Emails that have been replied to |
| `UNANSWERED` | Emails not yet replied to |
| `FLAGGED` | Starred / flagged emails |
| `UNFLAGGED` | Not starred |
| `DELETED` | Marked for deletion |
| `UNDELETED` | Not marked for deletion |
| `DRAFT` | Draft emails |
| `UNDRAFT` | Not drafts |

---

## 🏷 Message Flags Reference

Flags are returned as a `List<string>` on both `MailMessage` and `MailInboxes`. Common values:

| Flag | Meaning |
|---|---|
| `Seen` | Email has been read |
| `Answered` | Email has been replied to |
| `Flagged` | Email is starred / flagged |
| `Deleted` | Email is marked for deletion |
| `Draft` | Email is a draft |
| `Recent` | Email is newly arrived (set by server) |

**Check if an email is unread:**
```csharp
bool isUnread = !message.Flags.Contains(@"Seen");
```

**Check if an email is flagged/starred:**
```csharp
bool isStarred = message.Flags.Contains(@"\Flagged");
```

---

## 💻 Sample Console App

A complete working example covering every feature.

```csharp
using BNet.IMAP.Mailer;

using var mail = new MailConfig();
bool ok = await mail.ConnectAsync("you@gmail.com", "your-app-password");
if (!ok) { Console.WriteLine("Connection failed."); return; }
Console.WriteLine("Connected.\n");

// ── List mailboxes ────────────────────────────────────────────────────────────
Console.WriteLine("=== Mailboxes ===");
var folders = await mail.ListMailboxesAsync();
foreach (var f in folders)
    Console.WriteLine($"  {f}");

// ── Counts by flag ────────────────────────────────────────────────────────────
Console.WriteLine("\n=== Counts ===");
var counts = await mail.GetEmailCountsByFlagsAsync("INBOX");
Console.WriteLine($"  All:     {counts[MailConfig.ImapFlags.ALL]}");
Console.WriteLine($"  Unread:  {counts[MailConfig.ImapFlags.UNSEEN]}");
Console.WriteLine($"  Read:    {counts[MailConfig.ImapFlags.SEEN]}");
Console.WriteLine($"  Flagged: {counts[MailConfig.ImapFlags.FLAGGED]}");
Console.WriteLine($"  Deleted: {counts[MailConfig.ImapFlags.DELETED]}");
Console.WriteLine($"  Draft:   {counts[MailConfig.ImapFlags.DRAFT]}");

// ── Inbox (page 1, 10 per page, unread only) ──────────────────────────────────
Console.WriteLine("\n=== Inbox (Unread, Page 1) ===");
var inbox = await mail.GetInboxAsync("INBOX", MailConfig.ImapFlags.UNSEEN, pageSize: 10, pageIndex: 0);
Console.WriteLine($"  Total emails : {(inbox.Count > 0 ? inbox[0].TotalEmail : 0)}");
Console.WriteLine($"  Total pages  : {(inbox.Count > 0 ? inbox[0].TotalPagination : 0)}\n");

foreach (var msg in inbox)
{
    Console.WriteLine($"  [{msg.Id}] {msg.Subject}");
    Console.WriteLine($"         From  : {msg.FromName} <{msg.FromEmail}>");
    Console.WriteLine($"         Date  : {msg.Date:g}");
    Console.WriteLine($"       Folder  : {msg.Folder}");
    Console.WriteLine($"        Flags  : {string.Join(", ", msg.Flags)}");
    Console.WriteLine();
}

// ── Full message + attachments ────────────────────────────────────────────────
if (inbox.Count > 0)
{
    string uid = inbox[0].Id;

    Console.WriteLine($"=== Full Message (UID {uid}) ===");
    var full = await mail.GetFullMessageAsync(uid, "INBOX");

    Console.WriteLine($"  Subject    : {full.Subject}");
    Console.WriteLine($"  From       : {full.FromName} <{full.FromEmail}>");
    Console.WriteLine($"  To         : {string.Join(", ", full.To ?? new List<string>())}");
    Console.WriteLine($"  CC         : {string.Join(", ", full.CC ?? new List<string>())}");
    Console.WriteLine($"  Date       : {full.Date:g}");
    Console.WriteLine($"  Flags      : {string.Join(", ", full.Flags)}");
    Console.WriteLine($"  PlainText  : {full.PlainTextBody?[..Math.Min(200, full.PlainTextBody?.Length ?? 0)]}...");
    Console.WriteLine($"  Attachments: {full.FileAttachments?.Count ?? 0}");

    if (full.HasAttachments)
    {
        Console.WriteLine("\n  --- Attachments ---");
        foreach (var file in full.FileAttachments)
        {
            Console.WriteLine($"    {file.FileName}  ({file.SizeBytes / 1024.0:0.0} KB)  [{file.ContentType}]");
            File.WriteAllBytes(file.FileName, file.Data);
            Console.WriteLine($"    Saved → {file.FileName}");
        }
    }

    // ── Mark as read / unread ─────────────────────────────────────────────
    await mail.MarkAsSeenAsync(uid);
    Console.WriteLine($"\n  Marked UID {uid} as read.");

    await mail.MarkAsUnseenAsync(uid);
    Console.WriteLine($"  Marked UID {uid} as unread.");

    // ── Thread ────────────────────────────────────────────────────────────
    Console.WriteLine($"\n=== Thread for UID {uid} ===");
    var thread = await mail.GetThreadAsync(uid, "INBOX");
    Console.WriteLine($"  Subject : {thread.Subject}");
    Console.WriteLine($"  Flags   : {string.Join(", ", thread.Flags)}");
    Console.WriteLine($"  Replies : {thread.Submail?.Count ?? 0}");

    foreach (var reply in thread.Submail ?? new List<MailMessage>())
    {
        Console.WriteLine($"\n    [{reply.Id}] {reply.Subject}");
        Console.WriteLine($"           From  : {reply.FromName} <{reply.FromEmail}>");
        Console.WriteLine($"           Date  : {reply.Date:g}");
        Console.WriteLine($"           Flags : {string.Join(", ", reply.Flags)}");

        if (reply.HasAttachments)
            foreach (var f in reply.FileAttachments)
                Console.WriteLine($"     Attachment : {f.FileName} ({f.SizeBytes / 1024.0:0.0} KB)");
    }

    // ── Move (uncomment to use) ───────────────────────────────────────────
    // await mail.MoveToFolderAsync(uid, "INBOX", "[Gmail]/All Mail");
    // Console.WriteLine($"Moved UID {uid} to All Mail.");

    // ── Delete (uncomment to use) ─────────────────────────────────────────
    // await mail.DeleteMessageAsync(uid);
    // Console.WriteLine($"Deleted UID {uid}.");
}

await mail.Logout();
Console.WriteLine("\nLogged out.");
```

---

### OAuth2 / XOAUTH2 (Outlook / Microsoft 365)

```csharp
using var mail = new MailConfig();
bool ok = await mail.XOAuth2Async("you@outlook.com", accessToken, "outlook.office365.com");
if (!ok) { Console.WriteLine("OAuth2 connection failed."); return; }

var inbox = await mail.GetInboxAsync("INBOX", MailConfig.ImapFlags.UNSEEN);
// ... same API as above

await mail.Logout();
```

---

## 🐛 Bug Fixes & Changelog

### Latest Updates

| # | Area | Fix / Enhancement |
|---|---|---|
| 1 | **Date Parsing** | Fixed `ParseDate` to correctly handle timezone offsets, parenthetical comments like `(PST)`, and malformed date strings. Now uses `DateTimeOffset.TryParse` with `AssumeUniversal` before falling back to `DateTime.TryParse`. |
| 2 | **Mark as Unread** | Added `MarkAsUnseenAsync(string id, string folder)` — uses `UID STORE -FLAGS (\Seen)` to remove the `\Seen` flag. |
| 3 | **Action Methods Fixed** | All action methods (`MarkAsSeenAsync`, `MarkAsUnseenAsync`, `DeleteMessageAsync`, `MoveToFolderAsync`) now correctly issue a `SELECT` command before any `UID STORE` or `UID MOVE`, preventing the `BAD UID STORE not allowed now` error. Folder names containing spaces are automatically quoted. |
| 4 | **Flags Support** | `Flags` property (`List<string>`) added to both `MailMessage` and `MailInboxes`. Flags are fetched via `UID FETCH (UID FLAGS ...)` and parsed by `ExtractFlags(response, uid)`. `FetchFullMessageInternalAsync` issues a separate `UID FETCH (UID FLAGS)` call before fetching the body to avoid corrupting the literal byte reader. |

### Previously Fixed

| Issue | Fix |
|---|---|
| `TimeoutException: Timeout reading literal body at 0/N` | Literal bodies now read from `StreamReader.ReadAsync` instead of `SslStream.ReadAsync`. The `StreamReader` buffers ahead; reading from the raw stream would find 0 bytes and stall. |
| `BAD invalid tag` on first command | `StreamWriter` now uses `new UTF8Encoding(false)` (no BOM). The default `Encoding.UTF8` emits a 3-byte BOM (`0xEF 0xBB 0xBF`) before the first command, which the IMAP server cannot parse as a tag. |
| MIME encoded display names not decoded (e.g. `=?UTF-8?B?...?=`) | `ParseFrom` now calls `DecodeMimeEncodedWords` before parsing, and `DecodeMimeEncodedWords` handles multiple encoded-word tokens in a single header. |
| `ExtractHeaderFields` returns empty when `FLAGS` precedes `BODY[HEADER.FIELDS]` | Fixed marker search — now finds `BODY[HEADER.FIELDS` starting from the `UID {uid}` position instead of matching both as a single fixed string. |

---

## 🔐 Notes

- Ensure IMAP is enabled in your email provider settings.
- For Gmail, use an **App Password** if 2FA is enabled. Generate one at [myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords).
- Default connection uses SSL on port **993**.
- `MailConfig` is **thread-safe** — a single instance serializes all async calls via `SemaphoreSlim`.
- The `StreamWriter` uses **UTF-8 without BOM** internally. This is required for IMAP protocol compliance — servers reject commands that begin with a byte-order mark.
- Literal message bodies are read via `StreamReader` (not the raw `SslStream`) to avoid timeouts caused by internal read-ahead buffering.
- `UID MOVE` requires the MOVE extension (RFC 6851). Supported by Gmail and Outlook. For servers that don't support it, fall back to `UID COPY` + `UID STORE +FLAGS (\Deleted)` + `EXPUNGE`.

---

## 📚 Repository

https://github.com/bryanjaybodino/BNet.Library/tree/master/BNet.IMAP.Mailer

---

## 📄 License

MIT License