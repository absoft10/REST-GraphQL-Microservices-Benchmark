using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lms.RequiredBytes;

/// <summary>
/// RFC 8785 JSON Canonicalization Scheme for the CRIR (required-bytes-spec.md §1, §6).
/// Object keys sorted by UTF-16 code units; minimal string escaping as ECMAScript JSON.stringify; no whitespace.
/// Numbers: the CRIR contains only integers and DECIMAL(10,2) values, so numbers are formatted from their exact
/// decimal value with trailing zeros removed. For values with at most 15 significant digits and magnitude in
/// [1e-6, 1e21) this equals ES6 Number serialisation; anything else is rejected rather than approximated.
/// </summary>
public static class Jcs
{
    public static string Serialize(JsonNode? node)
    {
        var sb = new StringBuilder();
        Write(node, sb);
        return sb.ToString();
    }

    public static int ByteLength(JsonNode? node) => Encoding.UTF8.GetByteCount(Serialize(node));

    private static void Write(JsonNode? node, StringBuilder sb)
    {
        switch (node)
        {
            case null:
                sb.Append("null");
                break;
            case JsonObject obj:
                sb.Append('{');
                var first = true;
                foreach (var (key, value) in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(key, sb);
                    sb.Append(':');
                    Write(value, sb);
                }
                sb.Append('}');
                break;
            case JsonArray arr:
                sb.Append('[');
                for (var i = 0; i < arr.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    Write(arr[i], sb);
                }
                sb.Append(']');
                break;
            case JsonValue v:
                switch (v.GetValueKind())
                {
                    case JsonValueKind.String: WriteString(v.GetValue<string>(), sb); break;
                    case JsonValueKind.Number: sb.Append(FormatNumber(v.ToJsonString())); break;
                    case JsonValueKind.True: sb.Append("true"); break;
                    case JsonValueKind.False: sb.Append("false"); break;
                    case JsonValueKind.Null: sb.Append("null"); break;
                    default: throw new NotSupportedException($"Unsupported JSON value kind {v.GetValueKind()}.");
                }
                break;
        }
    }

    public static string FormatNumber(string token)
    {
        if (!decimal.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            throw new NotSupportedException($"Number '{token}' is outside the CRIR number domain.");
        if (d == 0m) return "0";
        var s = d.ToString("0.############################", CultureInfo.InvariantCulture);
        var digits = s.TrimStart('-').Replace(".", "", StringComparison.Ordinal).TrimStart('0').Length;
        var abs = Math.Abs(d);
        if (digits > 15 || abs >= 1e21m || abs < 0.000001m)
            throw new NotSupportedException($"Number '{token}' is outside the CRIR number domain (<=15 significant digits, [1e-6,1e21)).");
        return s;
    }

    public static void WriteString(string s, StringBuilder sb)
    {
        sb.Append('"');
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else if (char.IsHighSurrogate(c))
                    {
                        if (i + 1 >= s.Length || !char.IsLowSurrogate(s[i + 1])) throw new NotSupportedException("Lone surrogate (not I-JSON).");
                        sb.Append(c).Append(s[++i]);
                    }
                    else if (char.IsLowSurrogate(c)) throw new NotSupportedException("Lone surrogate (not I-JSON).");
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }
}
