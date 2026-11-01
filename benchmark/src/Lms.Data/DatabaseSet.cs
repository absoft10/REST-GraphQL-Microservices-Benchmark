using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Lms.Data;

/// <summary>
/// The three service-owned database names for one environment, e.g. prefix "lms_" → lms_catalog, lms_members, lms_loans.
/// Local default uses Windows integrated authentication against LocalDB (no secrets).
/// A full connection template may be supplied instead; it must contain "{database}".
/// </summary>
public sealed partial record DatabaseSet(string Server, string Prefix, string? ConnectionTemplate = null)
{
    public const string LocalDbServer = @"(localdb)\MSSQLLocalDB";

    public string CatalogDb => Prefix + "catalog";
    public string MembersDb => Prefix + "members";
    public string LoansDb => Prefix + "loans";

    public string ConnectionString(string database)
    {
        if (!SafeName().IsMatch(database)) throw new ArgumentException($"Unsafe database name '{database}'.");
        if (ConnectionTemplate is not null) return ConnectionTemplate.Replace("{database}", database, StringComparison.Ordinal);
        return new SqlConnectionStringBuilder
        {
            DataSource = Server,
            InitialCatalog = database,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            Encrypt = false,
        }.ConnectionString;
    }

    public string MasterConnectionString => ConnectionString("master");
    public string CatalogConnectionString => ConnectionString(CatalogDb);
    public string MembersConnectionString => ConnectionString(MembersDb);
    public string LoansConnectionString => ConnectionString(LoansDb);

    public IReadOnlyDictionary<string, string> SqlCmdVariables => new Dictionary<string, string>
    {
        ["CatalogDb"] = CatalogDb,
        ["MembersDb"] = MembersDb,
        ["LoansDb"] = LoansDb,
    };

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,99}$")]
    private static partial Regex SafeName();
}
