using Lms.Contracts;

namespace Lms.UnitTests;

/// <summary>errors-and-pagination.md §2–§4.</summary>
public class QueryParsingTests
{
    [Theory]
    [InlineData("0", "1", 0, 1)]
    [InlineData("5", "1000", 5, 1000)]
    public void Paging_accepts_valid_values(string o, string l, int eo, int el)
    {
        Assert.True(QueryParsing.TryParsePaging(o, l, out var p, out _));
        Assert.Equal(new Paging(eo, el), p);
    }

    [Theory]
    [InlineData(null, "10")]
    [InlineData("0", null)]
    [InlineData("-1", "10")]
    [InlineData("0", "0")]
    [InlineData("0", "1001")]
    [InlineData("+1", "10")]
    [InlineData(" 1", "10")]
    [InlineData("1.0", "10")]
    [InlineData("0", "99999999999")]
    public void Paging_rejects_invalid_values(string? o, string? l) =>
        Assert.False(QueryParsing.TryParsePaging(o, l, out _, out _));

    [Fact]
    public void Id_list_is_sorted_and_deduplicated()
    {
        Assert.True(QueryParsing.TryParseIdList("42,3,17,3", out var ids, out _));
        Assert.Equal([3, 17, 42], ids);
        Assert.Equal("3,17,42", QueryParsing.FormatIdList([42, 3, 17, 3]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1,,2")]
    [InlineData("1, 2")]
    [InlineData("1,2,")]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("a")]
    [InlineData("2147483648")]
    public void Id_list_rejects_malformed_input(string value) =>
        Assert.False(QueryParsing.TryParseIdList(value, out _, out _));

    [Fact]
    public void Id_list_limit_is_1000()
    {
        Assert.True(QueryParsing.TryParseIdList(string.Join(',', Enumerable.Range(1, 1000)), out _, out _));
        Assert.False(QueryParsing.TryParseIdList(string.Join(',', Enumerable.Range(1, 1001)), out _, out _));
    }
}
