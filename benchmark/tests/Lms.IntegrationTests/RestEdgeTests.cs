using System.Net;
using System.Text.Json;
using Lms.Contracts;
using Lms.Edge.Rest;
using Lms.Hosting;

namespace Lms.IntegrationTests;

/// <summary>Public REST edge against rest-api.md: routes, full representations, headers, errors.</summary>
[Collection(StackCollection.Name)]
public sealed class RestEdgeTests(StackFixture f)
{
    private static readonly string[] BookFields = ["bookId", "title", "authors", "description", "category", "publisher", "price", "publishMonth", "publishYear"];
    private static readonly string[] MemberFields = ["memberId", "firstName", "lastName", "email", "phoneNumber", "address"];
    private static readonly string[] LoanFields = ["loanId", "memberId", "bookId", "loanDate", "returnDate", "isReturned"];

    private async Task<(HttpResponseMessage, JsonElement)> Get(string url)
    {
        var r = await f.RestClient.SendAsync(StackFixture.Get(url));
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return (r, doc.RootElement.Clone());
    }

    [Fact]
    public async Task Flat_books_full_representation_in_order()
    {
        var (r, body) = await Get("/api/books?offset=0&limit=100");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(100, body.GetArrayLength());
        Assert.Equal(Enumerable.Range(1, 100), body.EnumerateArray().Select(b => b.GetProperty("bookId").GetInt32()));
        Assert.All(body.EnumerateArray(), b => Assert.Equal(BookFields, b.EnumerateObject().Select(p => p.Name)));
        // Nulls are explicit (book 7 has no description in the fixture: id % 7 == 0)
        Assert.Equal(JsonValueKind.Null, body[6].GetProperty("description").ValueKind);
    }

    [Fact]
    public async Task Response_headers_follow_contract()
    {
        var (r, _) = await Get("/api/books?offset=0&limit=5");
        Assert.Equal("application/json", r.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", r.Content.Headers.ContentType?.CharSet);
        Assert.False(r.Headers.Contains("Server"));
        Assert.False(r.Headers.Contains("Cache-Control"));
        Assert.False(r.Headers.Contains("ETag"));
        Assert.Empty(r.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task Members_loans_and_books_by_ids()
    {
        var (_, members) = await Get("/api/members?offset=0&limit=10");
        Assert.Equal(Enumerable.Range(1, 10), members.EnumerateArray().Select(m => m.GetProperty("memberId").GetInt32()));
        Assert.All(members.EnumerateArray(), m => Assert.Equal(MemberFields, m.EnumerateObject().Select(p => p.Name)));

        var (_, loans) = await Get("/api/loans?memberIds=1,2,3,4,5,6,7,8,9,10");
        Assert.Equal(50, loans.GetArrayLength());
        Assert.All(loans.EnumerateArray(), l => Assert.Equal(LoanFields, l.EnumerateObject().Select(p => p.Name)));
        var pairs = loans.EnumerateArray().Select(l => (l.GetProperty("memberId").GetInt32(), l.GetProperty("loanId").GetInt32())).ToList();
        Assert.Equal(pairs.OrderBy(p => p.Item1).ThenBy(p => p.Item2), pairs);
        Assert.Contains(loans.EnumerateArray(), l => l.GetProperty("returnDate").ValueKind == JsonValueKind.Null);

        var (_, books) = await Get("/api/books?ids=30,3,30");
        Assert.Equal([3, 30], books.EnumerateArray().Select(b => b.GetProperty("bookId").GetInt32()));
    }

    [Fact]
    public async Task Composite_embeds_full_representations()
    {
        var (r, body) = await Get("/api/members-with-loans?offset=0&limit=10");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(10, body.GetArrayLength());
        var m = body[0];
        Assert.Equal([.. MemberFields, "loans"], m.EnumerateObject().Select(p => p.Name));
        var loan = m.GetProperty("loans")[0];
        Assert.Equal([.. LoanFields, "book"], loan.EnumerateObject().Select(p => p.Name));
        Assert.Equal(BookFields, loan.GetProperty("book").EnumerateObject().Select(p => p.Name));
        Assert.All(body.EnumerateArray(), x => Assert.Equal(5, x.GetProperty("loans").GetArrayLength()));
    }

    [Theory]
    [InlineData("/api/books?offset=0", "invalid-parameter")]
    [InlineData("/api/books?offset=-1&limit=5", "invalid-parameter")]
    [InlineData("/api/books?offset=0&limit=5&ids=1", "invalid-parameter")]
    [InlineData("/api/books?ids=", "invalid-id-list")]
    [InlineData("/api/members?offset=0&limit=0", "invalid-parameter")]
    [InlineData("/api/loans?memberIds=1,,2", "invalid-id-list")]
    [InlineData("/api/members-with-loans?limit=10", "invalid-parameter")]
    public async Task Validation_errors_are_problem_json(string url, string type)
    {
        var (r, body) = await Get(url);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("application/problem+json", r.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ProblemTypes.Base + type, body.GetProperty("type").GetString());
        Assert.Equal(["type", "title", "status", "detail", "instance"], body.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task Unknown_route_is_problem_404()
    {
        var (r, body) = await Get("/api/unknown");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Equal(ProblemTypes.NotFound, body.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Upstream_failure_maps_to_502()
    {
        using var failing = f.Edge<RestEdgeMarker>(() => new StatusHandler(HttpStatusCode.InternalServerError));
        using var c = failing.CreateClient();
        var r = await c.SendAsync(StackFixture.Get("/api/books?offset=0&limit=5"));
        Assert.Equal(HttpStatusCode.BadGateway, r.StatusCode);
        Assert.Contains(ProblemTypes.UpstreamFailure, await r.Content.ReadAsStringAsync());
    }
}

/// <summary>Primary handler that always answers with a fixed status (simulated domain failure).</summary>
internal sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{}") });
}
