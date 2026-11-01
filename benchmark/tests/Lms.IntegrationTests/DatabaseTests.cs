using Lms.Data;
using Lms.Data.Dataset;
using Microsoft.Data.SqlClient;

namespace Lms.IntegrationTests;

/// <summary>Database consistency against real LocalDB (doc 03 §2.4; DECISION-004).</summary>
[Collection(StackCollection.Name)]
public sealed class DatabaseTests(StackFixture f)
{
    [Fact]
    public async Task Data_freeze_verification_passes_every_check()
    {
        var report = await DataFreezeVerifier.VerifyAsync(f.Databases, f.Manifest);
        Assert.All(report.Checks, c => Assert.True(c.Passed, $"{c.Name}: {c.Detail}"));
        Assert.True(report.Passed);
    }

    [Fact]
    public async Task Database_content_hashes_equal_generator_hashes()
    {
        Assert.Equal(CanonicalEncoding.HashBooks(f.Data.Books), CanonicalEncoding.HashBooks(await DatasetStore.ReadBooksAsync(f.Databases)));
        Assert.Equal(CanonicalEncoding.HashMembers(f.Data.Members), CanonicalEncoding.HashMembers(await DatasetStore.ReadMembersAsync(f.Databases)));
        Assert.Equal(CanonicalEncoding.HashLoans(f.Data.Loans), CanonicalEncoding.HashLoans(await DatasetStore.ReadLoansAsync(f.Databases)));
    }

    [Fact]
    public async Task Tampering_is_detected_by_content_hash()
    {
        var other = Manifest(f.Manifest, DatasetArtifacts.MembersTable, "0000");
        var report = await DataFreezeVerifier.VerifyAsync(f.Databases, other);
        Assert.False(report.Passed);
        Assert.Contains(report.Checks, c => c.Name == "content-hash members.member" && !c.Passed);
    }

    [Fact]
    public async Task Ddl_is_idempotent_and_databases_are_service_owned()
    {
        await DdlRunner.InitializeAsync(f.Databases); // second run must be a no-op
        foreach (var (cs, table) in new[] { (f.Databases.CatalogConnectionString, "catalog.book"), (f.Databases.MembersConnectionString, "members.member"), (f.Databases.LoansConnectionString, "loans.loan") })
        {
            await using var c = new SqlConnection(cs);
            await c.OpenAsync();
            await using var cmd = new SqlCommand("SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0 AND SCHEMA_NAME(schema_id) <> 'meta'", c);
            Assert.Equal(1, (int)(await cmd.ExecuteScalarAsync())!); // exactly one domain table per service database
            await using var cnt = new SqlCommand($"SELECT COUNT_BIG(*) FROM {table}", c);
            Assert.True((long)(await cnt.ExecuteScalarAsync())! > 0);
        }
    }

    [Fact]
    public async Task Return_date_check_constraint_is_enforced()
    {
        await using var c = new SqlConnection(f.Databases.LoansConnectionString);
        await c.OpenAsync();
        await using var cmd = new SqlCommand("INSERT INTO loans.loan VALUES (999999, 1, 1, '2025-01-01', NULL, 1)", c);
        await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
    }

    private static DatasetManifest Manifest(DatasetManifest m, string table, string hash) =>
        m with { Tables = [.. m.Tables.Select(t => t.Table == table ? t with { CanonicalSha256 = hash } : t)] };
}
