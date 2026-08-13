# Windows Automation Smoke Test

## Scope

The first real automation backend replaces the previous simulation for four bounded operations: inspect a visible window, activate a visible window, click explicit screen coordinates, and inject Unicode text into an explicitly targeted window. The implementation is exposed through `IWindowsAutomationBackend`, then wrapped by `CapabilityGuardedAutomationBackend`; no caller should register `Win32AutomationBackend` directly for mission execution.

## Safety preconditions

Run this test only on a disposable Windows profile or a test VM. Do not use a password field, administrator console, game, payment flow, messaging account, or any application containing sensitive information. The test must run with Safe Mode available and with Emergency Stop visible in the application.

| Check | Procedure | Expected result |
|---|---|---|
| Window inspection | Open Notepad and request an `Inspect` operation using an exact or unique title. | The result contains the resolved HWND and title; no input is generated. |
| Window activation | Open two harmless test windows and request `Activate` for the inactive test window. | The requested window becomes foreground, or the operation returns a clear Win32 failure without retrying blindly. |
| Pointer click | Use a disposable text editor with a known coordinate and request `Pointer` with `x,y`. | A single left-click occurs only after the MouseControl approval; denial produces no click. |
| Unicode keyboard | Focus a disposable editor and request `Keyboard` with English and Arabic fixture text. | The exact Unicode text appears once after KeyboardControl approval; cancellation stops before the next character sequence. |
| Expiry | Use a short test lease and hold the backend at a cancellation-aware boundary. | Lease expiry cancels the backend and no subsequent side effect is attempted. |
| Revocation | Start a blocking test operation, revoke the lease, then inspect the backend token. | The backend receives cancellation and terminates; the mission records the cancellation event. |
| Emergency Stop | Start a blocking test operation and trigger Emergency Stop. | All capability leases are revoked, Safe Mode becomes active, and the backend receives cancellation. |

## Known limitations

This slice is a real Win32 backend, not yet a semantic UI Automation backend. `SetValue` and `Capture` intentionally return unsupported results and must be implemented by the future UIA and screen-understanding adapters. `SetForegroundWindow` is subject to Windows foreground-activation policy and may fail when another application owns the foreground lock; the failure is reported rather than bypassed. Physical input is inherently less deterministic than UIA control patterns and must remain a fallback with explicit capability and scope.

The implementation currently keeps the native interop declarations private to the Core automation adapter. The next hardening step is to replace hand-maintained declarations with the source-generated CsWin32 package, then add a dedicated UIA3 adapter behind the same contract. Windows UIA/Win32 smoke tests must run on an actual interactive desktop session; a headless CI build proves compilation and unit behavior but cannot prove foreground focus or physical input behavior.

## References

[1]: https://learn.microsoft.com/en-us/windows/apps/develop/interop/call-win32-apis "Call Win32 APIs from a C# Windows app"
[2]: https://github.com/FlaUI/FlaUI "FlaUI UI automation library"
