using System.Text.Json;
using Lms.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Lms.Hosting;

/// <summary>RFC 9457 problem body exactly as in docs/api-contracts/errors-and-pagination.md §4.</summary>
public sealed record ProblemBody(string Type, string Title, int Status, string? Detail, string Instance);

public static class ApiProblems
{
    public const string ContentType = "application/problem+json; charset=utf-8";

    public static Task WriteAsync(HttpContext context, int status, string type, string title, string? detail)
    {
        var body = new ProblemBody(type, title, status, detail, context.Request.Path.Value ?? "/");
        context.Response.StatusCode = status;
        context.Response.ContentType = ContentType;
        return JsonSerializer.SerializeAsync(context.Response.Body, body, BenchmarkJson.Options, context.RequestAborted);
    }

    public static IResult InvalidParameter(string detail) => new ProblemResult(400, ProblemTypes.InvalidParameter, "Invalid parameter", detail);
    public static IResult InvalidIdList(string detail) => new ProblemResult(400, ProblemTypes.InvalidIdList, "Invalid id list", detail);
}

/// <summary>Minimal-API result.</summary>
public sealed class ProblemResult(int status, string type, string title, string? detail) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext) => ApiProblems.WriteAsync(httpContext, status, type, title, detail);
}

/// <summary>MVC result with the same bytes as the minimal-API result (no traceId or extensions added).</summary>
public sealed class ProblemActionResult(int status, string type, string title, string? detail) : IActionResult
{
    public Task ExecuteResultAsync(ActionContext context) => ApiProblems.WriteAsync(context.HttpContext, status, type, title, detail);

    public static ProblemActionResult InvalidParameter(string detail) => new(400, ProblemTypes.InvalidParameter, "Invalid parameter", detail);
    public static ProblemActionResult InvalidIdList(string detail) => new(400, ProblemTypes.InvalidIdList, "Invalid id list", detail);
}
