#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CherryQuickRid
{
    /// <summary>
    /// Minimaler JSON-Leser und -Schreiber für die Sperrliste.
    /// </summary>
    /// <remarks>
    /// Bewusst von Hand statt über <c>UnityEngine.JsonUtility</c>: die hat im Test die Einträge
    /// stillschweigend weggelassen und nur <c>{"version": 1}</c> geschrieben, obwohl die Liste
    /// gefüllt war. Newtonsoft ist in der asmdef nicht referenziert. Das Format ist flach und
    /// vollständig unter unserer Kontrolle, ein eigener Leser ist deshalb überschaubar – und ohne
    /// Unity testbar, weil hier nichts aus UnityEngine vorkommt.
    /// <para>
    /// Der Leser ist absichtlich nachsichtig: die Datei soll von Hand bearbeitbar sein, also sind
    /// Reihenfolge, Leerraum und unbekannte Felder egal.
    /// </para>
    /// </remarks>
    public static class QuickRidJson
    {
        // --- Schreiben -----------------------------------------------------------

        public static string Write(QuickRidBlacklistFile file)
        {
            var builder = new StringBuilder(256);
            builder.Append("{\n  \"version\": ")
                   .Append(file.version.ToString(CultureInfo.InvariantCulture))
                   .Append(",\n  \"entries\": [");

            List<QuickRidBlacklistEntry> entries = file.entries ?? new List<QuickRidBlacklistEntry>();
            int written = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                QuickRidBlacklistEntry entry = entries[i];
                if (entry == null)
                    continue;

                builder.Append(written > 0 ? ",\n" : "\n");
                builder.Append("    {\n");
                AppendString(builder, "streetName", entry.streetName, true);
                builder.Append("      \"streetNumber\": ")
                       .Append(entry.streetNumber.ToString(CultureInfo.InvariantCulture))
                       .Append(",\n");
                AppendString(builder, "label", entry.label, true);
                AppendString(builder, "source", entry.source, true);
                AppendString(builder, "addedAt", entry.addedAt, false);
                builder.Append("    }");
                written++;
            }

            if (written > 0)
                builder.Append("\n  ");

            builder.Append("]\n}\n");
            return builder.ToString();
        }

        private static void AppendString(StringBuilder builder, string name, string? value, bool comma)
        {
            builder.Append("      \"").Append(name).Append("\": ");
            AppendEscaped(builder, value ?? string.Empty);
            builder.Append(comma ? ",\n" : "\n");
        }

        private static void AppendEscaped(StringBuilder builder, string value)
        {
            builder.Append('"');

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"':
                        builder.Append('\\').Append('"');
                        break;
                    case '\\':
                        builder.Append('\\').Append('\\');
                        break;
                    case '\n':
                        builder.Append('\\').Append('n');
                        break;
                    case '\r':
                        builder.Append('\\').Append('r');
                        break;
                    case '\t':
                        builder.Append('\\').Append('t');
                        break;
                    default:
                        if (c < ' ')
                            builder.Append('\\').Append('u').Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            builder.Append(c);
                        break;
                }
            }

            builder.Append('"');
        }

        // --- Lesen ---------------------------------------------------------------

        /// <summary>
        /// Liest die Datei. False bei kaputtem JSON – dann darf der Aufrufer sie nicht überschreiben,
        /// sonst kostet ein Tippfehler beim Handbearbeiten alle Einträge.
        /// </summary>
        public static bool TryRead(string json, out QuickRidBlacklistFile file)
        {
            file = new QuickRidBlacklistFile();

            try
            {
                int index = 0;
                object? root = ParseValue(json, ref index);

                if (!(root is Dictionary<string, object?> map))
                    return false;

                file.version = (int)ToNumber(Get(map, "version"), 1);

                if (Get(map, "entries") is List<object?> list)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (!(list[i] is Dictionary<string, object?> item))
                            continue;

                        string streetName = ToText(Get(item, "streetName"));
                        if (streetName.Length == 0)
                            continue;

                        file.entries.Add(new QuickRidBlacklistEntry
                        {
                            streetName = streetName,
                            streetNumber = (int)ToNumber(Get(item, "streetNumber"), 0),
                            label = ToText(Get(item, "label")),
                            source = ToText(Get(item, "source")),
                            addedAt = ToText(Get(item, "addedAt"))
                        });
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static object? Get(Dictionary<string, object?> map, string key)
        {
            return map.TryGetValue(key, out object? value) ? value : null;
        }

        private static string ToText(object? value)
        {
            return value as string ?? string.Empty;
        }

        private static double ToNumber(object? value, double fallback)
        {
            return value is double number ? number : fallback;
        }

        private static object? ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length)
                throw new FormatException("unerwartetes Ende");

            switch (s[i])
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't': Expect(s, ref i, "true"); return true;
                case 'f': Expect(s, ref i, "false"); return false;
                case 'n': Expect(s, ref i, "null"); return null;
                default: return ParseNumber(s, ref i);
            }
        }

        private static Dictionary<string, object?> ParseObject(string s, ref int i)
        {
            var map = new Dictionary<string, object?>(StringComparer.Ordinal);
            i++; // '{'
            SkipWhitespace(s, ref i);

            if (i < s.Length && s[i] == '}')
            {
                i++;
                return map;
            }

            while (true)
            {
                SkipWhitespace(s, ref i);
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);

                if (i >= s.Length || s[i] != ':')
                    throw new FormatException("Doppelpunkt erwartet");
                i++;

                map[key] = ParseValue(s, ref i);
                SkipWhitespace(s, ref i);

                if (i >= s.Length)
                    throw new FormatException("unerwartetes Ende im Objekt");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return map; }

                throw new FormatException("Komma oder schließende Klammer erwartet");
            }
        }

        private static List<object?> ParseArray(string s, ref int i)
        {
            var list = new List<object?>();
            i++; // '['
            SkipWhitespace(s, ref i);

            if (i < s.Length && s[i] == ']')
            {
                i++;
                return list;
            }

            while (true)
            {
                list.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);

                if (i >= s.Length)
                    throw new FormatException("unerwartetes Ende im Array");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return list; }

                throw new FormatException("Komma oder schließende Klammer erwartet");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"')
                throw new FormatException("Zeichenkette erwartet");

            i++;
            var builder = new StringBuilder(32);

            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"')
                    return builder.ToString();

                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }

                if (i >= s.Length)
                    break;

                char escape = s[i++];
                switch (escape)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length)
                            throw new FormatException("unvollständige Unicode-Sequenz");
                        builder.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default:
                        throw new FormatException("unbekannte Escape-Sequenz");
                }
            }

            throw new FormatException("nicht abgeschlossene Zeichenkette");
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E'))
                i++;

            if (i == start)
                throw new FormatException("Zahl erwartet");

            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static void Expect(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length || string.CompareOrdinal(s, i, literal, 0, literal.Length) != 0)
                throw new FormatException(literal + " erwartet");

            i += literal.Length;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
                i++;
        }
    }
}
