namespace ProductionIntelligence.Domain;

public sealed class ProductionShift
{
    public Guid Id { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public string LineId { get; set; } = string.Empty;
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }

    // Planned production time excludes scheduled breaks; the demo uses a shift with no breaks.
    public decimal PlannedProductionSeconds { get; set; }
}
