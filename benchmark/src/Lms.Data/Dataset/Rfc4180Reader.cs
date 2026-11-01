using System.Text;

namespace Lms.Data.Dataset;

/// <summary>
/// Strict RFC 4180 reader: comma separator, double-quote quoting with "" escapes, CRLF or LF record ends,
/// quoted fields may contain separators and line breaks. Malformed quoting is an error (no silent repair).
/// </summary>
public static class Rfc4180Reader
{
    public static IEnumerable<string[]> Read(string text)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var i = 0;
        var n = text.Length;
        var recordHasContent = false;
        while (i < n)
        {
            var c = text[i];
            if (c == '"' && field.Length == 0)
            {
                i++; // opening quote
                while (true)
                {
                    if (i >= n) throw new InvalidDataException("Unterminated quoted field.");
                    if (text[i] == '"')
                    {
                        if (i + 1 < n && text[i + 1] == '"') { field.Append('"'); i += 2; continue; }
                        i++; // closing quote
                        break;
                    }
                    field.Append(text[i++]);
                }
                if (i < n && text[i] is not (',' or '\r' or '\n'))
                    throw new InvalidDataException($"Unexpected character after closing quote at offset {i}.");
                recordHasContent = true;
                continue;
            }
            if (c == '"') throw new InvalidDataException($"Quote inside unquoted field at offset {i}.");
            if (c == ',') { fields.Add(field.ToString()); field.Clear(); recordHasContent = true; i++; continue; }
            if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < n && text[i + 1] == '\n') i++;
                i++;
                fields.Add(field.ToString()); field.Clear();
                yield return [.. fields];
                fields.Clear();
                recordHasContent = false;
                continue;
            }
            field.Append(c);
            recordHasContent = true;
            i++;
        }
        if (recordHasContent || field.Length > 0)
        {
            fields.Add(field.ToString());
            yield return [.. fields];
        }
    }
}
