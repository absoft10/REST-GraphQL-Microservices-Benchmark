using System.IO.Compression;
using System.Text;
using Lms.Data.Dataset;

namespace Lms.UnitTests;

/// <summary>Rules R1–R8 (docs/milestone2/09 §6, DECISION-009) on synthetic CSV inputs; no research data is used.</summary>
public class KaggleBooksImporterTests
{
    private const string Header = "Title,Authors,Description,Category,Publisher,Price Starting With ($),Publish Date (Month),Publish Date (Year)\r\n";
    private static byte[] Csv(params string[] rows) => new UTF8Encoding(false).GetBytes(Header + string.Concat(rows.Select(r => r + "\r\n")));

    [Fact]
    public void Ids_are_one_based_file_order_and_all_rows_are_kept()
    {
        var (books, stats) = KaggleBooksImporter.Parse(Csv(
            "B,By X,,,,1.00,May,2001",
            "A,By X,,,,1.00,May,2001",  // same title/author as a distinct listing: kept
            "A,By X,,,,1.00,May,2001")); // even an identical row is kept (R7: no de-duplication)
        Assert.Equal([1, 2, 3], books.Select(b => b.BookId));
        Assert.Equal(3, stats.SourceRows);
    }

    [Fact]
    public void Nfc_then_trim_unicode_whitespace_but_keep_internal_whitespace()
    {
        var decomposed = "Café";
        var (books, stats) = KaggleBooksImporter.Parse(Csv($"\"  {decomposed}  two  spaces  \",By Y,,\" Fiction , General \",P,2.50,June,1999"));
        var b = books.Single();
        Assert.Equal("Café  two  spaces", b.Title);
        Assert.Equal("Fiction , General", b.Category);   // DS-6: trim only, hierarchy string kept
        Assert.Equal(2, stats.ValuesChangedByNormalization);
    }

    [Fact]
    public void Empty_optional_values_become_null_and_counts_are_reported()
    {
        var (books, stats) = KaggleBooksImporter.Parse(Csv("T,By Z,  ,, ,3.00,,"));
        var b = books.Single();
        Assert.Null(b.Description); Assert.Null(b.Category); Assert.Null(b.Publisher); Assert.Null(b.PublishMonth); Assert.Null(b.PublishYear);
        Assert.Equal((1, 1, 1, 1, 1), (stats.NullDescription, stats.NullCategory, stats.NullPublisher, stats.NullPublishMonth, stats.NullPublishYear));
    }

    [Fact]
    public void Author_placeholder_html_entities_and_mojibake_are_preserved_verbatim()
    {
        var (books, stats) = KaggleBooksImporter.Parse(Csv("\"Love <i>you</i> &#151; now\",By,\"don&#39;t â€” stop\",,,4.99,January,2003"));
        var b = books.Single();
        Assert.Equal("By", b.Authors);                                   // DS-5 option A
        Assert.Equal("Love <i>you</i> &#151; now", b.Title);             // DS-7: no HTML decoding
        Assert.Equal("don&#39;t â€” stop", b.Description);              // DS-7: no mojibake repair
        Assert.Equal(1, stats.AuthorsBarePlaceholderBy);
    }

    [Theory]
    [InlineData("5.0", "5.00")]
    [InlineData("1095.0", "1095.00")]
    [InlineData("4.89", "4.89")]
    public void Prices_are_exact_with_scale_two(string token, string expected)
    {
        var (books, _) = KaggleBooksImporter.Parse(Csv($"T,By A,,,,{token},May,2000"));
        Assert.Equal(expected, books.Single().Price!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("T,By A,,,,5.123,May,2000")]      // would need rounding (R5)
    [InlineData("T,By A,,,,\"1,095.00\",May,2000")] // not a plain decimal
    [InlineData("T,By A,,,,-1.00,May,2000")]      // sign not allowed
    [InlineData("T,By A,,,,1.00,May,20.5")]       // year not an integer
    [InlineData("T,By A,,,,1.00,May,40000")]      // year exceeds SMALLINT
    [InlineData(",By A,,,,1.00,May,2000")]        // required title empty
    [InlineData("T,  ,,,,1.00,May,2000")]         // required authors empty
    [InlineData("T,By A,,,,1.00,May")]            // field count mismatch
    public void Invalid_rows_are_hard_errors(string row) =>
        Assert.Throws<InvalidDataException>(() => KaggleBooksImporter.Parse(Csv(row)));

    [Fact]
    public void Header_bom_and_utf8_are_strict()
    {
        Assert.Throws<InvalidDataException>(() => KaggleBooksImporter.Parse(Encoding.UTF8.GetBytes("Title,Authors\r\nT,A\r\n")));
        Assert.Throws<InvalidDataException>(() => KaggleBooksImporter.Parse([0xEF, 0xBB, 0xBF, .. Csv("T,By A,,,,1.00,May,2000")]));
        var bad = Csv("T,By A,,,,1.00,May,2000");
        bad[Header.Length] = 0xC3; // truncated multi-byte sequence
        Assert.ThrowsAny<Exception>(() => KaggleBooksImporter.Parse(bad));
    }

    [Fact]
    public void Archive_with_unpinned_hash_is_rejected()
    {
        var path = Path.Combine(Path.GetTempPath(), "lms-unpinned-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            using (var z = ZipFile.Open(path, ZipArchiveMode.Create))
            using (var w = new StreamWriter(z.CreateEntry(KaggleBooksSource.EntryName).Open()))
                w.Write(Header + "T,By A,,,,1.00,May,2000\r\n");
            var ex = Assert.Throws<InvalidDataException>(() => KaggleBooksImporter.Import(path));
            Assert.Contains("does not match the pinned", ex.Message);
        }
        finally { File.Delete(path); }
    }
}
