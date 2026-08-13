# Mahmoud AI Open-Source Acceleration Roadmap

## Current milestone

The first low-risk integration slice is complete. TaskGraph V2 is now the WinUI mission execution path, its lifecycle events are persisted idempotently to SQLite, the dashboard can observe the same events through an asynchronous hub, and capability-guarded decorators prevent automation and MCP adapters from invoking their inner backends without broker approval. The repository remains free of new high-risk native or provider dependencies in this milestone.

The Windows Quality Gate is green on GitHub Actions after repairing a malformed `MahmoudAI.Storage` configuration GUID in `MahmoudAI.sln`. The current run is `31656476620`, and the commit is `51075b8`.

## Recommended implementation order

| Wave | Scope | Why it comes next | Exit criteria |
|---|---|---|---|
| 1 | Model gateway with `Microsoft.Extensions.AI` abstractions and an Ollama adapter | It creates a common provider boundary without changing TaskGraph authority. | Contract tests prove cancellation, streaming termination, tool-schema normalization, timeout behavior, and failure classification. |
| 2 | MCP C# SDK client adapter in an isolated process boundary | It replaces the placeholder protocol while preserving `PluginExecution` authorization. | Tool listing and invocation are schema-validated, capability-checked, cancellable, traced, and covered by a child-process shutdown test. |
| 3 | Windows automation worker with FlaUI UIA3 plus a narrow CsWin32 wrapper | It provides semantic UI control before physical input and keeps hung COM/UIA work outside WinUI. | Windows smoke tests prove approval-before-side-effect, cancellation, worker termination, workspace scoping, and no raw P/Invoke exposure to agents. |
| 4 | Screen observation pipeline | UI Automation properties should be preferred; capture and OCR should be fallbacks with provenance. | Windows tests cover UIA text, Windows.Graphics.Capture, OCR regions, Arabic text fixtures, and redaction boundaries. |
| 5 | Local speech worker | A process boundary prevents native speech runtimes from destabilizing the WinUI host. | STT, TTS, VAD, language metadata, cancellation, model loading, and audio-device failure paths are tested on Windows. |
| 6 | OpenTelemetry projection | Mission/task/attempt spans make autonomous behavior diagnosable after the state store is correct. | Redaction tests prove prompts, screenshots, transcripts, tokens, and secrets never leave the approved telemetry boundary. |
| 7 | Durable checkpoint and recovery reconciliation | Event persistence is present; recovery must now reconcile interrupted side effects and resume safely. | Kill-and-restart tests prove duplicate completed work is not repeated and Emergency Stop remains effective. |
| 8 | Optional Quartz.NET wakeups | Calendar scheduling should wake a mission, not replace TaskGraph execution. | A scheduled wakeup creates a normal mission request and is covered by persistence, cancellation, and duplicate-trigger tests. |

## Dependency adoption rules

Each wave must add only the minimum package surface necessary for its adapter. Before merging a dependency, record its license, target frameworks, native assets, transitive packages, release status, Windows support, and rollback path. Package addition must be isolated from behavior changes where possible, and the Windows Quality Gate must remain the final authority for WinUI and native integration.

Preview or experimental projects remain research references until their API surface is pinned and a Windows smoke test exists. In particular, the Microsoft `winapp` CLI should guide capture and UIA fallback design but should not become a production runtime dependency until its preview status changes.

## Non-negotiable safety invariants

TaskGraph V2 owns mission ordering and cancellation. The AdvancedPermissionBroker owns authority. SQLite owns recoverable mission state. Third-party packages own mechanics only. Every side effect must be attributable to a mission and task, carry a normalized scope and required capability, honor cancellation, and emit a durable lifecycle event. No provider, MCP server, automation backend, speech engine, OCR process, vector store, scheduler, or telemetry exporter may bypass these invariants.

## References

[1]: https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai "Microsoft.Extensions.AI libraries - .NET"
[2]: https://github.com/FlaUI/FlaUI "FlaUI repository"
[3]: https://github.com/modelcontextprotocol/csharp-sdk "Official Model Context Protocol C# SDK"
[4]: https://learn.microsoft.com/en-us/windows/uwp/audio-video-camera/screen-capture "Windows screen capture documentation"
[5]: https://github.com/open-telemetry/opentelemetry-dotnet "OpenTelemetry .NET repository"
[6]: https://www.quartz-scheduler.net/ "Quartz.NET documentation"
