using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Unicode;

namespace Lms.Hosting;

/// <summary>
/// Shared JSON serialization policy SP-1 (DECISION-005), System.Text.Json side.
/// The GraphQL edge applies the same encoder to the HotChocolate formatter (Lms.Edge.GraphQL).
/// </summary>
public static class BenchmarkJson
{
    /// <summary>
    /// Rule 7 (PROVISIONAL): non-ASCII emitted as UTF-8; HTML-sensitive characters stay escaped.
    /// UnsafeRelaxedJsonEscaping was evaluated and rejected (DECISION-005).
    /// </summary>
    public static readonly JavaScriptEncoder Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);

    public static JsonSerializerOptions Options { get; } = Create();

    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        Apply(options);
        options.MakeReadOnly();
        return options;
    }

    public static void Apply(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;   // rule 2
        options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;  // rule 3: nulls written
        options.WriteIndented = false;                               // rule 1
        options.Encoder = Encoder;                                   // rule 7
        options.NumberHandling = JsonNumberHandling.Strict;          // rules 4-5: numbers as JSON numbers
        options.PropertyNameCaseInsensitive = false;
    }
}
