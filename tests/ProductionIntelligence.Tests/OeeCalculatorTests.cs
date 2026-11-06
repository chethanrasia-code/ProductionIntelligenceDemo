using ProductionIntelligence.Domain;

namespace ProductionIntelligence.Tests;

public sealed class OeeCalculatorTests
{
    [Fact]
    public void Calculate_ProducesExpectedShiftMetrics()
    {
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var shift = new ProductionShift
        {
            Id = Guid.NewGuid(),
            LineId = "line-1",
            StartUtc = start,
            EndUtc = start.AddMinutes(450),
            PlannedProductionSeconds = 450m * 60m
        };
        var line = new ProductionLine
        {
            Id = "line-1",
            Name = "Test line",
            IdealCycleTimeSeconds = 30m
        };
        var events = new[]
        {
            Production(shift.Id, start.AddMinutes(10), 700, 14),
            Downtime(shift.Id, start.AddMinutes(120), start.AddMinutes(165), "Jam")
        };

        var result = OeeCalculator.Calculate(shift, line, events);

        Assert.Equal(90m, result.AvailabilityPercent);
        Assert.Equal(86.42m, result.PerformancePercent);
        Assert.Equal(98m, result.QualityPercent);
        Assert.Equal(76.22m, result.OeePercent);
        Assert.Equal(405m, result.RunTimeMinutes);
        Assert.Equal(700, result.TotalUnits);
        Assert.Equal(686, result.GoodUnits);
    }

    [Fact]
    public void GetUnionDowntimeSeconds_DoesNotDoubleCountOverlappingIntervals()
    {
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var events = new[]
        {
            Downtime(Guid.NewGuid(), start.AddMinutes(10), start.AddMinutes(30), "Jam"),
            Downtime(Guid.NewGuid(), start.AddMinutes(20), start.AddMinutes(40), "Sensor")
        };

        var downtime = OeeCalculator.GetUnionDowntimeSeconds(events, start, start.AddHours(1));

        Assert.Equal(30m * 60m, downtime);
    }

    [Fact]
    public void Calculate_DoesNotClampPerformanceAboveOneHundredPercent()
    {
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var shift = new ProductionShift
        {
            Id = Guid.NewGuid(),
            LineId = "line-1",
            StartUtc = start,
            EndUtc = start.AddSeconds(30),
            PlannedProductionSeconds = 30m
        };
        var line = new ProductionLine { Id = "line-1", IdealCycleTimeSeconds = 60m };

        var result = OeeCalculator.Calculate(
            shift,
            line,
            [Production(shift.Id, start.AddSeconds(1), 1, 0)]);

        Assert.Equal(200m, result.PerformancePercent);
        Assert.Equal(200m, result.OeePercent);
    }

    [Fact]
    public void Calculate_WhenThereIsNoRunTime_ReturnsZeroAvailabilityAndPerformance()
    {
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var shift = new ProductionShift
        {
            Id = Guid.NewGuid(),
            LineId = "line-1",
            StartUtc = start,
            EndUtc = start.AddMinutes(30),
            PlannedProductionSeconds = 30m * 60m
        };
        var line = new ProductionLine { Id = "line-1", IdealCycleTimeSeconds = 30m };
        var downtime = Downtime(shift.Id, start, shift.EndUtc, "Equipment fault");

        var result = OeeCalculator.Calculate(shift, line, [downtime]);

        Assert.Equal(0m, result.AvailabilityPercent);
        Assert.Equal(0m, result.PerformancePercent);
        Assert.Equal(0m, result.OeePercent);
    }

    [Fact]
    public void LossAnalyzer_GroupsDowntimeByReasonAndRanksLongestLossFirst()
    {
        var shiftId = Guid.NewGuid();
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var events = new[]
        {
            Downtime(shiftId, start, start.AddMinutes(5), "Jam"),
            Downtime(shiftId, start.AddMinutes(10), start.AddMinutes(22), "Changeover"),
            Downtime(shiftId, start.AddMinutes(30), start.AddMinutes(38), "jam")
        };

        var result = LossAnalyzer.Analyze(events);

        Assert.Equal(25m, result.TotalDowntimeMinutes);
        Assert.Equal("Jam", result.TopDrivers[0].Reason);
        Assert.Equal(13m, result.TopDrivers[0].DurationMinutes);
        Assert.Equal("Changeover", result.TopDrivers[1].Reason);
        Assert.Equal(12m, result.TopDrivers[1].DurationMinutes);
    }

    private static ProductionEvent Production(Guid shiftId, DateTimeOffset at, int units, int rejects) =>
        new()
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftId,
            Type = ProductionEventType.Production,
            TimestampUtc = at,
            UnitsProduced = units,
            RejectedUnits = rejects
        };

    private static ProductionEvent Downtime(
        Guid shiftId,
        DateTimeOffset start,
        DateTimeOffset end,
        string reason) =>
        new()
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftId,
            Type = ProductionEventType.Downtime,
            TimestampUtc = start,
            StartedAtUtc = start,
            EndedAtUtc = end,
            Reason = reason
        };
}
