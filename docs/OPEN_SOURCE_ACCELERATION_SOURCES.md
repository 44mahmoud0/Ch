# Open-Source Acceleration Sources

This file records external references consulted while evaluating the attached acceleration plan. Package versions and repository activity are time-sensitive and must be revalidated before each dependency addition.

| Area | Source | Current observation from research | Integration implication |
|---|---|---|---|
| Unified AI abstractions | [Microsoft.Extensions.AI documentation](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai) | Microsoft documents common .NET AI abstractions for chat and embedding components. | Add a Mahmoud-owned model gateway first; introduce MEAI adapters only after package/API validation. |
| Unified AI abstractions | [Microsoft.Extensions.AI on NuGet](https://www.nuget.org/packages/Microsoft.Extensions.AI/) | Search returned the 10.8.3 package listing. | Do not assume the listed version is compatible with the pinned .NET/Windows stack; lock and audit before adding. |
| Windows UI automation | [FlaUI repository](https://github.com/FlaUI/FlaUI) | Upstream describes FlaUI as a .NET UI automation library. | Keep FlaUI behind `IWindowsAutomationBackend`; authorization remains in CapabilityBroker. |
| Windows UI automation | [FlaUI.Core on NuGet](https://www.nuget.org/packages/FlaUI.Core/) | Search returned FlaUI.Core 5.0.0. | Validate target frameworks and native/UIA behavior on Windows before production use. |
| MCP | [Official MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) | The repository is the official C# SDK source. | Use MCP only behind `IMcpToolGateway`; external servers remain untrusted and require PluginExecution authorization. |
| MCP | [ModelContextProtocol on NuGet](https://www.nuget.org/packages/ModelContextProtocol/) | Search returned the 2.1.0 package listing. | Validate SDK package split, transport, DI, and security behavior before adding to the trusted application. |
| MCP low-level package | [ModelContextProtocol.Core on NuGet](https://www.nuget.org/packages/ModelContextProtocol.Core/) | Search returned the 1.4.1 package listing. | Prefer the minimum package surface needed for isolated client/server adapters. |
| Official .NET AI guidance | [AI and vector data extensions GA announcement](https://devblogs.microsoft.com/dotnet/ai-vector-data-dotnet-extensions-ga/) | Microsoft announced GA packages for AI and vector-data extensions. | Preserve Qdrant behind `IVectorMemoryStore`; the embedding model and dimension remain application-owned. |

## Architectural constraints retained from the research

TaskGraph V2 remains the mission/DAG authority. The Capability Broker remains the authority for side effects. SQLite remains the canonical recoverable mission state store. Provider, automation, MCP, speech, OCR, vector, and telemetry libraries must be adapters behind Mahmoud-owned contracts. No third-party package may create an alternate execution path that bypasses cancellation, authorization, idempotency, workspace isolation, or mission events.

The first implemented slice deliberately adds no high-risk native automation or model-provider dependency. It introduces durable, idempotent TaskGraph event persistence, an asynchronous event hub/composite sink, a V2 WinUI mission path, and adapter contracts that can be validated independently before adding packages.

## Windows automation research notes

The [FlaUI repository](https://github.com/FlaUI/FlaUI) describes FlaUI as a .NET wrapper around Microsoft's native UI Automation libraries, with UIA2/UIA3 support and access to native UIA objects when higher-level wrappers are insufficient. The [Microsoft CsWin32 guidance](https://learn.microsoft.com/en-us/windows/apps/develop/interop/call-win32-apis) recommends source-generated, type-safe Win32 wrappers, requests APIs through `NativeMethods.txt`, and shows WinUI HWND retrieval through `WindowNative.GetWindowHandle`. The first implementation therefore uses a Windows-specific adapter boundary: semantic UIA work through FlaUI/UIA3 when available, and a narrow CsWin32-generated wrapper for approved window targeting and SendInput fallback. Raw generated APIs remain private to the adapter and are never exposed to TaskGraph agents.

## Wave 2 UIA3 validation

FlaUI upstream describes the library as a .NET wrapper over Microsoft's native UI Automation libraries, with UIA2/UIA3 support and access to native UIA objects when higher-level wrappers are insufficient. The current NuGet release validated for this integration is `FlaUI.UIA3` 5.0.0, targeting .NET 6.0 and compatible with higher frameworks. It will remain behind `IWindowsAutomationBackend` and the Capability Broker, with no direct access from agents. Sources: [FlaUI upstream](https://github.com/FlaUI/FlaUI), [FlaUI.UIA3 5.0.0](https://www.nuget.org/packages/FlaUI.UIA3/5.0.0).
