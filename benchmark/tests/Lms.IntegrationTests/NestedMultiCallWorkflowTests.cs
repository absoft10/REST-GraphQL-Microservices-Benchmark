using System.Net;
using System.Text.Json;
using Lms.Contracts;
using Lms.Hosting;

namespace Lms.IntegrationTests;

/// <summary>N-RM three-call chain exactly as nested-rest-multicall-sequence.md §1–§2.</summary>
[Collection(StackCollection.Name)]
public sealed class NestedMultiCallWorkflowTests(StackFixture f)
{
    public sealed record ChainResult(List<MemberDto> Members, List<LoanDto> Loans, List<BookDto> Books, string[] Urls);

    public async Task<ChainResult> RunChainAsync()
    {
        var url1 = $"/api/members?offset=0&limit={StackFixture.NestedLimit}";
        var members = await Step<MemberDto>(url1);
        var memberIds = members.Select(m => m.MemberId).ToList();
        Assert.Equal(memberIds.Order(), memberIds); // call-1 order already ascending, kept

        var url2 = "/api/loans?memberIds=" + string.Join(',', memberIds);
        var loans = await Step<LoanDto>(url2);

        var url3 = "/api/books?ids=" + QueryParsing.FormatIdList(loans.Select(l => l.BookId)); // sorted + de-duplicated (§2)
        var books = await Step<BookDto>(url3);
        return new ChainResult(members, loans, books, [url1, url2, url3]);
    }

    private async Task<List<T>> Step<T>(string url)
    {
        var r = await f.RestClient.SendAsync(StackFixture.Get(url));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return JsonSerializer.Deserialize<List<T>>(await r.Content.ReadAsStringAsync(), BenchmarkJson.Options)!;
    }

    [Fact]
    public async Task Chain_returns_expected_counts_per_call()
    {
        var c = await RunChainAsync();
        var m = StackFixture.Options.TargetMembers;
        var l = StackFixture.Options.LoansPerTarget;
        Assert.Equal(m, c.Members.Count);
        Assert.Equal(m * l, c.Loans.Count);
        Assert.Equal(m * l, c.Books.Count); // distinct books by O-8
    }

    [Fact]
    public async Task Book_id_list_for_call3_is_sorted_even_though_loans_are_not_book_ordered()
    {
        var c = await RunChainAsync();
        var raw = c.Loans.Select(l => l.BookId).ToList();
        Assert.NotEqual(raw.Order(), raw); // demonstrates why the plan must sort (fixture shuffles target books)
        Assert.Equal(c.Books.Select(b => b.BookId).Order(), c.Books.Select(b => b.BookId));
    }

    [Fact]
    public async Task Chain_joins_to_the_same_relationships_as_the_dataset()
    {
        var c = await RunChainAsync();
        foreach (var loan in c.Loans)
        {
            var expected = f.Data.Loans.Single(x => x.LoanId == loan.LoanId);
            Assert.Equal(expected, loan);
            Assert.Contains(c.Books, b => b.BookId == loan.BookId);
        }
    }

    [Fact]
    public async Task Invalid_id_list_in_step2_fails_the_chain()
    {
        var r = await f.RestClient.SendAsync(StackFixture.Get("/api/loans?memberIds=__NONE__"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }
}
