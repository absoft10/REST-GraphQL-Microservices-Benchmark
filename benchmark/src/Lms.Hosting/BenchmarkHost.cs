using Lms.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lms.Hosting;

/// <summary>
/// One host configuration for all five services so that symmetry is auditable in one place
/// (doc 02 §4; doc 01 §5; docs/api-contracts/README.md §3).
/// Deliberately absent everywhere: HTTPS redirection, response compression, response/output caching, Swagger.
/// </summary>
public static class BenchmarkHost
{
    public static WebApplicationBuilder AddBenchmarkHost(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;                                            // README §3.4
            kestrel.ConfigureEndpointDefaults(l => l.Protocols = HttpProtocols.Http1);  // AP-3, AD-05
        });

        builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
        builder.Logging.AddFilter("System.Net.Http", LogLevel.Warning);

        builder.Services.ConfigureHttpJsonOptions(o => BenchmarkJson.Apply(o.SerializerOptions));
        return builder;
    }

    /// <summary>Exception handler producing problem+json 500 without exception detail.</summary>
    public static WebApplication UseBenchmarkErrorHandling(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp => errorApp.Run(ctx =>
            ApiProblems.WriteAsync(ctx, StatusCodes.Status500InternalServerError, ProblemTypes.Internal, "Internal error", null)));
        return app;
    }

    /// <summary>/health (not part of any workload) and a problem+json 404 fallback.</summary>
    public static WebApplication MapBenchmarkInfrastructure(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Text("Healthy", "text/plain"));
        app.MapFallback(ctx => ApiProblems.WriteAsync(ctx, StatusCodes.Status404NotFound, ProblemTypes.NotFound,
            "Not found", "No resource matches the request URL."));
        return app;
    }
}
