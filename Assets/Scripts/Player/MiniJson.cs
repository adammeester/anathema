using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Anathema.Player
{
    // Tiny JSON reader (Unity's JsonUtility can't read free-form dictionaries). Produces
    // Dictionary<string, object>, List<object>, double, string, bool or null. Throws FormatException on bad input.
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            int i = 0;
            object value = ReadValue(json, ref i);
            SkipSpace(json, ref i);
            if (i != json.Length) throw Error(json, i, "unexpected trailing text");
            return value;
        }

        static System.FormatException Error(string s, int i, string message)
        {
            int line = 1;
            for (int k = 0; k < i && k < s.Length; k++) if (s[k] == '\n') line++;
            return new System.FormatException($"JSON error on line {line}: {message}");
        }

        static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static object ReadValue(string s, ref int i)
        {
            SkipSpace(s, ref i);
            if (i >= s.Length) throw Error(s, i, "unexpected end");

            char c = s[i];
            if (c == '{') return ReadObject(s, ref i);
            if (c == '[') return ReadArray(s, ref i);
            if (c == '"') return ReadString(s, ref i);
            if (Match(s, ref i, "true")) return true;
            if (Match(s, ref i, "false")) return false;
            if (Match(s, ref i, "null")) return null;

            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == start || !double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                throw Error(s, start, "unexpected character '" + c + "'");
            return number;
        }

        static bool Match(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length;
            return true;
        }

        static Dictionary<string, object> ReadObject(string s, ref int i)
        {
            var result = new Dictionary<string, object>();
            i++; // {
            SkipSpace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return result; }

            while (true)
            {
                SkipSpace(s, ref i);
                if (i >= s.Length || s[i] != '"') throw Error(s, i, "expected a property name");
                string key = ReadString(s, ref i);
                SkipSpace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw Error(s, i, "expected ':'");
                i++;
                result[key] = ReadValue(s, ref i);
                SkipSpace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return result; }
                throw Error(s, i, "expected ',' or '}'");
            }
        }

        static List<object> ReadArray(string s, ref int i)
        {
            var result = new List<object>();
            i++; // [
            SkipSpace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return result; }

            while (true)
            {
                result.Add(ReadValue(s, ref i));
                SkipSpace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return result; }
                throw Error(s, i, "expected ',' or ']'");
            }
        }

        static string ReadString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }

                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw Error(s, i, "bad unicode escape");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            throw Error(s, i, "unterminated string");
        }
    }
}
