using ProductionIntelligence.Application;
using ProductionIntelligence.Contracts;
using ProductionIntelligence.Domain;

namespace ProductionIntelligence.Api;

public static class ProductionEndpointMappings
{
    public static void MapProductionEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
            .WithName("Health");

        app.MapGet("/api/lines", async (ProductionService service, CancellationToken cancellationToken) =>
                Results.Ok((await service.GetLinesAsync(cancellationToken))
                    .Select(line => ApiMappings.ToDto(line))))
            .WithName("GetLines");

        app.MapPost("/api/shifts", async (
                CreateShiftRequest request,
                ProductionService service,
                CancellationToken cancellationToken) =>
            {
                var shift = new ProductionShift
                {
                    Id = request.Id,
                    ShiftName = request.ShiftName,
                    LineId = request.LineId,
                    StartUtc = request.StartUtc.ToUniversalTime(),
                    EndUtc = request.EndUtc.ToUniversalTime(),
                    PlannedProductionSeconds = request.PlannedProductionSeconds
                };
                var created = await service.CreateShiftAsync(shift, cancellationToken);
                return Results.Created($"/api/shifts/{created.Id}", ApiMappings.ToDto(created));
            })
            .WithName("CreateShift");

        app.MapGet("/api/shifts", async (
                ProductionService service,
                CancellationToken cancellationToken) =>
                Results.Ok((await service.GetShiftsAsync(cancellationToken)).Select(ApiMappings.ToDto)))
            .WithName("GetShifts");

        app.MapGet("/api/shifts/{shiftId:guid}", async (
                Guid shiftId,
                ProductionService service,
                CancellationToken cancellationToken) =>
            {
                var result = await service.GetOeeAsync(shiftId, cancellationToken);
                return Results.Ok(ApiMappings.ToDto(result.Shift));
            })
            .WithName("GetShift");

        app.MapPost("/api/shifts/{shiftId:guid}/events", async (
                Guid shiftId,
                ProductionEventDto request,
                ProductionService service,
                CancellationToken cancellationToken) =>
            {
                var productionEvent = ApiMappings.ToDomain(request);
                var saved = await service.IngestEventAsync(shiftId, productionEvent, cancellationToken);
                return Results.Ok(ApiMappings.ToDto(saved));
            })
            .WithName("IngestEvent");

        app.MapGet("/api/shifts/{shiftId:guid}/events", async (
                Guid shiftId,
                ProductionService service,
                CancellationToken cancellationToken) =>
                Results.Ok((await service.GetEventsAsync(shiftId, cancellationToken)).Select(ApiMappings.ToDto)))
            .WithName("GetShiftEvents");

        app.MapGet("/api/shifts/{shiftId:guid}/oee", async (
                Guid shiftId,
                ProductionService service,
                CancellationToken cancellationToken) =>
            {
                var result = await service.GetOeeAsync(shiftId, cancellationToken);
                return Results.Ok(new { shift = ApiMappings.ToDto(result.Shift), metrics = result.Metrics });
            })
            .WithName("GetOee");

        app.MapGet("/api/shifts/{shiftId:guid}/losses", async (
                Guid shiftId,
                ProductionService service,
                CancellationToken cancellationToken) =>
            {
                var result = await service.GetLossesAsync(shiftId, cancellationToken);
                return Results.Ok(new { shiftId, shiftName = result.Shift.ShiftName, losses = result.Losses });
            })
            .WithName("GetLosses");

        app.MapPost("/api/shifts/{shiftId:guid}/insights", async (
                Guid shiftId,
                ProductionService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.GenerateInsightAsync(shiftId, cancellationToken)))
            .WithName("GenerateInsight");
    }
}
