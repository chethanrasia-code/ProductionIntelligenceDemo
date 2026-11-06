using ProductionIntelligence.Contracts;
using ProductionIntelligence.Domain;

namespace ProductionIntelligence.Api;

internal static class ApiMappings
{
    public static ProductionLineDto ToDto(ProductionLine line) =>
        new(line.Id, line.Name, line.IdealCycleTimeSeconds);

    public static ProductionShiftDto ToDto(ProductionShift shift) =>
        new(shift.Id, shift.ShiftName, shift.LineId, shift.StartUtc, shift.EndUtc, shift.PlannedProductionSeconds);

    public static ProductionEventDto ToDto(ProductionEvent item) =>
        new(
            item.Id,
            item.ShiftId,
            (ProductionEventTypeContract)item.Type,
            item.TimestampUtc,
            item.StartedAtUtc,
            item.EndedAtUtc,
            item.UnitsProduced,
            item.RejectedUnits,
            item.Reason);

    public static ProductionEvent ToDomain(ProductionEventDto item) =>
        new()
        {
            Id = item.Id,
            ShiftId = item.ShiftId,
            Type = (ProductionEventType)item.Type,
            TimestampUtc = item.TimestampUtc.ToUniversalTime(),
            StartedAtUtc = item.StartedAtUtc?.ToUniversalTime(),
            EndedAtUtc = item.EndedAtUtc?.ToUniversalTime(),
            UnitsProduced = item.UnitsProduced,
            RejectedUnits = item.RejectedUnits,
            Reason = item.Reason
        };
}
