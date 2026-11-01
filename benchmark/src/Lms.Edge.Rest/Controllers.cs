using Lms.Contracts;
using Lms.DomainClient;
using Lms.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Lms.Edge.Rest;

/// <summary>Maps domain-tier failures to 502 problem+json (errors-and-pagination.md §4).</summary>
public sealed class UpstreamFailureFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not UpstreamException) return;
        context.Result = new ProblemActionResult(StatusCodes.Status502BadGateway, ProblemTypes.UpstreamFailure,
            "Upstream failure", "A domain service is unavailable or returned an error.");
        context.ExceptionHandled = true;
    }
}

/// <summary>F-R (§2.1) and N-RM call 3 (§2.2).</summary>
[ApiController]
[Route("api/books")]
public sealed class BooksController(IDomainClient domain) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (!TrySelect(Request.Query, "ids", out var selection, out var paging, out var ids, out var problem)) return problem!;
        var books = selection == RequestQuery.Selection.Ids
            ? await domain.GetBooksByIdsAsync(ids, ct)
            : await domain.GetBooksPageAsync(paging, ct);
        return Ok(books);
    }

    internal static bool TrySelect(IQueryCollection q, string idParam, out RequestQuery.Selection s, out Paging p, out int[] ids, out IActionResult? problem)
    {
        problem = null;
        if (RequestQuery.TrySelect(q, idParam, out s, out p, out ids, out var result)) return true;
        problem = new ResultAdapter(result!);
        return false;
    }
}

/// <summary>N-RM call 1 (§2.3).</summary>
[ApiController]
[Route("api/members")]
public sealed class MembersController(IDomainClient domain) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (!RequestQuery.TryPaging(Request.Query, out var paging, out var problem)) return new ResultAdapter(problem!);
        return Ok(await domain.GetMembersPageAsync(paging, ct));
    }
}

/// <summary>N-RM call 2 (§2.4).</summary>
[ApiController]
[Route("api/loans")]
public sealed class LoansController(IDomainClient domain) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (!RequestQuery.TryIds(Request.Query, "memberIds", out var memberIds, out var problem)) return new ResultAdapter(problem!);
        return Ok(await domain.GetLoansByMembersAsync(memberIds, ct));
    }
}

/// <summary>
/// N-RC composite (§2.5): exactly 3 internal calls in the fixed order members → loans → books,
/// the same calls and parameters as N-G and N-RM; in-memory join; full representations embedded (AD-10).
/// </summary>
[ApiController]
[Route("api/members-with-loans")]
public sealed class MembersWithLoansController(IDomainClient domain) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (!RequestQuery.TryPaging(Request.Query, out var paging, out var problem)) return new ResultAdapter(problem!);
        return Ok(await MembersWithLoansAggregator.AggregateAsync(domain, paging, ct));
    }
}

/// <summary>B-8: the component that aggregates for N-RC. File: Controllers.cs, function: AggregateAsync.</summary>
public static class MembersWithLoansAggregator
{
    public static async Task<IReadOnlyList<MemberWithLoansDto>> AggregateAsync(IDomainClient domain, Paging paging, CancellationToken ct)
    {
        var members = await domain.GetMembersPageAsync(paging, ct);
        if (members.Count == 0) return [];
        var loans = await domain.GetLoansByMembersAsync(members.Select(m => m.MemberId), ct);
        var books = loans.Count == 0
            ? new Dictionary<int, BookDto>()
            : (await domain.GetBooksByIdsAsync(loans.Select(l => l.BookId), ct)).ToDictionary(b => b.BookId);
        var loansByMember = loans.ToLookup(l => l.MemberId); // preserves (memberId, loanId) order from the Loans service

        return members.Select(m => new MemberWithLoansDto(
            m.MemberId, m.FirstName, m.LastName, m.Email, m.PhoneNumber, m.Address,
            loansByMember[m.MemberId].Select(l => new LoanWithBookDto(
                l.LoanId, l.MemberId, l.BookId, l.LoanDate, l.ReturnDate, l.IsReturned,
                books.TryGetValue(l.BookId, out var b) ? b : throw new UpstreamException($"Book {l.BookId} referenced by loan {l.LoanId} was not returned by Catalog.")))
                .ToList())).ToList();
    }
}

/// <summary>Executes a minimal-API problem IResult from MVC so both paths emit identical bytes.</summary>
internal sealed class ResultAdapter(IResult result) : IActionResult
{
    public Task ExecuteResultAsync(ActionContext context) => result.ExecuteAsync(context.HttpContext);
}
