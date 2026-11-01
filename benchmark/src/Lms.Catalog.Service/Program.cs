using Lms.Contracts;
using Lms.Data;
using Lms.Hosting;
using Microsoft.EntityFrameworkCore;

// Catalog domain service (internal-domain-api.md §1). Fixed full-row contract: no field selection is accepted (AP-8).
var builder = WebApplication.CreateBuilder(args);
builder.AddBenchmarkHost();
builder.Services.AddLmsDbContext<CatalogDbContext>();

var app = builder.Build();
app.UseBenchmarkErrorHandling();

app.MapGet("/internal/books", async (HttpRequest request, CatalogDbContext db, CancellationToken ct) =>
{
    if (!RequestQuery.TrySelect(request.Query, "ids", out var selection, out var paging, out var ids, out var problem))
        return problem!;

    var query = selection == RequestQuery.Selection.Ids
        ? db.Books.TagWith("q:" + QueryIds.BooksByIds).Where(b => ids.Contains(b.BookId)).OrderBy(b => b.BookId)
        : db.Books.TagWith("q:" + QueryIds.BooksPage).OrderBy(b => b.BookId).Skip(paging.Offset).Take(paging.Limit);

    var books = await query
        .Select(b => new BookDto(b.BookId, b.Title, b.Authors, b.Description, b.Category, b.Publisher, b.Price, b.PublishMonth, (int?)b.PublishYear))
        .ToListAsync(ct);
    return Results.Ok(books);
});

app.MapBenchmarkInfrastructure();
app.Run();

namespace Lms.Catalog.Service
{
    /// <summary>Entry-point marker for WebApplicationFactory.</summary>
    public sealed class CatalogServiceMarker;
}
