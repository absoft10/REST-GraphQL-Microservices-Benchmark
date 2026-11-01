using HotChocolate.AspNetCore;
using HotChocolate.Types;
using Lms.Contracts;
using Lms.DomainClient;
using Lms.Edge.GraphQL;
using Lms.Hosting;

// Public GraphQL edge (docs/api-contracts/graphql-api.md). Library defaults kept (AD-24) except where the contract requires.
var builder = WebApplication.CreateBuilder(args);
builder.AddBenchmarkHost();
builder.Services.AddDomainClient(builder.Configuration);

builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddType<BookType>()
    .AddType<MemberType>()
    .AddType<LoanType>()
    .BindRuntimeType<DateOnly, DateType>()        // contract scalar "Date" = "YYYY-MM-DD" (SP-1 rule 6)
    .AddDataLoader<LoansByMemberDataLoader>()
    .AddDataLoader<BookByIdDataLoader>()
    .AddErrorFilter<ContractErrorFilter>()
    .ModifyRequestOptions(o => o.IncludeExceptionDetails = false)
    .AddHttpResponseFormatter(new HotChocolate.AspNetCore.Formatters.HttpResponseFormatterOptions
    {
        Json = new HotChocolate.Transport.Formatters.JsonResultFormatterOptions
        {
            Indented = false,
            Encoder = BenchmarkJson.Encoder,          // SP-1 rule 7: same encoder as System.Text.Json side
        },
    });

// OPEN SCIENTIFIC DECISION (AD-24, Tier 2): HotChocolate 16 limits concurrent executions to 64 by default.
// REST has no equivalent limiter. The value is configuration ("GraphQL:MaxConcurrentExecutions"; 0 = unlimited)
// and stays at the library default until the researcher decides. It is recorded under B-4.
var maxConcurrent = builder.Configuration.GetValue<int?>("GraphQL:MaxConcurrentExecutions");

var app = builder.Build();
app.UseBenchmarkErrorHandling();
app.MapGraphQL("/graphql").WithOptions(o =>
{
    o.EnableGetRequests = false;                      // POST only (graphql-api.md §1)
    o.Batching = AllowedBatching.None;                // query/variable batching disabled (graphql-api.md §1)
    o.EnableSchemaRequests = true;
    o.Tool.Enable = false;
    if (maxConcurrent is { } m) o.MaxConcurrentExecutions = m == 0 ? null : m;
});
app.MapBenchmarkInfrastructure();
app.Run();

namespace Lms.Edge.GraphQL
{
    public sealed class GraphQLEdgeMarker;
}
