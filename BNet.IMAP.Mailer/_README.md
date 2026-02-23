# BNet.IMAP.Mailer

Lightweight, dependency-free IMAP client for .NET to fetch, read, move, and manage emails over secure SSL (supports Gmail and other IMAP servers).

---

## ✨ Features

- 🔐 Secure SSL/TLS IMAP connection
- 📥 Get unread, seen, deleted, or all messages with pagination
- 📊 Get email counts by flags (All, Seen, Unseen, Answered, Flagged, Deleted, Draft, etc.)
- 📄 Fetch full email (HTML + Plain Text)
- 📎 Extract file attachments and inline embedded images
- 🧵 Fetch email threads (conversation grouping by subject)
- 👤 Sender profile: `FromName`, `FromEmail`, `FromImage` (colored initials avatar)
- 🖼 Inline images auto-replaced with data URIs (renders without extra requests)
- ✅ Mark message as read
- 🗑 Delete and expunge messages
- 📂 Move messages to another folder
- 📋 List all mailboxes
- ⚡ Async/await support with thread-safe SemaphoreSlim
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
Represents a full email with body content and attachments.

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
    public string       Subject         { get; set; }
    public string       Folder          { get; set; }  // "INBOX", "Sent", etc.
    public DateTime     Date            { get; set; }
    public List<MailMessage> Submail    { get; set; }  // thread replies — populated by GetThreadAsync
}
```

---

## 🚀 Quick Start

### Initialize Mail Client
```csharp
var mail = new MailConfig();
await mail.ConnectAsync(userEmail, userPassword, hostname);
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
    Console.WriteLine($"FromImage:  {message.FromImage}");
    Console.WriteLine($"Subject:    {message.Subject}");
    Console.WriteLine($"Date:       {message.Date}");
    Console.WriteLine($"Folder:     {message.Folder}");
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
var count = await mail.GetEmailCountsByFlagsAsync("INBOX");

int TotalAll        = count[MailConfig.ImapFlags.ALL];
int TotalSeen       = count[MailConfig.ImapFlags.SEEN];
int TotalUnseen     = count[MailConfig.ImapFlags.UNSEEN];
int TotalAnswered   = count[MailConfig.ImapFlags.ANSWERED];
int TotalUnanswered = count[MailConfig.ImapFlags.UNANSWERED];
int TotalFlagged    = count[MailConfig.ImapFlags.FLAGGED];
int TotalUnflagged  = count[MailConfig.ImapFlags.UNFLAGGED];
int TotalDeleted    = count[MailConfig.ImapFlags.DELETED];
int TotalUndeleted  = count[MailConfig.ImapFlags.UNDELETED];
int TotalDraft      = count[MailConfig.ImapFlags.DRAFT];
int TotalUndraft    = count[MailConfig.ImapFlags.UNDRAFT];

Console.WriteLine($"TotalAll:        {TotalAll}");
Console.WriteLine($"TotalSeen:       {TotalSeen}");
Console.WriteLine($"TotalUnseen:     {TotalUnseen}");
Console.WriteLine($"TotalAnswered:   {TotalAnswered}");
Console.WriteLine($"TotalUnanswered: {TotalUnanswered}");
Console.WriteLine($"TotalFlagged:    {TotalFlagged}");
Console.WriteLine($"TotalUnflagged:  {TotalUnflagged}");
Console.WriteLine($"TotalDeleted:    {TotalDeleted}");
Console.WriteLine($"TotalUndeleted:  {TotalUndeleted}");
Console.WriteLine($"TotalDraft:      {TotalDraft}");
Console.WriteLine($"TotalUndraft:    {TotalUndraft}");
```

Returns a dictionary keyed by `MailConfig.ImapFlags`, giving you the count of emails matching each flag in the specified mailbox.

---

### 📄 Get Full Message
```csharp
var fullMessage = await mail.GetFullMessageAsync("MESSAGE_UID");

Console.WriteLine($"Id:           {fullMessage.Id}");
Console.WriteLine($"FromName:     {fullMessage.FromName}");
Console.WriteLine($"FromEmail:    {fullMessage.FromEmail}");
Console.WriteLine($"FromImage:    {fullMessage.FromImage}");
Console.WriteLine($"To:           {string.Join(", ", fullMessage.To)}");
Console.WriteLine($"CC:           {string.Join(", ", fullMessage.CC)}");
Console.WriteLine($"BCC:          {string.Join(", ", fullMessage.BCC)}");
Console.WriteLine($"Subject:      {fullMessage.Subject}");
Console.WriteLine($"Date:         {fullMessage.Date}");
Console.WriteLine($"HtmlBody:     {fullMessage.HtmlBody}");
Console.WriteLine($"PlainText:    {fullMessage.PlainTextBody}");
Console.WriteLine($"Attachments:  {fullMessage.HasAttachments}");
```

---

### 📎 Working with Attachments
```csharp
var fullMessage = await mail.GetFullMessageAsync("MESSAGE_UID");

if (fullMessage.HasAttachments)
{
    foreach (var file in fullMessage.FileAttachments)
    {
        Console.WriteLine($"FileName:    {file.FileName}");
        Console.WriteLine($"ContentType: {file.ContentType}");
        Console.WriteLine($"SizeBytes:   {file.SizeBytes}");
        Console.WriteLine($"IsInline:    {file.IsInline}");

        // Save to disk
        File.WriteAllBytes(file.FileName, file.Data);

        // Or use DataUri directly in HTML
        // <a href="@file.DataUri" download="@file.FileName">Download</a>
    }
}
```

> **Inline images** embedded in `HtmlBody` with `cid:` references are automatically replaced with `data:` URIs — no extra handling needed. Just render `HtmlBody` and images will appear.

---

### 🧵 Get Email Thread
```csharp
var thread = await mail.GetThreadAsync("MESSAGE_UID", "INBOX");

// Primary email (header info)
Console.WriteLine($"Id:        {thread.Id}");
Console.WriteLine($"FromName:  {thread.FromName}");
Console.WriteLine($"FromEmail: {thread.FromEmail}");
Console.WriteLine($"Subject:   {thread.Subject}");
Console.WriteLine($"Date:      {thread.Date}");

// Related emails in thread — full MailMessage with body + attachments
Console.WriteLine($"Thread count: {thread.Submail.Count}");

foreach (var reply in thread.Submail)
{
    Console.WriteLine($"--- Reply ---");
    Console.WriteLine($"FromName:  {reply.FromName}");
    Console.WriteLine($"FromEmail: {reply.FromEmail}");
    Console.WriteLine($"Date:      {reply.Date}");
    Console.WriteLine($"Subject:   {reply.Subject}");
    Console.WriteLine($"HtmlBody:  {reply.HtmlBody}");

    if (reply.HasAttachments)
    {
        foreach (var file in reply.FileAttachments)
            Console.WriteLine($"  Attachment: {file.FileName} ({file.SizeBytes / 1024}kb)");
    }
}
```

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
```

---

### 🗑 Delete Message
```csharp
await mail.DeleteMessageAsync("MESSAGE_UID");
```

> ⚠️ This permanently removes the email. Move to Trash first if you want recoverable deletion.

---

### 📂 Move Message to Folder
```csharp
await mail.MoveToFolderAsync("MESSAGE_UID", "Archive");

// Gmail examples
await mail.MoveToFolderAsync("MESSAGE_UID", "[Gmail]/Trash");
await mail.MoveToFolderAsync("MESSAGE_UID", "[Gmail]/All Mail");
```

---

### 📋 List All Mailboxes
```csharp
var folders = await mail.ListMailboxesAsync();
foreach (var folder in folders)
{
    Console.WriteLine(folder);
    // INBOX
    // [Gmail]/Sent Mail
    // [Gmail]/Trash
    // [Gmail]/All Mail
    // [Gmail]/Drafts
    // [Gmail]/Starred
}
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

## 🌐 WebForms Usage

### Display Inbox

**.aspx**
```aspx
<asp:Repeater ID="rptInbox" runat="server">
    <ItemTemplate>
        <%# Eval("FromImage") %>
        <strong><%# Eval("FromName") %></strong>
        <span><%# Eval("Subject") %></span>
        <small><%# Eval("Date") %></small>
    </ItemTemplate>
</asp:Repeater>
```

**.aspx.cs**
```csharp
protected async void Page_Load(object sender, EventArgs e)
{
    if (!IsPostBack)
    {
        var mail = new MailConfig();
        await mail.ConnectAsync("you@gmail.com", "app-password");

        var inbox = await mail.GetInboxAsync("INBOX", MailConfig.ImapFlags.UNSEEN);
        rptInbox.DataSource = inbox;
        rptInbox.DataBind();

        await mail.Logout();
    }
}
```

---

### Display Full Email

**.aspx**
```aspx
<asp:Literal ID="litAvatar"  runat="server" />
<asp:Literal ID="litSubject" runat="server" />
<asp:Literal ID="litBody"    runat="server" />

<asp:Repeater ID="rptAttachments" runat="server">
    <ItemTemplate>
        <a href='DownloadAttachment.ashx?uid=<%# Request.QueryString["uid"] %>&index=<%# Container.ItemIndex %>'>
            <%# Eval("FileName") %> (<%# ((long)Eval("SizeBytes") / 1024) %>kb)
        </a>
    </ItemTemplate>
</asp:Repeater>
```

**.aspx.cs**
```csharp
protected async void Page_Load(object sender, EventArgs e)
{
    if (!IsPostBack)
    {
        string uid  = Request.QueryString["uid"];
        var mail    = new MailConfig();
        await mail.ConnectAsync("you@gmail.com", "app-password");

        var full = await mail.GetFullMessageAsync(uid, "INBOX");
        await mail.MarkAsSeenAsync(uid);
        await mail.Logout();

        // Store in session for download handler
        Session[$"attachments_{uid}"] = full.FileAttachments;

        litAvatar.Text  = full.FromImage;
        litSubject.Text = full.Subject;
        litBody.Text    = full.HtmlBody;

        rptAttachments.DataSource = full.FileAttachments
            .Select((a, i) => new { a.FileName, a.SizeBytes, Index = i }).ToList();
        rptAttachments.DataBind();
    }
}
```

---

### Download Attachment Handler

**DownloadAttachment.ashx**
```csharp
public class DownloadAttachment : IHttpHandler
{
    public void ProcessRequest(HttpContext context)
    {
        string uid = context.Request.QueryString["uid"];
        int index  = int.Parse(context.Request.QueryString["index"] ?? "0");

        var attachments = context.Session[$"attachments_{uid}"] as List<MailAttachment>;
        if (attachments == null || index >= attachments.Count)
        {
            context.Response.StatusCode = 404;
            return;
        }

        var file = attachments[index];
        context.Response.ContentType = file.ContentType;
        context.Response.AddHeader("Content-Disposition", $"attachment; filename=\"{file.FileName}\"");
        context.Response.BinaryWrite(file.Data);
        context.Response.End();
    }

    public bool IsReusable => false;
}
```

---

## 🔐 Notes

- Ensure IMAP is enabled in your email provider settings.
- For Gmail, use an **App Password** if 2FA is enabled. Generate one at [myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords).
- Default connection uses SSL on port 993.
- `MailConfig` is thread-safe — a single instance serializes all async calls via `SemaphoreSlim`.
- Always call `Logout()` when done to cleanly close the connection.

---

## 📚 Repository

https://github.com/bryanjaybodino/BNet.Library/tree/master/BNet.IMAP.Mailer

---

## 📄 License

MIT License