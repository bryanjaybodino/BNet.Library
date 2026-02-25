using System.Collections.Generic;

namespace BNet.IMAP.Mailer
{
    // ─────────────────────────────────────────────────────────────────────────
    //  IMAP RESPONSE  — holds lines + parsed status for one tagged exchange
    // ─────────────────────────────────────────────────────────────────────────
    internal class ImapResponse
    {
        public bool IsOk { get; set; }
        public bool IsNo { get; set; }
        public bool IsBad { get; set; }
        public bool IsBye { get; set; }
        public string Tag { get; set; }
        public string Raw { get; set; }
        public List<string> Lines { get; set; } = new List<string>();

        public void ThrowIfFailed(string context = "")
        {
            if (!IsOk)
                throw new ImapException(
                    $"IMAP {(IsNo ? "NO" : IsBad ? "BAD" : "BYE")} [{context}]: {Raw}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  IMAP EXCEPTION
    // ─────────────────────────────────────────────────────────────────────────
    public class ImapException : System.Exception
    {
        public ImapException(string message) : base(message) { }
        public ImapException(string message, System.Exception inner) : base(message, inner) { }
    }
}