using System.Text.Json.Nodes;
using Lms.RequiredBytes;

namespace Lms.UnitTests;

/// <summary>RFC 8785 behaviour within the CRIR domain (required-bytes-spec.md §6).</summary>
public class JcsTests
{
    [Fact]
    public void Rfc8785_string_and_literal_vector()
    {
        // String and literals part of the RFC 8785 §3.2.3 example (the numbers part uses doubles outside the CRIR domain).
        // Built from explicit \uXXXX fragments so the vector is exactly the RFC text: "€$\u000F\u000aA'B"\\\\"\/"
        static string U(string hex) => "\\u" + hex;
        var json = "{\"string\":\"" + U("20ac") + "$" + U("000F") + U("000a") + "A'" + U("0042") + U("0022") + U("005c")
                   + "\\\\" + "\\\"" + "\\/" + "\",\"literals\":[null,true,false]}";
        var input = JsonNode.Parse(json);
        Assert.Equal("""{"literals":[null,true,false],"string":"€$\u000f\nA'B\"\\\\\"/"}""", Jcs.Serialize(input));
    }

    [Fact]
    public void Keys_are_sorted_by_utf16_code_units()
    {
        var input = JsonNode.Parse("""{"b":1,"a":2,"B":3,"aa":4,"é":5,"😀":6}""");
        // 'B'(0x42) < 'a' < 'aa' < 'b' < 'é'(0xE9) < U+1F600 (surrogate 0xD83D)
        Assert.Equal("""{"B":3,"a":2,"aa":4,"b":1,"é":5,"😀":6}""", Jcs.Serialize(input));
    }

    [Theory]
    [InlineData("12.50", "12.5")]
    [InlineData("4.50", "4.5")]
    [InlineData("2e-3", "0.002")]
    [InlineData("100", "100")]
    [InlineData("0.00", "0")]
    [InlineData("-0", "0")]
    [InlineData("99999999.99", "99999999.99")]
    public void Numbers_use_shortest_decimal_form(string token, string expected) =>
        Assert.Equal(expected, Jcs.FormatNumber(token));

    [Theory]
    [InlineData("333333333.33333329")] // > 15 significant digits: double rounding would apply in ES6
    [InlineData("1e30")]
    [InlineData("1e-7")]
    public void Numbers_outside_crir_domain_are_rejected(string token) =>
        Assert.Throws<NotSupportedException>(() => Jcs.FormatNumber(token));

    [Fact]
    public void Control_characters_use_short_or_lowercase_unicode_escapes()
    {
        var s = Jcs.Serialize(JsonValue.Create("a\u0001\b\t\u001f"));
        Assert.Equal("\"a\\u0001\\b\\t\\u001f\"", s);
    }

    [Fact]
    public void Html_sensitive_and_non_ascii_characters_are_literal_in_jcs()
    {
        Assert.Equal("\"<b>&'+`日本📚\"", Jcs.Serialize(JsonValue.Create("<b>&'+`日本📚")));
    }

    [Fact]
    public void Lone_surrogate_is_rejected() =>
        Assert.Throws<NotSupportedException>(() => Jcs.Serialize(JsonValue.Create("x\ud800y")));
}
