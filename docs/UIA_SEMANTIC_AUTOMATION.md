# UIA Semantic Automation V1

Mahmoud AI now treats UI Automation as a typed semantic-targeting subsystem rather than a string parser layered directly on top of physical input. The Core project owns the dependency-free contracts, while the WindowsIntegration project owns FlaUI/UIA3 resolution and pattern invocation. Raw `AutomationElement` instances remain internal to the integration assembly.

## Resolution contract

A `UiaQueryRequest` identifies a root window with an `hwnd:<handle>` target and supplies a `UiaSelectorPath`. Each path segment can constrain `AutomationId`, accessible `Name`, `ControlType`, `ClassName`, `FrameworkId`, and optional process identity. Name matching supports exact, contains, and starts-with modes. Nested paths are resolved segment by segment, so a common name such as `Save` is not searched globally when a parent pane can disambiguate it.

The result is a `UiaQueryResult` containing a typed status and safe `UiaElementSnapshot` values. The engine never returns a raw UIA object. `Ambiguous` is a hard failure; the resolver never chooses the first match. `ProcessMismatch`, `Cancelled`, and `Timeout` are distinct outcomes so a planner can choose a safe retry, narrower selector, or fallback policy without confusing a budget violation with a missing control.

Traversal is bounded by `MaxDepth`, `MaxNodesVisited`, a timeout, and a cancellation token. A process identity check is applied at the root and every matched candidate. These boundaries are defense-in-depth controls against unbounded trees and cross-process name collisions.

## Pattern policy

The V1 executor supports `Invoke`, `SetValue`, `Toggle`, `Select`, and `Focus`. It returns `UnsupportedPattern` when the target does not expose the requested UIA pattern. It does not silently convert a semantic request into a mouse click or keyboard simulation. Physical input remains an explicit, separately guarded fallback.

Semantic queries request the `ScreenCapture` capability. Semantic actions request `KeyboardControl` for `SetValue` and `MouseControl` for other state-changing/focus actions. Both query and action paths pass through `CapabilityGuardedUiaSemanticAutomation`, which applies the existing risk policy, capability lease, revocation token, and emergency-stop cancellation boundary.

## Deterministic TestHost

`MahmoudAI.WindowsIntegration.TestHost` is a small WinUI application intended for Windows behavioral tests. It contains stable controls with known automation IDs: `SaveButton`, `NameInput`, `DarkModeToggle`, `LanguageList`, `SettingsPane`, `DisabledButton`, `HiddenButton`, and two buttons sharing the accessible name `Save`. The host provides deterministic targets for exact lookup, nested lookup, ambiguity, disabled/hidden handling, and state-changing pattern tests without depending on the version-specific UI of Notepad or Calculator.

The current automated suite covers composition boundaries, invalid-window behavior, cancellation, and capability guarding. The next Windows-only E2E increment should launch the TestHost, discover its HWND/PID, and verify exact selector resolution, ambiguous-name rejection, `Invoke`, `Value`, `Toggle`, `SelectionItem`, timeout budgets, stale-window behavior, and process-boundary rejection.

## Closed-loop roadmap

The semantic layer is deliberately placed before probabilistic vision:

```text
Native/API targeting
    -> UIA semantic selector and pattern execution
    -> guarded Win32/COM fallback
    -> Windows.Graphics.Capture + OCR + VLM grounding
    -> physical input only when semantic targeting is unavailable
    -> recapture and expected-state verification
```

This ordering makes Screen Understanding a grounding and verification layer instead of the authority that directly emits coordinates.
