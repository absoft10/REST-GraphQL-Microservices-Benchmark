using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Lms.Contracts;
using Lms.Data.Dataset;

namespace Lms.UnitTests;

/// <summary>Determinism and O-8 invariants of the generator; independent re-implementation of the canonical encoding.</summary>
public class DatasetGeneratorTests
{
    private static readonly GeneratorOptions Small = new() { Seed = 7, FixtureBookCount = 300, MemberCount = 200, TargetMembers = 10, LoansPerTarget = 5 };

    [Fact]
    public void Same_seed_produces_identical_hashes()
    {
        var a = DatasetGenerator.Generate(Small);
        var b = DatasetGenerator.Generate(Small);
        Assert.Equal(CanonicalEncoding.HashBooks(a.Books), CanonicalEncoding.HashBooks(b.Books));
        Assert.Equal(CanonicalEncoding.HashMembers(a.Members), CanonicalEncoding.HashMembers(b.Members));
        Assert.Equal(CanonicalEncoding.HashLoans(a.Loans), CanonicalEncoding.HashLoans(b.Loans));
    }

    [Fact]
    public void Different_seed_produces_different_members_and_loans()
    {
        var a = DatasetGenerator.Generate(Small);
        var b = DatasetGenerator.Generate(Small with { Seed = 8 });
        Assert.NotEqual(CanonicalEncoding.HashMembers(a.Members), CanonicalEncoding.HashMembers(b.Members));
        Assert.NotEqual(CanonicalEncoding.HashLoans(a.Loans), CanonicalEncoding.HashLoans(b.Loans));
    }

    [Fact]
    public void Target_set_invariants_hold()
    {
        var d = DatasetGenerator.Generate(Small);
        var target = d.Loans.Where(l => l.MemberId <= Small.TargetMembers).ToList();
        Assert.Equal(Small.TargetMembers * Small.LoansPerTarget, target.Count);
        Assert.All(target.GroupBy(l => l.MemberId), g => Assert.Equal(Small.LoansPerTarget, g.Count()));
        Assert.Equal(target.Count, target.Select(l => l.BookId).Distinct().Count());
        foreach (var g in target.GroupBy(l => l.MemberId))
        {
            var ordered = g.OrderBy(l => l.LoanId).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                Assert.Equal(i % 2 == 0, ordered[i].IsReturned); // positions 1,3,5 returned
                Assert.Equal(ordered[i].IsReturned, ordered[i].ReturnDate.HasValue);
            }
        }
    }

    [Fact]
    public void All_references_resolve_and_ids_are_dense()
    {
        var d = DatasetGenerator.Generate(Small);
        var books = d.Books.Select(b => b.BookId).ToHashSet();
        var members = d.Members.Select(m => m.MemberId).ToHashSet();
        Assert.All(d.Loans, l => { Assert.Contains(l.BookId, books); Assert.Contains(l.MemberId, members); });
        Assert.Equal(Enumerable.Range(1, d.Loans.Count), d.Loans.Select(l => l.LoanId));
        Assert.Equal(BookFixture.SourceLabel, d.BooksSource);
    }

    [Fact]
    public void Fixture_exercises_nulls_non_ascii_and_html_sensitive_text()
    {
        var d = DatasetGenerator.Generate(Small);
        Assert.Contains(d.Books, b => b.Description is null);
        Assert.Contains(d.Books, b => b.Price is null);
        Assert.Contains(d.Books, b => b.Title.Any(c => c > 127));
        Assert.Contains(d.Books, b => b.Title.Contains('<') && b.Title.Contains('&'));
        Assert.All(d.Books, b => Assert.Equal(b.Title, b.Title.Normalize(NormalizationForm.FormC)));
    }

    [Fact]
    public void Canonical_encoding_matches_independent_reference()
    {
        var rows = new[] { new LoanDto(2, 1, 9, new DateOnly(2025, 1, 2), null, false), new LoanDto(1, 1, 5, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 3), true) };
        var reference = "1\u001F1\u001F5\u001F2025-01-01\u001F2025-01-03\u001F1\n" + "2\u001F1\u001F9\u001F2025-01-02\u001F\u0000\u001F0\n";
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(reference))), CanonicalEncoding.HashLoans(rows));

        var book = new[] { new BookDto(1, "é", "a", null, "c", null, 1.5m, null, 2001) };
        var bookRef = "1\u001Fé\u001Fa\u001F\u0000\u001Fc\u001F\u0000\u001F1.50\u001F\u0000\u001F2001\n";
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(bookRef))), CanonicalEncoding.HashBooks(book));
    }

    [Fact]
    public void Manifest_and_csv_hashes_are_reproducible()
    {
        var d = DatasetGenerator.Generate(Small);
        var dir1 = Path.Combine(Path.GetTempPath(), "lms-gen-" + Guid.NewGuid().ToString("N"));
        var dir2 = Path.Combine(Path.GetTempPath(), "lms-gen-" + Guid.NewGuid().ToString("N"));
        try
        {
            var m1 = DatasetArtifacts.Write(d, "t", dir1);
            var m2 = DatasetArtifacts.Write(DatasetGenerator.Generate(Small), "t", dir2);
            Assert.Equal(m1.Tables, m2.Tables);
            Assert.Equal(m1.Tables.Single(t => t.Table == DatasetArtifacts.LoansTable).RowCount.ToString(CultureInfo.InvariantCulture), d.Loans.Count.ToString(CultureInfo.InvariantCulture));
        }
        finally
        {
            if (Directory.Exists(dir1)) Directory.Delete(dir1, true);
            if (Directory.Exists(dir2)) Directory.Delete(dir2, true);
        }
    }

    [Fact]
    public void Too_few_books_for_distinct_target_loans_is_rejected() =>
        Assert.Throws<ArgumentException>(() => DatasetGenerator.Generate(Small with { FixtureBookCount = 49 }));
}
