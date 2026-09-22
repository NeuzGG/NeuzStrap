using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace NeuzStrap.Core
{
    /// <summary>Marks a property that should not be written to / read from JSON.</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class JsonIgnoreAttribute : Attribute { }

    /// <summary>
    /// A small, dependency-free JSON reader/writer. Objects parse to Dictionary&lt;string, object&gt;,
    /// arrays to List&lt;object&gt;, numbers to long or double. It also maps plain C# classes
    /// (public get/set properties) to and from JSON, which is all NeuzStrap needs.
    /// </summary>
    public static class Json
    {
        // ------------------------------------------------------------------ parsing

        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var p = new Parser(text);
            p.SkipWhitespace();
            object value = p.ReadValue();
            p.SkipWhitespace();
            if (!p.AtEnd) throw p.Error("Unexpected text after the end of the JSON");
            return value;
        }

        public static bool TryParse(string text, out object value, out string error)
        {
            try { value = Parse(text); error = null; return true; }
            catch (FormatException ex) { value = null; error = ex.Message; return false; }
        }

        sealed class Parser
        {
            readonly string _s;
            int _i;

            public Parser(string s)
            {
                _s = s;
                if (_s.Length > 0 && _s[0] == '\uFEFF') _i = 1; // BOM
            }

            public bool AtEnd => _i >= _s.Length;

            public FormatException Error(string msg)
            {
                int line = 1, col = 1;
                for (int k = 0; k < Math.Min(_i, _s.Length); k++)
                {
                    if (_s[k] == '\n') { line++; col = 1; } else col++;
                }
                return new FormatException($"{msg} (line {line}, column {col})");
            }

            public void SkipWhitespace()
            {
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n') { _i++; continue; }
                    // be forgiving about // and /* */ comments people paste in
                    if (c == '/' && _i + 1 < _s.Length && _s[_i + 1] == '/')
                    {
                        while (_i < _s.Length && _s[_i] != '\n') _i++;
                        continue;
                    }
                    if (c == '/' && _i + 1 < _s.Length && _s[_i + 1] == '*')
                    {
                        int end = _s.IndexOf("*/", _i + 2, StringComparison.Ordinal);
                        _i = end < 0 ? _s.Length : end + 2;
                        continue;
                    }
                    break;
                }
            }

            public object ReadValue()
            {
                if (AtEnd) throw Error("Unexpected end of JSON");
                char c = _s[_i];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Error($"Unexpected character '{c}'");
                }
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) throw Error($"Expected '{word}'");
                _i += word.Length;
            }

            Dictionary<string, object> ReadObject()
            {
                var dict = new Dictionary<string, object>(StringComparer.Ordinal);
                _i++; // {
                SkipWhitespace();
                if (!AtEnd && _s[_i] == '}') { _i++; return dict; }
                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || _s[_i] != '"') throw Error("Expected a property name in double quotes");
                    string key = ReadString();
                    SkipWhitespace();
                    if (AtEnd || _s[_i] != ':') throw Error("Expected ':' after property name");
                    _i++;
                    SkipWhitespace();
                    dict[key] = ReadValue();
                    SkipWhitespace();
                    if (AtEnd) throw Error("Unexpected end of JSON inside an object");
                    if (_s[_i] == ',')
                    {
                        _i++;
                        SkipWhitespace();
                        if (!AtEnd && _s[_i] == '}') { _i++; return dict; } // trailing comma
                        continue;
                    }
                    if (_s[_i] == '}') { _i++; return dict; }
                    throw Error("Expected ',' or '}'");
                }
            }

            List<object> ReadArray()
            {
                var list = new List<object>();
                _i++; // [
                SkipWhitespace();
                if (!AtEnd && _s[_i] == ']') { _i++; return list; }
                while (true)
                {
                    SkipWhitespace();
                    list.Add(ReadValue());
                    SkipWhitespace();
                    if (AtEnd) throw Error("Unexpected end of JSON inside an array");
                    if (_s[_i] == ',')
                    {
                        _i++;
                        SkipWhitespace();
                        if (!AtEnd && _s[_i] == ']') { _i++; return list; }
                        continue;
                    }
                    if (_s[_i] == ']') { _i++; return list; }
                    throw Error("Expected ',' or ']'");
                }
            }

            string ReadString()
            {
                _i++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Error("Unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw Error("Unterminated escape");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw Error("Bad \\u escape");
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: throw Error($"Bad escape '\\{e}'");
                    }
                }
            }

            object ReadNumber()
            {
                int start = _i;
                if (_s[_i] == '-') _i++;
                bool isFloat = false;
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c >= '0' && c <= '9') { _i++; continue; }
                    if (c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-') { isFloat = true; _i++; continue; }
                    break;
                }
                string num = _s.Substring(start, _i - start);
                if (!isFloat && long.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)) return l;
                if (double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
                throw Error($"Invalid number '{num}'");
            }
        }

        // ------------------------------------------------------------------ writing

        public static string Serialize(object value, bool indent = true)
        {
            var sb = new StringBuilder();
            WriteValue(sb, ToJsonValue(value), indent, 0);
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, object v, bool indent, int depth)
        {
            switch (v)
            {
                case null: sb.Append("null"); return;
                case string s: WriteString(sb, s); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case double d: sb.Append(double.IsNaN(d) || double.IsInfinity(d) ? "0" : d.ToString("R", CultureInfo.InvariantCulture)); return;
                case float f: sb.Append(((double)f).ToString("R", CultureInfo.InvariantCulture)); return;
                case decimal m: sb.Append(m.ToString(CultureInfo.InvariantCulture)); return;
                case IDictionary<string, object> dict:
                {
                    if (dict.Count == 0) { sb.Append("{}"); return; }
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in dict)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        NewLine(sb, indent, depth + 1);
                        WriteString(sb, kv.Key);
                        sb.Append(indent ? ": " : ":");
                        WriteValue(sb, kv.Value, indent, depth + 1);
                    }
                    NewLine(sb, indent, depth);
                    sb.Append('}');
                    return;
                }
                case IList<object> list:
                {
                    if (list.Count == 0) { sb.Append("[]"); return; }
                    sb.Append('[');
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        NewLine(sb, indent, depth + 1);
                        WriteValue(sb, list[i], indent, depth + 1);
                    }
                    NewLine(sb, indent, depth);
                    sb.Append(']');
                    return;
                }
                default:
                    if (IsNumber(v))
                    {
                        sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
                        return;
                    }
                    WriteString(sb, Convert.ToString(v, CultureInfo.InvariantCulture));
                    return;
            }
        }

        static void NewLine(StringBuilder sb, bool indent, int depth)
        {
            if (!indent) return;
            sb.Append("\r\n");
            sb.Append(' ', depth * 2);
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        static bool IsNumber(object v) =>
            v is int || v is long || v is short || v is byte || v is uint || v is ulong || v is ushort || v is sbyte;

        // ------------------------------------------------------------------ object mapping

        /// <summary>Converts a typed object graph into dictionaries/lists/primitives.</summary>
        public static object ToJsonValue(object obj)
        {
            switch (obj)
            {
                case null: return null;
                case string _:
                case bool _:
                case double _:
                case float _:
                case decimal _:
                    return obj;
                case Enum e: return e.ToString();
                case DateTime dt: return dt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
                case IDictionary dict:
                {
                    var copy = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (DictionaryEntry kv in dict) copy[Convert.ToString(kv.Key, CultureInfo.InvariantCulture)] = ToJsonValue(kv.Value);
                    return copy;
                }
                case IEnumerable seq:
                {
                    var list = new List<object>();
                    foreach (var item in seq) list.Add(ToJsonValue(item));
                    return list;
                }
            }

            if (IsNumber(obj)) return obj;

            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || !prop.CanWrite || prop.GetIndexParameters().Length > 0) continue;
                if (prop.IsDefined(typeof(JsonIgnoreAttribute), true)) continue;
                result[prop.Name] = ToJsonValue(prop.GetValue(obj, null));
            }
            return result;
        }

        public static T Deserialize<T>(string text) => (T)FromJsonValue(Parse(text), typeof(T));

        /// <summary>
        /// Maps parsed JSON onto a type. Unknown keys are ignored and anything that doesn't fit keeps its
        /// default value, so an old or hand-edited settings file never crashes the launcher.
        /// </summary>
        public static object FromJsonValue(object json, Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
                return json == null ? null : FromJsonValue(json, underlying);

            if (json == null)
                return type.IsValueType ? Activator.CreateInstance(type) : null;

            if (type == typeof(object)) return json;

            if (type == typeof(string))
            {
                if (json is bool jb) return jb ? "True" : "False";
                if (json is double jd) return jd.ToString("R", CultureInfo.InvariantCulture);
                return Convert.ToString(json, CultureInfo.InvariantCulture);
            }

            if (type == typeof(bool))
            {
                if (json is bool b) return b;
                return string.Equals(Convert.ToString(json, CultureInfo.InvariantCulture), "true", StringComparison.OrdinalIgnoreCase);
            }

            if (type == typeof(DateTime))
            {
                return DateTime.TryParse(Convert.ToString(json, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                                         DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
                    ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                    : default(DateTime);
            }

            if (type.IsEnum)
            {
                string s = Convert.ToString(json, CultureInfo.InvariantCulture);
                try { return Enum.Parse(type, s, true); }
                catch { return Activator.CreateInstance(type); }
            }

            if (type.IsPrimitive || type == typeof(decimal))
                return Convert.ChangeType(json, type, CultureInfo.InvariantCulture);

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var itemType = type.GetGenericArguments()[0];
                var list = (IList)Activator.CreateInstance(type);
                if (json is List<object> src)
                    foreach (var item in src)
                    {
                        try { list.Add(FromJsonValue(item, itemType)); } catch { /* skip bad item */ }
                    }
                return list;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var args = type.GetGenericArguments();
                var dict = (IDictionary)Activator.CreateInstance(type);
                if (json is Dictionary<string, object> src && args[0] == typeof(string))
                    foreach (var kv in src)
                    {
                        try { dict[kv.Key] = FromJsonValue(kv.Value, args[1]); } catch { }
                    }
                return dict;
            }

            var instance = Activator.CreateInstance(type);
            if (json is Dictionary<string, object> map)
            {
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!prop.CanWrite || prop.IsDefined(typeof(JsonIgnoreAttribute), true)) continue;
                    if (!map.TryGetValue(prop.Name, out var raw)) continue;
                    try { prop.SetValue(instance, FromJsonValue(raw, prop.PropertyType), null); }
                    catch { /* keep default */ }
                }
            }
            return instance;
        }

        // ------------------------------------------------------------------ small helpers for API responses

        public static Dictionary<string, object> AsObject(object v) => v as Dictionary<string, object>;
        public static List<object> AsArray(object v) => v as List<object>;

        public static object Get(object obj, string key) =>
            obj is Dictionary<string, object> d && d.TryGetValue(key, out var v) ? v : null;

        public static string GetString(object obj, string key) =>
            Get(obj, key) is object v ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;

        public static long GetLong(object obj, string key)
        {
            var v = Get(obj, key);
            if (v == null) return 0;
            try { return Convert.ToInt64(v, CultureInfo.InvariantCulture); } catch { return 0; }
        }
    }
}
