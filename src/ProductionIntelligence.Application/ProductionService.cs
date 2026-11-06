using ProductionIntelligence.Domain;
using Microsoft.Extensions.Logging;

namespace ProductionIntelligence.Application;

public sealed class ProductionService(
    IProductionStore store,
    IInsightGenerator insightGenerator,
    ILogger<ProductionService> logger)
{
    public Task<IReadOnlyList<ProductionLine>> GetLinesAsync(CancellationToken cancellationToken) =>
        store.GetLinesAsync(cancellationToken);

    public Task<IReadOnlyList<ProductionShift>> GetShiftsAsync(CancellationToken cancellationToken) =>
        store.GetShiftsAsync(cancellationToken);

    public async Task<ProductionShift> CreateShiftAsync(
        ProductionShift shift,
        CancellationToken cancellationToken)
    {
        if (shift.Id == Guid.Empty)
        {
            throw new RequestValidationException("Shift id must be a non-empty GUID.");
        }

        if (string.IsNullOrWhiteSpace(shift.ShiftName))
        {
            throw new RequestValidationException("Shift name is required.");
        }

        if (string.IsNullOrWhiteSpace(shift.LineId))
        {
            throw new RequestValidationException("Line id is required.");
        }

        _ = await store.GetLineAsync(shift.LineId, cancellationToken)
            ?? throw new ResourceNotFoundException($"Line '{shift.LineId}' was not found.");
        if (shift.StartUtc >= shift.EndUtc)
        {
            throw new RequestValidationException("Shift end must be later than shift start.");
        }

        var elapsedSeconds = (decimal)(shift.EndUtc - shift.StartUtc).TotalSeconds;
        if (shift.PlannedProductionSeconds <= 0 || shift.PlannedProductionSeconds > elapsedSeconds)
        {
            throw new RequestValidationException(
                "Planned production seconds must be positive and no longer than the shift.");
        }

        if (await store.GetShiftAsync(shift.Id, cancellationToken) is not null)
        {
            throw new RequestConflictException($"Shift '{shift.Id}' already exists.");
        }

        await store.AddShiftAsync(shift, cancellationToken);
        return shift;
    }

    public async Task<ProductionEvent> IngestEventAsync(
        Guid routeShiftId,
        ProductionEvent item,
        CancellationToken cancellationToken)
    {
        var shift = await store.GetShiftAsync(routeShiftId, cancellationToken)
            ?? throw new ResourceNotFoundException($"Shift '{routeShiftId}' was not found.");
        if (item.ShiftId != routeShiftId)
        {
            throw new RequestValidationException("Event shift id must match the route shift id.");
        }

        ValidateEvent(item, shift);
        var existing = await store.GetEventAsync(item.Id, cancellationToken);
        if (existing is not null)
        {
            if (EventsEqual(existing, item))
            {
                return existing;
            }

            throw new RequestConflictException($"Event '{item.Id}' already exists with different data.");
        }

        var events = await store.GetEventsAsync(routeShiftId, cancellationToken);
        if (item.Type is ProductionEventType.Downtime or ProductionEventType.Changeover)
        {
            var overlaps = events.Any(existingEvent =>
                existingEvent.Type is ProductionEventType.Downtime or ProductionEventType.Changeover
                && item.StartedAtUtc < existingEvent.EndedAtUtc
                && item.EndedAtUtc > existingEvent.StartedAtUtc);
            if (overlaps)
            {
                throw new RequestConflictException(
                    "Downtime intervals for a shift cannot overlap; submit one event with the combined reason instead.");
            }

            var combinedDowntime = events.Append(item).ToArray();
            if (OeeCalculator.GetUnionDowntimeSeconds(combinedDowntime, shift.StartUtc, shift.EndUtc) >
                shift.PlannedProductionSeconds)
            {
                throw new RequestValidationException(
                    "Recorded downtime cannot exceed the shift's planned production time.");
            }
        }

        await store.AddEventAsync(item, cancellationToken);
        return item;
    }

    public async Task<(ProductionShift Shift, OeeMetrics Metrics)> GetOeeAsync(
        Guid shiftId,
        CancellationToken cancellationToken)
    {
        var shift = await GetShiftOrThrowAsync(shiftId, cancellationToken);
        var line = await store.GetLineAsync(shift.LineId, cancellationToken)
            ?? throw new ResourceNotFoundException($"Line '{shift.LineId}' was not found.");
        var events = await store.GetEventsAsync(shiftId, cancellationToken);
        return (shift, OeeCalculator.Calculate(shift, line, events));
    }

    public async Task<IReadOnlyList<ProductionEvent>> GetEventsAsync(
        Guid shiftId,
        CancellationToken cancellationToken)
    {
        _ = await GetShiftOrThrowAsync(shiftId, cancellationToken);
        return (await store.GetEventsAsync(shiftId, cancellationToken))
            .OrderBy(item => item.TimestampUtc)
            .ThenBy(item => item.Id)
            .ToArray();
    }

    public async Task<(ProductionShift Shift, LossAnalysis Losses)> GetLossesAsync(
        Guid shiftId,
        CancellationToken cancellationToken)
    {
        var shift = await GetShiftOrThrowAsync(shiftId, cancellationToken);
        var events = await store.GetEventsAsync(shiftId, cancellationToken);
        return (shift, LossAnalyzer.Analyze(events));
    }

    public async Task<GeneratedInsight> GenerateInsightAsync(
        Guid shiftId,
        CancellationToken cancellationToken)
    {
        var shift = await GetShiftOrThrowAsync(shiftId, cancellationToken);
        var line = await store.GetLineAsync(shift.LineId, cancellationToken)
            ?? throw new ResourceNotFoundException($"Line '{shift.LineId}' was not found.");
        var events = await store.GetEventsAsync(shiftId, cancellationToken);
        if (events.Count == 0)
        {
            throw new RequestValidationException("Ingest production or downtime events before requesting insights.");
        }

        var metrics = OeeCalculator.Calculate(shift, line, events);
        var losses = LossAnalyzer.Analyze(events);
        var draft = await insightGenerator.GenerateAsync(line, shift, metrics, losses, events, cancellationToken);
        var errors = InsightValidator.Validate(draft, events.Select(item => item.Id).ToHashSet());
        if (errors.Count > 0)
        {
            logger.LogWarning("Insight validation failed for shift {ShiftId}: {ValidationErrors}",
                shiftId, string.Join(" ", errors));
            throw new RequestValidationException("Generated insight failed grounding checks: " + string.Join(" ", errors));
        }

        return new GeneratedInsight(
            shift.Id,
            shift.ShiftName,
            insightGenerator.Provider,
            insightGenerator.Model,
            DateTimeOffset.UtcNow,
            draft.Summary,
            draft.EvidenceEventIds,
            draft.Recommendations);
    }

    public Task ResetDemoDataAsync(CancellationToken cancellationToken) =>
        store.ResetDemoDataAsync(cancellationToken);

    private async Task<ProductionShift> GetShiftOrThrowAsync(Guid shiftId, CancellationToken cancellationToken) =>
        await store.GetShiftAsync(shiftId, cancellationToken)
        ?? throw new ResourceNotFoundException($"Shift '{shiftId}' was not found.");

    private static void ValidateEvent(ProductionEvent item, ProductionShift shift)
    {
        if (item.Id == Guid.Empty)
        {
            throw new RequestValidationException("Event id must be a non-empty GUID.");
        }

        if (item.TimestampUtc < shift.StartUtc || item.TimestampUtc >= shift.EndUtc)
        {
            throw new RequestValidationException("Event timestamp must fall within the shift.");
        }

        if (item.Type == ProductionEventType.Production)
        {
            if (item.UnitsProduced <= 0 || item.RejectedUnits < 0 || item.RejectedUnits > item.UnitsProduced)
            {
                throw new RequestValidationException(
                    "Production units must be positive and rejected units must be between zero and total units.");
            }

            if (item.StartedAtUtc.HasValue || item.EndedAtUtc.HasValue || !string.IsNullOrWhiteSpace(item.Reason))
            {
                throw new RequestValidationException(
                    "Production events use unit counts only; interval and reason fields are for downtime events.");
            }
        }
        else if (item.Type is ProductionEventType.Downtime or ProductionEventType.Changeover)
        {
            if (item.StartedAtUtc is null || item.EndedAtUtc is null ||
                item.StartedAtUtc < shift.StartUtc || item.EndedAtUtc > shift.EndUtc ||
                item.StartedAtUtc >= item.EndedAtUtc ||
                item.TimestampUtc != item.StartedAtUtc.Value)
            {
                throw new RequestValidationException(
                    "Downtime interval must be positive, fully contained within the shift, and timestamped at its start.");
            }

            if (string.IsNullOrWhiteSpace(item.Reason))
            {
                throw new RequestValidationException("Downtime and changeover events require a reason.");
            }

            if (item.UnitsProduced != 0 || item.RejectedUnits != 0)
            {
                throw new RequestValidationException("Downtime events cannot include production counts.");
            }
        }
        else
        {
            throw new RequestValidationException("Unsupported production event type.");
        }
    }

    private static bool EventsEqual(ProductionEvent left, ProductionEvent right) =>
        left.Id == right.Id &&
        left.ShiftId == right.ShiftId &&
        left.Type == right.Type &&
        left.TimestampUtc == right.TimestampUtc &&
        left.StartedAtUtc == right.StartedAtUtc &&
        left.EndedAtUtc == right.EndedAtUtc &&
        left.UnitsProduced == right.UnitsProduced &&
        left.RejectedUnits == right.RejectedUnits &&
        string.Equals(left.Reason, right.Reason, StringComparison.Ordinal);
}
