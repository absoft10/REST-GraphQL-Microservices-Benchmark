using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lms.Contracts;

namespace Lms.RequiredBytes;

/// <summary>Transmitted GraphQL query texts (graphql-api.md §1, canonical single-line form). Book fields PROVISIONAL.</summary>
public static class GraphQLDocuments
{
    public const string FlatBooks =
        "query FlatBooks($offset: Int!, $limit: Int!) { books(offset: $offset, limit: $limit) { bookId title authors category price publishYear } }";
    public const string NestedMembers =
        "query NestedMembers($offset: Int!, $limit: Int!) { members(offset: $offset, limit: $limit) { memberId firstName lastName loans { loanId loanDate returnDate book { bookId title authors } } } }";

    /// <summary>Compact POST body with exactly query, operationName, variables.</summary>
    public static string RequestBody(string query, string operationName, int offset, int limit)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("query", query);
            w.WriteString("operationName", operationName);
            w.WriteStartObject("variables");
            w.WriteNumber("offset", offset);
            w.WriteNumber("limit", limit);
            w.WriteEndObject();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>Contract rule: strip comment lines, collapse whitespace runs to one space, trim.</summary>
    public static string Canonicalize(string documentText) =>
        System.Text.RegularExpressions.Regex.Replace(
            string.Join('\n', documentText.Split('\n').Where(l => !l.TrimStart().StartsWith('#'))), @"\s+", " ").Trim();
}

public sealed record CapturedExchange(string Method, string Url, string? RequestBody, int Status, string ContentType,
    IReadOnlyDictionary<string, string> Headers, string Body);

public sealed record ConfigResult(
    ConfigId Config,
    IReadOnlyList<CapturedExchange> Exchanges,
    string NormalizedJcs,
    bool EquivalentToOracle,
    bool AllCompact,
    ByteAccount Bytes,
    string? Failure);

public sealed record EquivalenceResult(
    string FlatOracleJcs, string NestedOracleJcs, long FlatRequiredBytes, long NestedRequiredBytes,
    IReadOnlyList<ConfigResult> Configs)
{
    public bool Passed => Configs.All(c => c.Failure is null && c.EquivalentToOracle && c.AllCompact && c.Bytes.SumsExactly
                                           && c.Bytes.ActualBytes >= c.Bytes.RequiredBytes);
}

/// <summary>
/// Runs the five configurations against live (or in-memory) edges and checks M2 §2.3 equivalence against the
/// dataset oracle (required-bytes-spec.md §3, doc 03 §4.4). Used by `lms-bench equivalence` and by the integration tests.
/// </summary>
public static class EquivalenceRunner
{
    public static async Task<EquivalenceResult> RunAsync(HttpClient rest, HttpClient graphql,
        IReadOnlyList<BookDto> books, IReadOnlyList<MemberDto> members, IReadOnlyList<LoanDto> loans,
        int flatLimit, int nestedLimit, int offset = 0, CancellationToken ct = default)
    {
        var flatOracle = CrirOracle.Flat(books, offset, flatLimit);
        var nestedOracle = CrirOracle.Nested(members, loans, books, offset, nestedLimit);
        var flatJcs = Jcs.Serialize(flatOracle);
        var nestedJcs = Jcs.Serialize(nestedOracle);
        var flatReq = ByteAccounting.Utf8(flatJcs);
        var nestedReq = ByteAccounting.Utf8(nestedJcs);
        var o = offset.ToString(CultureInfo.InvariantCulture);

        var results = new List<ConfigResult>
        {
            await Single(ConfigId.FR, flatJcs, flatReq, () => Get(rest, $"/api/books?offset={o}&limit={flatLimit}", ct), CrirNormalizer.FlatRest),
            await Single(ConfigId.FG, flatJcs, flatReq, () => Post(graphql, GraphQLDocuments.RequestBody(GraphQLDocuments.FlatBooks, "FlatBooks", offset, flatLimit), ct), CrirNormalizer.FlatGraphQL),
            await Single(ConfigId.NRC, nestedJcs, nestedReq, () => Get(rest, $"/api/members-with-loans?offset={o}&limit={nestedLimit}", ct), CrirNormalizer.NestedComposite),
            await MultiCall(rest, nestedJcs, nestedReq, offset, nestedLimit, ct),
            await Single(ConfigId.NG, nestedJcs, nestedReq, () => Post(graphql, GraphQLDocuments.RequestBody(GraphQLDocuments.NestedMembers, "NestedMembers", offset, nestedLimit), ct), CrirNormalizer.NestedGraphQL),
        };
        return new EquivalenceResult(flatJcs, nestedJcs, flatReq, nestedReq, results);
    }

    private static async Task<ConfigResult> Single(ConfigId id, string oracle, long required,
        Func<Task<CapturedExchange>> call, Func<string, JsonArray> normalize)
    {
        var ex = await call();
        try
        {
            if (ex.Status is < 200 or > 299) throw new InvalidDataException($"HTTP {ex.Status}");
            var jcs = Jcs.Serialize(normalize(ex.Body));
            return new ConfigResult(id, [ex], jcs, jcs == oracle, ByteAccounting.IsCompact(ex.Body), ByteAccounting.SingleBody(id, ex.Body, required), null);
        }
        catch (Exception e) when (e is InvalidDataException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return Failed(id, [ex], required, e.Message);
        }
    }

    /// <summary>N-RM: the three-call chain exactly as nested-rest-multicall-sequence.md §1 (sorted, de-duplicated ID lists).</summary>
    private static async Task<ConfigResult> MultiCall(HttpClient rest, string oracle, long required, int offset, int limit, CancellationToken ct)
    {
        var exchanges = new List<CapturedExchange>();
        try
        {
            var e1 = await Get(rest, $"/api/members?offset={offset.ToString(CultureInfo.InvariantCulture)}&limit={limit.ToString(CultureInfo.InvariantCulture)}", ct);
            exchanges.Add(e1);
            if (e1.Status != 200) throw new InvalidDataException($"call 1 HTTP {e1.Status}");
            var memberIds = JsonNode.Parse(e1.Body)!.AsArray().Select(m => m!["memberId"]!.GetValue<int>());
            var e2 = await Get(rest, $"/api/loans?memberIds={QueryParsing.FormatIdList(memberIds)}", ct);
            exchanges.Add(e2);
            if (e2.Status != 200) throw new InvalidDataException($"call 2 HTTP {e2.Status}");
            var bookIds = JsonNode.Parse(e2.Body)!.AsArray().Select(l => l!["bookId"]!.GetValue<int>());
            var e3 = await Get(rest, $"/api/books?ids={QueryParsing.FormatIdList(bookIds)}", ct);
            exchanges.Add(e3);
            if (e3.Status != 200) throw new InvalidDataException($"call 3 HTTP {e3.Status}");

            var jcs = Jcs.Serialize(CrirNormalizer.NestedMultiCall(e1.Body, e2.Body, e3.Body));
            return new ConfigResult(ConfigId.NRM, exchanges, jcs, jcs == oracle,
                exchanges.All(x => ByteAccounting.IsCompact(x.Body)), ByteAccounting.MultiCall(e1.Body, e2.Body, e3.Body, required), null);
        }
        catch (Exception e) when (e is InvalidDataException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return Failed(ConfigId.NRM, exchanges, required, e.Message);
        }
    }

    private static ConfigResult Failed(ConfigId id, IReadOnlyList<CapturedExchange> ex, long required, string message) =>
        new(id, ex, "", false, false, new ByteAccount(ConfigNames.WorkloadOf(id).ToString().ToLowerInvariant(), ConfigNames.Cell(id),
            ex.Count, required, ex.Sum(x => ByteAccounting.Utf8(x.Body)), 0, 0, 0, 0), message);

    public static HttpRequestMessage NewRequest(HttpMethod method, string url)
    {
        var r = new HttpRequestMessage(method, url) { Version = System.Net.HttpVersion.Version11, VersionPolicy = HttpVersionPolicy.RequestVersionExact };
        r.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        r.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
        return r;
    }

    private static Task<CapturedExchange> Get(HttpClient c, string url, CancellationToken ct) => Send(c, NewRequest(HttpMethod.Get, url), null, ct);

    private static Task<CapturedExchange> Post(HttpClient c, string body, CancellationToken ct)
    {
        var r = NewRequest(HttpMethod.Post, "/graphql");
        r.Content = new StringContent(body, new UTF8Encoding(false), "application/json");
        r.Content.Headers.ContentType!.CharSet = null; // Content-Type: application/json (errors-and-pagination.md §5)
        return Send(c, r, body, ct);
    }

    private static async Task<CapturedExchange> Send(HttpClient c, HttpRequestMessage r, string? body, CancellationToken ct)
    {
        using var resp = await c.SendAsync(r, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        var headers = resp.Headers.Concat(resp.Content.Headers)
            .ToDictionary(h => h.Key, h => string.Join(", ", h.Value), StringComparer.OrdinalIgnoreCase);
        return new CapturedExchange(r.Method.Method, r.RequestUri!.ToString(), body, (int)resp.StatusCode,
            resp.Content.Headers.ContentType?.ToString() ?? "", headers, text);
    }
}
