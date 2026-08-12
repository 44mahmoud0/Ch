# Mahmoud AI - Windows 11 Build, Packaging, and E2E Runbook

This document details the exact engineering prerequisites, build commands, and packaging workflow required to produce the final signed Windows executable (`MahmoudAI.exe`) and MSIX installer package from the source code repository (`44mahmoud0/Ch`).

## 1. System Prerequisites
To build and package Mahmoud AI Desktop locally on Windows:
- **Operating System**: Windows 11 (Build 19041 or higher required for WinUI 3 App SDK).
- **IDE**: Visual Studio 2022 (Community, Professional, or Enterprise) with the following workloads installed:
  - `.NET Desktop Development` (with .NET 10 LTS SDK or .NET 8/9 compatibility targets as configured).
  - `Windows App SDK C# Templates` and Windows 10/11 SDK (10.0.19041.0+).
- **CLI Alternative**: .NET 10 SDK + `dotnet workload install windows-sdk` (via Developer Command Prompt).

---

## 2. Solution Structure & Projects
- `MahmoudAI.sln`: Main solution file.
- `src/MahmoudAI.Core`: Autonomous orchestration, task graph, permission broker, workspace isolation, memory stores.
- `src/MahmoudAI.Storage`: SQLite mission store and Qdrant vector memory bindings.
- `src/MahmoudAI.Mcp`: Model Context Protocol client/server integration layer.
- `src/MahmoudAI.App`: WinUI 3 native desktop application shell and dashboard UI.

---

## 3. Local Build & Test Commands
Open the Developer Command Prompt for VS 2022 or PowerShell in the repository root:

```powershell
# Restore dependencies across the solution
dotnet restore MahmoudAI.sln

# Run unit and regression tests (100% pass target)
dotnet test MahmoudAI.sln --configuration Release --no-restore

# Build the WinUI 3 desktop app for x64 Release
dotnet build src/MahmoudAI.App/MahmoudAI.App.csproj --configuration Release --platform x64
```

---

## 4. MSIX Packaging & Code Signing (.wapproj)
To package the application into `MahmoudAI_Setup.msix`:
1. Open `MahmoudAI.sln` in **Visual Studio 2022**.
2. Right-click the solution -> **Add** -> **New Project** -> Search for **Windows Application Packaging Project**.
3. Name it `MahmoudAI.Packaging` and reference `MahmoudAI.App`.
4. Right-click `MahmoudAI.Packaging` -> **Publish** -> **Create App Packages**.
5. Select **Sideloading** (or Microsoft Store if publishing publicly).
6. Sign the package with a local self-signed development certificate (`.pfx`) or production EV Code Signing certificate.
7. Output installer: `MahmoudAI_Setup.msix` / `MahmoudAI_Setup.exe`.

---

## 5. Next Engineering Milestone
- **Local Audio & Vision Integration**: Integrating Whisper.net native binaries and Windows.Graphics.Capture routines on a live Windows 11 test rig.
- **Automated CI Packaging**: Configuring Windows runner actions in GitHub Actions to build and publish signed artifacts on tags.
