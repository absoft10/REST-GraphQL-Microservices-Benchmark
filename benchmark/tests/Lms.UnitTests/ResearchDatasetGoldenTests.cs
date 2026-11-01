using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lms.Data.Dataset;
using Lms.RequiredBytes;

namespace Lms.UnitTests;

/// <summary>Runs only where the git-ignored research archive is present (benchmark/data/source/...).</summary>
public sealed class RequiresResearchArchiveFactAttribute : FactAttribute
{
    public RequiresResearchArchiveFactAttribute()
    {
        if (ResearchDataset.ArchivePath is null)
            Skip = "Research archive not present (git-ignored). See benchmark/README.md §2b to download it.";
    }
}

internal static class ResearchDataset
{
    public static string BenchmarkRoot
    {
        get
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d is not null && !File.Exists(Path.Combine(d.FullName, "LmsBenchmark.slnx"))) d = d.Parent;
            return d?.FullName ?? throw new DirectoryNotFoundException("benchmark root not found");
        }
    }

    public static string? ArchivePath
    {
        get
        {
            var p = Path.Combine(BenchmarkRoot, KaggleBooksSource.DefaultArchivePath);
            return File.Exists(p) ? p : null;
        }
    }

    public static JsonElement Golden => JsonDocument.Parse(File.ReadAllText(Path.Combine(BenchmarkRoot, "tests", "golden", "kaggle-v3.golden.json"))).RootElement;

    private static readonly Lazy<GeneratedDataset> Data = new(() =>
    {
        var g = Golden.GetProperty("generator");
        return DatasetGenerator.Generate(new GeneratorOptions
        {
            Seed = g.GetProperty("seed").GetUInt64(),
            MemberCount = g.GetProperty("memberCount").GetInt32(),
            TargetMembers = g.GetProperty("targetMembers").GetInt32(),
            LoansPerTarget = g.GetProperty("loansPerTarget").GetInt32(),
            MaxLoansOtherMembers = g.GetProperty("maxLoansOtherMembers").GetInt32(),
        }, KaggleBooksImporter.Import(ArchivePath!));
    });

    public static GeneratedDataset Dataset => Data.Value;
}

/// <summary>
/// Golden checks for the research dataset (DECISION-009): pinned source, row counts, rule statistics that match the
/// read-only profile, canonical table hashes (independently reproduced), and CRIR Required Bytes for the approved
/// record sets. Values live in tests/golden/kaggle-v3.golden.json (text-free).
/// </summary>
public class ResearchDatasetGoldenTests
{
    [RequiresResearchArchiveFact]
    public void Source_rows_and_rule_statistics_match_golden()
    {
        var d = ResearchDataset.Dataset;
        var gold = ResearchDataset.Golden;
        var info = d.BooksSourceDetail!;
        Assert.Equal(gold.GetProperty("source").GetProperty("archiveSha256").GetString(), info.ArchiveSha256);
        Assert.Equal(gold.GetProperty("source").GetProperty("entrySha256").GetString(), info.EntrySha256);
        Assert.Equal(gold.GetProperty("rows").GetProperty("catalog.book").GetInt32(), d.Books.Count);
        Assert.Equal(Enumerable.Range(1, d.Books.Count), d.Books.Select(b => b.BookId));
        var s = gold.GetProperty("importStats");
        Assert.Equal(s.GetProperty("valuesChangedByNormalization").GetInt64(), info.Stats.ValuesChangedByNormalization);
        Assert.Equal(s.GetProperty("nullDescription").GetInt64(), info.Stats.NullDescription);
        Assert.Equal(s.GetProperty("nullCategory").GetInt64(), info.Stats.NullCategory);
        Assert.Equal(s.GetProperty("nullPublisher").GetInt64(), info.Stats.NullPublisher);
        Assert.Equal(s.GetProperty("nullPrice").GetInt64(), info.Stats.NullPrice);
        Assert.Equal(s.GetProperty("authorsBarePlaceholderBy").GetInt64(), info.Stats.AuthorsBarePlaceholderBy);
        Assert.Equal(s.GetProperty("pricesWithOneDecimalInSource").GetInt64(), info.Stats.PricesWithOneDecimalInSource);
    }

    [RequiresResearchArchiveFact]
    public void Canonical_table_hashes_match_golden()
    {
        var d = ResearchDataset.Dataset;
        var h = ResearchDataset.Golden.GetProperty("canonicalSha256");
        Assert.Equal(h.GetProperty("catalog.book").GetString(), CanonicalEncoding.HashBooks(d.Books));
        Assert.Equal(h.GetProperty("members.member").GetString(), CanonicalEncoding.HashMembers(d.Members));
        Assert.Equal(h.GetProperty("loans.loan").GetString(), CanonicalEncoding.HashLoans(d.Loans));
        Assert.Equal(ResearchDataset.Golden.GetProperty("rows").GetProperty("loans.loan").GetInt32(), d.Loans.Count);
    }

    [RequiresResearchArchiveFact]
    public void Crir_required_bytes_for_approved_record_sets_match_golden()
    {
        var d = ResearchDataset.Dataset;
        var c = ResearchDataset.Golden.GetProperty("crir");
        var flat = Jcs.Serialize(CrirOracle.Flat(d.Books, c.GetProperty("flat").GetProperty("offset").GetInt32(), c.GetProperty("flat").GetProperty("limit").GetInt32()));
        var nested = Jcs.Serialize(CrirOracle.Nested(d.Members, d.Loans, d.Books, c.GetProperty("nested").GetProperty("offset").GetInt32(), c.GetProperty("nested").GetProperty("limit").GetInt32()));
        Assert.Equal(c.GetProperty("flat").GetProperty("requiredBytes").GetInt64(), Encoding.UTF8.GetByteCount(flat));
        Assert.Equal(c.GetProperty("nested").GetProperty("requiredBytes").GetInt64(), Encoding.UTF8.GetByteCount(nested));
        Assert.Equal(c.GetProperty("flat").GetProperty("jcsSha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(flat))));
        Assert.Equal(c.GetProperty("nested").GetProperty("jcsSha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(nested))));
    }

    [RequiresResearchArchiveFact]
    public void Real_data_fits_the_ddl_column_limits()
    {
        var b = ResearchDataset.Dataset.Books;
        Assert.True(b.Max(x => x.Title.Length) <= 400);
        Assert.True(b.Max(x => x.Authors.Length) <= 400);
        Assert.True(b.Max(x => x.Category?.Length ?? 0) <= 200);
        Assert.True(b.Max(x => x.Publisher?.Length ?? 0) <= 200);
        Assert.True(b.Max(x => x.PublishMonth?.Length ?? 0) <= 20);
        Assert.All(b, x => Assert.True(x.Price is null || (x.Price < 100_000_000m && decimal.Round(x.Price.Value, 2) == x.Price)));
        Assert.All(b, x => Assert.True(x.PublishYear is null || x.PublishYear <= short.MaxValue));
    }
}
