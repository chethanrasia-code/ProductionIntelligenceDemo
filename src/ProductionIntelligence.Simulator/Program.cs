using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using ProductionIntelligence.Contracts;

var runId = Guid.NewGuid().ToString("N")[..8];
var logger = new FileTraceLogger(Path.Combine(AppContext.BaseDirectory, "logs"), runId);

try
{
    logger.Info("Simulator started.");
    var options = SimulatorOptions.Parse(args);
    logger.Info($"Configuration loaded. Base URL: {options.BaseUrl}; line: {options.LineId ?? "first two"}; reset: {options.Reset}; interval: {options.IntervalMilliseconds} ms.");
    using var httpClient = new HttpClient { BaseAddress = new Uri(options.BaseUrl) };
    var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    if (options.Reset)
    {
        logger.Info("Resetting existing demo shifts and events.");
        using var reset = await httpClient.PostAsync("/api/demo/reset", content: null);
        await EnsureSuccessAsync(reset, "Reset demo data");
        logger.Info("Demo data reset completed.");
    }

    logger.Info("Loading production lines from the API.");
    var lines = await httpClient.GetFromJsonAsync<List<ProductionLineDto>>("/api/lines", jsonOptions)
        ?? throw new InvalidOperationException("API returned no production lines.");
    logger.Info($"Loaded {lines.Count} production line(s).");

    var selectedLines = options.LineId is null
        ? lines.Take(2).ToArray()
        : lines.Where(item => item.Id == options.LineId).ToArray();
    if (selectedLines.Length == 0)
    {
        var requestedLine = options.LineId ?? "the first two API lines";
        throw new InvalidOperationException($"Line selection '{requestedLine}' was not returned by the API.");
    }
    logger.Info($"Selected {selectedLines.Length} production line(s).");

    var now = DateTimeOffset.UtcNow;
    var totalAccepted = 0;
    foreach (var line in selectedLines)
    {
        logger.Info($"Starting simulation for {line.Name} ({line.Id}).");
        for (var shiftIndex = 0; shiftIndex < 3; shiftIndex++)
        {
            var shiftStart = now.AddHours(-8 * (3 - shiftIndex));
            var shiftEnd = shiftStart.AddHours(8);
            var shiftId = Guid.NewGuid();
            var shiftName = $"shift-{line.Id}-{shiftStart:yyyyMMdd-HHmmss}";
            logger.Info($"Creating shift {shiftName} ({shiftId}).");

            using (var createShift = await httpClient.PostAsJsonAsync(
                       "/api/shifts",
                       new CreateShiftRequest(
                           shiftId,
                           shiftName,
                           line.Id,
                           shiftStart,
                           shiftEnd,
                           (decimal)(shiftEnd - shiftStart).TotalSeconds),
                       jsonOptions))
            {
                await EnsureSuccessAsync(createShift, "Create simulated shift");
            }

            var events = CreateSampleEvents(shiftId, shiftStart);
            logger.Info($"Sending {events.Count} synthetic events to {options.BaseUrl} for {line.Name}, {shiftName}.");
            var accepted = 0;
            foreach (var productionEvent in events)
            {
                using var response = await httpClient.PostAsJsonAsync(
                    $"/api/shifts/{shiftId}/events",
                    productionEvent,
                    jsonOptions);
                await EnsureSuccessAsync(response, $"Ingest event {productionEvent.Id}");
                accepted++;
                totalAccepted++;
                if (options.IntervalMilliseconds > 0)
                {
                    await Task.Delay(options.IntervalMilliseconds);
                }
            }

            logger.Info($"Shift completed. Name: {shiftName}; id: {shiftId}; events accepted: {accepted}.");
        }
    }

    logger.Info($"Simulation completed successfully. Generated {selectedLines.Length} line(s), {selectedLines.Length * 3} shifts, and {totalAccepted} events.");
    Console.ReadLine();
}
catch (Exception exception)
{
    logger.Error(exception, "Simulator stopped because an unexpected error occurred.");
    Environment.ExitCode = 1;
}

static List<ProductionEventDto> CreateSampleEvents(Guid shiftId, DateTimeOffset shiftStart)
{
    var counts = new[] { 90, 88, 84, 86, 87, 89, 88, 88, 91, 86 };
    var rejects = new[] { 2, 1, 2, 1, 2, 2, 2, 2, 1, 2 };
    var events = new List<ProductionEventDto>();
    for (var index = 0; index < counts.Length; index++)
    {
        var minutesAfterStart = 15 + index * 40;
        events.Add(new ProductionEventDto(
            Guid.NewGuid(),
            shiftId,
            ProductionEventTypeContract.Production,
            shiftStart.AddMinutes(minutesAfterStart),
            null,
            null,
            counts[index],
            rejects[index],
            null));
    }

    AddDowntime(100, 8, ProductionEventTypeContract.Downtime, "Minor jam");
    AddDowntime(140, 7, ProductionEventTypeContract.Downtime, "Minor jam");
    AddDowntime(235, 20, ProductionEventTypeContract.Changeover, "Product changeover");
    AddDowntime(350, 10, ProductionEventTypeContract.Downtime, "Material wait");
    AddDowntime(400, 6, ProductionEventTypeContract.Downtime, "Quality inspection");
    return events.OrderBy(item => item.TimestampUtc).ToList();

    void AddDowntime(
        int startMinute,
        int durationMinutes,
        ProductionEventTypeContract type,
        string reason)
    {
        var start = shiftStart.AddMinutes(startMinute);
        var end = start.AddMinutes(durationMinutes);
        events.Add(new ProductionEventDto(
            Guid.NewGuid(),
            shiftId,
            type,
            start,
            start,
            end,
            0,
            0,
            reason));
    }
}

static async Task EnsureSuccessAsync(HttpResponseMessage response, string action)
{
    if (response.IsSuccessStatusCode)
    {
        return;
    }

    var responseBody = await response.Content.ReadAsStringAsync();
    throw new HttpRequestException(
        $"{action} failed with {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}");
}

internal sealed record SimulatorOptions(
    string BaseUrl,
    string? LineId,
    bool Reset,
    int IntervalMilliseconds)
{
    public static SimulatorOptions Parse(string[] args)
    {
        var baseUrl = "http://localhost:5258";
        string? lineId = null;
        var reset = false;
        var intervalMilliseconds = 0;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--base-url" when index + 1 < args.Length:
                    baseUrl = args[++index].TrimEnd('/');
                    break;
                case "--line" when index + 1 < args.Length:
                    lineId = args[++index];
                    break;
                case "--interval-ms" when index + 1 < args.Length:
                    if (!int.TryParse(args[++index], out intervalMilliseconds) ||
                        intervalMilliseconds < 0)
                    {
                        throw new ArgumentException("--interval-ms must be a non-negative integer.");
                    }

                    break;
                case "--reset":
                    reset = true;
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
                default:
                    throw new ArgumentException($"Unknown or incomplete argument: {args[index]}");
            }
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("--base-url must be an absolute HTTP or HTTPS URL.");
        }

        return new SimulatorOptions(baseUrl, lineId, reset, intervalMilliseconds);
    }

    private static void PrintHelp() =>
        Console.WriteLine(
            "Usage: dotnet run --project .\\src\\ProductionIntelligence.Simulator -- " +
            "[--base-url http://localhost:5258] [--line line-1] [--reset] [--interval-ms 100]");
}
