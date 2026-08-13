# Mahmoud AI Research and Implementation TODO

- [x] Inventory the independent desktop repository and separate it from Still / Signal.
- [x] Review Microsoft Agent Framework for production multi-agent workflow patterns.
- [x] Review Ollama for optional local model serving.
- [x] Review the official C# Model Context Protocol SDK.
- [x] Review Microsoft WinUI Gallery for native UI and accessibility patterns.
- [x] Review LLamaSharp for embedded local inference.
- [x] Review Whisper.net for local speech-to-text and VAD.
- [x] Review Microsoft Kernel Memory and record its archived/reference-only status.
- [ ] Replace the hand-written MCP placeholder with a real official MCP SDK adapter after package/version validation.
- [ ] Implement the real local memory store with SQLite and durable mission records.
- [ ] Add a local model provider abstraction with Ollama health check and cancellation.
- [ ] Add robust TaskGraph cancellation, failure propagation, timeout, and retry policies.
- [ ] Add security policy and audit events before exposing filesystem or shell tools.
- [ ] Add WinUI 3 application entry point, navigation shell, mission view, approval view, and diagnostics view.
- [ ] Add feature-level unit and integration tests for the new capabilities.
- [ ] Add Windows packaging workflow, MSIX path, and reproducible release documentation.
- [ ] Verify all supported features on Windows or document Windows-only verification gaps.
- [ ] Publish the independent desktop repository and important release files to GitHub.

## New architecture-audit backlog

- [ ] Adopt a real Microsoft Agent Framework adapter without replacing the Mahmoud Mission Runtime or Permission Broker.
- [ ] Replace the provider fallback response with typed local/cloud provider contracts, health checks, retries, streaming, privacy policy, and budget enforcement.
- [ ] Expand SQLite into the authoritative state store with profiles, missions, tasks, dependencies, grants, timeline, artifacts, experiences, and checkpoints.
- [ ] Replace the automatic permission grant behavior with deterministic tool descriptors, normalized scopes, approval decisions, capability leases, and audit events.
- [ ] Implement Workspace Isolation with path normalization, traversal/junction escape protection, and per-Mission artifact roots.
- [ ] Replace the OCR simulated result with a Windows-specific capture/OCR adapter and a safe cross-platform fallback.
- [ ] Add a UI Automation-first screen observation pipeline with capture/OCR/vision fallback provenance.
- [ ] Add local speech provider interfaces for sherpa-onnx/whisper.cpp and an optional Azure Speech adapter without bundling credentials.
- [ ] Add PresentMon-based read-only gaming telemetry contracts and exclude game-memory/input-cheat behavior.
- [ ] Add official MCP C# SDK integration with tool reclassification through the Permission Broker.
- [ ] Add OpenTelemetry/Serilog redaction policies and health states without exporting secrets, prompts, screenshots, or transcripts.
- [ ] Add MSIX/App Installer and optional Velopack release workflows with signing documentation and SBOM/hash generation.
- [ ] Add failure-injection, profile-isolation, permission-expiry, recovery, and duplicate-side-effect tests.
- [ ] Reconcile all documentation and feature statuses after implementation; do not claim production readiness before Windows verification.

- [x] V2.1: Preserve every running worker result after scheduler cancellation
- [x] V2.1: Report stalled graphs before clearing pending tasks
- [x] V2.1: Reject maxConcurrency values less than one
- [x] V2.1: Include all retry attempts and backoff in task duration
- [x] V2.1: Validate RetryPolicy inputs before execution
- [x] V2.1: Emit Skipped events for dependency-failed tasks
- [x] V2.1: Emit Cancelled events when cancellation occurs during retry backoff
- [x] V2.1: Add asynchronous IMissionEventSink boundary for persistence and telemetry
- [x] V2.1: Expand scheduler and executor regression coverage
- [x] V2.1: Run available core tests (36/36 passing)
- [x] V2.1: Confirm Windows quality-gate is green and document remaining non-fatal analyzer warnings
- [x] V2.1: Commit hardened milestone to GitHub
- [x] Fix App.xaml.cs DispatcherQueue namespace/reference for the WinUI Composition Root
- [x] Fix AppHost.Initialize call to pass the UI DispatcherQueue without breaking DI startup
- [x] Repair all whitespace formatting violations reported by dotnet format
- [x] Make the format gate deterministic on the solution with Windows-only WinUI projects (local and Windows CI per-project whitespace validation pass)
- [x] Remove redundant System.Text.Json PackageReferences that trigger NU1510 during dotnet format

- [x] Acceleration: preserve TaskGraph V2 and Capability Broker as the only side-effect authority
- [x] Acceleration: document third-party integration boundaries and threat-model constraints
- [x] Acceleration: add a composite asynchronous mission event sink for durable storage and telemetry
- [x] Acceleration: connect mission events to SQLite with idempotent persistence semantics
- [x] Acceleration: add provider, automation, MCP, speech, OCR, and observability adapter contracts without bypass paths
- [ ] Acceleration: validate dependency licenses, versions, and Windows compatibility in the repository
- [x] Acceleration: add regression and security tests for the new integration boundaries (42/42 passing)
- [x] Acceleration: update the Windows quality gate and documentation after integration validation (Windows CI run 31656476620 is green)
- [x] Acceleration: commit the open-source integration milestone to GitHub (commits 2a2bd06 and 51075b8)
- [x] Repair malformed MahmoudAI.Storage project GUID entries in MahmoudAI.sln so the App solution build includes Storage

- [x] Windows automation: replace simulation with real Win32 window targeting, activation, pointer, and Unicode keyboard input
- [x] Windows automation: wire the real backend through CapabilityGuardedAutomationBackend and DI
- [x] Windows automation: add cancellation, denial, lease expiry, revocation, and Emergency Stop regression coverage (46/46 core tests passing)
- [x] Windows automation: add Windows smoke-test strategy and document native interop limitations
- [x] Windows automation: run the Windows Quality Gate and preserve a clean GitHub milestone

- [x] Wave 2: Close DI automation bypass by making Win32AutomationBackend internal and exposing only guarded IWindowsAutomationBackend
- [x] Wave 2: Fix lease timing race by taking timestamp after approval and using a robust TimeProvider abstraction
- [x] Wave 2: Eliminate lease handle cancellation source races by storing immutable cancellation token snapshots
- [x] Wave 2: Strengthen window targeting with strict HWND validation, process identity, and pre-action revalidation
- [x] Wave 2: Replace tool-controlled MCP scopes with Mahmoud-owned policy derivation from verified manifests
- [x] Wave 2: Remove legacy synchronous RequestCapability(...) method to prevent future deadlocks
- [x] Wave 2: Move text-pattern filters into contextual risk and policy evaluation rather than static string matching
- [x] Wave 2: Replace hand-maintained Win32 declarations with pinned CsWin32-generated interop
- [x] Wave 2: Add a UIA3 semantic automation adapter boundary with safe fallback behavior

- [x] Wave 2: Include MahmoudAI.WindowsIntegration in deterministic whitespace verification and add semantic fallback regression coverage
- [ ] Wave 2: Run the complete Windows interactive smoke test for UIA3, HWND revalidation, and CsWin32 input on a disposable desktop session

- [ ] Push Wave 2 hardening and WindowsIntegration assembly to GitHub repository 44mahmoud0/Ch main branch
