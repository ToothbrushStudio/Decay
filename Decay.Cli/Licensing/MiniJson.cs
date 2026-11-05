using System.Globalization;
using System.Text;

namespace Decay.Cli.Licensing
{
    /// <summary>
    /// Reads the one JSON shape a license payload has: a flat object such as
    /// {"n":"Name","t":"pro","d":"2026-01-02"}. .NET Framework has no
    /// System.Text.Json, and a license is too small to justify a dependency.
    /// String values are returned; other scalar values (numbers, true, false,
    /// null) are skipped, and anything else (nested objects or arrays,
    /// trailing text, bad escapes) makes the whole payload invalid.
    /// </summary>
    internal static class MiniJson
    {
        /// <summary>Returns the string fields, or null if the text is not a flat JSON object.</summary>
        internal static Dictionary<string, string>? ParseStringFields(string json)
        {
            int at = 0;
            Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.Ordinal);

            SkipSpace(json, ref at);

            if (!Take(json, ref at, '{'))
            {
                return null;
            }

            SkipSpace(json, ref at);

            if (Take(json, ref at, '}'))
            {
                return EndOfText(json, at) ? fields : null;
            }

            while (true)
            {
                SkipSpace(json, ref at);

                string? name = ReadString(json, ref at);

                if (name is null)
                {
                    return null;
                }

                SkipSpace(json, ref at);

                if (!Take(json, ref at, ':'))
                {
                    return null;
                }

                SkipSpace(json, ref at);

                if (at < json.Length && json[at] == '"')
                {
                    string? value = ReadString(json, ref at);

                    if (value is null)
                    {
                        return null;
                    }

                    fields[name] = value;
                }
                else
                {
                    fields.Remove(name);

                    if (!SkipScalar(json, ref at))
                    {
                        return null;
                    }
                }

                SkipSpace(json, ref at);

                if (Take(json, ref at, ','))
                {
                    continue;
                }

                if (Take(json, ref at, '}'))
                {
                    return EndOfText(json, at) ? fields : null;
                }

                return null;
            }
        }

        private static string? ReadString(string json, ref int at)
        {
            if (!Take(json, ref at, '"'))
            {
                return null;
            }

            StringBuilder text = new StringBuilder();

            while (at < json.Length)
            {
                char c = json[at++];

                if (c == '"')
                {
                    return text.ToString();
                }

                if (c < ' ')
                {
                    return null;
                }

                if (c != '\\')
                {
                    text.Append(c);

                    continue;
                }

                if (at >= json.Length)
                {
                    return null;
                }

                char escape = json[at++];

                switch (escape)
                {
                    case '"': text.Append('"'); break;
                    case '\\': text.Append('\\'); break;
                    case '/': text.Append('/'); break;
                    case 'b': text.Append('\b'); break;
                    case 'f': text.Append('\f'); break;
                    case 'n': text.Append('\n'); break;
                    case 'r': text.Append('\r'); break;
                    case 't': text.Append('\t'); break;
                    case 'u':
                        if (at + 4 > json.Length
                            || !int.TryParse(
                                json.Substring(at, 4),
                                NumberStyles.AllowHexSpecifier,
                                CultureInfo.InvariantCulture,
                                out int code))
                        {
                            return null;
                        }

                        text.Append((char)code);
                        at += 4;
                        break;
                    default:
                        return null;
                }
            }

            return null;
        }

        // Numbers, true, false, null: consume up to the next delimiter.
        private static bool SkipScalar(string json, ref int at)
        {
            int start = at;

            while (at < json.Length && ",}] \t\r\n".IndexOf(json[at]) < 0)
            {
                if (json[at] == '{' || json[at] == '[' || json[at] == '"')
                {
                    return false;
                }

                at++;
            }

            string token = json.Substring(start, at - start);

            return token == "true"
                || token == "false"
                || token == "null"
                || double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }

        private static void SkipSpace(string json, ref int at)
        {
            while (at < json.Length && (json[at] == ' ' || json[at] == '\t' || json[at] == '\r' || json[at] == '\n'))
            {
                at++;
            }
        }

        private static bool Take(string json, ref int at, char expected)
        {
            if (at < json.Length && json[at] == expected)
            {
                at++;

                return true;
            }

            return false;
        }

        private static bool EndOfText(string json, int at)
        {
            SkipSpace(json, ref at);

            return at == json.Length;
        }
    }
}
