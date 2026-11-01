using System.Globalization;

namespace Lms.Contracts;

/// <summary>Validation limits and parsing rules (docs/api-contracts/errors-and-pagination.md §2–§4).</summary>
public static class ApiLimits
{
    public const int MaxLimit = 1000;
    public const int MaxIds = 1000;
}

/// <summary>Problem type URIs (identifiers only; errors-and-pagination.md §4).</summary>
public static class ProblemTypes
{
    public const string Base = "https://lms-benchmark.local/errors/";
    public const string InvalidParameter = Base + "invalid-parameter";
    public const string InvalidIdList = Base + "invalid-id-list";
    public const string NotFound = Base + "not-found";
    public const string UpstreamFailure = Base + "upstream-failure";
    public const string Internal = Base + "internal";
}

/// <summary>Query identifiers used in TagWith and _dbq.csv (internal-domain-api.md §1; DECISION-007).</summary>
public static class QueryIds
{
    public const string BooksPage = "catalog.books.page";
    public const string BooksByIds = "catalog.books.ids";
    public const string MembersPage = "members.page";
    public const string LoansByMembers = "loans.byMembers";
}

public readonly record struct Paging(int Offset, int Limit);

public static class QueryParsing
{
    /// <summary>Both values are required; strict non-negative decimal digits only.</summary>
    public static bool TryParsePaging(string? offset, string? limit, out Paging paging, out string error)
    {
        paging = default;
        if (!TryParseStrictInt(offset, out var o)) { error = "offset is required and must be an integer >= 0"; return false; }
        if (!TryParseStrictInt(limit, out var l)) { error = $"limit is required and must be an integer between 1 and {ApiLimits.MaxLimit}"; return false; }
        return TryValidatePaging(o, l, out paging, out error);
    }

    public static bool TryValidatePaging(int offset, int limit, out Paging paging, out string error)
    {
        paging = default;
        if (offset < 0) { error = "offset must be >= 0"; return false; }
        if (limit < 1 || limit > ApiLimits.MaxLimit) { error = $"limit must be between 1 and {ApiLimits.MaxLimit}"; return false; }
        paging = new Paging(offset, limit);
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Comma-separated positive 32-bit integers, 1..MaxIds items, no spaces, no empty items.
    /// Returns the ids de-duplicated and sorted ascending (errors-and-pagination.md §3).
    /// </summary>
    public static bool TryParseIdList(string? value, out int[] ids, out string error)
    {
        ids = [];
        if (string.IsNullOrEmpty(value)) { error = "id list is required"; return false; }
        var parts = value.Split(',');
        if (parts.Length > ApiLimits.MaxIds) { error = $"at most {ApiLimits.MaxIds} ids are allowed"; return false; }
        var parsed = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!TryParseStrictInt(parts[i], out var id) || id < 1)
            {
                error = "ids must be comma-separated positive integers";
                return false;
            }
            parsed[i] = id;
        }
        ids = NormalizeIds(parsed);
        error = string.Empty;
        return true;
    }

    /// <summary>Sorted ascending, de-duplicated.</summary>
    public static int[] NormalizeIds(IEnumerable<int> ids)
    {
        var set = new SortedSet<int>(ids);
        return [.. set];
    }

    public static string FormatIdList(IEnumerable<int> ids) =>
        string.Join(',', NormalizeIds(ids).Select(i => i.ToString(CultureInfo.InvariantCulture)));

    private static bool TryParseStrictInt(string? s, out int value)
    {
        value = 0;
        if (string.IsNullOrEmpty(s) || s.Length > 10) return false;
        foreach (var c in s) if (c is < '0' or > '9') return false;
        return int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
