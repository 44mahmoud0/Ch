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
