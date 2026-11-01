using System.Net.Http.Headers;
using System.Text;
using Lms.Catalog.Service;
using Lms.Contracts;
using Lms.Data;
using Lms.Data.Dataset;
using Lms.DomainClient;
using Lms.Edge.GraphQL;
using Lms.Edge.Rest;
using Lms.Loans.Service;
using Lms.Members.Service;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Lms.IntegrationTests;

/// <summary>
/// Real LocalDB integration environment (no Docker): dedicated databases lms_it_catalog/members/loans are reset,
/// created from the committed DDL and seeded by the generator; the five services run in-memory and the edges'
/// domain clients are routed to the in-memory domain services. The dataset is the TEMPORARY book fixture.
/// </summary>
public sealed class StackFixture : IAsyncLifetime
{
    public const int FlatLimit = 100, NestedLimit = 10;
    public static readonly GeneratorOptions Options = new()
    {
        Seed = 424242, FixtureBookCount = 300, MemberCount = 200, TargetMembers = 10, LoansPerTarget = 5, MaxLoansOtherMembers = 8,
    };

    public DatabaseSet Databases { get; } = new(DatabaseSet.LocalDbServer, "lms_it_");
    public GeneratedDataset Data { get; private set; } = null!;
    public DatasetManifest Manifest { get; private set; } = null!;
    public WebApplicationFactory<CatalogServiceMarker> Catalog { get; private set; } = null!;
    public WebApplicationFactory<MembersServiceMarker> Members { get; private set; } = null!;
    public WebApplicationFactory<LoansServiceMarker> Loans { get; private set; } = null!;
    public WebApplicationFactory<RestEdgeMarker> Rest { get; private set; } = null!;
    public WebApplicationFactory<GraphQLEdgeMarker> GraphQL { get; private set; } = null!;
    public HttpClient RestClient { get; private set; } = null!;
    public HttpClient GraphQLClient { get; private set; } = null!;
    public InternalCallRecorder RestCalls => Rest.Services.GetRequiredService<InternalCallRecorder>();
    public InternalCallRecorder GraphQLCalls => GraphQL.Services.GetRequiredService<InternalCallRecorder>();

    private string _artifactDir = "";

    public async Task InitializeAsync()
    {
        await DdlRunner.DropAsync(Databases);
        await DdlRunner.InitializeAsync(Databases);
        Data = DatasetGenerator.Generate(Options);
        _artifactDir = Path.Combine(Path.GetTempPath(), "lms-it-" + Guid.NewGuid().ToString("N"));
        Manifest = DatasetArtifacts.Write(Data, "integration-tests", _artifactDir);
        await DatasetStore.LoadAsync(Databases, Data, Manifest);

        Catalog = Domain<CatalogServiceMarker>(Databases.CatalogConnectionString);
        Members = Domain<MembersServiceMarker>(Databases.MembersConnectionString);
        Loans = Domain<LoansServiceMarker>(Databases.LoansConnectionString);
        Rest = Edge<RestEdgeMarker>();
        GraphQL = Edge<GraphQLEdgeMarker>();
        RestClient = Rest.CreateClient();
        GraphQLClient = GraphQL.CreateClient();
        RestCalls.RecordUris = true;
        GraphQLCalls.RecordUris = true;
    }

    private static WebApplicationFactory<T> Domain<T>(string connectionString) where T : class =>
        new WebApplicationFactory<T>().WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Default", connectionString));

    /// <summary>Edge whose domain clients use the in-memory domain servers (same handler chain otherwise).</summary>
    public WebApplicationFactory<T> Edge<T>(Func<HttpMessageHandler>? catalogOverride = null) where T : class =>
        new WebApplicationFactory<T>().WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            void Route(string name, Func<HttpMessageHandler> handler) =>
                s.Configure<HttpClientFactoryOptions>(name, o => o.HttpMessageHandlerBuilderActions.Add(h => h.PrimaryHandler = handler()));
            Route(DomainClientNames.Catalog, catalogOverride ?? (() => Catalog.Server.CreateHandler()));
            Route(DomainClientNames.Members, () => Members.Server.CreateHandler());
            Route(DomainClientNames.Loans, () => Loans.Server.CreateHandler());
        }));

    public static HttpRequestMessage Get(string url) => Lms.RequiredBytes.EquivalenceRunner.NewRequest(HttpMethod.Get, url);

    public async Task<HttpResponseMessage> PostGraphQLAsync(string query, string? operationName = null, string? variablesJson = null, HttpClient? client = null)
    {
        var body = "{\"query\":" + System.Text.Json.JsonSerializer.Serialize(query)
                   + (operationName is null ? "" : ",\"operationName\":\"" + operationName + "\"")
                   + (variablesJson is null ? "" : ",\"variables\":" + variablesJson) + "}";
        var r = Lms.RequiredBytes.EquivalenceRunner.NewRequest(HttpMethod.Post, "/graphql");
        r.Content = new StringContent(body, new UTF8Encoding(false), "application/json");
        r.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return await (client ?? GraphQLClient).SendAsync(r);
    }

    public IReadOnlyList<BookDto> Books => Data.Books;

    public async Task DisposeAsync()
    {
        RestClient?.Dispose();
        GraphQLClient?.Dispose();
        foreach (var f in new IDisposable?[] { Rest, GraphQL, Catalog, Members, Loans }) f?.Dispose();
        await Task.CompletedTask;
        if (Directory.Exists(_artifactDir)) Directory.Delete(_artifactDir, true);
    }
}

[CollectionDefinition(Name)]
public sealed class StackCollection : ICollectionFixture<StackFixture>
{
    public const string Name = "LocalDB stack";
}
