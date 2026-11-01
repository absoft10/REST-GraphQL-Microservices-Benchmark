using System.Text;
using Lms.Contracts;

namespace Lms.Data.Dataset;

public sealed record GeneratorOptions
{
    /// <summary>Fixed, recorded seed. Default value has no scientific meaning; it is recorded in the manifest.</summary>
    public ulong Seed { get; init; } = 20261101;
    /// <summary>Number of TEMPORARY fixture books (only used while the Kaggle dataset is unverified).</summary>
    public int FixtureBookCount { get; init; } = 2000;
    /// <summary>Member and loan volumes are Tier 2 placeholders (doc 06 §2 #11).</summary>
    public int MemberCount { get; init; } = 10000;
    /// <summary>M: target members of the nested record set (O-8; placeholder 10).</summary>
    public int TargetMembers { get; init; } = 10;
    /// <summary>L: loans per target member (O-8; placeholder 5).</summary>
    public int LoansPerTarget { get; init; } = 5;
    /// <summary>Non-target members get 0..MaxLoansOtherMembers loans.</summary>
    public int MaxLoansOtherMembers { get; init; } = 20;
}

public sealed record GeneratedDataset(
    string BooksSource,
    GeneratorOptions Options,
    IReadOnlyList<BookDto> Books,
    IReadOnlyList<MemberDto> Members,
    IReadOnlyList<LoanDto> Loans);

/// <summary>
/// Deterministic generator for the three service-owned datasets (doc 03 §2.3; DECISION-004 O-4/O-8).
/// Members and loans are synthetic and approved in structure. Books come from <see cref="BookFixture"/>,
/// a TEMPORARY development fixture that is NOT the research dataset.
/// </summary>
public static class DatasetGenerator
{
    public const string GeneratorVersion = "lms-dataset-generator/1.0";
    private const ulong BookStream = 1, MemberStream = 2, LoanStream = 3;
    private static readonly DateOnly LoanEpoch = new(2025, 1, 1);

    public static GeneratedDataset Generate(GeneratorOptions o)
    {
        Validate(o);
        var books = BookFixture.Generate(o.FixtureBookCount, SplitMix64.ForStream(o.Seed, BookStream));
        var members = GenerateMembers(o, SplitMix64.ForStream(o.Seed, MemberStream));
        var loans = GenerateLoans(o, books.Count, SplitMix64.ForStream(o.Seed, LoanStream));
        return new GeneratedDataset(BookFixture.SourceLabel, o, books, members, loans);
    }

    private static void Validate(GeneratorOptions o)
    {
        if (o.TargetMembers < 1 || o.LoansPerTarget < 1) throw new ArgumentException("M and L must be >= 1.");
        if (o.MemberCount < o.TargetMembers) throw new ArgumentException("MemberCount must be >= TargetMembers.");
        if (o.FixtureBookCount < o.TargetMembers * o.LoansPerTarget)
            throw new ArgumentException("Book count must be >= M*L so target loans can use distinct books.");
    }

    private static List<MemberDto> GenerateMembers(GeneratorOptions o, SplitMix64 rng)
    {
        var list = new List<MemberDto>(o.MemberCount);
        for (var id = 1; id <= o.MemberCount; id++)
        {
            var first = rng.Pick(Names.First);
            var last = rng.Pick(Names.Last);
            list.Add(new MemberDto(
                id,
                first,
                last,
                $"{first}.{last}.{id}@example.invalid".ToLowerInvariant(),
                $"+00 {rng.Next(900) + 100} {rng.Next(9000) + 1000}",
                $"{rng.Next(999) + 1} {rng.Pick(Names.Streets)} {rng.Pick(Names.StreetSuffixes)}, {rng.Pick(Names.Cities)}"));
        }
        return list;
    }

    private static List<LoanDto> GenerateLoans(GeneratorOptions o, int bookCount, SplitMix64 rng)
    {
        // Distinct books for the target set: Fisher–Yates shuffle of 1..bookCount, take the first M*L.
        var pool = Enumerable.Range(1, bookCount).ToArray();
        for (var i = pool.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        var targetBooks = pool.AsSpan(0, o.TargetMembers * o.LoansPerTarget).ToArray();

        var loans = new List<LoanDto>();
        var loanId = 0;
        var targetIndex = 0;
        for (var memberId = 1; memberId <= o.MemberCount; memberId++)
        {
            var isTarget = memberId <= o.TargetMembers;
            var count = isTarget ? o.LoansPerTarget : rng.Next(o.MaxLoansOtherMembers + 1);
            for (var k = 1; k <= count; k++)
            {
                var bookId = isTarget ? targetBooks[targetIndex++] : rng.Next(bookCount) + 1;
                var loanDate = LoanEpoch.AddDays(rng.Next(540));
                // O-8: within a target member, loans alternate returned (odd position) / not returned (even).
                var returned = isTarget ? k % 2 == 1 : rng.Next(10) < 7;
                DateOnly? returnDate = returned ? loanDate.AddDays(rng.Next(60) + 1) : null;
                loans.Add(new LoanDto(++loanId, memberId, bookId, loanDate, returnDate, returned));
            }
        }
        return loans;
    }

    internal static string Nfc(string s) => s.Normalize(NormalizationForm.FormC);
}

/// <summary>
/// TEMPORARY BOOK FIXTURE — synthetic placeholder books for development and testing ONLY.
/// It is not the research dataset and must never be used for measured runs (DECISION-004, O-7).
/// Its column set mirrors the PROVISIONAL contract (rest-api.md §1.1); it does not assert the Kaggle schema.
/// Values deliberately include non-ASCII text, HTML-sensitive characters and nulls to exercise SP-1.
/// </summary>
public static class BookFixture
{
    public const string SourceLabel = "TEMPORARY_FIXTURE_NOT_RESEARCH_DATA";

    private static readonly string[] TitleVariants =
    [
        "", " — Café crème", " naïve <b>bold</b> & 'quoted'", " 日本語の本", " emoji 📚", " \"double\" quotes",
        " back\\slash", " Ünïcödé Straße", "", " plus+sign `tick`",
    ];
    private static readonly string[] Categories = ["Fiction", "History", "Science", "Poetry", "Travel", "Art & Design", "Biography", "Children's"];
    private static readonly string[] Publishers = ["Fixture House", "Placeholder Press", "Sample & Sons", "Éditions Fictives", "Test Books Ltd"];
    private static readonly string[] Months = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
    private static readonly string[] Words = ["lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit", "sed", "do"];

    public static List<BookDto> Generate(int count, SplitMix64 rng)
    {
        var books = new List<BookDto>(count);
        for (var i = 1; i <= count; i++)
        {
            var title = DatasetGenerator.Nfc($"Fixture Title {i:D5}{TitleVariants[i % TitleVariants.Length]}");
            var authors = DatasetGenerator.Nfc($"Fixture Author {rng.Next(500) + 1:D3}");
            string? description = i % 7 == 0 ? null : DatasetGenerator.Nfc(Description(i, rng));
            string? category = i % 9 == 0 ? null : rng.Pick(Categories);
            string? publisher = i % 8 == 0 ? null : DatasetGenerator.Nfc(rng.Pick(Publishers));
            decimal? price = i % 11 == 0 ? null : new decimal(rng.Next(9900) + 100, 0, 0, false, 2); // scale 2, e.g. 12.50
            string? month = i % 5 == 0 ? null : rng.Pick(Months);
            int? year = i % 13 == 0 ? null : 1950 + rng.Next(76);
            books.Add(new BookDto(i, title, authors, description, category, publisher, price, month, year));
        }
        return books;
    }

    private static string Description(int i, SplitMix64 rng)
    {
        var sb = new StringBuilder($"TEMPORARY FIXTURE description for book {i}.");
        var n = rng.Next(40);
        for (var k = 0; k < n; k++) sb.Append(' ').Append(rng.Pick(Words));
        return sb.ToString();
    }
}

internal static class Names
{
    public static readonly string[] First =
    [
        "Alice", "Bob", "Carmen", "David", "Elif", "Farah", "George", "Hana", "Ivan", "Jia", "Kofi", "Lena", "Mateo", "Nadia",
        "Omar", "Priya", "Quinn", "Rosa", "Sami", "Tara", "Umar", "Vera", "Wei", "Xenia", "Yusuf", "Zara",
    ];
    public static readonly string[] Last =
    [
        "Anders", "Baker", "Chen", "Diaz", "Evans", "Fischer", "Garcia", "Hassan", "Ito", "Jensen", "Khan", "Larsen", "Moreau",
        "Novak", "Okafor", "Patel", "Quist", "Rossi", "Silva", "Tanaka", "Ueda", "Varga", "Weber", "Xu", "Yilmaz", "Zimmer",
    ];
    public static readonly string[] Streets = ["Oak", "Maple", "Cedar", "Elm", "Birch", "Willow", "Pine", "Ash", "Hill", "Lake"];
    public static readonly string[] StreetSuffixes = ["Street", "Avenue", "Road", "Lane", "Way"];
    public static readonly string[] Cities = ["Springfield", "Rivertown", "Lakeside", "Hillview", "Fairhaven", "Brookfield"];
}
