# Mahmoud AI — Native Windows Desktop AI Application

Mahmoud AI is a high-performance native Windows 11 desktop AI workspace built with **.NET 10 LTS** and **WinUI 3 / Windows App SDK**. It provides autonomous mission planning, multi-agent teamwork orchestration, task graph execution, and local-first memory persistence.

---

## Architecture

- **MahmoudAI.App**: WinUI 3 desktop shell and interactive user workspace.
- **MahmoudAI.Core**: Core runtime, Mission context model, Task Graph engine, and Agent Orchestrator.
- **MahmoudAI.Core.Tests**: xUnit automated test suite verifying task dependencies and execution safety.

---

## Building MahmoudAI.exe

To compile and build the desktop application on Windows with the .NET 10 SDK:

```powershell
dotnet restore MahmoudAI.sln
dotnet build MahmoudAI.sln --configuration Release
dotnet test MahmoudAI.sln --configuration Release
```
