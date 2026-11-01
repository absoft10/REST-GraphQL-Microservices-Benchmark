using GreenDonut;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Types;
using Lms.Contracts;
using Lms.DomainClient;

namespace Lms.Edge.GraphQL;

/// <summary>Root query (docs/api-contracts/schema.graphql). Validation as errors-and-pagination.md §4.</summary>
public sealed class Query
{
    public async Task<IReadOnlyList<BookDto>> GetBooks(int offset, int limit, [Service] IDomainClient domain, CancellationToken ct) =>
        await domain.GetBooksPageAsync(Validate(offset, limit), ct);

    public async Task<IReadOnlyList<MemberDto>> GetMembers(int offset, int limit, [Service] IDomainClient domain, CancellationToken ct) =>
        await domain.GetMembersPageAsync(Validate(offset, limit), ct);

    private static Paging Validate(int offset, int limit)
    {
        if (!QueryParsing.TryValidatePaging(offset, limit, out var paging, out var error))
            throw new GraphQLException(ErrorBuilder.New().SetMessage(error).SetCode(ErrorCodes.InvalidArgument).Build());
        return paging;
    }
}

public static class ErrorCodes
{
    public const string InvalidArgument = "INVALID_ARGUMENT";
    public const string UpstreamFailure = "UPSTREAM_FAILURE";
    public const string Internal = "INTERNAL";
}

public sealed class BookType : ObjectType<BookDto>
{
    protected override void Configure(IObjectTypeDescriptor<BookDto> d) => d.Name("Book");
}

public sealed class MemberType : ObjectType<MemberDto>
{
    protected override void Configure(IObjectTypeDescriptor<MemberDto> d)
    {
        d.Name("Member");
        d.Field("loans")
            .Type<NonNullType<ListType<NonNullType<LoanType>>>>()
            .Resolve(async ctx =>
            {
                var member = ctx.Parent<MemberDto>();
                return (await ctx.DataLoader<LoansByMemberDataLoader>().LoadAsync(member.MemberId, ctx.RequestAborted)) ?? [];
            });
    }
}

public sealed class LoanType : ObjectType<LoanDto>
{
    protected override void Configure(IObjectTypeDescriptor<LoanDto> d)
    {
        d.Name("Loan");
        d.Field("book")
            .Type<NonNullType<BookType>>()
            .Resolve(async ctx =>
            {
                var loan = ctx.Parent<LoanDto>();
                return await ctx.DataLoader<BookByIdDataLoader>().LoadAsync(loan.BookId, ctx.RequestAborted)
                       ?? throw new UpstreamException($"Book {loan.BookId} referenced by loan {loan.LoanId} was not returned by Catalog.");
            });
    }
}

/// <summary>Member.loans: one batched call per request level, keyed by memberId (graphql-api.md §2.2).</summary>
public sealed class LoansByMemberDataLoader(IDomainClient domain, IBatchScheduler scheduler, DataLoaderOptions options)
    : GroupedDataLoader<int, LoanDto>(scheduler, options)
{
    protected override async Task<ILookup<int, LoanDto>> LoadGroupedBatchAsync(IReadOnlyList<int> keys, CancellationToken ct) =>
        (await domain.GetLoansByMembersAsync(keys, ct)).ToLookup(l => l.MemberId); // order within a member = loanId (service order)
}

/// <summary>Loan.book: one batched call per request level, keyed by bookId.</summary>
public sealed class BookByIdDataLoader(IDomainClient domain, IBatchScheduler scheduler, DataLoaderOptions options)
    : BatchDataLoader<int, BookDto>(scheduler, options)
{
    protected override async Task<IReadOnlyDictionary<int, BookDto>> LoadBatchAsync(IReadOnlyList<int> keys, CancellationToken ct) =>
        (await domain.GetBooksByIdsAsync(keys, ct)).ToDictionary(b => b.BookId);
}

/// <summary>Maps exceptions to contract error codes without exception detail (graphql-api.md §4).</summary>
public sealed class ContractErrorFilter : IErrorFilter
{
    public IError OnError(IError error)
    {
        if (error.Exception is null) return error;
        var root = error.Exception is AggregateException agg ? agg.Flatten().InnerException ?? error.Exception : error.Exception;
        return root is UpstreamException
            ? error.WithMessage("Upstream domain service failure.").WithCode(ErrorCodes.UpstreamFailure).WithException(null)
            : error.WithMessage("Internal error.").WithCode(ErrorCodes.Internal).WithException(null);
    }
}
