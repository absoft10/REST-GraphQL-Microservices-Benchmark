using Lms.Contracts;
using Microsoft.AspNetCore.Http;

namespace Lms.Hosting;

/// <summary>
/// Query-string rules shared by the domain services and the REST edge (errors-and-pagination.md §2–§4).
/// A repeated parameter (e.g. ids=1&amp;ids=2) is rejected: the contract allows exactly one occurrence.
/// </summary>
public static class RequestQuery
{
    public enum Selection { Paging, Ids }

    /// <summary>Exactly one of {offset+limit} or {idParam} must be present.</summary>
    public static bool TrySelect(IQueryCollection q, string idParam, out Selection selection, out Paging paging, out int[] ids, out IResult? problem)
    {
        selection = default; paging = default; ids = []; problem = null;
        var hasIds = q.ContainsKey(idParam);
        var hasPaging = q.ContainsKey("offset") || q.ContainsKey("limit");
        if (hasIds == hasPaging)
        {
            problem = ApiProblems.InvalidParameter($"supply either offset and limit, or {idParam}");
            return false;
        }
        if (hasIds) { selection = Selection.Ids; return TryIds(q, idParam, out ids, out problem); }
        selection = Selection.Paging;
        return TryPaging(q, out paging, out problem);
    }

    public static bool TryPaging(IQueryCollection q, out Paging paging, out IResult? problem)
    {
        problem = null;
        if (!QueryParsing.TryParsePaging(Single(q, "offset"), Single(q, "limit"), out paging, out var error))
        {
            problem = ApiProblems.InvalidParameter(error);
            return false;
        }
        return true;
    }

    public static bool TryIds(IQueryCollection q, string name, out int[] ids, out IResult? problem)
    {
        problem = null;
        if (!QueryParsing.TryParseIdList(Single(q, name), out ids, out var error))
        {
            problem = ApiProblems.InvalidIdList(error);
            return false;
        }
        return true;
    }

    /// <summary>Returns the single value, or null when absent or repeated (both invalid).</summary>
    public static string? Single(IQueryCollection q, string name) =>
        q.TryGetValue(name, out var v) && v.Count == 1 ? v[0] : null;
}
