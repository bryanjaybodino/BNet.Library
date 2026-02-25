using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BNet.IMAP.Mailer
{
    // ─────────────────────────────────────────────────────────────────────────
    //  BODYSTRUCTURE PARSER
    //  Parses the IMAP BODYSTRUCTURE response per RFC 3501 §7.4.2
    //  Supports nested multipart and all parameter lists.
    // ─────────────────────────────────────────────────────────────────────────
    internal static class BodyStructureParser
    {
        public static BodyPart Parse(string raw)
        {
            raw = raw.Trim();
            // Strip outer FETCH wrapper if present
            var wrapper = Regex.Match(raw, @"BODYSTRUCTURE\s+(\(.+\))\s*$",
                                      RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (wrapper.Success) raw = wrapper.Groups[1].Value;

            int pos = 0;
            var part = ParsePart(raw, ref pos, "");
            return part;
        }

        // ── Flat list walker ──────────────────────────────────────────────────
        public static IEnumerable<BodyPart> Flatten(BodyPart root)
        {
            if (root == null) yield break;
            yield return root;
            foreach (var child in root.Children)
                foreach (var p in Flatten(child))
                    yield return p;
        }

        // ─────────────────────────────────────────────────────────────────────
        private static BodyPart ParsePart(string s, ref int pos, string section)
        {
            SkipWhitespace(s, ref pos);
            if (pos >= s.Length) return null;

            if (s[pos] != '(')
                return null;

            pos++; // consume '('

            // ── Peek: if first char is also '(' → multipart ──────────────────
            SkipWhitespace(s, ref pos);

            if (pos < s.Length && s[pos] == '(')
            {
                return ParseMultipart(s, ref pos, section);
            }

            return ParseSinglePart(s, ref pos, section);
        }

        // ── Single-part: (type subtype (params) id description encoding size …) ─
        private static BodyPart ParseSinglePart(string s, ref int pos, string section)
        {
            var part = new BodyPart { Section = section };

            part.Type = ReadString(s, ref pos)?.ToLowerInvariant();
            part.SubType = ReadString(s, ref pos)?.ToLowerInvariant();

            // Parameters list: ("key" "val" "key2" "val2") or NIL
            var paramsList = ReadParamList(s, ref pos);
            if (paramsList != null)
            {
                if (paramsList.TryGetValue("charset", out string cs)) part.Charset = cs;
                if (paramsList.TryGetValue("name", out string nm)) part.FileName = MimeDecoder.DecodeEncodedWords(nm);
            }

            // ID, Description, Encoding, Size
            string id = ReadString(s, ref pos);
            string description = ReadString(s, ref pos);
            part.Encoding = ReadString(s, ref pos)?.ToLowerInvariant();
            part.Size = ReadNumber(s, ref pos);

            // text/* has line count after size
            if (part.Type == "text") ReadNumber(s, ref pos);

            // message/rfc822 has envelope, body, lines
            if (part.Type == "message" && part.SubType == "rfc822")
            {
                SkipParenGroup(s, ref pos); // envelope
                int innerPos = pos;
                SkipWhitespace(s, ref pos);
                if (pos < s.Length && s[pos] == '(')
                    ParsePart(s, ref pos, section + ".1");
                ReadNumber(s, ref pos); // lines
            }

            // Extension data: MD5, disposition, language, location …
            SkipWhitespace(s, ref pos);
            if (pos < s.Length && s[pos] != ')')
            {
                ReadString(s, ref pos); // MD5
                // Disposition: ("attachment" ("filename" "foo.pdf")) or NIL
                SkipWhitespace(s, ref pos);
                if (pos < s.Length && s[pos] == '(')
                {
                    pos++; // (
                    part.Disposition = ReadString(s, ref pos)?.ToLowerInvariant();
                    var dispParams = ReadParamList(s, ref pos);
                    if (dispParams != null && dispParams.TryGetValue("filename", out string fn))
                        part.FileName = MimeDecoder.DecodeEncodedWords(fn);
                    SkipWhitespace(s, ref pos);
                    if (pos < s.Length && s[pos] == ')') pos++;
                }
                else if (pos < s.Length && s[pos] != ')')
                    ReadString(s, ref pos); // NIL
                // Language, Location (skip)
                SkipRemainingExtensions(s, ref pos);
            }

            // Content-ID (some servers include it as an extra field)
            part.ContentId = id?.Trim('<', '>');

            SkipWhitespace(s, ref pos);
            if (pos < s.Length && s[pos] == ')') pos++;

            return part;
        }

        // ── Multipart: ((part)(part)… subtype (params) …) ────────────────────
        private static BodyPart ParseMultipart(string s, ref int pos, string section)
        {
            var part = new BodyPart { Type = "multipart", Section = section };
            int child = 1;

            // Parse sub-parts until we hit a non-'(' token
            while (pos < s.Length && s[pos] == '(')
            {
                string childSection = string.IsNullOrEmpty(section)
                    ? child.ToString()
                    : $"{section}.{child}";

                var subPart = ParsePart(s, ref pos, childSection);
                if (subPart != null) part.Children.Add(subPart);
                child++;
                SkipWhitespace(s, ref pos);
            }

            // SubType: "mixed", "alternative", "related" …
            part.SubType = ReadString(s, ref pos)?.ToLowerInvariant();

            // Extension: params, disposition, language, location
            SkipWhitespace(s, ref pos);
            if (pos < s.Length && s[pos] == '(')
            {
                var paramsList = ReadParamList(s, ref pos);
                // boundary is in here but we don't need it for fetch
            }
            SkipRemainingExtensions(s, ref pos);

            SkipWhitespace(s, ref pos);
            if (pos < s.Length && s[pos] == ')') pos++;

            return part;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Token readers
        // ─────────────────────────────────────────────────────────────────────

        private static string ReadString(string s, ref int pos)
        {
            SkipWhitespace(s, ref pos);
            if (pos >= s.Length) return null;

            // NIL
            if (pos + 2 < s.Length &&
                s.Substring(pos, 3).Equals("NIL", StringComparison.OrdinalIgnoreCase) &&
                (pos + 3 >= s.Length || !char.IsLetterOrDigit(s[pos + 3])))
            {
                pos += 3;
                return null;
            }

            // Quoted string
            if (s[pos] == '"')
            {
                pos++;
                var sb = new System.Text.StringBuilder();
                while (pos < s.Length && s[pos] != '"')
                {
                    if (s[pos] == '\\' && pos + 1 < s.Length) { pos++; sb.Append(s[pos++]); }
                    else sb.Append(s[pos++]);
                }
                if (pos < s.Length) pos++; // closing "
                return sb.ToString();
            }

            // Literal {n}\r\ndata (shouldn't normally appear in BODYSTRUCTURE but handle anyway)
            if (s[pos] == '{')
            {
                int end = s.IndexOf('}', pos);
                if (end > pos)
                {
                    int count = int.Parse(s.Substring(pos + 1, end - pos - 1));
                    pos = end + 1;
                    if (pos < s.Length && s[pos] == '\r') pos++;
                    if (pos < s.Length && s[pos] == '\n') pos++;
                    string data = s.Substring(pos, Math.Min(count, s.Length - pos));
                    pos += data.Length;
                    return data;
                }
            }

            // Atom
            int start = pos;
            while (pos < s.Length && s[pos] != ' ' && s[pos] != ')' && s[pos] != '(' && s[pos] != '\r' && s[pos] != '\n')
                pos++;
            return s.Substring(start, pos - start);
        }

        private static int ReadNumber(string s, ref int pos)
        {
            string raw = ReadString(s, ref pos);
            return int.TryParse(raw, out int n) ? n : 0;
        }

        private static Dictionary<string, string> ReadParamList(string s, ref int pos)
        {
            SkipWhitespace(s, ref pos);
            if (pos >= s.Length) return null;

            if (pos + 2 < s.Length && s.Substring(pos, 3).Equals("NIL", StringComparison.OrdinalIgnoreCase))
            { pos += 3; return null; }

            if (s[pos] != '(') return null;
            pos++; // (

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (pos < s.Length && s[pos] != ')')
            {
                string key = ReadString(s, ref pos);
                string val = ReadString(s, ref pos);
                if (key != null) dict[key] = val ?? "";
                SkipWhitespace(s, ref pos);
            }
            if (pos < s.Length) pos++; // )
            return dict;
        }

        private static void SkipParenGroup(string s, ref int pos)
        {
            SkipWhitespace(s, ref pos);
            if (pos >= s.Length) return;

            if (s[pos] != '(')
            {
                // It might be NIL
                ReadString(s, ref pos);
                return;
            }

            int depth = 0;
            bool inQuote = false;
            while (pos < s.Length)
            {
                char c = s[pos++];
                if (c == '"') inQuote = !inQuote;
                else if (!inQuote)
                {
                    if (c == '(') depth++;
                    else if (c == ')') { depth--; if (depth == 0) return; }
                }
            }
        }

        private static void SkipRemainingExtensions(string s, ref int pos)
        {
            while (true)
            {
                SkipWhitespace(s, ref pos);
                if (pos >= s.Length || s[pos] == ')') break;
                if (s[pos] == '(') SkipParenGroup(s, ref pos);
                else ReadString(s, ref pos);
            }
        }

        private static void SkipWhitespace(string s, ref int pos)
        {
            while (pos < s.Length && (s[pos] == ' ' || s[pos] == '\t' || s[pos] == '\r' || s[pos] == '\n'))
                pos++;
        }
    }
}