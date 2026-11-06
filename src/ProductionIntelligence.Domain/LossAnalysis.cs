namespace ProductionIntelligence.Domain;

public sealed record LossDriver(
    string Reason,
    int Occurrences,
    decimal DurationMinutes,
    IReadOnlyList<Guid> EvidenceEventIds);

public sealed record LossAnalysis(
    decimal TotalDowntimeMinutes,
    IReadOnlyList<LossDriver> TopDrivers);
