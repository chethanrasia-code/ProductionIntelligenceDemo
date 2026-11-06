using System.Text;

namespace ProductionIntelligence.Api;

internal static class ApiExceptionFileLogger
{
    private static readonly object Sync = new();

    public static void WriteRequest(HttpContext context, string message)
    {
        Write(
            Path.Combine(AppContext.BaseDirectory, "logs", $"api-{DateTime.Now:yyyyMMdd}.log"),
            $"{DateTimeOffset.Now:O} [INFO] [trace:{context.TraceIdentifier}] {message}");
    }

    public static void Write(HttpContext context, Exception exception, int statusCode)
    {
        try
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(directory);
            var traceId = context.TraceIdentifier;
            var entry = new StringBuilder()
                .AppendLine($"{DateTimeOffset.Now:O} [ERROR] [trace:{traceId}] [status:{statusCode}]")
                .AppendLine($"{context.Request.Method} {context.Request.Path}")
                .AppendLine(exception.ToString())
                .ToString();
            Write(Path.Combine(directory, $"exceptions-{DateTime.Now:yyyyMMdd}.log"), entry);
        }
        catch (Exception loggingException)
        {
            context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(nameof(ApiExceptionFileLogger))
                .LogError(loggingException, "Unable to write the API exception log file.");
        }
    }

    private static void Write(string path, string entry)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            lock (Sync)
            {
                File.AppendAllText(path, entry + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never interrupt an API request.
        }
    }
}
