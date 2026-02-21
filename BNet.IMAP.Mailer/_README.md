# BNet.IMAP.Mailer

Lightweight, dependency-free IMAP client for .NET to fetch, read, move, and manage emails over secure SSL (supports Gmail and other IMAP servers).

---

## ✨ Features

- 🔐 Secure SSL/TLS IMAP connection  
- 📥 Get unread, seen, deleted, or all messages  
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
var mail = new MailConfig("your-email@gmail.com", "your-password");
```

---

### 📥 Get Unread Messages

```csharp
var inbox = await mail.GetInboxAsync(MailConfig.ImapFlags.UNSEEN);

foreach (var message in inbox)
{
    Console.WriteLine($"From: {message.From}");
    Console.WriteLine($"Subject: {message.Subject}");
    Console.WriteLine($"Date: {message.Date}");
}
```

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

## ⚙️ Constructor

```csharp
public MailConfig(string username, string password, 
                  string host = "imap.gmail.com", 
                  int port = 993)
```

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