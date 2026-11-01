using System.Text.Json;
using Lms.Contracts;
using Lms.Hosting;

namespace Lms.UnitTests;

/// <summary>SER-1..SER-5 for the System.Text.Json side of policy SP-1 (DECISION-005). Synthetic fixtures only.</summary>
public class SerializationPolicyTests
{
    private static string Ser<T>(T v) => JsonSerializer.Serialize(v, BenchmarkJson.Options);

    [Fact]
    public void Names_are_camelCase_and_match_contract_order()
    {
        var json = Ser(new BookDto(1, "t", "a", null, null, null, null, null, null));
        var names = JsonDocument.Parse(json).RootElement.EnumerateObject().Select(p => p.Name);
        Assert.Equal(["bookId", "title", "authors", "description", "category", "publisher", "price", "publishMonth", "publishYear"], names);
        var loanNames = JsonDocument.Parse(Ser(new LoanDto(1, 2, 3, new DateOnly(2026, 1, 2), null, false))).RootElement.EnumerateObject().Select(p => p.Name);
        Assert.Equal(["loanId", "memberId", "bookId", "loanDate", "returnDate", "isReturned"], loanNames);
        var memberNames = JsonDocument.Parse(Ser(new MemberDto(1, "f", "l", "e", "p", "a"))).RootElement.EnumerateObject().Select(p => p.Name);
        Assert.Equal(["memberId", "firstName", "lastName", "email", "phoneNumber", "address"], memberNames);
    }

    [Fact]
    public void Nulls_are_written_explicitly() =>
        Assert.Equal("""{"bookId":1,"title":"t","authors":"a","description":null,"category":null,"publisher":null,"price":null,"publishMonth":null,"publishYear":null}""",
            Ser(new BookDto(1, "t", "a", null, null, null, null, null, null)));

    [Theory]
    [InlineData("0.00", "0.00")]
    [InlineData("12.50", "12.50")]
    [InlineData("99999999.99", "99999999.99")]
    public void Decimals_keep_stored_scale(string value, string expected) =>
        Assert.Equal(expected, Ser(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Dates_are_iso_calendar_dates()
    {
        Assert.Equal("\"2024-02-29\"", Ser(new DateOnly(2024, 2, 29)));
        Assert.Equal("\"0001-01-01\"", Ser(DateOnly.MinValue));
        Assert.Equal("\"9999-12-31\"", Ser(DateOnly.MaxValue));
    }

    [Fact]
    public void Non_ascii_is_emitted_as_utf8_and_html_sensitive_characters_are_escaped()
    {
        Assert.Equal("\"Café 日本語 Straße\"", Ser("Café 日本語 Straße"));
        var html = Ser("<b>&'\"+`");
        Assert.DoesNotContain("<", html);
        Assert.DoesNotContain("&", html);
        Assert.DoesNotContain("'", html);
        Assert.Contains("\\u003C", html);
    }

    [Fact]
    public void Control_characters_and_backslash_are_escaped() =>
        Assert.Equal("\"a\\nb\\\\c\\u0001\"", Ser("a\nb\\c\u0001"));

    [Fact]
    public void Output_is_compact() => Assert.DoesNotContain(" ", Ser(new MemberDto(1, "f", "l", "e", "p", "a")));
}
