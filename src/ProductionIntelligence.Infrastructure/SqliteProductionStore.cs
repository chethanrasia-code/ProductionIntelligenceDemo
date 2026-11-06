using Microsoft.EntityFrameworkCore;
using ProductionIntelligence.Application;
using ProductionIntelligence.Domain;

namespace ProductionIntelligence.Infrastructure;

public sealed class SqliteProductionStore(ProductionDbContext dbContext) : IProductionStore
{
    public async Task<IReadOnlyList<ProductionLine>> GetLinesAsync(CancellationToken cancellationToken) =>
        await dbContext.Lines.AsNoTracking().OrderBy(line => line.Id).ToListAsync(cancellationToken);

    public Task<ProductionLine?> GetLineAsync(string id, CancellationToken cancellationToken) =>
        dbContext.Lines.AsNoTracking().SingleOrDefaultAsync(line => line.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ProductionShift>> GetShiftsAsync(CancellationToken cancellationToken) =>
        (await dbContext.Shifts.AsNoTracking().ToListAsync(cancellationToken))
            .OrderByDescending(shift => shift.StartUtc)
            .ThenByDescending(shift => shift.Id)
            .ToArray();

    public Task<ProductionShift?> GetShiftAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Shifts.AsNoTracking().SingleOrDefaultAsync(shift => shift.Id == id, cancellationToken);

    public async Task AddShiftAsync(ProductionShift shift, CancellationToken cancellationToken)
    {
        dbContext.Shifts.Add(shift);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductionEvent>> GetEventsAsync(
        Guid shiftId,
        CancellationToken cancellationToken) =>
        await dbContext.Events.AsNoTracking()
            .Where(item => item.ShiftId == shiftId)
            .ToListAsync(cancellationToken);

    public Task<ProductionEvent?> GetEventAsync(Guid eventId, CancellationToken cancellationToken) =>
        dbContext.Events.AsNoTracking().SingleOrDefaultAsync(item => item.Id == eventId, cancellationToken);

    public async Task AddEventAsync(ProductionEvent productionEvent, CancellationToken cancellationToken)
    {
        dbContext.Events.Add(productionEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetDemoDataAsync(CancellationToken cancellationToken)
    {
        await dbContext.Events.ExecuteDeleteAsync(cancellationToken);
        await dbContext.Shifts.ExecuteDeleteAsync(cancellationToken);
    }
}
