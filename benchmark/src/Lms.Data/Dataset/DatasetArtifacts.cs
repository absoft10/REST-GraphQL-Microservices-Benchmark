using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lms.Contracts;

namespace Lms.Data.Dataset;

public sealed record TableManifest(string Table, long RowCount, string CanonicalSha256, string CsvFile, string CsvSha256);

/// <summary>Reproducible data manifest (doc 03 §2.3). Hashes are deterministic for a given seed and options.</summary>
public sealed record DatasetManifest(
    string Label,
    string BooksSource,
    string GeneratorVersion,
    GeneratorOptions Options,
    IReadOnlyList<TableManifest> Tables,
    string CreatedUtc)
{
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.Strict,
    };

    public TableManifest Table(string name) => Tables.Single(t => t.Table == name);
}

public static class DatasetArtifacts
{
    public const string BooksTable = "catalog.book", MembersTable = "members.member", LoansTable = "loans.loan";

    /// <summary>Writes RFC 4180 CSV exports plus manifest.json into <paramref name="directory"/> and returns the manifest.</summary>
    public static DatasetManifest Write(GeneratedDataset data, string label, string directory)
    {
        Directory.CreateDirectory(directory);
        var tables = new List<TableManifest>
        {
            WriteTable(directory, BooksTable, "books.csv", CanonicalEncoding.HashBooks(data.Books), data.Books.Count,
                ["book_id", "title", "authors", "description", "category", "publisher", "price", "publish_month", "publish_year"],
                data.Books.Select(b => new[] { I(b.BookId), b.Title, b.Authors, b.Description, b.Category, b.Publisher, D(b.Price), b.PublishMonth, I(b.PublishYear) })),
            WriteTable(directory, MembersTable, "members.csv", CanonicalEncoding.HashMembers(data.Members), data.Members.Count,
                ["member_id", "first_name", "last_name", "email", "phone_number", "address"],
                data.Members.Select(m => new[] { I(m.MemberId), m.FirstName, m.LastName, m.Email, m.PhoneNumber, m.Address })),
            WriteTable(directory, LoansTable, "loans.csv", CanonicalEncoding.HashLoans(data.Loans), data.Loans.Count,
                ["loan_id", "member_id", "book_id", "loan_date", "return_date", "is_returned"],
                data.Loans.Select(l => new[] { I(l.LoanId), I(l.MemberId), I(l.BookId), Dt(l.LoanDate), Dt(l.ReturnDate), l.IsReturned ? "1" : "0" })),
        };
        var manifest = new DatasetManifest(label, data.BooksSource, DatasetGenerator.GeneratorVersion, data.Options, tables,
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(manifest, DatasetManifest.Json), new UTF8Encoding(false));
        return manifest;
    }

    public static DatasetManifest ReadManifest(string path) =>
        JsonSerializer.Deserialize<DatasetManifest>(File.ReadAllText(path), DatasetManifest.Json)
        ?? throw new InvalidDataException($"Cannot read manifest {path}.");

    private static TableManifest WriteTable(string dir, string table, string file, string canonical, int count,
        string[] header, IEnumerable<string?[]> rows)
    {
        var path = Path.Combine(dir, file);
        var sb = new StringBuilder();
        sb.Append(string.Join(',', header)).Append("\r\n");
        foreach (var r in rows) sb.Append(string.Join(',', r.Select(Csv))).Append("\r\n");
        var bytes = new UTF8Encoding(false).GetBytes(sb.ToString());
        File.WriteAllBytes(path, bytes);
        return new TableManifest(table, count, canonical, file, CanonicalEncoding.Sha256Hex(bytes));
    }

    // NULL is an empty unquoted field; empty strings are written as "" (quoted) so the two stay distinguishable.
    private static string Csv(string? v) => v is null ? "" : "\"" + v.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    private static string I(int? v) => v?.ToString(CultureInfo.InvariantCulture)!;
    private static string D(decimal? v) => v?.ToString("0.00", CultureInfo.InvariantCulture)!;
    private static string Dt(DateOnly? v) => v?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)!;
}
