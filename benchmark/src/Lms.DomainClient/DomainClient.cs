using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Lms.Contracts;
using Lms.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lms.DomainClient;

/// <summary>Base URLs and connection settings for the domain tier (internal-domain-api.md; AD-33 values are Tier 3).</summary>
public sealed class DomainServiceOptions
{
    public const string Section = "DomainServices";
    public string CatalogBaseUrl { get; set; } = "http://localhost:5101";
    public string MembersBaseUrl { get; set; } = "http://localhost:5102";
    public string LoansBaseUrl { get; set; } = "http://localhost:5103";
    public int TimeoutSeconds { get; set; } = 30;
    /// <summary>0 = SocketsHttpHandler default (unlimited). Recorded either way.</summary>
    public int MaxConnectionsPerServer { get; set; }
    public int PooledConnectionLifetimeSeconds { get; set; } = 600;
}

/// <summary>Thrown for any domain-tier failure; edges map it to REST 502 / GraphQL UPSTREAM_FAILURE.</summary>
public sealed class UpstreamException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>The only way both edges reach the domain tier (DECISION-002 AP-2).</summary>
public interface IDomainClient
{
    Task<IReadOnlyList<BookDto>> GetBooksPageAsync(Paging paging, CancellationToken ct);
    Task<IReadOnlyList<BookDto>> GetBooksByIdsAsync(IEnumerable<int> ids, CancellationToken ct);
    Task<IReadOnlyList<MemberDto>> GetMembersPageAsync(Paging paging, CancellationToken ct);
    Task<IReadOnlyList<LoanDto>> GetLoansByMembersAsync(IEnumerable<int> memberIds, CancellationToken ct);
}

/// <summary>Counts every internal request and, when enabled, records its path and query (tests and verification).</summary>
public sealed class InternalCallRecorder
{
    private long _count;
    private readonly ConcurrentQueue<string> _calls = new();
    public bool RecordUris { get; set; }
    public long Count => Interlocked.Read(ref _count);
    public IReadOnlyList<string> Calls => [.. _calls];

    public void Record(Uri uri)
    {
        Interlocked.Increment(ref _count);
        if (RecordUris) _calls.Enqueue(uri.PathAndQuery);
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _count, 0);
        _calls.Clear();
    }
}

internal sealed class RecordingHandler(InternalCallRecorder recorder) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        recorder.Record(request.RequestUri!);
        return base.SendAsync(request, ct);
    }
}

/// <summary>Named HttpClient registrations (one per domain service).</summary>
public static class DomainClientNames
{
    public const string Catalog = "domain-catalog", Members = "domain-members", Loans = "domain-loans";
    public static readonly string[] All = [Catalog, Members, Loans];
}

internal sealed class HttpDomainClient(IHttpClientFactory factory) : IDomainClient
{
    private const string Catalog = DomainClientNames.Catalog, Members = DomainClientNames.Members, Loans = DomainClientNames.Loans;

    public Task<IReadOnlyList<BookDto>> GetBooksPageAsync(Paging p, CancellationToken ct) =>
        GetAsync<BookDto>(Catalog, $"/internal/books?offset={I(p.Offset)}&limit={I(p.Limit)}", ct);

    public Task<IReadOnlyList<BookDto>> GetBooksByIdsAsync(IEnumerable<int> ids, CancellationToken ct) =>
        GetAsync<BookDto>(Catalog, $"/internal/books?ids={QueryParsing.FormatIdList(ids)}", ct);

    public Task<IReadOnlyList<MemberDto>> GetMembersPageAsync(Paging p, CancellationToken ct) =>
        GetAsync<MemberDto>(Members, $"/internal/members?offset={I(p.Offset)}&limit={I(p.Limit)}", ct);

    public Task<IReadOnlyList<LoanDto>> GetLoansByMembersAsync(IEnumerable<int> memberIds, CancellationToken ct) =>
        GetAsync<LoanDto>(Loans, $"/internal/loans?memberIds={QueryParsing.FormatIdList(memberIds)}", ct);

    private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);

    private async Task<IReadOnlyList<T>> GetAsync<T>(string client, string relative, CancellationToken ct)
    {
        try
        {
            using var response = await factory.CreateClient(client).GetAsync(relative, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                throw new UpstreamException($"Domain service '{client}' returned {(int)response.StatusCode}.");
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<List<T>>(stream, BenchmarkJson.Options, ct)
                   ?? throw new UpstreamException($"Domain service '{client}' returned an empty body.");
        }
        catch (UpstreamException) { throw; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new UpstreamException($"Domain service '{client}' failed.", ex);
        }
    }
}

public static class DomainClientRegistration
{
    /// <summary>One pooled SocketsHttpHandler per domain service; HTTP/1.1 exact; no compression; no retries.</summary>
    public static IServiceCollection AddDomainClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DomainServiceOptions>(configuration.GetSection(DomainServiceOptions.Section));
        services.AddSingleton<InternalCallRecorder>();
        services.AddTransient<RecordingHandler>();
        services.AddSingleton<IDomainClient, HttpDomainClient>();

        foreach (var (name, url) in new (string, Func<DomainServiceOptions, string>)[]
                 {
                     (DomainClientNames.Catalog, o => o.CatalogBaseUrl),
                     (DomainClientNames.Members, o => o.MembersBaseUrl),
                     (DomainClientNames.Loans, o => o.LoansBaseUrl),
                 })
        {
            services.AddHttpClient(name, (sp, client) =>
                {
                    var o = sp.GetRequiredService<IOptions<DomainServiceOptions>>().Value;
                    client.BaseAddress = new Uri(url(o));
                    client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
                    client.DefaultRequestVersion = HttpVersion.Version11;
                    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
                })
                .ConfigurePrimaryHttpMessageHandler(sp =>
                {
                    var o = sp.GetRequiredService<IOptions<DomainServiceOptions>>().Value;
                    var h = new SocketsHttpHandler
                    {
                        AutomaticDecompression = DecompressionMethods.None,
                        PooledConnectionLifetime = TimeSpan.FromSeconds(o.PooledConnectionLifetimeSeconds),
                        UseCookies = false,
                    };
                    if (o.MaxConnectionsPerServer > 0) h.MaxConnectionsPerServer = o.MaxConnectionsPerServer;
                    return h;
                })
                .AddHttpMessageHandler<RecordingHandler>();
        }
        return services;
    }
}
