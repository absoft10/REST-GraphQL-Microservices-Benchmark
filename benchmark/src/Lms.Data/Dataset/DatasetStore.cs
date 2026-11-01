using System.Data;
using Lms.Contracts;
using Microsoft.Data.SqlClient;

namespace Lms.Data.Dataset;

/// <summary>Bulk loads a generated dataset and reads tables back with plain ADO.NET (independent of the services).</summary>
public static class DatasetStore
{
    public static async Task LoadAsync(DatabaseSet dbs, GeneratedDataset data, DatasetManifest manifest, CancellationToken ct = default)
    {
        var books = new DataTable();
        foreach (var (n, t) in new[] { ("book_id", typeof(int)), ("title", typeof(string)), ("authors", typeof(string)), ("description", typeof(string)),
                     ("category", typeof(string)), ("publisher", typeof(string)), ("price", typeof(decimal)), ("publish_month", typeof(string)), ("publish_year", typeof(short)) })
            books.Columns.Add(n, t);
        foreach (var b in data.Books)
            books.Rows.Add(b.BookId, b.Title, b.Authors, N(b.Description), N(b.Category), N(b.Publisher), N(b.Price), N(b.PublishMonth),
                b.PublishYear is { } y ? (object)(short)y : DBNull.Value);

        var members = new DataTable();
        foreach (var n in new[] { "member_id", "first_name", "last_name", "email", "phone_number", "address" })
            members.Columns.Add(n, n == "member_id" ? typeof(int) : typeof(string));
        foreach (var m in data.Members) members.Rows.Add(m.MemberId, m.FirstName, m.LastName, m.Email, m.PhoneNumber, m.Address);

        var loans = new DataTable();
        foreach (var (n, t) in new[] { ("loan_id", typeof(int)), ("member_id", typeof(int)), ("book_id", typeof(int)), ("loan_date", typeof(DateTime)),
                     ("return_date", typeof(DateTime)), ("is_returned", typeof(bool)) })
            loans.Columns.Add(n, t);
        foreach (var l in data.Loans)
            loans.Rows.Add(l.LoanId, l.MemberId, l.BookId, l.LoanDate.ToDateTime(TimeOnly.MinValue),
                l.ReturnDate is { } r ? r.ToDateTime(TimeOnly.MinValue) : DBNull.Value, l.IsReturned);

        await ReplaceAsync(dbs.CatalogConnectionString, DatasetArtifacts.BooksTable, books, manifest, ct);
        await ReplaceAsync(dbs.MembersConnectionString, DatasetArtifacts.MembersTable, members, manifest, ct);
        await ReplaceAsync(dbs.LoansConnectionString, DatasetArtifacts.LoansTable, loans, manifest, ct);
    }

    private static object N(object? v) => v ?? DBNull.Value;

    private static async Task ReplaceAsync(string cs, string table, DataTable rows, DatasetManifest manifest, CancellationToken ct)
    {
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);
        await Exec(conn, tx, $"DELETE FROM {table}; DELETE FROM meta.dataset_info;", ct);
        using (var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.CheckConstraints, tx)
        { DestinationTableName = table, BatchSize = 10000, BulkCopyTimeout = 600 })
        {
            foreach (DataColumn c in rows.Columns) bulk.ColumnMappings.Add(c.ColumnName, c.ColumnName);
            await bulk.WriteToServerAsync(rows, ct);
        }
        var t = manifest.Table(table);
        var info = new Dictionary<string, string>
        {
            ["label"] = manifest.Label,
            ["books_source"] = manifest.BooksSource,
            ["generator_version"] = manifest.GeneratorVersion,
            ["seed"] = manifest.Options.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["table"] = table,
            ["row_count"] = t.RowCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["canonical_sha256"] = t.CanonicalSha256,
            ["loaded_utc"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
        };
        foreach (var (k, v) in info)
        {
            await using var cmd = new SqlCommand("INSERT INTO meta.dataset_info (info_key, info_value) VALUES (@k, @v)", conn, tx);
            cmd.Parameters.AddWithValue("@k", k);
            cmd.Parameters.AddWithValue("@v", v);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    private static async Task Exec(SqlConnection c, SqlTransaction tx, string sql, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(sql, c, tx) { CommandTimeout = 600 };
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ---- Readers (ordered by primary key; used by the verifier and as the equivalence oracle source) ----

    public static Task<List<BookDto>> ReadBooksAsync(DatabaseSet dbs, CancellationToken ct = default) =>
        ReadAsync(dbs.CatalogConnectionString,
            "SELECT book_id, title, authors, description, category, publisher, price, publish_month, publish_year FROM catalog.book ORDER BY book_id",
            r => new BookDto(r.GetInt32(0), r.GetString(1), r.GetString(2), Str(r, 3), Str(r, 4), Str(r, 5),
                r.IsDBNull(6) ? null : r.GetDecimal(6), Str(r, 7), r.IsDBNull(8) ? null : r.GetInt16(8)), ct);

    public static Task<List<MemberDto>> ReadMembersAsync(DatabaseSet dbs, CancellationToken ct = default) =>
        ReadAsync(dbs.MembersConnectionString,
            "SELECT member_id, first_name, last_name, email, phone_number, address FROM members.member ORDER BY member_id",
            r => new MemberDto(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5)), ct);

    public static Task<List<LoanDto>> ReadLoansAsync(DatabaseSet dbs, CancellationToken ct = default) =>
        ReadAsync(dbs.LoansConnectionString,
            "SELECT loan_id, member_id, book_id, loan_date, return_date, is_returned FROM loans.loan ORDER BY loan_id",
            r => new LoanDto(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), DateOnly.FromDateTime(r.GetDateTime(3)),
                r.IsDBNull(4) ? null : DateOnly.FromDateTime(r.GetDateTime(4)), r.GetBoolean(5)), ct);

    private static string? Str(SqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);

    private static async Task<List<T>> ReadAsync<T>(string cs, string sql, Func<SqlDataReader, T> map, CancellationToken ct)
    {
        var list = new List<T>();
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 600 };
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(map(r));
        return list;
    }

    public static async Task<Dictionary<string, string>> ReadDatasetInfoAsync(string cs, CancellationToken ct = default)
    {
        var d = new Dictionary<string, string>();
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT info_key, info_value FROM meta.dataset_info ORDER BY info_key", conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) d[r.GetString(0)] = r.GetString(1);
        return d;
    }
}
