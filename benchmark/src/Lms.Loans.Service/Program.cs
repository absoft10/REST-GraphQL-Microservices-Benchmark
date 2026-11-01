using Lms.Contracts;
using Lms.Data;
using Lms.Hosting;
using Microsoft.EntityFrameworkCore;

// Loans domain service (internal-domain-api.md §1). Ordered by (memberId, loanId); index ix_loan_member.
var builder = WebApplication.CreateBuilder(args);
builder.AddBenchmarkHost();
builder.Services.AddLmsDbContext<LoansDbContext>();

var app = builder.Build();
app.UseBenchmarkErrorHandling();

app.MapGet("/internal/loans", async (HttpRequest request, LoansDbContext db, CancellationToken ct) =>
{
    if (!RequestQuery.TryIds(request.Query, "memberIds", out var memberIds, out var problem)) return problem!;
    var loans = await db.Loans.TagWith("q:" + QueryIds.LoansByMembers)
        .Where(l => memberIds.Contains(l.MemberId))
        .OrderBy(l => l.MemberId).ThenBy(l => l.LoanId)
        .Select(l => new LoanDto(l.LoanId, l.MemberId, l.BookId, l.LoanDate, l.ReturnDate, l.IsReturned))
        .ToListAsync(ct);
    return Results.Ok(loans);
});

app.MapBenchmarkInfrastructure();
app.Run();

namespace Lms.Loans.Service
{
    public sealed class LoansServiceMarker;
}
