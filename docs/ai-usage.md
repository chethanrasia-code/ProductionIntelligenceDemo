# AI usage disclosure and safeguards

## Development assistance

- **Tool used:** Copilot SDK during development of this prototype.
- **Development use:** solution scaffolding, implementation assistance, test cases, documentation, and code review support.
- **Responsibility:** the submitting engineer remains responsible for reviewing, running, explaining, and validating the code and design decisions.

## Runtime AI capability

The runtime AI capability is optional. `ProductionIntelligence.Infrastructure.InsightGenerator` generates a shift summary and recommendations from API-calculated metrics, loss groupings, and event evidence.

Supported modes:

- **Mock:** deterministic template output for offline demonstrations. It is explicitly labeled `Mock (offline demo)` and is not a model call.
- **Ollama:** local OpenAI-compatible chat-completions endpoint and current default configuration.
- **OpenAICompatible:** configurable remote chat-completions provider using an API key supplied through environment or secret configuration.

The provider receives the line, shift, OEE metrics, loss analysis, and event evidence. The API owns authoritative calculations and persists generated insight metadata.

## Grounding and human review

The response contract requires a summary string, summary evidence event IDs, and recommendations with text and evidence event IDs. The application validates that every referenced event belongs to the current shift. Missing or unknown evidence causes the request to fail.

Common provider responses that return the summary as an object are normalized only when a clear text-like field is present; the final application contract remains a string summary. The prompt prohibits invented metrics, events, IDs, or confirmed root causes.

Evidence-ID validation prevents fabricated references but does not prove that every natural-language assertion is semantically correct. Recommendations are hypotheses for human review, not automated decisions. A production deployment should add a reviewed evaluation set, independent metric and claim checks, model/version tracking, schema regression tests, and escalation procedures.

## Privacy and security

- Demo data is synthetic.
- Do not send employee, patient, batch, confidential process, or regulated information to an external provider without formal approval.
- API keys must be supplied through environment variables, a secret manager, or equivalent secure configuration; never commit them.
- The prototype has no authentication or authorization and is not production-ready.
- Providers must be approved for organizational data residency and retention requirements.

## Logging and operations

The API logs request trace information, provider/model, elapsed time, token count when available, HTTP failures, and grounding-validation failures. Daily API and exception logs are written under the API output `logs` directory. The simulator writes run-correlated progress and exception logs under its own output `logs` directory.

Raw prompts and generated content are intentionally excluded from normal operational logs. Exception logs contain stack traces and request metadata and should be protected according to environment access and retention policy.

The API HTTP client timeout is three minutes because local model startup and generation can be slow. AI generation is an on-demand shift-summary capability, not a high-frequency control loop. Keep alarms, event validation, and OEE calculations deterministic.

## Ownership boundary

The project owns the event contract, validation rules, storage abstraction, OEE calculation, loss analysis, and evidence validation. The provider is consumed for language generation. Self-hosting, model selection, residency, cost, and operational governance remain separate deployment decisions.
