using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Lms.Data.Dataset;

public sealed record VerificationCheck(string Name, bool Passed, string Detail);

public sealed record VerificationReport(
    string Label,
    string CheckedUtc,
    bool Passed,
    IReadOnlyList<VerificationCheck> Checks,
    IReadOnlyDictionary<string, string> Environment);

/// <summary>
/// Data-freeze verification (doc 03 §2.4, AD-36): content hashes equal the manifest (= generator), row counts,
/// cross-database orphan checks, target-set invariants (O-8), schema fingerprint and dataset provenance.
/// Replaces CHECKSUM_AGG. Read-only.
/// </summary>
public static class DataFreezeVerifier
{
    public static async Task<VerificationReport> VerifyAsync(DatabaseSet dbs, DatasetManifest manifest, CancellationToken ct = default)
    {
        var checks = new List<VerificationCheck>();
        var books = await DatasetStore.ReadBooksAsync(dbs, ct);
        var members = await DatasetStore.ReadMembersAsync(dbs, ct);
        var loans = await DatasetStore.ReadLoansAsync(dbs, ct);

        void Hash(string table, string actual, long count)
        {
            var t = manifest.Table(table);
            checks.Add(new($"content-hash {table}", actual == t.CanonicalSha256, $"db={actual} manifest={t.CanonicalSha256}"));
            checks.Add(new($"row-count {table}", count == t.RowCount, $"db={count} manifest={t.RowCount}"));
        }
        Hash(DatasetArtifacts.BooksTable, CanonicalEncoding.HashBooks(books), books.Count);
        Hash(DatasetArtifacts.MembersTable, CanonicalEncoding.HashMembers(members), members.Count);
        Hash(DatasetArtifacts.LoansTable, CanonicalEncoding.HashLoans(loans), loans.Count);

        // COUNT_BIG cross-check (independent of the reader path).
        checks.Add(await CountCheck(dbs.CatalogConnectionString, DatasetArtifacts.BooksTable, books.Count, ct));
        checks.Add(await CountCheck(dbs.MembersConnectionString, DatasetArtifacts.MembersTable, members.Count, ct));
        checks.Add(await CountCheck(dbs.LoansConnectionString, DatasetArtifacts.LoansTable, loans.Count, ct));

        // Referential integrity across service databases (no cross-db FKs exist).
        var memberIds = members.Select(m => m.MemberId).ToHashSet();
        var bookIds = books.Select(b => b.BookId).ToHashSet();
        var orphanMembers = loans.Count(l => !memberIds.Contains(l.MemberId));
        var orphanBooks = loans.Count(l => !bookIds.Contains(l.BookId));
        checks.Add(new("orphans loan.member_id", orphanMembers == 0, $"count={orphanMembers}"));
        checks.Add(new("orphans loan.book_id", orphanBooks == 0, $"count={orphanBooks}"));
        var badReturn = loans.Count(l => l.IsReturned != l.ReturnDate.HasValue);
        checks.Add(new("loan is_returned/return_date consistency", badReturn == 0, $"count={badReturn}"));

        // Target-set invariants (O-8).
        var m = manifest.Options.TargetMembers;
        var l = manifest.Options.LoansPerTarget;
        var targetIds = Enumerable.Range(1, m).ToList();
        var allTargetsExist = targetIds.All(memberIds.Contains);
        checks.Add(new("target members 1..M exist", allTargetsExist, $"M={m}"));
        var targetLoans = loans.Where(x => x.MemberId <= m).OrderBy(x => x.MemberId).ThenBy(x => x.LoanId).ToList();
        var perMember = targetLoans.GroupBy(x => x.MemberId).ToDictionary(g => g.Key, g => g.ToList());
        var exactlyL = targetIds.All(id => perMember.TryGetValue(id, out var ls) && ls.Count == l);
        checks.Add(new("each target member has exactly L loans", exactlyL, $"L={l}"));
        var distinctBooks = targetLoans.Select(x => x.BookId).Distinct().Count() == targetLoans.Count;
        checks.Add(new("target loans use distinct books", distinctBooks, $"loans={targetLoans.Count}"));
        var booksPresent = targetLoans.All(x => bookIds.Contains(x.BookId));
        checks.Add(new("target books present", booksPresent, ""));
        var alternation = perMember.Values.All(ls => ls.Select((x, i) => x.IsReturned == ((i + 1) % 2 == 1)).All(ok => ok));
        checks.Add(new("target loans alternate returned/unreturned by loanId", alternation, "odd=returned, even=null"));

        // Provenance stored in each database.
        foreach (var cs in new[] { dbs.CatalogConnectionString, dbs.MembersConnectionString, dbs.LoansConnectionString })
        {
            var info = await DatasetStore.ReadDatasetInfoAsync(cs, ct);
            var ok = info.TryGetValue("label", out var lab) && lab == manifest.Label
                     && info.TryGetValue("books_source", out var src) && src == manifest.BooksSource;
            checks.Add(new($"dataset_info {new SqlConnectionStringBuilder(cs).InitialCatalog}", ok,
                $"label={info.GetValueOrDefault("label")} books_source={info.GetValueOrDefault("books_source")}"));
        }

        var env = new Dictionary<string, string>
        {
            ["server_version"] = await Scalar(dbs.MasterConnectionString, "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128))", ct),
            ["server_edition"] = await Scalar(dbs.MasterConnectionString, "SELECT CAST(SERVERPROPERTY('Edition') AS nvarchar(128))", ct),
            ["books_source"] = manifest.BooksSource,
        };
        foreach (var (name, cs) in new[] { (dbs.CatalogDb, dbs.CatalogConnectionString), (dbs.MembersDb, dbs.MembersConnectionString), (dbs.LoansDb, dbs.LoansConnectionString) })
        {
            env[$"{name}.collation"] = await Scalar(cs, "SELECT CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS nvarchar(128))", ct);
            env[$"{name}.compatibility_level"] = await Scalar(cs, "SELECT CAST(compatibility_level AS nvarchar(10)) FROM sys.databases WHERE name = DB_NAME()", ct);
            env[$"{name}.schema_sha256"] = await SchemaFingerprint(cs, ct);
        }
        checks.Add(new("compatibility level pinned", env.Where(kv => kv.Key.EndsWith(".compatibility_level", StringComparison.Ordinal))
            .All(kv => kv.Value == PersistenceRegistration.CompatibilityLevel.ToString(CultureInfo.InvariantCulture)), "expected 160"));

        return new VerificationReport(manifest.Label, DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            checks.All(c => c.Passed), checks, env);
    }

    private static async Task<VerificationCheck> CountCheck(string cs, string table, long expected, CancellationToken ct)
    {
        var v = long.Parse(await Scalar(cs, $"SELECT CAST(COUNT_BIG(*) AS nvarchar(20)) FROM {table}", ct), CultureInfo.InvariantCulture);
        return new($"count_big {table}", v == expected, $"count_big={v}");
    }

    private static async Task<string> Scalar(string cs, string sql, CancellationToken ct)
    {
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        return (await cmd.ExecuteScalarAsync(ct))?.ToString() ?? "";
    }

    /// <summary>SHA-256 over ordered column definitions and index definitions of the user tables.</summary>
    private static async Task<string> SchemaFingerprint(string cs, CancellationToken ct)
    {
        const string sql = """
            SELECT CONCAT(c.TABLE_SCHEMA,'.',c.TABLE_NAME,'.',c.COLUMN_NAME,'|',c.ORDINAL_POSITION,'|',c.DATA_TYPE,'|',
                          COALESCE(CAST(c.CHARACTER_MAXIMUM_LENGTH AS varchar(20)),''),'|',COALESCE(CAST(c.NUMERIC_PRECISION AS varchar(10)),''),'|',
                          COALESCE(CAST(c.NUMERIC_SCALE AS varchar(10)),''),'|',c.IS_NULLABLE)
            FROM INFORMATION_SCHEMA.COLUMNS c ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION;
            SELECT CONCAT(SCHEMA_NAME(t.schema_id) COLLATE DATABASE_DEFAULT,'.',t.name COLLATE DATABASE_DEFAULT,'|',i.name COLLATE DATABASE_DEFAULT,'|',
                          i.type_desc COLLATE DATABASE_DEFAULT,'|',ic.key_ordinal,'|',COL_NAME(ic.object_id, ic.column_id) COLLATE DATABASE_DEFAULT,'|',ic.is_included_column)
            FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            ORDER BY t.name, i.name, ic.is_included_column, ic.key_ordinal, COL_NAME(ic.object_id, ic.column_id);
            """;
        var sb = new StringBuilder();
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        do { while (await r.ReadAsync(ct)) sb.Append(r.GetString(0)).Append('\n'); } while (await r.NextResultAsync(ct));
        return CanonicalEncoding.Sha256Hex(Encoding.UTF8.GetBytes(sb.ToString()));
    }
}
