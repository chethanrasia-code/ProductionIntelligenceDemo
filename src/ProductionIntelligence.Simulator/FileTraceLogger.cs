using System.Globalization;

internal sealed class FileTraceLogger
{
    private static readonly object Sync = new();
    private readonly string _runId;
    private readonly string _logDirectory;

    public FileTraceLogger(string logDirectory, string runId)
    {
        _logDirectory = logDirectory;
        _runId = runId;
        Directory.CreateDirectory(_logDirectory);
    }

    public void Info(string message) => Write("INFO", message, null);

    public void Error(Exception exception, string message) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        var entry = $"{DateTimeOffset.Now:O} [{level}] [run:{_runId}] {message}";
        if (exception is not null)
        {
            entry += Environment.NewLine + exception;
        }

        Console.WriteLine(entry);
        TryAppend(Path.Combine(_logDirectory, $"simulator-{DateTime.Now:yyyyMMdd}.log"), entry);
        if (exception is not null)
        {
            TryAppend(Path.Combine(_logDirectory, $"exceptions-{DateTime.Now:yyyyMMdd}.log"), entry);
        }
    }

    private static void TryAppend(string path, string entry)
    {
        try
        {
            lock (Sync)
            {
                File.AppendAllText(path, entry + Environment.NewLine, System.Text.Encoding.UTF8);
            }
        }
        catch (Exception loggingException)
        {
            Console.Error.WriteLine($"Unable to write log file '{path}': {loggingException.Message}");
        }
    }
}
