using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductionIntelligence.Application;
using ProductionIntelligence.Domain;

namespace ProductionIntelligence.Infrastructure;

public sealed class InsightGenerator : IInsightGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly AiOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<InsightGenerator> _logger;

    public InsightGenerator(
        IOptions<AiOptions> options,
        HttpClient httpClient,
        ILogger<InsightGenerator> logger)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    public string Provider => IsMock
        ? "Mock (offline demo)"
        : IsOllama
            ? "Ollama"
            : "OpenAI-compatible API";
    public string Model => IsMock ? "deterministic-template" : _options.Model;

    public async Task<InsightDraft> GenerateAsync(
        ProductionLine line,
        ProductionShift shift,
        OeeMetrics metrics,
        LossAnalysis losses,
        IReadOnlyList<ProductionEvent> events,
        CancellationToken cancellationToken)
    {
        if (IsMock)
        {
            return GenerateMock(line, metrics, losses, events);
        }

        if (!IsOllama && string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "AI:ApiKey is required when AI:Mode is OpenAICompatible.");
        }

        var payload = new ChatCompletionRequest(
            _options.Model,
            [
                new ChatMessage(
                    "system",
                    "You are a manufacturing shift analyst. The API calculated the supplied OEE and loss metrics " +
                    "deterministically from the production line, shift, and events stored in SQLite. Treat those " +
                    "metrics as authoritative; do not recalculate, alter, or invent metrics. Use only the supplied " +
                    "line, shift, metrics, loss, and event evidence. Do not infer a root cause or claim facts not " +
                    "present in the evidence. Recommendations are hypotheses for a human to review. Return only " +
                    "JSON with fields summary (a plain JSON string, never an object), evidenceEventIds (GUID strings), recommendations (objects with text " +
                    "and evidenceEventIds). Every claim and recommendation must cite one or more supplied event IDs. " +
                    "Do not invent IDs, events, or metrics."),
                new ChatMessage("user", BuildEvidence(line, shift, metrics, losses, events))
            ],
            new ResponseFormat("json_object"));

        var endpoint = new Uri(new Uri(EnsureTrailingSlash(_options.BaseUrl)), "chat/completions");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        if (!IsOllama)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
        request.Content = JsonContent.Create(payload, options: JsonOptions);

        var stopwatch = Stopwatch.StartNew();
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        stopwatch.Stop();
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "AI provider returned HTTP {StatusCode} in {ElapsedMs} ms for shift {ShiftId}.",
                (int)response.StatusCode, stopwatch.ElapsedMilliseconds, shift.Id);
            throw new HttpRequestException(
                $"AI provider returned HTTP {(int)response.StatusCode}.");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var completion = JsonSerializer.Deserialize<ChatCompletionResponse>(responseBody, JsonOptions)
            ?? throw new InvalidOperationException("AI provider returned an empty completion response.");
        var content = completion.Choices.FirstOrDefault()?.Message.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("AI provider returned no insight content.");
        }

        var draft = DeserializeInsightDraft(content)
            ?? throw new InvalidOperationException("AI provider returned invalid insight JSON.");
        _logger.LogInformation(
            "AI insight completed for shift {ShiftId}: model {Model}, {ElapsedMs} ms, {TotalTokens} tokens.",
            shift.Id, _options.Model, stopwatch.ElapsedMilliseconds, completion.Usage?.TotalTokens);
        return draft;
    }

    private InsightDraft? DeserializeInsightDraft(string content)
    {
        try
        {
            return JsonSerializer.Deserialize<InsightDraft>(NormalizeInsightJson(content), JsonOptions);
        }
        catch (JsonException exception)
        {
            _logger.LogError(
                exception,
                "AI provider returned insight JSON that does not match the expected schema. Response preview: {ResponsePreview}",
                content.Length <= 500 ? content : content[..500]);
            throw new InvalidOperationException(
                "AI provider returned insight data in an unsupported format. Expected summary to be a string.",
                exception);
        }
    }

    private static string NormalizeInsightJson(string content)
    {
        var root = JsonNode.Parse(content)?.AsObject()
            ?? throw new JsonException("AI insight content must be a JSON object.");
        if (root["summary"] is JsonObject summaryObject)
        {
            var summaryText = summaryObject["text"]?.GetValue<string>()
                ?? summaryObject["content"]?.GetValue<string>()
                ?? summaryObject["value"]?.GetValue<string>()
                ?? summaryObject["summary"]?.GetValue<string>();
            if (summaryText is null)
            {
                throw new JsonException("AI insight summary object did not contain text, content, value, or summary.");
            }

            root["summary"] = summaryText;
        }

        return root.ToJsonString();
    }

    private static bool IsMockMode(string mode) =>
        string.Equals(mode, "Mock", StringComparison.OrdinalIgnoreCase);

    private static bool IsOllamaMode(string mode) =>
        string.Equals(mode, "Ollama", StringComparison.OrdinalIgnoreCase);

    private bool IsMock => IsMockMode(_options.Mode);
    private bool IsOllama => IsOllamaMode(_options.Mode);

    private static InsightDraft GenerateMock(
        ProductionLine line,
        OeeMetrics metrics,
        LossAnalysis losses,
        IReadOnlyList<ProductionEvent> events)
    {
        var evidenceIds = events.Select(item => item.Id).ToArray();
        var topLoss = losses.TopDrivers.FirstOrDefault();
        var summary = topLoss is null
            ? $"{line.Name} recorded {metrics.OeePercent:F2}% OEE across {metrics.TotalUnits} units. " +
              $"Availability was {metrics.AvailabilityPercent:F2}%, performance {metrics.PerformancePercent:F2}%, " +
              $"and quality {metrics.QualityPercent:F2}%. No downtime events were recorded."
            : $"{line.Name} recorded {metrics.OeePercent:F2}% OEE across {metrics.TotalUnits} units. " +
              $"Availability was {metrics.AvailabilityPercent:F2}%, performance {metrics.PerformancePercent:F2}%, " +
              $"and quality {metrics.QualityPercent:F2}%. {topLoss.Reason} was the largest recorded downtime " +
              $"category at {topLoss.DurationMinutes:F2} minutes across {topLoss.Occurrences} event(s).";

        var recommendationText = topLoss is null
            ? "Review the production records for this shift and confirm the planned cycle time and good-unit counts."
            : $"Review the recorded {topLoss.Reason} events with the shift team and check the relevant maintenance " +
              "or material records before selecting a corrective action; the event data alone does not establish a cause.";
        var recommendationEvidence = topLoss?.EvidenceEventIds ?? evidenceIds.Take(1).ToArray();

        return new InsightDraft(
            summary,
            evidenceIds,
            [new InsightRecommendation(recommendationText, recommendationEvidence)]);
    }

    private static string BuildEvidence(
        ProductionLine line,
        ProductionShift shift,
        OeeMetrics metrics,
        LossAnalysis losses,
        IReadOnlyList<ProductionEvent> events)
    {
        var evidence = new
        {
            line = new { line.Id, line.Name, line.IdealCycleTimeSeconds },
            shift = new { shift.StartUtc, shift.EndUtc, shift.PlannedProductionSeconds },
            metrics,
            losses,
            events = events.Select(item => new
            {
                eventId = item.Id,
                type = item.Type.ToString(),
                item.TimestampUtc,
                item.StartedAtUtc,
                item.EndedAtUtc,
                item.UnitsProduced,
                item.RejectedUnits,
                item.Reason
            })
        };
        return JsonSerializer.Serialize(evidence, JsonOptions);
    }

    private static string EnsureTrailingSlash(string value) =>
        value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";

    private sealed record ChatCompletionRequest(
        string Model,
        IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("response_format")] ResponseFormat ResponseFormat);

    private sealed record ChatMessage(string Role, string Content);
    private sealed record ResponseFormat(string Type);
    private sealed record ChatCompletionResponse(
        IReadOnlyList<ChatChoice> Choices,
        ChatUsage? Usage);
    private sealed record ChatChoice([property: JsonPropertyName("message")] ChatMessage Message);
    private sealed record ChatUsage([property: JsonPropertyName("total_tokens")] int? TotalTokens);
}
