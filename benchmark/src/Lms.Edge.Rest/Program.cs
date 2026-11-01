using Lms.DomainClient;
using Lms.Edge.Rest;
using Lms.Hosting;

// Public REST edge (docs/api-contracts/rest-api.md). [ApiController] controllers (O-13).
var builder = WebApplication.CreateBuilder(args);
builder.AddBenchmarkHost();
builder.Services.AddDomainClient(builder.Configuration);
builder.Services
    .AddControllers(o => o.Filters.Add<UpstreamFailureFilter>())
    .AddJsonOptions(o => BenchmarkJson.Apply(o.JsonSerializerOptions))
    .ConfigureApiBehaviorOptions(o => o.SuppressModelStateInvalidFilter = true); // validation is explicit (contract errors)

var app = builder.Build();
app.UseBenchmarkErrorHandling();
app.MapControllers();
app.MapBenchmarkInfrastructure();
app.Run();

namespace Lms.Edge.Rest
{
    public sealed class RestEdgeMarker;
}
