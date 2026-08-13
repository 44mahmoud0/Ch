# Mahmoud AI Integration Boundaries

## Purpose

Mahmoud AI integrates third-party libraries as replaceable adapters, not as alternate runtime authorities. This boundary keeps the Windows desktop application auditable and preserves the semantics already established by TaskGraph V2.

## Authority model

| Concern | Mahmoud-owned authority | Third-party libraries may provide |
|---|---|---|
| Mission ordering, retries, dependencies, cancellation, and terminal results | TaskGraph V2 | No orchestration authority |
| Whether an operation is permitted | AdvancedPermissionBroker | Capability metadata and operation mechanics only |
| Emergency Stop and Safe Mode | Mission runtime plus permission broker | Cooperative cancellation; child-process termination hooks |
| Recoverable mission state and event history | SQLite mission store | SQL/ADO.NET mapping helpers |
| Semantic retrieval | Memory gateway and Qdrant adapter | Vector indexing and search |
| Provider interoperability | Model gateway and model router | Provider protocol/client implementation |
| Tool protocol | MCP gateway and capability mapping | MCP transport and schema handling |
| Windows UI mechanics | Automation gateway and policy wrapper | UIA, Win32, capture, input primitives |
| Speech and OCR inference | Speech/screen gateways | Model inference and native runtime |
| Metrics and traces | Redaction policy and mission telemetry | OpenTelemetry exporters and SDK plumbing |

## Trust boundaries

Third-party model clients, MCP servers, native automation libraries, speech runtimes, OCR binaries, and vector services are **untrusted integrations**. They must not receive raw permission leases, arbitrary filesystem paths, unrestricted process handles, secrets, or direct access to WinUI controls.

Every side effect must carry a mission identifier, task identifier where applicable, a normalized scope, a required capability, and a cancellation token. The adapter is responsible for translating the request into provider-specific mechanics; the runtime remains responsible for authorization, event emission, idempotency, and terminal-state handling.

MCP servers run as isolated processes or through an explicitly governed remote transport. A listed MCP tool is metadata, not authorization. The `PluginExecution` capability must be checked again at invocation time, and tool arguments must be validated against the normalized schema before execution.

Windows automation must prefer semantic UI Automation patterns over physical input. Physical mouse and keyboard input are fallback operations with separate capability types and must execute through a worker boundary that can be cancelled or terminated after Emergency Stop. Raw P/Invoke, COM objects, window handles, and process handles stay inside the adapter implementation.

Telemetry is observational only. Prompt text, OCR text, screenshots, transcripts, access tokens, credentials, and arbitrary file contents must be excluded or redacted before export. Telemetry failure must not silently create a second mission state store.

## Event and durability rules

TaskGraph V2 emits lifecycle events through `IMissionEventSink`. The first production sink is a composite that writes idempotently to SQLite and fans out to an in-process event hub for the WinUI dashboard. Event persistence uses a deterministic event identity and `INSERT OR IGNORE`, so retries or duplicate delivery do not duplicate history. A persistence failure is surfaced to the mission boundary rather than discarded.

SQLite is the canonical recoverable event store. OpenTelemetry, in-memory telemetry bags, dashboards, and vector memory are projections or indexes; none of them may be used as the source of truth for mission recovery.

## Adapter contract requirements

Each provider adapter must demonstrate cancellation, streaming termination, tool-schema normalization, timeout behavior, and failure classification. Each Windows automation adapter must demonstrate that authorization precedes every physical side effect and that the worker terminates after cancellation or Emergency Stop. Speech and OCR adapters must report provenance, language/model identifiers, and confidence where available. All adapters must preserve `CancellationToken` semantics and avoid fire-and-forget work.

## Deliberate dependency policy

No high-risk dependency is promoted to the default runtime merely because it is popular. Before adding a package, the repository must record its license, target frameworks, native assets, transitive dependency impact, Windows support, release status, and rollback strategy. Preview or experimental projects are references and test fixtures until a stable API and Windows smoke test exist.

The current low-risk slice therefore adds Mahmoud-owned contracts and durable event infrastructure first. Microsoft.Extensions.AI, FlaUI, the official MCP C# SDK, speech runtimes, OCR engines, OpenTelemetry exporters, and Quartz.NET remain adapter-specific follow-up work and must enter through these boundaries.
