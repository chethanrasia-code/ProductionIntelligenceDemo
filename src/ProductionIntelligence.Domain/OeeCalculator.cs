namespace ProductionIntelligence.Domain;

public static class OeeCalculator
{
    public static OeeMetrics Calculate(
        ProductionShift shift,
        ProductionLine line,
        IReadOnlyCollection<ProductionEvent> events)
    {
        ArgumentNullException.ThrowIfNull(shift);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(events);

        var plannedSeconds = shift.PlannedProductionSeconds;
        var downtimeSeconds = GetUnionDowntimeSeconds(events, shift.StartUtc, shift.EndUtc);
        var runTimeSeconds = Math.Max(0m, plannedSeconds - downtimeSeconds);
        var totalUnits = events
            .Where(item => item.Type == ProductionEventType.Production)
            .Sum(item => item.UnitsProduced);
        var rejectedUnits = events
            .Where(item => item.Type == ProductionEventType.Production)
            .Sum(item => item.RejectedUnits);
        var goodUnits = totalUnits - rejectedUnits;

        var availability = plannedSeconds == 0 ? 0m : runTimeSeconds / plannedSeconds;
        var performance = runTimeSeconds == 0
            ? 0m
            : line.IdealCycleTimeSeconds * totalUnits / runTimeSeconds;
        var quality = totalUnits == 0 ? 0m : (decimal)goodUnits / totalUnits;

        return new OeeMetrics(
            Percent(availability),
            Percent(performance),
            Percent(quality),
            Percent(availability * performance * quality),
            plannedSeconds / 60m,
            downtimeSeconds / 60m,
            runTimeSeconds / 60m,
            totalUnits,
            goodUnits,
            rejectedUnits,
            line.IdealCycleTimeSeconds);
    }

    public static decimal GetUnionDowntimeSeconds(
        IReadOnlyCollection<ProductionEvent> events,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd)
    {
        var intervals = events
            .Where(item => item.Type is ProductionEventType.Downtime or ProductionEventType.Changeover)
            .Where(item => item.StartedAtUtc.HasValue && item.EndedAtUtc.HasValue)
            .Select(item => (
                Start: item.StartedAtUtc!.Value > rangeStart ? item.StartedAtUtc.Value : rangeStart,
                End: item.EndedAtUtc!.Value < rangeEnd ? item.EndedAtUtc.Value : rangeEnd))
            .Where(interval => interval.End > interval.Start)
            .OrderBy(interval => interval.Start)
            .ToArray();

        if (intervals.Length == 0)
        {
            return 0m;
        }

        var seconds = 0m;
        var currentStart = intervals[0].Start;
        var currentEnd = intervals[0].End;
        foreach (var interval in intervals.Skip(1))
        {
            if (interval.Start <= currentEnd)
            {
                if (interval.End > currentEnd)
                {
                    currentEnd = interval.End;
                }

                continue;
            }

            seconds += (decimal)(currentEnd - currentStart).TotalSeconds;
            currentStart = interval.Start;
            currentEnd = interval.End;
        }

        return seconds + (decimal)(currentEnd - currentStart).TotalSeconds;
    }

    private static decimal Percent(decimal ratio) => decimal.Round(ratio * 100m, 2);
}
