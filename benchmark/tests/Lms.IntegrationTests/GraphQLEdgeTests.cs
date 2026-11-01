using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lms.Edge.GraphQL;
using Lms.RequiredBytes;

namespace Lms.IntegrationTests;

/// <summary>GraphQL edge against schema.graphql and graphql-api.md.</summary>
[Collection(StackCollection.Name)]
public sealed partial class GraphQLEdgeTests(StackFixture f)
{
    private async Task<(HttpResponseMessage, JsonElement)> Post(string query, string? op = null, string? vars = null, HttpClient? client = null)
    {
        var r = await f.PostGraphQLAsync(query, op, vars, client);
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return (r, doc.RootElement.Clone());
    }

    [Fact]
    public async Task Schema_matches_committed_contract_sdl()
    {
        var expected = ParseContractSdl();
        const string introspection = """
            { __schema { types { name kind fields { name args { name type { ...T } } type { ...T } } } } }
            fragment T on __Type { kind name ofType { kind name ofType { kind name ofType { kind name } } } }
            """;
        var (r, body) = await Post(introspection);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var types = body.GetProperty("data").GetProperty("__schema").GetProperty("types").EnumerateArray()
            .Where(t => t.GetProperty("kind").GetString() == "OBJECT" && !t.GetProperty("name").GetString()!.StartsWith("__", StringComparison.Ordinal))
            .ToDictionary(t => t.GetProperty("name").GetString()!, t => t);

        Assert.Equal(expected.Keys.Order(), types.Keys.Order());
        foreach (var (typeName, fields) in expected)
        {
            var actual = types[typeName].GetProperty("fields").EnumerateArray()
                .ToDictionary(x => x.GetProperty("name").GetString()!, x => Signature(x));
            Assert.Equal(fields.Keys.Order(), actual.Keys.Order());
            foreach (var (field, sig) in fields) Assert.True(sig == actual[field], $"{typeName}.{field}: contract '{sig}' vs server '{actual[field]}'");
        }
    }

    [Fact]
    public async Task FlatBooks_returns_exactly_required_fields()
    {
        var (r, body) = await Post(GraphQLDocuments.FlatBooks, "FlatBooks", """{"offset":0,"limit":100}""");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.False(body.TryGetProperty("errors", out _));
        var books = body.GetProperty("data").GetProperty("books");
        Assert.Equal(100, books.GetArrayLength());
        Assert.All(books.EnumerateArray(), b => Assert.Equal(RequiredFields.Flat, b.EnumerateObject().Select(p => p.Name)));
        Assert.Equal(["data"], body.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task NestedMembers_returns_chain_with_explicit_nulls()
    {
        var (_, body) = await Post(GraphQLDocuments.NestedMembers, "NestedMembers", """{"offset":0,"limit":10}""");
        var members = body.GetProperty("data").GetProperty("members");
        Assert.Equal(10, members.GetArrayLength());
        Assert.All(members.EnumerateArray(), m => Assert.Equal(5, m.GetProperty("loans").GetArrayLength()));
        var loans = members.EnumerateArray().SelectMany(m => m.GetProperty("loans").EnumerateArray()).ToList();
        Assert.Contains(loans, l => l.GetProperty("returnDate").ValueKind == JsonValueKind.Null);
        Assert.All(loans, l => Assert.Equal(RequiredFields.NestedBook, l.GetProperty("book").EnumerateObject().Select(p => p.Name)));
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, 1001)]
    public async Task Invalid_arguments_return_INVALID_ARGUMENT_with_null_data(int offset, int limit)
    {
        var (r, body) = await Post(GraphQLDocuments.FlatBooks, "FlatBooks", $$"""{"offset":{{offset}},"limit":{{limit}}}""");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("data").ValueKind);
        Assert.Equal(ErrorCodes.InvalidArgument, body.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Upstream_failure_maps_to_UPSTREAM_FAILURE_without_exception_detail()
    {
        using var failing = f.Edge<GraphQLEdgeMarker>(() => new StatusHandler(HttpStatusCode.ServiceUnavailable));
        using var c = failing.CreateClient();
        var (_, body) = await Post(GraphQLDocuments.FlatBooks, "FlatBooks", """{"offset":0,"limit":5}""", c);
        var error = body.GetProperty("errors")[0];
        Assert.Equal(ErrorCodes.UpstreamFailure, error.GetProperty("extensions").GetProperty("code").GetString());
        Assert.DoesNotContain("stackTrace", body.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task Get_requests_are_not_served()
    {
        var r = await f.GraphQLClient.SendAsync(StackFixture.Get("/graphql?query=%7Bbooks(offset:0,limit:1)%7BbookId%7D%7D"));
        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task Request_batching_is_rejected()
    {
        var req = EquivalenceRunner.NewRequest(HttpMethod.Post, "/graphql");
        req.Content = new StringContent("""[{"query":"{ books(offset:0,limit:1) { bookId } }"},{"query":"{ books(offset:1,limit:1) { bookId } }"}]""",
            System.Text.Encoding.UTF8, "application/json");
        var r = await f.GraphQLClient.SendAsync(req);
        var text = await r.Content.ReadAsStringAsync();
        Assert.False(r.IsSuccessStatusCode && !text.Contains("\"errors\"", StringComparison.Ordinal), $"batch accepted: {(int)r.StatusCode} {text}");
    }

    [Fact]
    public async Task Response_headers_follow_contract()
    {
        var r = await f.PostGraphQLAsync(GraphQLDocuments.FlatBooks, "FlatBooks", """{"offset":0,"limit":1}""");
        Assert.Equal("application/json", r.Content.Headers.ContentType?.MediaType);
        Assert.False(r.Headers.Contains("Server"));
        Assert.False(r.Headers.Contains("Cache-Control"));
        Assert.False(r.Headers.Contains("ETag"));
    }

    // ---- helpers: tiny SDL reader for the committed contract (types → field → signature) ----

    private static Dictionary<string, Dictionary<string, string>> ParseContractSdl()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "docs", "api-contracts", "schema.graphql"))) d = d.Parent;
        var lines = File.ReadAllLines(Path.Combine(d!.FullName, "docs", "api-contracts", "schema.graphql"));
        var result = new Dictionary<string, Dictionary<string, string>>();
        Dictionary<string, string>? current = null;
        foreach (var raw in lines)
        {
            var line = raw.Split('#')[0].Trim();
            if (line.Length == 0 || line.StartsWith('"')) continue;
            var t = TypeStart().Match(line);
            if (t.Success) { current = []; result[t.Groups[1].Value] = current; continue; }
            if (line == "}") { current = null; continue; }
            if (current is null) continue;
            var fm = FieldLine().Match(line);
            if (fm.Success)
                current[fm.Groups[1].Value] = (fm.Groups[2].Success ? Regex.Replace(fm.Groups[2].Value, @"\s+", "") : "") + ":" + fm.Groups[3].Value;
        }
        return result;
    }

    private static string Signature(JsonElement field)
    {
        var args = field.GetProperty("args").EnumerateArray().ToList();
        var a = args.Count == 0 ? "" : "(" + string.Join(",", args.Select(x => x.GetProperty("name").GetString() + ":" + TypeRef(x.GetProperty("type")))) + ")";
        return a + ":" + TypeRef(field.GetProperty("type"));
    }

    private static string TypeRef(JsonElement t) => t.GetProperty("kind").GetString() switch
    {
        "NON_NULL" => TypeRef(t.GetProperty("ofType")) + "!",
        "LIST" => "[" + TypeRef(t.GetProperty("ofType")) + "]",
        _ => t.GetProperty("name").GetString()!,
    };

    [GeneratedRegex(@"^type\s+(\w+)\s*\{$")]
    private static partial Regex TypeStart();

    [GeneratedRegex(@"^(\w+)\s*(\([^)]*\))?\s*:\s*([\w\[\]!]+)$")]
    private static partial Regex FieldLine();
}
