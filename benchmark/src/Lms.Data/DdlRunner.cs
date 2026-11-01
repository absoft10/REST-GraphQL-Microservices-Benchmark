using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Lms.Data;

/// <summary>Executes the committed DDL scripts (benchmark/data/ddl) with sqlcmd-style $(Var) substitution and GO batching.</summary>
public static partial class DdlRunner
{
    public static string DdlDirectory => Path.Combine(AppContext.BaseDirectory, "ddl");

    public static async Task InitializeAsync(DatabaseSet dbs, CancellationToken ct = default)
    {
        await RunFileAsync(dbs.MasterConnectionString, "00_create_databases.sql", dbs, ct);
        await RunFileAsync(dbs.CatalogConnectionString, "10_catalog.sql", dbs, ct);
        await RunFileAsync(dbs.MembersConnectionString, "20_members.sql", dbs, ct);
        await RunFileAsync(dbs.LoansConnectionString, "30_loans.sql", dbs, ct);
    }

    public static Task DropAsync(DatabaseSet dbs, CancellationToken ct = default) =>
        RunFileAsync(dbs.MasterConnectionString, "99_drop_databases.sql", dbs, ct);

    public static async Task RunFileAsync(string connectionString, string fileName, DatabaseSet dbs, CancellationToken ct)
    {
        var text = await File.ReadAllTextAsync(Path.Combine(DdlDirectory, fileName), ct);
        foreach (var (name, value) in dbs.SqlCmdVariables)
            text = text.Replace($"$({name})", value, StringComparison.Ordinal);
        if (text.Contains("$(", StringComparison.Ordinal))
            throw new InvalidOperationException($"Unresolved sqlcmd variable in {fileName}.");

        SqlConnection.ClearAllPools();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        foreach (var batch in GoSeparator().Split(text))
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            await using var cmd = new SqlCommand(batch, conn) { CommandTimeout = 120 };
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();
}
