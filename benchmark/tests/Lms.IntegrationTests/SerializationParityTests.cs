using System.Text.Json;
using Lms.RequiredBytes;

namespace Lms.IntegrationTests;

/// <summary>
/// SER-1/SER-2/SER-5 across stacks (DECISION-005): every shared field value is serialized to the identical JSON
/// token by the REST edge (System.Text.Json) and the GraphQL edge (HotChocolate). Whole bodies are NOT equalized.
/// </summary>
[Collection(StackCollection.Name)]
public sealed class SerializationParityTests(StackFixture f)
{
    [Fact]
    public async Task Shared_book_fields_have_identical_tokens_in_rest_and_graphql()
    {
        var rest = await (await f.RestClient.SendAsync(StackFixture.Get("/api/books?offset=0&limit=300"))).Content.ReadAsStringAsync();
        var gql = await (await f.PostGraphQLAsync(GraphQLDocuments.FlatBooks, "FlatBooks", """{"offset":0,"limit":300}""")).Content.ReadAsStringAsync();
        using var r = JsonDocument.Parse(rest);
        using var g = JsonDocument.Parse(gql);
        var restBooks = r.RootElement.EnumerateArray().ToList();
        var gqlBooks = g.RootElement.GetProperty("data").GetProperty("books").EnumerateArray().ToList();
        Assert.Equal(300, restBooks.Count);
        Assert.Equal(restBooks.Count, gqlBooks.Count);
        var mismatches = new List<string>();
        for (var i = 0; i < restBooks.Count; i++)
            foreach (var field in RequiredFields.Flat)
            {
                var a = restBooks[i].GetProperty(field).GetRawText();
                var b = gqlBooks[i].GetProperty(field).GetRawText();
                if (a != b) mismatches.Add($"book {i + 1}.{field}: REST {a} vs GraphQL {b}");
            }
        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches.Take(10)));
    }

    [Fact]
    public async Task Shared_loan_date_and_null_tokens_are_identical()
    {
        var rest = await (await f.RestClient.SendAsync(StackFixture.Get("/api/members-with-loans?offset=0&limit=10"))).Content.ReadAsStringAsync();
        var gql = await (await f.PostGraphQLAsync(GraphQLDocuments.NestedMembers, "NestedMembers", """{"offset":0,"limit":10}""")).Content.ReadAsStringAsync();
        using var r = JsonDocument.Parse(rest);
        using var g = JsonDocument.Parse(gql);
        var restLoans = r.RootElement.EnumerateArray().SelectMany(m => m.GetProperty("loans").EnumerateArray()).ToList();
        var gqlLoans = g.RootElement.GetProperty("data").GetProperty("members").EnumerateArray().SelectMany(m => m.GetProperty("loans").EnumerateArray()).ToList();
        Assert.Equal(restLoans.Count, gqlLoans.Count);
        for (var i = 0; i < restLoans.Count; i++)
            foreach (var field in RequiredFields.Loan)
                Assert.Equal(restLoans[i].GetProperty(field).GetRawText(), gqlLoans[i].GetProperty(field).GetRawText());
        Assert.Contains(gqlLoans, l => l.GetProperty("returnDate").GetRawText() == "null");
        Assert.All(gqlLoans, l => Assert.Matches("^\"\\d{4}-\\d{2}-\\d{2}\"$", l.GetProperty("loanDate").GetRawText()));
    }

    [Fact]
    public async Task Html_sensitive_characters_are_escaped_and_non_ascii_is_literal_in_both_stacks()
    {
        var rest = await (await f.RestClient.SendAsync(StackFixture.Get("/api/books?offset=0&limit=20"))).Content.ReadAsStringAsync();
        var gql = await (await f.PostGraphQLAsync(GraphQLDocuments.FlatBooks, "FlatBooks", """{"offset":0,"limit":20}""")).Content.ReadAsStringAsync();
        foreach (var body in new[] { rest, gql })
        {
            Assert.Contains("\\u003Cb\\u003E", body);   // "<b>" escaped
            Assert.DoesNotContain("<b>", body);
            Assert.Contains("日本語の本", body);          // non-ASCII emitted as UTF-8
            Assert.Contains("Café", body);
            Assert.True(ByteAccounting.IsCompact(body));
        }
    }
}
