# Architecture — 6-slide document

## Slide 1 — Problem and scope

- Shift leads need a clear view of equipment effectiveness and the losses behind the result.
- The prototype flow is: synthetic events → HTTP validation → SQLite persistence → deterministic OEE/loss analysis → optional grounded insight.
- The solution targets a laptop-scale demonstration: one API process, one SQLite database, one simulator client, and no broker or cluster.
- The API hosts a lightweight browser view and development OpenAPI/Swagger tooling.

## Slide 2 — Component architecture

```text
┌──────────────────────┐       HTTP contracts       ┌─────────────────────────────┐
│ Simulator             │ ─────────────────────────> │ ASP.NET Core API            │
│ .NET console client   │                            │ Minimal endpoints + UI      │
└──────────────────────┘                            └──────────────┬──────────────┘
                                                                    │
                                                   Application services / validation
                                                                    │
                              ┌─────────────────────────────────────┴──────────────┐
                              │                                                    │
                     Domain calculations                              Infrastructure adapters
                  OEE + loss analysis                         SQLite store + AI provider client
                              │                                                    │
                              └──────────────────────┬─────────────────────────────┘
                                                     │
                                                SQLite database
```

- `Api` composes application and infrastructure, exposes routes, hosts static files, and handles exceptions.
- `Application` orchestrates use cases and defines `IProductionStore` and `IInsightGenerator`.
- `Domain` contains production entities and deterministic calculations.
- `Infrastructure` implements SQLite persistence and Ollama/OpenAI-compatible insight generation.
- `Contracts` contains shared request/response records; the simulator references this contract project rather than API internals.

## Slide 3 — Data model and ingestion flow

- `ProductionLine`: stable ID, display name, and ideal cycle time in seconds.
- `ProductionShift`: line, UTC boundaries, name, and planned production seconds.
- `ProductionEvent`: GUID, shift ID, type, timestamp, production counts or downtime interval, and optional reason.
- The simulator selects the first two lines by default, creates three eight-hour shifts per line, and submits 10 production plus 5 downtime/changeover events per shift.
- API validation enforces shift existence, route/event identity, UTC boundary containment, valid counts, required reasons, valid intervals, and non-overlapping downtime.
- Event GUIDs provide idempotency: identical retries return the existing event; different data with the same ID conflicts.

## Slide 4 — Metrics and loss analysis

```text
Availability = runtime / planned production time
Performance  = ideal cycle time × total units / runtime
Quality      = good units / total units
OEE          = availability × performance × quality
```

- The API calculates metrics after persistence; neither the simulator nor AI provider is authoritative for OEE.
- Scheduled breaks must already be excluded from planned production seconds.
- Downtime and changeover inside planned production reduce availability.
- Loss analysis groups downtime by reason, duration, and event count.
- Performance above 100% is exposed as a data/configuration signal rather than clamped.
- Zero runtime or zero output produces zero for the applicable ratios.

## Slide 5 — AI approach and safeguards

- The API calculates OEE and losses first, then sends those values and event evidence to the selected provider.
- `Mock` provides deterministic offline template output; `Ollama` is the current local default; `OpenAICompatible` supports an approved remote provider.
- The expected response contains a summary and recommendations with event-ID evidence.
- The application rejects missing or unknown evidence IDs and normalizes common object-shaped summaries only when a text-like value is available.
- The prompt prohibits invented events, IDs, metrics, and confirmed root-cause claims.
- AI output is reviewable decision support, not automated control or authoritative analysis.

## Slide 6 — Operations, observability, and evolution

- Local execution uses .NET 10, SQLite, the ASP.NET Core API, a static UI, and the simulator.
- API request and exception logs are written daily under `logs`; entries include trace IDs, status, duration, and stack traces for server errors.
- Simulator logs include a run ID, progress messages, and exception details.
- The test suite contains 8 deterministic tests for calculations, loss handling, AI response behavior, and evidence validation.
- Synthetic data and local development are assumed; the prototype has no authentication, RBAC, migrations, high availability, or regulated model governance.
- Production evolution: authenticated connectors, EF Core migrations, shift calendars, durable audit, structured observability, approved model hosting, retention controls, and reviewed AI evaluation.
