using Lms.Data.Dataset;

namespace Lms.UnitTests;

public class Rfc4180ReaderTests
{
    [Fact]
    public void Quoted_fields_with_separators_quotes_and_line_breaks()
    {
        var rows = Rfc4180Reader.Read("a,\"b,c\",\"say \"\"hi\"\"\",\"line1\r\nline2\"\r\nx,,z\r\n").ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(["a", "b,c", "say \"hi\"", "line1\r\nline2"], rows[0]);
        Assert.Equal(["x", "", "z"], rows[1]);
    }

    [Fact]
    public void Lf_endings_and_missing_final_newline()
    {
        Assert.Equal([["1", "2"], ["3", "4"]], Rfc4180Reader.Read("1,2\n3,4").ToList());
        Assert.Equal([["", ""]], Rfc4180Reader.Read(",\r\n").ToList());
    }

    [Theory]
    [InlineData("a,\"unterminated")]
    [InlineData("a,\"x\"y\r\n")]
    [InlineData("a,b\"c\r\n")]
    public void Malformed_quoting_is_an_error(string text) =>
        Assert.Throws<InvalidDataException>(() => Rfc4180Reader.Read(text).ToList());
}
