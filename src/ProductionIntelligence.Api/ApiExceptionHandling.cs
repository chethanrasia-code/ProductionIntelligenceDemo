using System.Diagnostics;
using ProductionIntelligence.Application;

namespace ProductionIntelligence.Api;

public static class ApiExceptionHandling
{
    public static void UseApiExceptionHandling(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var requestStarted = Stopwatch.GetTimestamp();
            ApiExceptionFileLogger.WriteRequest(
                context,
                $"Request started: {context.Request.Method} {context.Request.Path}");
            try
            {
                await next(context);
                var elapsedMilliseconds = Stopwatch.GetElapsedTime(requestStarted).TotalMilliseconds;
                ApiExceptionFileLogger.WriteRequest(
                    context,
                    $"Request completed: {context.Response.StatusCode} in {elapsedMilliseconds:F0} ms");
            }
            catch (Exception exception) when (!context.Response.HasStarted)
            {
                var (statusCode, message) = exception switch
                {
                    ResourceNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
                    RequestConflictException => (StatusCodes.Status409Conflict, exception.Message),
                    RequestValidationException => (StatusCodes.Status400BadRequest, exception.Message),
                    BadHttpRequestException => (StatusCodes.Status400BadRequest, "The request body is invalid."),
                    HttpRequestException => (StatusCodes.Status502BadGateway, "The configured AI provider request failed."),
                    InvalidOperationException => (StatusCodes.Status503ServiceUnavailable, exception.Message),
                    _ => (StatusCodes.Status500InternalServerError, "An unexpected server error occurred.")
                };

                if (statusCode >= 500)
                {
                    app.Logger.LogError(exception, "Request failed with status {StatusCode}.", statusCode);
                    ApiExceptionFileLogger.Write(context, exception, statusCode);
                }

                ApiExceptionFileLogger.WriteRequest(context, $"Request failed: {statusCode}");

                context.Response.StatusCode = statusCode;
                await context.Response.WriteAsJsonAsync(new { error = message });
            }
        });
    }
}
