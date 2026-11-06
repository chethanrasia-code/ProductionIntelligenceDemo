using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ProductionIntelligence.Api;
using ProductionIntelligence.Application;
using ProductionIntelligence.Domain;
using ProductionIntelligence.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ProductionDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Production")
        ?? "Data Source=production-intelligence.db"));
builder.Services.AddScoped<IProductionStore, SqliteProductionStore>();
builder.Services.AddScoped<ProductionService>();
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));
var aiMode = builder.Configuration[$"{AiOptions.SectionName}:Mode"] ?? "Ollama";
if (!string.Equals(aiMode, "Mock", StringComparison.OrdinalIgnoreCase) &&
    !string.Equals(aiMode, "Ollama", StringComparison.OrdinalIgnoreCase) &&
    !string.Equals(aiMode, "OpenAICompatible", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("AI:Mode must be 'Ollama', 'Mock', or 'OpenAICompatible'.");
}

builder.Services.AddHttpClient<IInsightGenerator, InsightGenerator>(client =>
    client.Timeout = TimeSpan.FromMinutes(3));

var app = builder.Build();

app.UseApiExceptionHandling();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapProductionEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapPost("/api/demo/reset", async (
        ProductionService service,
        CancellationToken cancellationToken) =>
    {
        await service.ResetDemoDataAsync(cancellationToken);
        return Results.Ok(new { message = "Demo shifts and events were removed." });
    }).WithName("ResetDemoData");
}

await InitializeDatabaseAsync(app);
app.Run();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ProductionDbContext>();
    await db.Database.EnsureCreatedAsync();
    var hasShiftName = await db.Database
        .SqlQueryRaw<int>("SELECT COUNT(*) AS \"Value\" FROM pragma_table_info('Shifts') WHERE name = 'ShiftName'")
        .SingleAsync() > 0;
    if (!hasShiftName)
    {
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Shifts\" ADD COLUMN \"ShiftName\" TEXT NOT NULL DEFAULT ''");
    }

    if (!await db.Lines.AnyAsync())
    {
        db.Lines.AddRange(
            new ProductionLine
            {
                Id = "line-1",
                Name = "Packaging Line 1",
                IdealCycleTimeSeconds = 30m
            },
            new ProductionLine
            {
                Id = "line-2",
                Name = "Filling Line 2",
                IdealCycleTimeSeconds = 24m
            });
        await db.SaveChangesAsync();
    }
}

public partial class Program
{
}
