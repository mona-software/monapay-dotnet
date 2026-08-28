using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MonaPay
{
    internal static class JsonCodec
    {
        public static string Serialize(object? value)
        {
            var output = new StringBuilder();
            Write(value, output);
            return output.ToString();
        }

        private static void Write(object? value, StringBuilder output)
        {
            if (value == null) { output.Append("null"); return; }
            if (value is string || value is char || value is Enum) { Quote(Convert.ToString(value, CultureInfo.InvariantCulture)!, output); return; }
            if (value is bool boolean) { output.Append(boolean ? "true" : "false"); return; }
            if (IsNumber(value))
            {
                if (value is double doubleValue && (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue))) throw new ArgumentException("JSON không hỗ trợ NaN/Infinity");
                if (value is float floatValue && (float.IsNaN(floatValue) || float.IsInfinity(floatValue))) throw new ArgumentException("JSON không hỗ trợ NaN/Infinity");
                output.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }
            if (value is IDictionary<string, object?> genericDictionary)
            {
                output.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, object?> entry in genericDictionary)
                {
                    if (!first) output.Append(',');
                    first = false;
                    Quote(entry.Key, output);
                    output.Append(':');
                    Write(entry.Value, output);
                }
                output.Append('}');
                return;
            }
            if (value is IDictionary dictionary)
            {
                output.Append('{');
                bool first = true;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (!first) output.Append(',');
                    first = false;
                    Quote(Convert.ToString(entry.Key, CultureInfo.InvariantCulture)!, output);
                    output.Append(':');
                    Write(entry.Value, output);
                }
                output.Append('}');
                return;
            }
            if (value is IEnumerable enumerable)
            {
                output.Append('[');
                bool first = true;
                foreach (object? item in enumerable)
                {
                    if (!first) output.Append(',');
                    first = false;
                    Write(item, output);
                }
                output.Append(']');
                return;
            }
            throw new ArgumentException("Chỉ hỗ trợ JSON primitive, IDictionary và IEnumerable: " + value.GetType().FullName);
        }

        private static bool IsNumber(object value)
        {
            return value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint ||
                   value is long || value is ulong || value is float || value is double || value is decimal;
        }

        private static void Quote(string value, StringBuilder output)
        {
            output.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"': output.Append("\\\""); break;
                    case '\\': output.Append("\\\\"); break;
                    case '\b': output.Append("\\b"); break;
                    case '\f': output.Append("\\f"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\r': output.Append("\\r"); break;
                    case '\t': output.Append("\\t"); break;
                    default:
                        if (character < 0x20) output.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else output.Append(character);
                        break;
                }
            }
            output.Append('"');
        }

        public static object? Parse(string source)
        {
            var parser = new Parser(source);
            object? value = parser.ReadValue();
            parser.SkipSpace();
            if (!parser.AtEnd) throw parser.Error("ký tự thừa");
            return value;
        }

        private sealed class Parser
        {
            private readonly string source;
            private int index;

            public Parser(string sourceValue) { source = sourceValue; }
            public bool AtEnd => index == source.Length;

            public object? ReadValue()
            {
                SkipSpace();
                if (index >= source.Length) throw Error("thiếu giá trị");
                char character = source[index];
                if (character == '{') return ReadObject();
                if (character == '[') return ReadArray();
                if (character == '"') return ReadString();
                if (character == 't') { ReadLiteral("true"); return true; }
                if (character == 'f') { ReadLiteral("false"); return false; }
                if (character == 'n') { ReadLiteral("null"); return null; }
                if (character == '-' || char.IsDigit(character)) return ReadNumber();
                throw Error("giá trị không hợp lệ");
            }

            private Dictionary<string, object?> ReadObject()
            {
                var result = new Dictionary<string, object?>();
                index++;
                SkipSpace();
                if (Take('}')) return result;
                while (true)
                {
                    SkipSpace();
                    if (index >= source.Length || source[index] != '"') throw Error("object key phải là string");
                    string key = ReadString();
                    SkipSpace();
                    if (!Take(':')) throw Error("thiếu ':'");
                    result[key] = ReadValue();
                    SkipSpace();
                    if (Take('}')) return result;
                    if (!Take(',')) throw Error("thiếu ',' hoặc '}'");
                }
            }

            private List<object?> ReadArray()
            {
                var result = new List<object?>();
                index++;
                SkipSpace();
                if (Take(']')) return result;
                while (true)
                {
                    result.Add(ReadValue());
                    SkipSpace();
                    if (Take(']')) return result;
                    if (!Take(',')) throw Error("thiếu ',' hoặc ']'");
                }
            }

            private string ReadString()
            {
                index++;
                var result = new StringBuilder();
                while (index < source.Length)
                {
                    char character = source[index++];
                    if (character == '"') return result.ToString();
                    if (character == '\\')
                    {
                        if (index >= source.Length) throw Error("escape bị thiếu");
                        char escaped = source[index++];
                        switch (escaped)
                        {
                            case '"': result.Append('"'); break;
                            case '\\': result.Append('\\'); break;
                            case '/': result.Append('/'); break;
                            case 'b': result.Append('\b'); break;
                            case 'f': result.Append('\f'); break;
                            case 'n': result.Append('\n'); break;
                            case 'r': result.Append('\r'); break;
                            case 't': result.Append('\t'); break;
                            case 'u':
                                if (index + 4 > source.Length) throw Error("unicode escape bị thiếu");
                                if (!ushort.TryParse(source.Substring(index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort code)) throw Error("unicode escape không hợp lệ");
                                result.Append((char)code);
                                index += 4;
                                break;
                            default: throw Error("escape không hợp lệ");
                        }
                    }
                    else
                    {
                        if (character < 0x20) throw Error("control character trong string");
                        result.Append(character);
                    }
                }
                throw Error("string chưa đóng");
            }

            private object ReadNumber()
            {
                int start = index;
                Take('-');
                if (Take('0'))
                {
                    if (index < source.Length && char.IsDigit(source[index])) throw Error("number không hợp lệ");
                }
                else ReadDigits();
                bool fractional = false;
                if (Take('.')) { fractional = true; ReadDigits(); }
                if (index < source.Length && (source[index] == 'e' || source[index] == 'E'))
                {
                    fractional = true;
                    index++;
                    if (index < source.Length && (source[index] == '+' || source[index] == '-')) index++;
                    ReadDigits();
                }
                string text = source.Substring(start, index - start);
                if (!fractional && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer)) return integer;
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) return number;
                throw Error("number không hợp lệ");
            }

            private void ReadDigits()
            {
                int start = index;
                while (index < source.Length && char.IsDigit(source[index])) index++;
                if (start == index) throw Error("thiếu chữ số");
            }

            private void ReadLiteral(string expected)
            {
                if (index + expected.Length > source.Length || source.Substring(index, expected.Length) != expected) throw Error("literal không hợp lệ");
                index += expected.Length;
            }

            public void SkipSpace()
            {
                while (index < source.Length)
                {
                    char character = source[index];
                    if (character != ' ' && character != '\n' && character != '\r' && character != '\t') return;
                    index++;
                }
            }

            private bool Take(char expected)
            {
                if (index < source.Length && source[index] == expected) { index++; return true; }
                return false;
            }

            public ArgumentException Error(string message) => new ArgumentException("JSON không hợp lệ tại vị trí " + index + ": " + message);
        }
    }
}
