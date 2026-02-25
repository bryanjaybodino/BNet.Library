using System;
using System.Collections.Generic;
using System.Linq;

namespace BNet.IMAP.Mailer
{
    // ─────────────────────────────────────────────────────────────────────────
    //  MAIL ATTACHMENT
    // ─────────────────────────────────────────────────────────────────────────
    public class MailAttachment
    {
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public string ContentId { get; set; }
        public bool IsInline { get; set; }
        public long SizeBytes { get; set; }
        public byte[] Data { get; set; }

        public string Base64Data => Data != null ? Convert.ToBase64String(Data) : null;
        public string DataUri => Data != null ? $"data:{ContentType};base64,{Base64Data}" : null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  BODY PART  — one node from a parsed BODYSTRUCTURE tree
    // ─────────────────────────────────────────────────────────────────────────
    public class BodyPart
    {
        public string Section { get; set; }   // e.g. "1", "1.1", "2"
        public string Type { get; set; }   // "text", "image", "application", "multipart"
        public string SubType { get; set; }   // "plain", "html", "pdf", "mixed" …
        public string Charset { get; set; }
        public string Encoding { get; set; }   // "base64", "quoted-printable", "7bit" …
        public int Size { get; set; }   // octets on server
        public string FileName { get; set; }
        public string ContentId { get; set; }
        public string Disposition { get; set; }   // "attachment" | "inline" | null
        public bool IsMultipart => Type != null && Type.Equals("multipart", StringComparison.OrdinalIgnoreCase);
        public bool IsAttachment => (Disposition != null && Disposition.Equals("attachment", StringComparison.OrdinalIgnoreCase))
                                      || (!string.IsNullOrEmpty(FileName) && !IsTextBody);
        public bool IsInline => !string.IsNullOrEmpty(ContentId)
                                      || (Disposition != null && Disposition.Equals("inline", StringComparison.OrdinalIgnoreCase));
        public bool IsTextBody => (Type != null && Type.Equals("text", StringComparison.OrdinalIgnoreCase))
                                      && !IsAttachment;
        public List<BodyPart> Children { get; set; } = new List<BodyPart>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  MAIL MESSAGE  — full email
    // ─────────────────────────────────────────────────────────────────────────
    public class MailMessage
    {
        public string Id { get; set; }
        public string Folder { get; set; }
        public List<string> Flags { get; set; } = new List<string>();

        public string From { get; set; }
        public string FromName { get; set; }
        public string FromEmail { get; set; }
        public string FromImage { get; set; }

        public List<string> To { get; set; } = new List<string>();
        public List<string> CC { get; set; } = new List<string>();
        public List<string> BCC { get; set; } = new List<string>();
        public string ReplyTo { get; set; }
        public string MessageId { get; set; }
        public string InReplyTo { get; set; }

        public string Subject { get; set; }
        public DateTime Date { get; set; }

        public string HtmlBody { get; set; }
        public string PlainTextBody { get; set; }

        public List<MailAttachment> Attachments { get; set; } = new List<MailAttachment>();
        public List<MailAttachment> FileAttachments { get { return Attachments == null ? new List<MailAttachment>() : Attachments.Where(a => !a.IsInline).ToList(); } }
        public List<MailAttachment> InlineAttachments { get { return Attachments == null ? new List<MailAttachment>() : Attachments.Where(a => a.IsInline).ToList(); } }
        public bool HasAttachments { get { var fa = FileAttachments; return fa != null && fa.Count > 0; } }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  MAIL INBOX ITEM  — lightweight list-view entry
    // ─────────────────────────────────────────────────────────────────────────
    public class MailInboxes
    {
        public int TotalEmail { get; set; }
        public int TotalPagination { get; set; }

        public string Id { get; set; }
        public string Folder { get; set; }
        public List<string> Flags { get; set; } = new List<string>();

        public string From { get; set; }
        public string FromName { get; set; }
        public string FromEmail { get; set; }
        public string FromImage { get; set; }

        public List<string> To { get; set; } = new List<string>();
        public List<string> CC { get; set; } = new List<string>();
        public List<string> BCC { get; set; } = new List<string>();

        public string Subject { get; set; }
        public DateTime Date { get; set; }
        public bool HasAttachment { get; set; }

        public List<MailMessage> Submail { get; set; } = new List<MailMessage>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  SEARCH QUERY  — fluent builder for IMAP SEARCH
    // ─────────────────────────────────────────────────────────────────────────
    public class ImapSearchQuery
    {
        private readonly List<string> _parts = new List<string>();

        public static ImapSearchQuery All() => new ImapSearchQuery().Add("ALL");
        public static ImapSearchQuery Unseen() => new ImapSearchQuery().Add("UNSEEN");
        public static ImapSearchQuery Seen() => new ImapSearchQuery().Add("SEEN");
        public static ImapSearchQuery Flagged() => new ImapSearchQuery().Add("FLAGGED");
        public static ImapSearchQuery Unflagged() => new ImapSearchQuery().Add("UNFLAGGED");
        public static ImapSearchQuery Answered() => new ImapSearchQuery().Add("ANSWERED");
        public static ImapSearchQuery Unanswered() => new ImapSearchQuery().Add("UNANSWERED");
        public static ImapSearchQuery Deleted() => new ImapSearchQuery().Add("DELETED");
        public static ImapSearchQuery Draft() => new ImapSearchQuery().Add("DRAFT");

        public ImapSearchQuery From(string addr) => Add($"FROM \"{addr}\"");
        public ImapSearchQuery To(string addr) => Add($"TO \"{addr}\"");
        public ImapSearchQuery Subject(string text) => Add($"SUBJECT \"{Esc(text)}\"");
        public ImapSearchQuery Body(string text) => Add($"BODY \"{Esc(text)}\"");
        public ImapSearchQuery Text(string text) => Add($"TEXT \"{Esc(text)}\"");
        public ImapSearchQuery Header(string name, string val) => Add($"HEADER \"{name}\" \"{Esc(val)}\"");
        public ImapSearchQuery Since(DateTime d) => Add($"SINCE {d:dd-MMM-yyyy}");
        public ImapSearchQuery Before(DateTime d) => Add($"BEFORE {d:dd-MMM-yyyy}");
        public ImapSearchQuery On(DateTime d) => Add($"ON {d:dd-MMM-yyyy}");
        public ImapSearchQuery Larger(int bytes) => Add($"LARGER {bytes}");
        public ImapSearchQuery Smaller(int bytes) => Add($"SMALLER {bytes}");

        public ImapSearchQuery Or(ImapSearchQuery a, ImapSearchQuery b)
            => Add($"OR ({a.Build()}) ({b.Build()})");
        public ImapSearchQuery Not(ImapSearchQuery q)
            => Add($"NOT ({q.Build()})");

        private ImapSearchQuery Add(string s) { _parts.Add(s); return this; }
        private static string Esc(string s) { return s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\""); }
        public string Build() => string.Join(" ", _parts);
        public override string ToString() => Build();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  NEW MAIL EVENT ARGS  — passed to OnNewMailAsync when new mail arrives
    // ─────────────────────────────────────────────────────────────────────────
    public class NewMailEventArgs
    {
        /// <summary>The full message — body, attachments, headers all populated.</summary>
        public MailMessage Message { get; private set; }

        /// <summary>The folder the message arrived in e.g. "INBOX".</summary>
        public string Folder { get; private set; }

        /// <summary>UTC time the event was raised.</summary>
        public DateTime ReceivedAt { get; private set; }

        public NewMailEventArgs(MailMessage message, string folder)
        {
            Message = message;
            Folder = folder;
            ReceivedAt = DateTime.UtcNow;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  IMAP FLAGS ENUM
    // ─────────────────────────────────────────────────────────────────────────
    public enum ImapFlags
    {
        ALL, SEEN, UNSEEN, ANSWERED, UNANSWERED,
        FLAGGED, UNFLAGGED, DELETED, UNDELETED, DRAFT, UNDRAFT
    }
}