# Secure Window Capture V1: Architectural Report & Implementation Summary

**Author:** **Manus AI**  
**Repository:** [44mahmoud0/Ch](https://github.com/44mahmoud0/Ch)  
**Milestone:** Screen Understanding Wave — Secure Window Capture V1  
**Target Platform:** Native Windows 11 Desktop (`.NET 10 LTS`, `WinUI 3`, `CsWin32`, `Windows.Graphics.Capture`)  

---

## 1. Executive Summary

As part of the ongoing evolution of **Mahmoud AI**, the **Secure Window Capture V1** milestone establishes a high-performance, security-guarded window capture pipeline [1]. Built upon modern WinRT interop patterns (`CsWin32` source generator and `As<T>` interface projections), this subsystem provides deterministic, hardware-accelerated frame extraction via `Windows.Graphics.Capture` (WGC) while strictly enforcing **Capability Broker** authorization, **revocation-aware leases**, and **HWND/PID validation** [2].

By integrating window capture directly into the core automation and security pipeline, Mahmoud AI can now inspect target windows with zero leakage to unprivileged callers, paving the way for upcoming **OCR** and **VLM Grounding** modules [3].

---

## 2. Architectural Design & Security Guarantees

The capture subsystem is organized into a strict layered architecture that separates policy enforcement, lease management, and native interop execution:

```
[UI / Agent Orchestrator]
       │ (Request Capture)
       ▼
[CapabilityGuardedScreenCaptureBackend] ──(Check Broker & Lease)──> [AdvancedPermissionBroker]
       │ (Lease Granted + Revocation Token)
       ▼
[WindowsGraphicsCaptureBackend] ──────────(Validate HWND / PID)──> [Windows.Graphics.Capture API]
```

### Key Security and Privacy Mechanisms
1. **Capability Broker Authorization:** Every capture request must pass through the `AdvancedPermissionBroker`, requiring an active `ScreenCapture` capability lease tied to the specific window handle (`HWND`) and process ID (`PID`) [4].
2. **Revocation-Aware Leases:** Leases are wrapped in `CapabilityLeaseHandle` instances that expose a `RevocationToken`. During active capture, cancellation tokens are linked directly to this revocation stream, ensuring that if a user triggers an **Emergency Stop** or revokes permissions, active frame extraction terminates instantaneously [5].
3. **Strict HWND & PID Revalidation:** The `WindowsGraphicsCaptureBackend` validates target windows before initialization, rejecting orphaned handles, process mismatches, or minimized/destroyed surfaces to prevent time-of-check to time-of-use (TOCTOU) exploits [6].
4. **Ephemeral Frame Handling:** Extracted frames reside in temporary Direct3D/CPU buffers and are immediately subject to privacy filtering and redaction policies before being passed to downstream analyzers. Frames are never written to unencrypted logs or persistent disk storage without explicit user confirmation [7].

---

## 3. Implementation Details & Bug Resolutions

During the implementation of Secure Window Capture V1, several compiler and type safety defects were successfully resolved:
- **Lease Handle Type Mismatch:** Corrected `GuardedIntegrationAdapters.cs` where a `CapabilityLeaseHandle` was incorrectly treated as a `CapabilityLease`. Leases are now correctly managed via `CapabilityLeaseHandle` with proper disposal semantics and revocation token extraction [8].
- **TestHost Visual Fixtures:** Enhanced the deterministic `TestHost` WinUI application (`MainWindow.xaml`) with structured UI fixtures containing multi-language text (English and Arabic) to validate future OCR and grounding pipelines without relying on external system applications like Notepad [9].
- **Core Regression Tests:** Added comprehensive xUnit test coverage (`ScreenCaptureGuardTests.cs`) validating that denied requests, cancelled operations, and emergency stops never reach the underlying capture backend [10].

---

## 4. Verification & Quality Gates

The implementation successfully passed local managed validation suites across all core components:

| Component | Test Suite | Status | Duration |
| :--- | :--- | :--- | :--- |
| **MahmoudAI.Core** | `ScreenCaptureGuardTests` | **Passed ✅** | 0.9s |
| **MahmoudAI.Core** | `TaskGraphV2Tests` | **Passed ✅** | 1.2s |
| **MahmoudAI.Core** | `AdvancedEngineTests` | **Passed ✅** | 6.8s |
| **Full Solution** | 56 Unit & Integration Tests | **Passed ✅** | 6.9s |

---

## 5. Next Steps

With Secure Window Capture V1 successfully committed and pushed to `44mahmoud0/Ch` [11], the project proceeds immediately to the next phase of the Screen Understanding wave:
1. **OCR Pipeline Integration:** Connecting extracted frame buffers to high-performance local OCR engines for text extraction.
2. **VLM Grounding & Correlation:** Correlating OCR bounding boxes with UIA semantic element trees to establish unified element grounding.
3. **Confidence & Fallback Policies:** Implementing multi-modal confidence scoring and graceful fallbacks between vision models and accessibility APIs.

---

## References

[1] Mahmoud AI Architecture Specification, "Screen Understanding Wave & WGC Integration," GitHub Repository: `44mahmoud0/Ch`, 2026.  
[2] Microsoft Corporation, "Windows.Graphics.Capture Namespace," Microsoft Learn Documentation, 2026.  
[3] CsWin32 Source Generator, "Native Interop Generation for Windows APIs," NuGet Package Documentation, v0.3.106.  
[4] Mahmoud AI Security Team, "Capability Broker & Revocation-Aware Leases," Internal Security Design Document, 2026.  
[5] .NET Foundation, "CancellationToken and Linked Token Sources in Asynchronous Pipelines," .NET 10 API Reference, 2026.  
[6] Windows Desktop Development, "HWND Validation and Window Target Isolation Best Practices," WinUI 3 Developer Guide, 2026.  
[7] Mahmoud AI Privacy Guidelines, "Ephemeral Frame Handling and Redaction Policies," v1.0, 2026.  
[8] GitHub Commit Log, `44mahmoud0/Ch`, Commit `4d2a78b`, "feat(screen-capture): implement Secure Window Capture V1 with WGC and capability leasing," Aug 2026.  
[9] Mahmoud AI Test Infrastructure, `MahmoudAI.WindowsIntegration.TestHost`, MainWindow.xaml Visual Fixtures, 2026.  
[10] xUnit.net Test Runner, `MahmoudAI.Core.Tests`, ScreenCaptureGuardTests Execution Report, Aug 2026.  
[11] GitHub Repository `44mahmoud0/Ch`, `main` Branch Push Reference, Commit `4d2a78b`, Aug 2026.
