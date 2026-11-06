using ProductionIntelligence.Domain;

namespace ProductionIntelligence.Application;

public interface IProductionStore
{
    Task<IReadOnlyList<ProductionLine>> GetLinesAsync(CancellationToken cancellationToken);
    Task<ProductionLine?> GetLineAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductionShift>> GetShiftsAsync(CancellationToken cancellationToken);
    Task<ProductionShift?> GetShiftAsync(Guid id, CancellationToken cancellationToken);
    Task AddShiftAsync(ProductionShift shift, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductionEvent>> GetEventsAsync(Guid shiftId, CancellationToken cancellationToken);
    Task<ProductionEvent?> GetEventAsync(Guid eventId, CancellationToken cancellationToken);
    Task AddEventAsync(ProductionEvent productionEvent, CancellationToken cancellationToken);
    Task ResetDemoDataAsync(CancellationToken cancellationToken);
}

public sealed record InsightEvidence(
    IReadOnlyList<Guid> EventIds,
    string Summary,
    IReadOnlyList<LossDriver> LossDrivers);

public sealed record InsightRecommendation(string Text, IReadOnlyList<Guid> EvidenceEventIds);

public sealed record InsightDraft(
    string Summary,
    IReadOnlyList<Guid> EvidenceEventIds,
    IReadOnlyList<InsightRecommendation> Recommendations);

public sealed record GeneratedInsight(
    Guid ShiftId,
    string ShiftName,
    string Provider,
    string Model,
    DateTimeOffset GeneratedAtUtc,
    string Summary,
    IReadOnlyList<Guid> EvidenceEventIds,
    IReadOnlyList<InsightRecommendation> Recommendations);

public interface IInsightGenerator
{
    string Provider { get; }
    string Model { get; }
    Task<InsightDraft> GenerateAsync(
        ProductionLine line,
        ProductionShift shift,
        OeeMetrics metrics,
        LossAnalysis losses,
        IReadOnlyList<ProductionEvent> events,
        CancellationToken cancellationToken);
}

public sealed class ResourceNotFoundException(string message) : Exception(message);
public sealed class RequestConflictException(string message) : Exception(message);
public sealed class RequestValidationException(string message) : Exception(message);
