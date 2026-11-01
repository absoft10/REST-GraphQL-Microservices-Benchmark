using Lms.Contracts;
using Lms.RequiredBytes;

namespace Lms.UnitTests;

/// <summary>Reproduces the worked example of required-bytes-spec.md §5 and checks the decomposition is exact.</summary>
public class ByteAccountingTests
{
    private static readonly BookDto[] ExampleBooks =
    [
        new(1, "T1", "A1", "D1", "C1", "P1", 12.50m, "M1", 2004),
        new(2, "T2", "A2", null, null, "P2", null, null, 1998),
    ];

    private const string ExampleGraphQL = """{"data":{"books":[{"bookId":1,"title":"T1","authors":"A1","category":"C1","price":12.50,"publishYear":2004},{"bookId":2,"title":"T2","authors":"A2","category":null,"price":null,"publishYear":1998}]}}""";
    private const string ExampleRest = """[{"bookId":1,"title":"T1","authors":"A1","description":"D1","category":"C1","publisher":"P1","price":12.50,"publishMonth":"M1","publishYear":2004},{"bookId":2,"title":"T2","authors":"A2","description":null,"category":null,"publisher":"P2","price":null,"publishMonth":null,"publishYear":1998}]""";

    [Fact]
    public void Worked_example_required_bytes_is_179()
    {
        var crir = CrirOracle.Flat(ExampleBooks, 0, 2);
        Assert.Equal("""[{"authors":"A1","bookId":1,"category":"C1","price":12.5,"publishYear":2004,"title":"T1"},{"authors":"A2","bookId":2,"category":null,"price":null,"publishYear":1998,"title":"T2"}]""", Jcs.Serialize(crir));
        Assert.Equal(179, ByteAccounting.RequiredBytesOf(crir));
    }

    [Fact]
    public void Worked_example_graphql_decomposition()
    {
        var a = ByteAccounting.SingleBody(ConfigId.FG, ExampleGraphQL, 179);
        Assert.Equal(199, a.ActualBytes);
        Assert.Equal(19, a.Envelope);
        Assert.Equal(0, a.NonRequired);
        Assert.Equal(1, a.Formatting);       // "12.50" vs JCS "12.5"
        Assert.True(a.SumsExactly);
        Assert.Equal(0.1005, a.OverFetch, 4);
    }

    [Fact]
    public void Worked_example_rest_decomposition()
    {
        var a = ByteAccounting.SingleBody(ConfigId.FR, ExampleRest, 179);
        Assert.Equal(292, a.ActualBytes);
        Assert.Equal(0, a.Envelope);
        Assert.Equal(112, a.NonRequired);
        Assert.Equal(1, a.Formatting);
        Assert.True(a.SumsExactly);
        Assert.Equal(0.3870, a.OverFetch, 4);
    }

    [Fact]
    public void Normalized_responses_equal_oracle()
    {
        var oracle = Jcs.Serialize(CrirOracle.Flat(ExampleBooks, 0, 2));
        Assert.Equal(oracle, Jcs.Serialize(CrirNormalizer.FlatGraphQL(ExampleGraphQL)));
        Assert.Equal(oracle, Jcs.Serialize(CrirNormalizer.FlatRest(ExampleRest)));
    }

    [Fact]
    public void Worked_example_nested_required_bytes_is_283()
    {
        var members = new[] { new MemberDto(1, "F1", "L1", "e", "p", "a") };
        var loans = new[]
        {
            new LoanDto(101, 1, 17, new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 20), true),
            new LoanDto(102, 1, 42, new DateOnly(2026, 5, 11), null, false),
        };
        var books = new[] { new BookDto(17, "T17", "A17", null, null, null, null, null, null), new BookDto(42, "T42", "A42", null, null, null, null, null, null) };
        Assert.Equal(283, ByteAccounting.RequiredBytesOf(CrirOracle.Nested(members, loans, books, 0, 1)));
    }

    [Fact]
    public void Multi_call_decomposition_sums_exactly_and_normalizes_to_oracle()
    {
        const string b1 = """[{"memberId":1,"firstName":"F1","lastName":"L1","email":"e","phoneNumber":"p","address":"a"}]""";
        const string b2 = """[{"loanId":101,"memberId":1,"bookId":17,"loanDate":"2026-03-02","returnDate":"2026-03-20","isReturned":true},{"loanId":102,"memberId":1,"bookId":42,"loanDate":"2026-05-11","returnDate":null,"isReturned":false}]""";
        const string b3 = """[{"bookId":17,"title":"T17","authors":"A17","description":null,"category":null,"publisher":null,"price":null,"publishMonth":null,"publishYear":null},{"bookId":42,"title":"T42","authors":"A42","description":null,"category":null,"publisher":null,"price":null,"publishMonth":null,"publishYear":null}]""";
        var a = ByteAccounting.MultiCall(b1, b2, b3, 283);
        Assert.True(a.SumsExactly);
        Assert.Equal(3, a.Calls);
        Assert.Equal(b1.Length + b2.Length + b3.Length, a.ActualBytes);
        Assert.Equal(283, ByteAccounting.RequiredBytesOf(CrirNormalizer.NestedMultiCall(b1, b2, b3)));
    }

    [Fact]
    public void Absent_required_key_fails_normalization() =>
        Assert.Throws<InvalidDataException>(() => CrirNormalizer.FlatRest("""[{"bookId":1,"title":"T","authors":"A","price":null,"publishYear":null}]"""));

    [Fact]
    public void Graphql_errors_fail_normalization() =>
        Assert.Throws<InvalidDataException>(() => CrirNormalizer.FlatGraphQL("""{"errors":[{"message":"x"}],"data":null}"""));

    [Theory]
    [InlineData("""[{"a":1}]""", true)]
    [InlineData("""[{"a":"x y"}]""", true)]
    [InlineData("""[{"a": 1}]""", false)]
    [InlineData("[\n]", false)]
    [InlineData("""[{"a":"q\" z"}]""", true)]
    public void Compactness_detection(string body, bool compact) => Assert.Equal(compact, ByteAccounting.IsCompact(body));
}
