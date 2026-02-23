# BNet.IMAP.Mailer

Lightweight, dependency-free IMAP client for .NET to fetch, read, move, and manage emails over secure SSL (supports Gmail and other IMAP servers).

---

## ✨ Features

- 🔐 Secure SSL/TLS IMAP connection
- 📥 Get unread, seen, deleted, or all messages
- 📊 Get email counts by flags (All, Seen, Unseen, Answered, Flagged, Deleted, Draft, etc.)
- 📄 Fetch full email (HTML + Plain Text)
- ✅ Mark message as read
- 🗑 Delete and expunge messages
- 📂 Move messages to another folder
- 📋 List all mailboxes
- ⚡ Async/await support
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
    string id = message.Id;
    Console.WriteLine($"Total: {message.TotalEmail}");
    Console.WriteLine($"Pagination: {message.TotalPagination}");
    Console.WriteLine($"From: {message.From}");
    Console.WriteLine($"Subject: {message.Subject}");
    Console.WriteLine($"Date: {message.Date}");
}
```

---

### 📊 Get Email Counts by Flags
```csharp
var count = await mail.GetEmailCountsByFlagsAsync("INBOX");

int TotalAll         = count[MailConfig.ImapFlags.ALL];
int TotalSeen        = count[MailConfig.ImapFlags.SEEN];
int TotalUnseen      = count[MailConfig.ImapFlags.UNSEEN];
int TotalAnswered    = count[MailConfig.ImapFlags.ANSWERED];
int TotalUnanswered  = count[MailConfig.ImapFlags.UNANSWERED];
int TotalFlagged     = count[MailConfig.ImapFlags.FLAGGED];
int TotalUnflagged   = count[MailConfig.ImapFlags.UNFLAGGED];
int TotalDeleted     = count[MailConfig.ImapFlags.DELETED];
int TotalUndeleted   = count[MailConfig.ImapFlags.UNDELETED];
int TotalDraft       = count[MailConfig.ImapFlags.DRAFT];
int TotalUndraft     = count[MailConfig.ImapFlags.UNDRAFT];

Console.WriteLine("\n=== Individual Counts ===");
Console.WriteLine($"TotalAll:         {TotalAll}");
Console.WriteLine($"TotalSeen:        {TotalSeen}");
Console.WriteLine($"TotalUnseen:      {TotalUnseen}");
Console.WriteLine($"TotalAnswered:    {TotalAnswered}");
Console.WriteLine($"TotalUnanswered:  {TotalUnanswered}");
Console.WriteLine($"TotalFlagged:     {TotalFlagged}");
Console.WriteLine($"TotalUnflagged:   {TotalUnflagged}");
Console.WriteLine($"TotalDeleted:     {TotalDeleted}");
Console.WriteLine($"TotalUndeleted:   {TotalUndeleted}");
Console.WriteLine($"TotalDraft:       {TotalDraft}");
Console.WriteLine($"TotalUndraft:     {TotalUndraft}");
```

Returns a dictionary keyed by `MailConfig.ImapFlags`, giving you the count of emails matching each flag in the specified mailbox.

---

### 📄 Get Full Message
```csharp
var fullMessage = await mail.GetFullMessageAsync("MESSAGE_UID");
Console.WriteLine(fullMessage.Subject);
Console.WriteLine(fullMessage.PlainTextBody);
```

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

---

### 📂 Move Message to Folder
```csharp
await mail.MoveToFolderAsync("MESSAGE_UID", "Archive");
```

---

### 📋 List All Mailboxes
```csharp
var folders = await mail.ListMailboxesAsync();
foreach (var folder in folders)
{
    Console.WriteLine(folder);
}
```

---

### 🚪 Logout
```csharp
await mail.Logout();
```

---

You can connect to any IMAP server by specifying a custom host and port.

---

## 🔐 Notes

- Ensure IMAP is enabled in your email provider.
- For Gmail accounts, you may need to use an App Password.
- Default connection uses SSL on port 993.

---

## 📚 Repository

https://github.com/bryanjaybodino/BNet.Library/tree/master/BNet.IMAP.Mailer

---

## 📄 License

MIT License
