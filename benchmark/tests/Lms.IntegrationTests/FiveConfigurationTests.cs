using Lms.Contracts;
using Lms.RequiredBytes;

namespace Lms.IntegrationTests;

/// <summary>
/// All five configurations (F-R, F-G, N-RC, N-RM, N-G): M2 §2.3 equivalence against the dataset oracle,
/// Required/Actual Bytes, decomposition, compactness, C-6 keys, and internal service-call counts.
/// </summary>
[Collection(StackCollection.Name)]
public sealed class FiveConfigurationTests(StackFixture f)
{
    private Task<EquivalenceResult> Run(int offset = 0) =>
        EquivalenceRunner.RunAsync(f.RestClient, f.GraphQLClient, f.Data.Books, f.Data.Members, f.Data.Loans,
            StackFixture.FlatLimit, StackFixture.NestedLimit, offset);

    [Fact]
    public async Task Every_configuration_returns_the_oracle_required_information()
    {
        var r = await Run();
        Assert.All(r.Configs, c =>
        {
            Assert.Null(c.Failure);
            Assert.True(c.EquivalentToOracle, $"{c.Config} differs from oracle");
        });
        Assert.True(r.Passed);
    }

    [Fact]
    public async Task Required_bytes_identical_within_workload_and_actual_never_below_required()
    {
        var r = await Run();
        var flat = r.Configs.Where(c => ConfigNames.WorkloadOf(c.Config) == Workload.Flat).Select(c => c.Bytes.RequiredBytes).Distinct();
        var nested = r.Configs.Where(c => ConfigNames.WorkloadOf(c.Config) == Workload.Nested).Select(c => c.Bytes.RequiredBytes).Distinct();
        Assert.Single(flat);
        Assert.Single(nested);
        Assert.All(r.Configs, c => Assert.True(c.Bytes.ActualBytes >= c.Bytes.RequiredBytes, $"{c.Config}"));
        Assert.All(r.Configs, c => Assert.True(c.Bytes.SumsExactly, $"{c.Config} decomposition"));
        Assert.All(r.Configs, c => Assert.True(c.AllCompact, $"{c.Config} not compact"));
    }

    [Fact]
    public async Task Envelope_only_for_graphql_and_multicall_overhead_only_for_nrm()
    {
        var r = await Run();
        foreach (var c in r.Configs)
        {
            Assert.Equal(c.Config is ConfigId.FG or ConfigId.NG, c.Bytes.Envelope > 0);
            Assert.Equal(c.Config == ConfigId.NRM, c.Bytes.MultiCall != 0);
            Assert.Equal(c.Config is ConfigId.FG or ConfigId.NG, c.Bytes.NonRequired == 0);
        }
    }

    [Fact]
    public async Task Serializer_formatting_delta_is_identical_across_rest_and_graphql()
    {
        // SP-1 aim: shared fields are serialized to the same tokens in both stacks, so the formatting
        // term (service tokens vs JCS) must be equal within a workload. Response sizes are NOT equalized.
        var r = await Run();
        var flat = r.Configs.Where(c => ConfigNames.WorkloadOf(c.Config) == Workload.Flat).Select(c => c.Bytes.Formatting).Distinct();
        var nested = r.Configs.Where(c => ConfigNames.WorkloadOf(c.Config) == Workload.Nested).Select(c => c.Bytes.Formatting).Distinct();
        Assert.Single(flat);
        Assert.Single(nested);
    }

    [Fact]
    public async Task Pagination_semantics_hold_at_nonzero_offset()
    {
        var r = await Run(offset: 3);
        Assert.True(r.Passed);
    }

    [Fact]
    public async Task C6_key_lists_are_identical_within_each_workload()
    {
        var r = await Run();
        Assert.Single(r.Configs.Where(c => ConfigNames.WorkloadOf(c.Config) == Workload.Flat).Select(c => c.NormalizedJcs).Distinct());
        Assert.Single(r.Configs.Where(c => ConfigNames.WorkloadOf(c.Config) == Workload.Nested).Select(c => c.NormalizedJcs).Distinct());
    }

    // ---- internal service-call counts (internal-domain-api.md §2) ----

    private string NestedBooksCall()
    {
        var bookIds = f.Data.Loans.Where(l => l.MemberId <= StackFixture.NestedLimit).Select(l => l.BookId);
        return "/internal/books?ids=" + QueryParsing.FormatIdList(bookIds);
    }

    private static readonly string FlatCall = $"/internal/books?offset=0&limit={StackFixture.FlatLimit}";
    private static readonly string MembersCall = $"/internal/members?offset=0&limit={StackFixture.NestedLimit}";
    private static readonly string LoansCall = "/internal/loans?memberIds=" + string.Join(',', Enumerable.Range(1, StackFixture.NestedLimit));

    [Fact]
    public async Task Flat_configurations_make_exactly_one_identical_internal_call()
    {
        f.RestCalls.Reset();
        await f.RestClient.SendAsync(StackFixture.Get($"/api/books?offset=0&limit={StackFixture.FlatLimit}"));
        Assert.Equal([FlatCall], f.RestCalls.Calls);

        f.GraphQLCalls.Reset();
        await f.PostGraphQLAsync(GraphQLDocuments.FlatBooks, "FlatBooks", $$"""{"offset":0,"limit":{{StackFixture.FlatLimit}}}""");
        Assert.Equal([FlatCall], f.GraphQLCalls.Calls);
    }

    [Fact]
    public async Task Nested_configurations_make_the_same_three_internal_calls()
    {
        string[] expected = [MembersCall, LoansCall, NestedBooksCall()];

        f.RestCalls.Reset();
        await f.RestClient.SendAsync(StackFixture.Get($"/api/members-with-loans?offset=0&limit={StackFixture.NestedLimit}"));
        Assert.Equal(expected, f.RestCalls.Calls);                 // N-RC

        f.GraphQLCalls.Reset();
        await f.PostGraphQLAsync(GraphQLDocuments.NestedMembers, "NestedMembers", $$"""{"offset":0,"limit":{{StackFixture.NestedLimit}}}""");
        Assert.Equal(expected, f.GraphQLCalls.Calls);              // N-G (DataLoaders batch one call per level)

        f.RestCalls.Reset();
        await new NestedMultiCallWorkflowTests(f).RunChainAsync(); // N-RM through the public routes
        Assert.Equal(expected, f.RestCalls.Calls);
    }
}
