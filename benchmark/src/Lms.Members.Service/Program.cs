using Lms.Contracts;
using Lms.Data;
using Lms.Hosting;
using Microsoft.EntityFrameworkCore;

// Members domain service (internal-domain-api.md §1).
var builder = WebApplication.CreateBuilder(args);
builder.AddBenchmarkHost();
builder.Services.AddLmsDbContext<MembersDbContext>();

var app = builder.Build();
app.UseBenchmarkErrorHandling();

app.MapGet("/internal/members", async (HttpRequest request, MembersDbContext db, CancellationToken ct) =>
{
    if (!RequestQuery.TryPaging(request.Query, out var paging, out var problem)) return problem!;
    var members = await db.Members.TagWith("q:" + QueryIds.MembersPage)
        .OrderBy(m => m.MemberId).Skip(paging.Offset).Take(paging.Limit)
        .Select(m => new MemberDto(m.MemberId, m.FirstName, m.LastName, m.Email, m.PhoneNumber, m.Address))
        .ToListAsync(ct);
    return Results.Ok(members);
});

app.MapBenchmarkInfrastructure();
app.Run();

namespace Lms.Members.Service
{
    public sealed class MembersServiceMarker;
}
