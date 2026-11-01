using System.Net;
using System.Text.Json;
using Lms.Contracts;
using Lms.Hosting;

namespace Lms.IntegrationTests;

/// <summary>Internal domain API (internal-domain-api.md): data, ordering, pagination, ID lists, validation.</summary>
[Collection(StackCollection.Name)]
public sealed class DomainServiceTests(StackFixture f)
{
    private static async Task<List<T>> Json<T>(HttpResponseMessage r)
    {
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return JsonSerializer.Deserialize<List<T>>(await r.Content.ReadAsStringAsync(), BenchmarkJson.Options)!;
    }

    [Fact]
    public async Task Books_page_returns_real_rows_in_primary_key_order()
    {
        using var c = f.Catalog.CreateClient();
        var books = await Json<BookDto>(await c.GetAsync("/internal/books?offset=10&limit=25"));
        Assert.Equal(f.Books.OrderBy(b => b.BookId).Skip(10).Take(25), books);
    }

    [Fact]
    public async Task Books_by_ids_are_sorted_deduplicated_and_unknown_ids_omitted()
    {
        using var c = f.Catalog.CreateClient();
        var books = await Json<BookDto>(await c.GetAsync("/internal/books?ids=42,7,42,999999"));
        Assert.Equal([7, 42], books.Select(b => b.BookId));
    }

    [Fact]
    public async Task Members_page_and_paging_beyond_end()
    {
        using var c = f.Members.CreateClient();
        Assert.Equal(f.Data.Members.Take(10), await Json<MemberDto>(await c.GetAsync("/internal/members?offset=0&limit=10")));
        Assert.Empty(await Json<MemberDto>(await c.GetAsync("/internal/members?offset=100000&limit=10")));
    }

    [Fact]
    public async Task Loans_by_members_are_ordered_by_member_then_loan()
    {
        using var c = f.Loans.CreateClient();
        var loans = await Json<LoanDto>(await c.GetAsync("/internal/loans?memberIds=3,1,2"));
        var expected = f.Data.Loans.Where(l => l.MemberId is >= 1 and <= 3).OrderBy(l => l.MemberId).ThenBy(l => l.LoanId);
        Assert.Equal(expected, loans);
    }

    [Theory]
    [InlineData("/internal/books?offset=0", "invalid-parameter")]
    [InlineData("/internal/books?offset=0&limit=10&ids=1", "invalid-parameter")]
    [InlineData("/internal/books", "invalid-parameter")]
    [InlineData("/internal/books?ids=1,x", "invalid-id-list")]
    [InlineData("/internal/books?ids=1&ids=2", "invalid-id-list")]
    [InlineData("/internal/books?offset=0&limit=1001", "invalid-parameter")]
    public async Task Catalog_validation_errors_are_problem_json(string url, string type)
    {
        using var c = f.Catalog.CreateClient();
        var r = await c.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal("application/problem+json", r.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        Assert.Equal(ProblemTypes.Base + type, doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Unknown_route_and_health()
    {
        using var c = f.Loans.CreateClient();
        var r = await c.GetAsync("/internal/nothing");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Contains("/errors/not-found", await r.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", await (await c.GetAsync("/health")).Content.ReadAsStringAsync());
    }
}
