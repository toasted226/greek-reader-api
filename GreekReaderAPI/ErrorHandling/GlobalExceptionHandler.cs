using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;

namespace GreekReaderAPI.ErrorHandling;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug(
                "Request {Method} {Path} was cancelled by the client. TraceId: {TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                traceId);

            return true;
        }

        var (statusCode, title, code, logLevel) = exception switch
        {
            BadHttpRequestException badRequest => (
                badRequest.StatusCode,
                "Bad request.",
                "bad_request",
                LogLevel.Warning),
            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                "internal_error",
                LogLevel.Error)
        };

        logger.Log(
            logLevel,
            exception,
            "Unhandled exception for {Method} {Path}. TraceId: {TraceId}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            traceId);

        if (httpContext.Response.HasStarted)
            return true;

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers["X-Trace-Id"] = traceId;

        var result = Results.Problem(
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = traceId,
                ["code"] = code
            });

        await result.ExecuteAsync(httpContext);
        return true;
    }
}
