using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lms.Data;

// EF Core entities and contexts for the three service-owned databases (DECISION-002 AP-6).
// Each domain service registers only its own context with its own connection string.

public sealed class BookEntity
{
    public int BookId { get; set; }
    public string Title { get; set; } = "";
    public string Authors { get; set; } = "";
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Publisher { get; set; }
    public decimal? Price { get; set; }
    public string? PublishMonth { get; set; }
    public short? PublishYear { get; set; }
}

public sealed class MemberEntity
{
    public int MemberId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Address { get; set; } = "";
}

public sealed class LoanEntity
{
    public int LoanId { get; set; }
    public int MemberId { get; set; }
    public int BookId { get; set; }
    public DateOnly LoanDate { get; set; }
    public DateOnly? ReturnDate { get; set; }
    public bool IsReturned { get; set; }
}

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<BookEntity> Books => Set<BookEntity>();

    protected override void OnModelCreating(ModelBuilder b) => b.Entity<BookEntity>(e =>
    {
        e.ToTable("book", "catalog");
        e.HasKey(x => x.BookId);
        e.Property(x => x.BookId).HasColumnName("book_id").ValueGeneratedNever();
        e.Property(x => x.Title).HasColumnName("title").HasMaxLength(400);
        e.Property(x => x.Authors).HasColumnName("authors").HasMaxLength(400);
        e.Property(x => x.Description).HasColumnName("description");
        e.Property(x => x.Category).HasColumnName("category").HasMaxLength(200);
        e.Property(x => x.Publisher).HasColumnName("publisher").HasMaxLength(200);
        e.Property(x => x.Price).HasColumnName("price").HasPrecision(10, 2);
        e.Property(x => x.PublishMonth).HasColumnName("publish_month").HasMaxLength(20);
        e.Property(x => x.PublishYear).HasColumnName("publish_year");
    });
}

public sealed class MembersDbContext(DbContextOptions<MembersDbContext> options) : DbContext(options)
{
    public DbSet<MemberEntity> Members => Set<MemberEntity>();

    protected override void OnModelCreating(ModelBuilder b) => b.Entity<MemberEntity>(e =>
    {
        e.ToTable("member", "members");
        e.HasKey(x => x.MemberId);
        e.Property(x => x.MemberId).HasColumnName("member_id").ValueGeneratedNever();
        e.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(100);
        e.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(100);
        e.Property(x => x.Email).HasColumnName("email").HasMaxLength(200);
        e.Property(x => x.PhoneNumber).HasColumnName("phone_number").HasMaxLength(40);
        e.Property(x => x.Address).HasColumnName("address").HasMaxLength(300);
    });
}

public sealed class LoansDbContext(DbContextOptions<LoansDbContext> options) : DbContext(options)
{
    public DbSet<LoanEntity> Loans => Set<LoanEntity>();

    protected override void OnModelCreating(ModelBuilder b) => b.Entity<LoanEntity>(e =>
    {
        e.ToTable("loan", "loans");
        e.HasKey(x => x.LoanId);
        e.Property(x => x.LoanId).HasColumnName("loan_id").ValueGeneratedNever();
        e.Property(x => x.MemberId).HasColumnName("member_id");
        e.Property(x => x.BookId).HasColumnName("book_id");
        e.Property(x => x.LoanDate).HasColumnName("loan_date");
        e.Property(x => x.ReturnDate).HasColumnName("return_date");
        e.Property(x => x.IsReturned).HasColumnName("is_returned");
    });
}

public static class PersistenceRegistration
{
    /// <summary>SQL compatibility level pinned for every service (doc 01 §5; DDL 00_create_databases.sql).</summary>
    public const int CompatibilityLevel = 160;

    /// <summary>Connection string "ConnectionStrings:Default" is resolved lazily so hosts and tests can override it.</summary>
    public static IServiceCollection AddLmsDbContext<TContext>(this IServiceCollection services)
        where TContext : DbContext =>
        services.AddDbContextPool<TContext>((sp, o) => o
            .UseSqlServer(
                sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured."),
                sql => sql.UseCompatibilityLevel(CompatibilityLevel))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
}
