using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace BNet.IMAP.Mailer
{
    // ─────────────────────────────────────────────────────────────────────────
    //  MIME DECODER  — RFC-compliant header / body decoding
    // ─────────────────────────────────────────────────────────────────────────
    internal static class MimeDecoder
    {
        // ── RFC 2047  =?charset?B/Q?text?=  (handles multiple words) ─────────
        public static string DecodeEncodedWords(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            return Regex.Replace(
                input,
                @"=\?([^?]+)\?(B|Q)\?([^?]*)\?=(\s*=\?[^?]+\?[BbQq]\?[^?]*\?=)*",
                m =>
                {
                    var sb = new StringBuilder();
                    foreach (Match word in Regex.Matches(m.Value, @"=\?([^?]+)\?(B|Q)\?([^?]*)\?="))
                    {
                        string charset = word.Groups[1].Value;
                        string method = word.Groups[2].Value.ToUpper();
                        string encoded = word.Groups[3].Value;
                        try
                        {
                            Encoding enc = Encoding.GetEncoding(charset);
                            if (method == "B")
                            {
                                // Pad base64 if needed
                                int pad = encoded.Length % 4;
                                if (pad > 0) encoded += new string('=', 4 - pad);
                                sb.Append(enc.GetString(Convert.FromBase64String(encoded)));
                            }
                            else // Q
                            {
                                encoded = encoded.Replace('_', ' ');
                                sb.Append(enc.GetString(DecodeQuotedPrintableBytes(encoded)));
                            }
                        }
                        catch
                        {
                            sb.Append(word.Value);
                        }
                    }
                    return sb.ToString();
                },
                RegexOptions.IgnoreCase);
        }

        // ── Quoted-Printable → bytes ──────────────────────────────────────────
        public static byte[] DecodeQuotedPrintableBytes(string input)
        {
            if (string.IsNullOrEmpty(input)) return new byte[0];

            // Soft line breaks
            input = input.Replace("=\r\n", "").Replace("=\n", "");

            var bytes = new List<byte>(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '=' && i + 2 < input.Length)
                {
                    string hex = input.Substring(i + 1, 2);
                    if (byte.TryParse(hex, NumberStyles.HexNumber, null, out byte b))
                    {
                        bytes.Add(b);
                        i += 2;
                        continue;
                    }
                }
                bytes.Add((byte)input[i]);
            }
            return bytes.ToArray();
        }

        // ── Decode a MIME body part ───────────────────────────────────────────
        public static string DecodePart(string body, string encoding, string charset)
        {
            body = body?.Trim() ?? "";
            byte[] bytes;

            switch ((encoding ?? "").ToLowerInvariant())
            {
                case "base64":
                    string cleaned = Regex.Replace(body, @"\s", "");
                    // Fix padding
                    int pad = cleaned.Length % 4;
                    if (pad > 0) cleaned += new string('=', 4 - pad);
                    try { bytes = Convert.FromBase64String(cleaned); }
                    catch { return body; }
                    break;

                case "quoted-printable":
                    bytes = DecodeQuotedPrintableBytes(body);
                    break;

                default:
                    bytes = Encoding.UTF8.GetBytes(body);
                    break;
            }

            Encoding enc;
            try { enc = string.IsNullOrEmpty(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset); }
            catch { enc = Encoding.UTF8; }

            return enc.GetString(bytes);
        }

        // ── Decode a binary attachment ────────────────────────────────────────
        public static byte[] DecodeAttachmentBytes(string body, string encoding)
        {
            body = body?.Trim() ?? "";
            switch ((encoding ?? "").ToLowerInvariant())
            {
                case "base64":
                    string cleaned = Regex.Replace(body, @"\s", "");
                    int pad = cleaned.Length % 4;
                    if (pad > 0) cleaned += new string('=', 4 - pad);
                    try { return Convert.FromBase64String(cleaned); }
                    catch { return Encoding.UTF8.GetBytes(body); }

                case "quoted-printable":
                    return DecodeQuotedPrintableBytes(body);

                default:
                    return Encoding.UTF8.GetBytes(body);
            }
        }

        // ── Parse RFC 2822 date ───────────────────────────────────────────────
        public static DateTime? ParseDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            raw = Regex.Replace(raw, @"\s*\(.*?\)", "");
            raw = Regex.Replace(raw, @"\s+", " ").Trim();

            // Normalize obsolete timezone names
            raw = Regex.Replace(raw, @"\b(UT|GMT|EST|EDT|CST|CDT|MST|MDT|PST|PDT)\b", m =>
            {
                switch (m.Value)
                {
                    case "UT": case "GMT": return "+0000";
                    case "EST": return "-0500";
                    case "EDT": return "-0400";
                    case "CST": return "-0600";
                    case "CDT": return "-0500";
                    case "MST": return "-0700";
                    case "MDT": return "-0600";
                    case "PST": return "-0800";
                    case "PDT": return "-0700";
                    default: return "+0000";
                }
            });

            if (DateTimeOffset.TryParse(raw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                    out var dto))
                return dto.UtcDateTime;

            if (DateTime.TryParse(raw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var dt))
                return dt;

            return null;
        }

        // ── HTML → plain text ─────────────────────────────────────────────────
        public static string HtmlToPlainText(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            string t = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"<p[^>]*>", "\n", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"<[^>]+>", "", RegexOptions.IgnoreCase);
            t = WebUtility.HtmlDecode(t);
            t = Regex.Replace(t, @"\n{3,}", "\n\n");
            return t.Trim();
        }

        // ── Extract charset from Content-Type header ──────────────────────────
        public static string ExtractCharset(string contentType)
        {
            var m = Regex.Match(contentType ?? "", @"charset\s*=\s*[""']?(?<c>[^;""'\s]+)", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups["c"].Value : null;
        }

        // ── Extract boundary from Content-Type header ─────────────────────────
        public static string ExtractBoundary(string contentType)
        {
            var m = Regex.Match(contentType ?? "", @"boundary\s*=\s*[""']?(?<b>[^;""'\r\n]+)[""']?", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups["b"].Value.Trim() : null;
        }

        // ── Extract filename from Content-Disposition / Content-Type ─────────
        public static string ExtractFilename(string disposition, string contentType)
        {
            // RFC 5987 extended parameter: filename*=UTF-8''name.pdf
            var ext = Regex.Match(disposition ?? "", @"filename\*\s*=\s*([^;""'\s]+)", RegexOptions.IgnoreCase);
            if (ext.Success)
            {
                try
                {
                    string v = ext.Groups[1].Value;
                    // format: charset'language'encoded_value
                    var parts = v.Split(new[] { '\'' }, 3);
                    if (parts.Length == 3)
                    {
                        string charset = parts[0];
                        string encoded = parts[2];
                        string decoded = Uri.UnescapeDataString(encoded);
                        Encoding enc;
                        try { enc = Encoding.GetEncoding(charset); }
                        catch { enc = Encoding.UTF8; }
                        return DecodeEncodedWords(decoded);
                    }
                }
                catch { }
            }

            // filename="foo.pdf"
            var fn = Regex.Match(disposition ?? "", @"filename\s*=\s*[""']?([^""\r\n;]+)[""']?", RegexOptions.IgnoreCase);
            if (fn.Success) return DecodeEncodedWords(fn.Groups[1].Value.Trim());

            // name= in Content-Type
            var nm = Regex.Match(contentType ?? "", @"name\s*=\s*[""']?([^""\r\n;]+)[""']?", RegexOptions.IgnoreCase);
            if (nm.Success) return DecodeEncodedWords(nm.Groups[1].Value.Trim());

            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HEADER PARSER  — robust multi-line header parsing
    // ─────────────────────────────────────────────────────────────────────────
    internal static class HeaderParser
    {
        /// <summary>Parse headers section into a dictionary (multi-value aware).</summary>
        public static Dictionary<string, List<string>> Parse(string block)
        {
            var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var lines = block.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            string lastName = null;
            string lastValue = null;

            void Commit()
            {
                if (lastName == null) return;
                if (!dict.TryGetValue(lastName, out var lst))
                    dict[lastName] = lst = new List<string>();
                lst.Add(lastValue);
            }

            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line)) { Commit(); lastName = null; lastValue = null; continue; }

                if ((line[0] == ' ' || line[0] == '\t') && lastName != null)
                {
                    // Folded header — unfold
                    lastValue += " " + line.Trim();
                    continue;
                }

                Commit();
                int colon = line.IndexOf(':');
                if (colon <= 0) { lastName = null; lastValue = null; continue; }

                lastName = line.Substring(0, colon).Trim();
                lastValue = line.Substring(colon + 1).Trim();
            }
            Commit();
            return dict;
        }

        public static string Get(Dictionary<string, List<string>> headers, string name)
        {
            if (headers.TryGetValue(name, out var vals) && vals.Count > 0)
                return MimeDecoder.DecodeEncodedWords(vals[0]);
            return null;
        }

        public static string GetRaw(Dictionary<string, List<string>> headers, string name)
        {
            if (headers.TryGetValue(name, out var vals) && vals.Count > 0)
                return vals[0];
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  ADDRESS PARSER  — RFC 2822 address parsing
    // ─────────────────────────────────────────────────────────────────────────
    internal static class AddressParser
    {
        public static void ParseSingle(string raw, out string name, out string email)
        {
            name = null;
            email = null;

            if (string.IsNullOrWhiteSpace(raw)) { name = "?"; email = ""; return; }
            raw = MimeDecoder.DecodeEncodedWords(raw.Trim());

            // "John Doe" <john@example.com>  or  John Doe <john@example.com>
            var angleMatch = Regex.Match(raw, @"^[""']?([^""'<]*?)[""']?\s*<([^>]+)>\s*$");
            if (angleMatch.Success)
            {
                string n = angleMatch.Groups[1].Value.Trim().Trim('"', '\'');
                string e = angleMatch.Groups[2].Value.Trim();
                email = e;
                name = string.IsNullOrEmpty(n) ? LocalPart(e) : n;
                return;
            }

            // Plain email
            if (Regex.IsMatch(raw, @"^[^@\s]+@[^@\s]+$"))
            {
                email = raw;
                name = LocalPart(raw);
                return;
            }

            name = raw;
            email = "";
        }

        public static List<string> ParseList(string raw)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            // Split on commas not inside quotes or angle brackets
            var addresses = SplitAddresses(raw);
            foreach (var addr in addresses)
            {
                string trimmed = addr.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var m = Regex.Match(trimmed, @"<([^>]+)>");
                result.Add(m.Success ? m.Groups[1].Value.Trim() : trimmed);
            }
            return result;
        }

        private static IEnumerable<string> SplitAddresses(string input)
        {
            var parts = new List<string>();
            var current = new StringBuilder();
            int depth = 0;
            bool inQuote = false;

            foreach (char c in input)
            {
                if (c == '"') { inQuote = !inQuote; current.Append(c); continue; }
                if (!inQuote)
                {
                    if (c == '<') depth++;
                    else if (c == '>') depth--;
                    else if (c == ',' && depth == 0)
                    {
                        parts.Add(current.ToString());
                        current.Clear();
                        continue;
                    }
                }
                current.Append(c);
            }
            if (current.Length > 0) parts.Add(current.ToString());
            return parts;
        }

        private static string LocalPart(string email)
        {
            int at = email.IndexOf('@');
            string local = at > 0 ? email.Substring(0, at) : email;
            return local.Length > 0 ? char.ToUpper(local[0]) + local.Substring(1) : local;
        }
    }
}