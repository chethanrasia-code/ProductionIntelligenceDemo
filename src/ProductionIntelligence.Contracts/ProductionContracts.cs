namespace ProductionIntelligence.Contracts;

public enum ProductionEventTypeContract
{
    Production,
    Downtime,
    Changeover
}

public sealed record CreateShiftRequest(
    Guid Id,
    string ShiftName,
    string LineId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    decimal PlannedProductionSeconds);

public sealed record ProductionLineDto(
    string Id,
    string Name,
    decimal IdealCycleTimeSeconds);

public sealed record ProductionShiftDto(
    Guid Id,
    string ShiftName,
    string LineId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    decimal PlannedProductionSeconds);

public sealed record ProductionEventDto(
    Guid Id,
    Guid ShiftId,
    ProductionEventTypeContract Type,
    DateTimeOffset TimestampUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    int UnitsProduced,
    int RejectedUnits,
    string? Reason);
