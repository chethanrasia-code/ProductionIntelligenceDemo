namespace ProductionIntelligence.Domain;

public sealed record OeeMetrics(
    decimal AvailabilityPercent,
    decimal PerformancePercent,
    decimal QualityPercent,
    decimal OeePercent,
    decimal PlannedProductionMinutes,
    decimal DowntimeMinutes,
    decimal RunTimeMinutes,
    int TotalUnits,
    int GoodUnits,
    int RejectedUnits,
    decimal IdealCycleTimeSeconds);
