# Windows Automation Smoke Test

## Scope

The first real automation backend replaces the previous simulation for bounded operations: inspect a visible window, activate a visible window, click explicit screen coordinates, inject Unicode text into an explicitly targeted window, and use UIA3 semantic inspection, activation, and ValuePattern writes when the target exposes them. The implementation is exposed through `IWindowsAutomationBackend`, then wrapped by `CapabilityGuardedAutomationBackend`; no caller should register `Win32AutomationBackend` or `Uia3AutomationBackend` directly for mission execution.

The Windows-specific assembly uses the pinned `Microsoft.Windows.CsWin32` source generator for the narrow `user32.dll` surface declared in `NativeMethods.txt`. `FlaUI.UIA3` 5.0.0 is used only inside the semantic adapter. Both implementations remain behind the Mahmoud-owned contract and capability broker.

## Safety preconditions

Run this test only on a disposable Windows profile or a test VM. Do not use a password field, administrator console, game, payment flow, messaging account, or any application containing sensitive information. The test must run with Safe Mode available and with Emergency Stop visible in the application.

| Check | Procedure | Expected result |
|---|---|---|
| Window inspection | Open Notepad and request an `Inspect` operation using an exact title or an `hwnd:` target. | UIA3 returns the element name/control type where available; otherwise the guarded Win32 fallback returns the resolved HWND and title. |
| Window activation | Open two harmless test windows and request `Activate` for the inactive test window. | UIA3 focuses the target when it resolves semantically; otherwise the Win32 path activates and revalidates the foreground HWND, or returns a clear failure without retrying blindly. |
| UIA semantic query | Launch `MahmoudAI.WindowsIntegration.TestHost`, resolve its HWND/PID, and query `SettingsPane -> NameInput` using a typed `UiaSelectorPath`. | The nested path returns exactly one safe `UiaElementSnapshot`; a selector that matches both `Save` buttons returns `Ambiguous` and never chooses the first element. |
| UIA value write | Use the TestHost `NameInput` control and request `SetValue` through the guarded semantic service. | The value is written through UIA3 only after the broker approves the contextual capability; controls without the pattern return `UnsupportedPattern`. |
| UIA pattern actions | Exercise `Invoke`, `Toggle`, `Select`, and `Focus` against `SaveButton`, `DarkModeToggle`, `LanguageList`, and a focused control. | Each action uses its declared UIA pattern; there is no implicit mouse fallback, and unsupported patterns produce a truthful typed failure. |
| Pointer click | Use a disposable text editor with a known coordinate and request `Pointer` with `window|x,y`. | A single left-click occurs only after MouseControl approval and strict foreground/process revalidation; denial produces no click. |
| Unicode keyboard | Focus a disposable editor and request `Keyboard` with English and Arabic fixture text. | The exact Unicode text appears once after KeyboardControl approval; cancellation stops before the next input sequence. |
| Expiry | Use a short test lease and hold the backend at a cancellation-aware boundary. | Lease expiry cancels the backend and no subsequent side effect is attempted. |
| Revocation | Start a blocking test operation, revoke the lease, then inspect the backend token. | The backend receives cancellation and terminates; the mission records the cancellation event. |
| Emergency Stop | Start a blocking test operation and trigger Emergency Stop. | All capability leases are revoked, Safe Mode becomes active, and the backend receives cancellation. |
| Target mismatch | Reuse an HWND after closing or retarget a process name/ID. | The operation is rejected before input; HWND validity, exact title, and process identity are checked again immediately before side effects. |
| Traversal budget | Query a large or synthetic tree with low `MaxDepth`, `MaxNodesVisited`, and timeout limits. | The result is `Timeout`, not `NotFound`, and no unbounded traversal continues. |
| Cancellation | Cancel a semantic query or action before and during resolution. | The typed result is `Cancelled` and no pattern action is attempted. |

## Known limitations

The semantic UIA3 adapter now exposes typed, bounded selector queries and pattern execution for `Invoke`, `SetValue`, `Toggle`, `Select`, and `Focus`, while retaining the legacy inspection, activation, and ValuePattern operation path. It intentionally does not synthesize physical pointer or keyboard input; those operations use the guarded CsWin32/Win32 fallback. Screen capture and OCR remain separate Screen Understanding gateway work. `SetForegroundWindow` is subject to Windows foreground-activation policy and may fail when another application owns the foreground lock; the failure is reported rather than bypassed.

UIA and physical-input behavior must be validated on an actual interactive Windows desktop session. A headless CI build proves package resolution, compilation, and unit behavior but cannot prove foreground focus, control patterns, desktop integrity-level restrictions, monitor coordinates, or physical input behavior. Any smoke test that produces unexpected input must stop immediately and capture the mission timeline and event log.

## References

[1]: https://learn.microsoft.com/en-us/windows/apps/develop/interop/call-win32-apis "Call Win32 APIs from a C# Windows app"
[2]: https://github.com/FlaUI/FlaUI "FlaUI UI automation library"
[3]: https://www.nuget.org/packages/FlaUI.UIA3/5.0.0 "FlaUI.UIA3 5.0.0"
