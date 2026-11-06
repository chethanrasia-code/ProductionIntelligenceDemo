namespace ProductionIntelligence.Domain;

public enum ProductionEventType
{
    Production,
    Downtime,
    Changeover
}

public sealed class ProductionEvent
{
    public Guid Id { get; set; }
    public Guid ShiftId { get; set; }
    public ProductionEventType Type { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }
    public int UnitsProduced { get; set; }
    public int RejectedUnits { get; set; }
    public string? Reason { get; set; }
}
