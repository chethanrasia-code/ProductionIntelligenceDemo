# Production Intelligence Demo

A .NET 10 demo application for manufacturing production intelligence. It accepts production and downtime events, calculates Overall Equipment Effectiveness (OEE), analyzes losses, and generates evidence-grounded shift insights with an AI provider.

The repository includes a minimal web UI, a REST API, a synthetic production-data simulator, and automated domain tests.

## Features

- Production lines and shift management
- Production, downtime, and changeover event ingestion
- Deterministic OEE calculation for availability, performance, quality, and total OEE
- Downtime loss analysis grouped by reason
- Evidence-grounded AI insights with event-ID validation
- SQLite persistence with automatic database initialization
- Swagger/OpenAPI in the Development environment
- Synthetic data generation for two lines and three shifts per line
- Offline `Mock` insight generation for local demos without an AI service

## Technology

- .NET 10
- ASP.NET Core Minimal APIs
- Entity Framework Core with SQLite
- xUnit
- Ollama or another OpenAI-compatible chat-completions provider for insights

## Repository structure

```text
src/
  ProductionIntelligence.Api/            ASP.NET Core API, static UI, and SQLite database
  ProductionIntelligence.Application/   Application services and validation
  ProductionIntelligence.Contracts/     API request and response contracts
  ProductionIntelligence.Domain/        OEE, loss analysis, and production domain models
  ProductionIntelligence.Infrastructure/SQLite store and AI insight integration
  ProductionIntelligence.Simulator/     Synthetic production-data client

tests/
  ProductionIntelligence.Tests/         Domain and application tests
```

## Prerequisites

- .NET 10 SDK
- Optional: Ollama running locally with the configured model, or an OpenAI-compatible API

Verify the SDK:

```powershell
dotnet --version
```

## Run the API

From the repository root:

```powershell
dotnet run --project .\src\ProductionIntelligence.Api --launch-profile http
```

The HTTP profile listens on `http://localhost:5258`. The HTTPS profile also exposes `https://localhost:7070`.

When the API is running:

- Open the demo UI at [http://localhost:5258](http://localhost:5258)
- Open Swagger at [http://localhost:5258/swagger](http://localhost:5258/swagger)
- Check health at [http://localhost:5258/api/health](http://localhost:5258/api/health)

The API creates `src/ProductionIntelligence.Api/production-intelligence.db` when it starts. In Development, `POST /api/demo/reset` removes existing demo shifts and events.

## Run the simulator

Start the API first, then run the simulator in a second terminal:

```powershell
dotnet run --project .\src\ProductionIntelligence.Simulator -- --reset
```

The simulator uses `http://localhost:5258` by default. It creates three eight-hour shifts for each of the first two production lines and sends synthetic production and downtime events. The generated shift IDs are logged to the console and to the simulator's `logs` directory.

Available options:

```text
--base-url <url>       API base URL; default: http://localhost:5258
--line <line-id>       Simulate one line, for example line-1
--reset                Reset demo shifts and events before simulation
--interval-ms <value>  Delay between events; default: 0
--help                 Show usage information
```

Example:

```powershell
dotnet run --project .\src\ProductionIntelligence.Simulator -- --base-url http://localhost:5258 --line line-1 --reset --interval-ms 100
```

After the simulator finishes, paste a printed shift ID into the web UI to view OEE, losses, events, and an insight.

## AI configuration

The default configuration uses Ollama:

```json
{
  "AI": {
	"Mode": "Ollama",
	"BaseUrl": "http://localhost:11434/v1/",
	"Model": "llama3.2"
  }
}
```

For an offline demo, use the `Mock` mode. This avoids external AI calls and returns a deterministic template insight:

```powershell
$env:AI__Mode = "Mock"
dotnet run --project .\src\ProductionIntelligence.Api --launch-profile http
```

For an OpenAI-compatible provider, configure `AI:Mode` as `OpenAICompatible`, set `AI:BaseUrl` and `AI:Model`, and provide the API key through an environment variable rather than committing it to source control:

```powershell
$env:AI__Mode = "OpenAICompatible"
$env:AI__BaseUrl = "https://your-provider.example/v1/"
$env:AI__Model = "your-model"
$env:AI__ApiKey = "your-api-key"
dotnet run --project .\src\ProductionIntelligence.Api --launch-profile http
```

Supported modes are `Mock`, `Ollama`, and `OpenAICompatible`.

## API overview

| Method | Route | Description |
| --- | --- | --- |
| `GET` | `/api/health` | Health check |
| `GET` | `/api/lines` | List production lines |
| `POST` | `/api/shifts` | Create a production shift |
| `GET` | `/api/shifts` | List shifts |
| `GET` | `/api/shifts/{shiftId}` | Get a shift |
| `POST` | `/api/shifts/{shiftId}/events` | Ingest a production, downtime, or changeover event |
| `GET` | `/api/shifts/{shiftId}/events` | List shift events |
| `GET` | `/api/shifts/{shiftId}/oee` | Calculate OEE metrics |
| `GET` | `/api/shifts/{shiftId}/losses` | Analyze downtime losses |
| `POST` | `/api/shifts/{shiftId}/insights` | Generate a validated shift insight |

See Swagger while running in Development for request and response schemas.

## Build and test

Build the solution:

```powershell
dotnet build .\ProductionIntelligence.slnx
```

Run all tests:

```powershell
dotnet test .\ProductionIntelligence.slnx
```

The tests cover OEE calculations, downtime interval unioning, performance behavior, zero-runtime behavior, and loss-driver ranking.

## OEE calculation

OEE is calculated from the shift, line, and stored events:

- **Availability** = runtime / planned production time
- **Performance** = ideal cycle time × total units / runtime
- **Quality** = good units / total units
- **OEE** = availability × performance × quality

Values are returned as percentages. Downtime intervals are clipped to the shift range and unioned so overlapping intervals are not double-counted. The API also rejects overlapping downtime/changeover events during ingestion.

## Notes

- The application is intended as a local demonstration rather than a production deployment template.
- The SQLite database is local application state and is not required in source control; it is created automatically if missing.
- AI-generated recommendations are treated as hypotheses for human review and are validated against the event evidence supplied to the model.
