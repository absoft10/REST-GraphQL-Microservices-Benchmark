using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lms.RequiredBytes;

public enum ConfigId { FR, FG, NRC, NRM, NG }

public static class ConfigNames
{
    public static string Cell(ConfigId c) => c switch
    {
        ConfigId.FR => "F-R", ConfigId.FG => "F-G", ConfigId.NRC => "N-RC", ConfigId.NRM => "N-RM", ConfigId.NG => "N-G",
        _ => throw new ArgumentOutOfRangeException(nameof(c)),
    };

    public static Workload WorkloadOf(ConfigId c) => c is ConfigId.FR or ConfigId.FG ? Workload.Flat : Workload.Nested;
}

/// <summary>
/// One row of bytes_summary.csv (required-bytes-spec.md §4/§6). The four terms sum exactly to Actual − Required.
/// </summary>
public sealed record ByteAccount(
    string Workload, string Config, int Calls,
    long RequiredBytes, long ActualBytes,
    long Envelope, long NonRequired, long Formatting, long MultiCall)
{
    public double OverFetch => ActualBytes == 0 ? 0 : (double)(ActualBytes - RequiredBytes) / ActualBytes;
    public bool SumsExactly => Envelope + NonRequired + Formatting + MultiCall == ActualBytes - RequiredBytes;
}

/// <summary>Computes Actual Bytes (raw transmitted bodies) and the decomposition, independently of Required Bytes.</summary>
public static class ByteAccounting
{
    private sealed record Shape(string[] Leaves, (string Key, Shape Child)[] Children);

    private static readonly Shape FlatShape = new(RequiredFields.Flat, []);
    private static readonly Shape BookShape = new(RequiredFields.NestedBook, []);
    private static readonly Shape LoanShape = new(RequiredFields.Loan, [("book", BookShape)]);
    private static readonly Shape MemberShape = new(RequiredFields.Member, [("loans", LoanShape)]);

    public static long Utf8(string s) => Encoding.UTF8.GetByteCount(s);

    public static ByteAccount SingleBody(ConfigId config, string body, long requiredBytes)
    {
        if (config == ConfigId.NRM) throw new ArgumentException("N-RM has three bodies; use MultiCall.");
        using var doc = JsonDocument.Parse(body);
        var payload = config switch
        {
            ConfigId.FG => doc.RootElement.GetProperty("data").GetProperty("books"),
            ConfigId.NG => doc.RootElement.GetProperty("data").GetProperty("members"),
            _ => doc.RootElement,
        };
        var shape = ConfigNames.WorkloadOf(config) == Workload.Flat ? FlatShape : MemberShape;
        var actual = Utf8(body);
        var p = Utf8(payload.GetRawText());
        var rs = Utf8(ProjectArray(payload, shape));
        return new ByteAccount(ConfigNames.WorkloadOf(config).ToString().ToLowerInvariant(), ConfigNames.Cell(config), 1,
            requiredBytes, actual, Envelope: actual - p, NonRequired: p - rs, Formatting: rs - requiredBytes, MultiCall: 0);
    }

    public static ByteAccount MultiCall(string membersBody, string loansBody, string booksBody, long requiredBytes)
    {
        using var m = JsonDocument.Parse(membersBody);
        using var l = JsonDocument.Parse(loansBody);
        using var b = JsonDocument.Parse(booksBody);
        var actual = Utf8(membersBody) + Utf8(loansBody) + Utf8(booksBody);

        var memberLeafOnly = new Shape(RequiredFields.Member, []);
        var loanLeafOnly = new Shape(RequiredFields.Loan, []);
        var loanWithLinks = new Shape([.. RequiredFields.Loan, .. RequiredFields.LoanLinks], []);

        var rs = Utf8(ProjectArray(m.RootElement, memberLeafOnly)) + Utf8(ProjectArray(l.RootElement, loanLeafOnly)) + Utf8(ProjectArray(b.RootElement, BookShape));
        var rLink = Utf8(ProjectArray(m.RootElement, memberLeafOnly)) + Utf8(ProjectArray(l.RootElement, loanWithLinks)) + Utf8(ProjectArray(b.RootElement, BookShape));
        var nonRequired = actual - rLink;
        var links = rLink - rs;
        var joined = Utf8(JoinedServiceFormat(m.RootElement, l.RootElement, b.RootElement));
        return new ByteAccount("nested", "N-RM", 3, requiredBytes, actual,
            Envelope: 0, NonRequired: nonRequired, Formatting: joined - requiredBytes, MultiCall: links + rs - joined);
    }

    /// <summary>Insignificant whitespace outside strings ⇒ not compact (spec: bodies must be compact).</summary>
    public static bool IsCompact(string body)
    {
        var inString = false;
        for (var i = 0; i < body.Length; i++)
        {
            var c = body[i];
            if (inString)
            {
                if (c == '\\') i++;
                else if (c == '"') inString = false;
            }
            else if (c == '"') inString = true;
            else if (c is ' ' or '\t' or '\r' or '\n') return false;
        }
        return true;
    }

    // ---- projections that keep the service's own tokens and property order ----

    private static string ProjectArray(JsonElement array, Shape shape) =>
        "[" + string.Join(',', array.EnumerateArray().Select(e => ProjectObject(e, shape))) + "]";

    private static string ProjectObject(JsonElement obj, Shape shape)
    {
        var parts = new List<string>();
        foreach (var p in obj.EnumerateObject())
        {
            if (shape.Leaves.Contains(p.Name)) parts.Add(Prop(p.Name, p.Value.GetRawText()));
            else if (shape.Children.FirstOrDefault(c => c.Key == p.Name) is { Child: { } child })
                parts.Add(Prop(p.Name, p.Value.ValueKind == JsonValueKind.Array ? ProjectArray(p.Value, child) : ProjectObject(p.Value, child)));
        }
        return "{" + string.Join(',', parts) + "}";
    }

    private static string JoinedServiceFormat(JsonElement members, JsonElement loans, JsonElement books)
    {
        var bookById = books.EnumerateArray().ToDictionary(x => x.GetProperty("bookId").GetInt32());
        var loansByMember = loans.EnumerateArray().ToLookup(x => x.GetProperty("memberId").GetInt32());
        var loanLeafOnly = new Shape(RequiredFields.Loan, []);
        return "[" + string.Join(',', members.EnumerateArray().Select(mem =>
        {
            var memberPart = ProjectObject(mem, new Shape(RequiredFields.Member, []));
            var loanParts = loansByMember[mem.GetProperty("memberId").GetInt32()].Select(ln =>
            {
                var loanPart = ProjectObject(ln, loanLeafOnly);
                var bookPart = ProjectObject(bookById[ln.GetProperty("bookId").GetInt32()], BookShape);
                return loanPart[..^1] + "," + Prop("book", bookPart) + "}";
            });
            return memberPart[..^1] + "," + Prop("loans", "[" + string.Join(',', loanParts) + "]") + "}";
        })) + "]";
    }

    private static string Prop(string name, string rawValue) => "\"" + name + "\":" + rawValue;

    public static long RequiredBytesOf(JsonArray crir) => Utf8(Jcs.Serialize(crir));
}
