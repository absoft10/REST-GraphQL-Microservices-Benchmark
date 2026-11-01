using System.Text.Json;
using Lms.RequiredBytes;

namespace Lms.UnitTests;

/// <summary>The code's query texts and required fields must equal the committed contract files (docs/api-contracts).</summary>
public class ContractConformanceTests
{
    private static string ContractsDir()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "docs", "api-contracts"))) d = d.Parent;
        return Path.Combine(d?.FullName ?? throw new DirectoryNotFoundException("docs/api-contracts not found"), "docs", "api-contracts");
    }

    [Theory]
    [InlineData("FlatBooks.graphql", GraphQLDocuments.FlatBooks)]
    [InlineData("NestedMembers.graphql", GraphQLDocuments.NestedMembers)]
    public void Transmitted_query_text_equals_canonicalized_committed_document(string file, string expected) =>
        Assert.Equal(expected, GraphQLDocuments.Canonicalize(File.ReadAllText(Path.Combine(ContractsDir(), "queries", file))));

    [Fact]
    public void Crir_schemas_list_exactly_the_required_fields()
    {
        using var flat = JsonDocument.Parse(File.ReadAllText(Path.Combine(ContractsDir(), "crir-flat.schema.json")));
        Assert.Equal(RequiredFields.Flat, flat.RootElement.GetProperty("items").GetProperty("required").EnumerateArray().Select(e => e.GetString()));

        using var nested = JsonDocument.Parse(File.ReadAllText(Path.Combine(ContractsDir(), "crir-nested.schema.json")));
        var member = nested.RootElement.GetProperty("items");
        Assert.Equal([.. RequiredFields.Member, "loans"], member.GetProperty("required").EnumerateArray().Select(e => e.GetString()));
        var loan = member.GetProperty("properties").GetProperty("loans").GetProperty("items");
        Assert.Equal([.. RequiredFields.Loan, "book"], loan.GetProperty("required").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(RequiredFields.NestedBook, loan.GetProperty("properties").GetProperty("book").GetProperty("required").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void Graphql_request_body_has_exactly_query_operationName_variables()
    {
        var body = GraphQLDocuments.RequestBody(GraphQLDocuments.FlatBooks, "FlatBooks", 0, 100);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(["query", "operationName", "variables"], doc.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("""{"offset":0,"limit":100}""", doc.RootElement.GetProperty("variables").GetRawText());
    }
}
