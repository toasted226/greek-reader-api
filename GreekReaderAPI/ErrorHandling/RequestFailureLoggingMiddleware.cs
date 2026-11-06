using System.Diagnostics;

namespace GreekReaderAPI.ErrorHandling;

public static class RequestFailureLogging
{
    internal const string ValidationErrorsItemKey = "GreekReaderAPI.ValidationErrors";
}

public sealed class RequestFailureLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestFailureLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        var statusCode = context.Response.StatusCode;
        if (statusCode is < 400 or >= 500)
            return;

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        // Routine client outcomes stay at Information; actionable failures are
        // promoted to Warning.
        var logLevel = statusCode is StatusCodes.Status401Unauthorized
            or StatusCodes.Status403Forbidden
            or StatusCodes.Status404NotFound
            ? LogLevel.Information
            : LogLevel.Warning;

        if (context.Items.TryGetValue(RequestFailureLogging.ValidationErrorsItemKey, out var value)
            && value is IDictionary<string, string[]> errors
            && errors.Count > 0)
        {
            var summary = string.Join(
                "; ",
                errors.SelectMany(error =>
                    error.Value.Select(message => $"{error.Key}: {message}")));

            logger.Log(
                logLevel,
                "Request failed validation with {StatusCode} for {Method} {Path}. TraceId: {TraceId}. Errors: {ValidationErrors}",
                statusCode,
                context.Request.Method,
                context.Request.Path,
                traceId,
                summary);

            return;
        }

        logger.Log(
            logLevel,
            "Request failed with {StatusCode} for {Method} {Path}. TraceId: {TraceId}",
            statusCode,
            context.Request.Method,
            context.Request.Path,
            traceId);
    }
}
