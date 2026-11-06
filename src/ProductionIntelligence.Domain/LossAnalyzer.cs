namespace ProductionIntelligence.Domain;

public static class LossAnalyzer
{
    public static LossAnalysis Analyze(IReadOnlyCollection<ProductionEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var drivers = events
            .Where(item => item.Type is ProductionEventType.Downtime or ProductionEventType.Changeover)
            .GroupBy(item => string.IsNullOrWhiteSpace(item.Reason) ? "Unspecified" : item.Reason.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ids = group.Select(item => item.Id).ToArray();
                var seconds = group.Sum(item =>
                    (decimal)(item.EndedAtUtc!.Value - item.StartedAtUtc!.Value).TotalSeconds);
                return new LossDriver(group.Key, group.Count(), decimal.Round(seconds / 60m, 2), ids);
            })
            .OrderByDescending(driver => driver.DurationMinutes)
            .ThenBy(driver => driver.Reason, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new LossAnalysis(
            drivers.Sum(driver => driver.DurationMinutes),
            drivers);
    }
}
