# Production Intelligence — Implementation decisions and assumptions

## 1. Scope and primary workflow

This project is a local manufacturing-efficiency prototype. Its primary workflow is:

```text
Synthetic events
    -> HTTP API validation
    -> SQLite persistence
    -> OEE and loss analysis
    -> Optional evidence-grounded insight
```

It is not a production-ready industrial control system. The API is the system of record for validation and calculations; the simulator is a repeatable client.

## 2. Modular monolith decision

A modular monolith was selected instead of microservices to keep local execution simple while separating responsibilities and preserving replaceable boundaries.

| Project | Responsibility |
|---|---|
| `ProductionIntelligence.Api` | HTTP endpoints, middleware, static UI, startup, and dependency composition |
| `ProductionIntelligence.Application` | Use cases, validation, orchestration, abstractions, and insight grounding |
| `ProductionIntelligence.Domain` | Production entities, OEE calculation, and loss analysis |
| `ProductionIntelligence.Infrastructure` | EF Core SQLite store and AI provider integration |
| `ProductionIntelligence.Contracts` | Shared HTTP DTOs and event enum contract |
| `ProductionIntelligence.Simulator` | Repeatable synthetic data generation and HTTP ingestion |
| `ProductionIntelligence.Tests` | Deterministic unit tests |

The application layer depends on domain types and interfaces. Infrastructure supplies implementations, and the API composes the graph.

## 3. Domain and contract decisions

The primary domain entities are `ProductionLine`, `ProductionShift`, and `ProductionEvent`:

```text
ProductionLine
    -> has many ProductionShifts

ProductionShift
    -> belongs to one ProductionLine
    -> has many ProductionEvents
```

A shift is the main unit of analysis because OEE, losses, and an insight are generated for a shift.

API contracts use records such as `CreateShiftRequest` and `ProductionEventDto` because they are value-oriented transport data. Domain and EF Core objects use classes because they represent mutable entities with identity and persistence state. The simulator references only the contracts project.

## 4. SQLite and initialization decisions

SQLite is appropriate for a laptop-scale, single-process demonstration because it requires no database server and is easy to inspect and reset. The API uses `EnsureCreatedAsync` at startup, adds the current `ShiftName` column when needed, and seeds `line-1` and `line-2` if the line table is empty.

Startup does not generate shifts or events. The simulator creates those later through the public HTTP API. A production system should use EF Core migrations, managed database infrastructure, backup, concurrency planning, and retention policies.

## 5. Simulator decision

The simulator is a separate .NET console application with its own trace and exception logging. Each run optionally resets Development data, selects the first two API lines by default or one line with `--line`, creates three eight-hour shifts per selected line, and sends ten production events plus five downtime/changeover events per shift.

Sample counts, timings, and reasons are deterministic while event and shift GUIDs are fresh. The simulator supports `--base-url`, `--interval-ms`, `--reset`, and `--help`. It does not calculate OEE or call the insight endpoint; those responsibilities remain in the API.

## 6. OEE and loss decisions

```text
Availability = Runtime / Planned Production Time
Performance  = Ideal Cycle Time × Total Units / Runtime
Quality      = Good Units / Total Units
OEE          = Availability × Performance × Quality
```

Assumptions:

- The caller excludes scheduled breaks from `PlannedProductionSeconds`.
- Downtime and changeover within planned production reduce availability.
- Runtime is based on planned time minus the union of downtime intervals.
- Performance above 100% remains visible rather than silently clamped.
- Zero runtime and zero output return zero for the relevant ratios.
- The API, not the simulator or AI provider, calculates authoritative metrics.

Loss analysis groups downtime by reason, duration, and count. Ingestion rejects overlapping downtime intervals so interval-based availability and reason totals remain explainable.

## 7. Event validation and idempotency decisions

Before persistence, the API validates that the shift exists, the event shift ID matches the route, timestamps are within the shift, counts are valid, rejected units do not exceed produced units, downtime/changeover intervals are valid and have reasons, downtime does not exceed planned production, and intervals do not overlap.

Event IDs are idempotency keys. An identical retry returns the existing event. Reusing an ID with different data returns a conflict. This supports safe client retries and future machine-ingestion adapters.

## 8. Storage abstraction decision

The application depends on `IProductionStore` rather than SQLite or EF Core directly. The current `SqliteProductionStore` can therefore be replaced by SQL Server, PostgreSQL, or another persistence implementation without changing use-case orchestration.

## 9. AI provider decision

AI is limited to shift summaries and recommendations. The API calculates metrics and loss analysis first, then supplies those results and event evidence to `InsightGenerator`.

Supported modes:

- `Mock`: deterministic offline template output, explicitly labeled as mock and not a model call.
- `Ollama`: local OpenAI-compatible `/chat/completions` endpoint; current default configuration.
- `OpenAICompatible`: remote provider with `AI:ApiKey`, `AI:BaseUrl`, and `AI:Model` supplied through configuration.

The API uses a three-minute HTTP client timeout because local model loading and generation can be slow. Provider failures and malformed responses are surfaced rather than silently falling back. Common object-shaped summary responses are normalized only when a text-like field is available.

## 10. AI grounding and safety decision

Every summary and recommendation must cite event IDs from the current shift. The API rejects missing or unknown references, including references from another shift. The prompt prohibits invented IDs, events, metrics, and confirmed root causes.

The evidence check protects traceability but does not prove semantic correctness. Recommendations remain hypotheses for human review and cannot control equipment or replace plant procedures.

## 11. API and UI decisions

Minimal API endpoints were selected for the focused surface: health, lines, shifts, events, OEE, losses, insights, and Development-only reset. Endpoint registration, DTO mapping, and exception handling are separated into `ProductionEndpointMappings.cs`, `ApiMappings.cs`, and `ApiExceptionHandling.cs`.

The API hosts the lightweight browser UI through `UseDefaultFiles` and `UseStaticFiles`, so the demo requires no separate frontend server. Swagger/OpenAPI is enabled for development.

## 12. Logging and observability decisions

The API uses built-in `Microsoft.Extensions.Logging` for console/debug output and a small file logger for daily request and exception files. API request logs include trace IDs, status codes, and elapsed time; exception files include request metadata and stack traces.

The simulator uses a run-correlated file logger for progress and exceptions. Raw AI prompts and generated content are excluded from normal operational logs. Production should add centralized structured logs, metrics, distributed tracing, alerting, retention, and audit controls.

## 13. Security and non-functional assumptions

The current prototype assumes synthetic data, local development, no authentication or authorization, no confidential data sent to external AI providers, API keys supplied through environment or secret configuration, one API process, and a local SQLite database.

It does not target high availability, horizontal scaling, high-frequency telemetry, multi-tenancy, or regulated AI deployment.

## Overall conclusion

The decisions create a clear, testable, locally runnable prototype while preserving a path toward production evolution. The essential boundaries are:

1. API-owned validation and authoritative calculations.
2. Domain-owned deterministic OEE and loss rules.
3. Application-owned orchestration and evidence validation.
4. Infrastructure-owned persistence and provider integration.
5. Simulator-owned repeatable demo input only.
6. AI-owned language generation only, with human review required.
