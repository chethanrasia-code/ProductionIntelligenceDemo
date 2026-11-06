namespace ProductionIntelligence.Domain;

public sealed class ProductionLine
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal IdealCycleTimeSeconds { get; set; }
}
