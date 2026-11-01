using System.Globalization;
using System.Text.Json.Nodes;
using Lms.Contracts;

namespace Lms.RequiredBytes;

/// <summary>A-6 required fields (DECISION-003). Book names are PROVISIONAL until O-7.</summary>
public static class RequiredFields
{
    public static readonly string[] Flat = ["bookId", "title", "authors", "category", "price", "publishYear"];
    public static readonly string[] Member = ["memberId", "firstName", "lastName"];
    public static readonly string[] Loan = ["loanId", "loanDate", "returnDate"];
    public static readonly string[] NestedBook = ["bookId", "title", "authors"];
    /// <summary>Linking keys present in REST loan representations but not in the CRIR (nesting expresses them).</summary>
    public static readonly string[] LoanLinks = ["memberId", "bookId"];
}

public enum Workload { Flat, Nested }

/// <summary>Oracle CRIR built from the dataset itself, never from an API response (required-bytes-spec.md §3).</summary>
public static class CrirOracle
{
    public static JsonArray Flat(IEnumerable<BookDto> allBooks, int offset, int limit) =>
        new([.. allBooks.OrderBy(b => b.BookId).Skip(offset).Take(limit).Select(b => (JsonNode)new JsonObject
        {
            ["bookId"] = b.BookId,
            ["title"] = b.Title,
            ["authors"] = b.Authors,
            ["category"] = b.Category,
            ["price"] = b.Price is { } p ? JsonValue.Create(p) : null,
            ["publishYear"] = b.PublishYear,
        })]);

    public static JsonArray Nested(IEnumerable<MemberDto> allMembers, IEnumerable<LoanDto> allLoans, IEnumerable<BookDto> allBooks, int offset, int limit)
    {
        var books = allBooks.ToDictionary(b => b.BookId);
        var loansByMember = allLoans.OrderBy(l => l.LoanId).ToLookup(l => l.MemberId);
        return new([.. allMembers.OrderBy(m => m.MemberId).Skip(offset).Take(limit).Select(m => (JsonNode)new JsonObject
        {
            ["memberId"] = m.MemberId,
            ["firstName"] = m.FirstName,
            ["lastName"] = m.LastName,
            ["loans"] = new JsonArray([.. loansByMember[m.MemberId].Select(l => (JsonNode)new JsonObject
            {
                ["loanId"] = l.LoanId,
                ["loanDate"] = Date(l.LoanDate),
                ["returnDate"] = l.ReturnDate is { } r ? Date(r) : null,
                ["book"] = new JsonObject
                {
                    ["bookId"] = books[l.BookId].BookId,
                    ["title"] = books[l.BookId].Title,
                    ["authors"] = books[l.BookId].Authors,
                },
            })]),
        })]);
    }

    private static string Date(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>Per-configuration normalisers: raw response body/bodies → CRIR (required-bytes-spec.md §3).</summary>
public static class CrirNormalizer
{
    public static JsonArray FlatRest(string body) => ProjectArray(ParseArray(body), RequiredFields.Flat);

    public static JsonArray FlatGraphQL(string body) => ProjectArray(GraphQLData(body, "books"), RequiredFields.Flat);

    public static JsonArray NestedComposite(string body) => NestedFromMemberTree(ParseArray(body));

    public static JsonArray NestedGraphQL(string body) => NestedFromMemberTree(GraphQLData(body, "members"));

    public static JsonArray NestedMultiCall(string membersBody, string loansBody, string booksBody)
    {
        var members = ParseArray(membersBody);
        var loans = ParseArray(loansBody);
        var books = ParseArray(booksBody).Select(b => b!.AsObject()).ToDictionary(b => Int(b, "bookId"));
        var loansByMember = loans.Select(l => l!.AsObject()).OrderBy(l => Int(l, "loanId")).ToLookup(l => Int(l, "memberId"));
        return new([.. members.Select(m =>
        {
            var mo = m!.AsObject();
            var o = Project(mo, RequiredFields.Member);
            o["loans"] = new JsonArray([.. loansByMember[Int(mo, "memberId")].Select(l =>
            {
                var lo = Project(l, RequiredFields.Loan);
                var bookId = Int(l, "bookId");
                lo["book"] = Project(books.TryGetValue(bookId, out var b) ? b : throw new InvalidDataException($"book {bookId} missing from call 3"), RequiredFields.NestedBook);
                return (JsonNode)lo;
            })]);
            return (JsonNode)o;
        })]);
    }

    private static JsonArray NestedFromMemberTree(JsonArray members) =>
        new([.. members.Select(m =>
        {
            var mo = m!.AsObject();
            var o = Project(mo, RequiredFields.Member);
            o["loans"] = new JsonArray([.. Required(mo, "loans")!.AsArray().Select(l =>
            {
                var loan = l!.AsObject();
                var lo = Project(loan, RequiredFields.Loan);
                lo["book"] = Project(Required(loan, "book")!.AsObject(), RequiredFields.NestedBook);
                return (JsonNode)lo;
            })]);
            return (JsonNode)o;
        })]);

    public static JsonArray GraphQLData(string body, string field)
    {
        var root = JsonNode.Parse(body)!.AsObject();
        if (root.ContainsKey("errors")) throw new InvalidDataException("GraphQL response contains errors.");
        return Required(Required(root, "data")!.AsObject(), field)!.AsArray();
    }

    private static JsonArray ParseArray(string body) => JsonNode.Parse(body)!.AsArray();

    private static JsonArray ProjectArray(JsonArray items, string[] fields) =>
        new([.. items.Select(i => (JsonNode)Project(i!.AsObject(), fields))]);

    /// <summary>Copies exactly the listed keys; an absent key is an equivalence failure (never an invented null).</summary>
    private static JsonObject Project(JsonObject src, string[] fields)
    {
        var o = new JsonObject();
        foreach (var f in fields) o[f] = Required(src, f)?.DeepClone();
        return o;
    }

    private static JsonNode? Required(JsonObject o, string key) =>
        o.TryGetPropertyValue(key, out var v) ? v : throw new InvalidDataException($"required key '{key}' is absent");

    private static int Int(JsonObject o, string key) => Required(o, key)!.GetValue<int>();
}
