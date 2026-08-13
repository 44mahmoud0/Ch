# Mahmoud AI — Screen Understanding Wave (Capture, OCR & Vision)

## Overview

Following the completion of Windows Automation Hardening Wave 2 (featuring semantic UIA3 inspection/activation, guarded Win32 input, pinned CsWin32 interop, lease revocation safety, and 100% passing core unit tests), the next production engineering milestone is **Screen Understanding**.

This wave transitions Mahmoud AI from blind mechanical execution (clicking coordinates or typing text based purely on window titles) to perceptual grounding. The agent must be able to capture window/display frames securely, perform local and fallback OCR, index visual UI control trees, and ground language model tool calls in observable screen content without leaking sensitive pixels.

---

## Architecture & Components

### 1. Secure Capture Pipeline (`MahmoudAI.Vision`)
- **DirectComposition / PrintWindow / DXGI Desktop Duplication**: Implement a multi-tier screen capture adapter that starts with safe window-level frame extraction (`PrintWindow`) and falls back to display duplication with strict user notification and consent.
- **Scope Restriction**: Capture requests must be authorized by the `AdvancedPermissionBroker` with a `ScreenCapture` capability lease. Captures are restricted to explicitly targeted mission bounding boxes or single window handles.
- **Redaction & Privacy Guard**: Automatic redaction of areas flagged by policy (e.g., password boxes, secure banking viewports, designated privacy regions) before any frame enters memory or is passed to a vision model.

### 2. OCR & Text Grounding Engine
- **Local OCR Adapter**: Integration with Windows ML / Media.TextRecognition or an embedded Tesseract/PaddleOCR runtime via the `MahmoudAI.WindowsIntegration` or new `MahmoudAI.Vision` assembly.
- **Bounding Box Normalization**: Extract detected text fragments alongside exact screen coordinates (`[x, y, width, height]`), establishing a spatial mapping index for the TaskGraph and VLM reasoning loop.

### 3. VLM Grounding & Vision Router
- **Model Router Integration**: Route captured frames and OCR text tokens to local models (e.g., LLaVA/Ollama) or authorized cloud vision models depending on the mission profile and privacy level.
- **Coordinate Normalization**: Translate VLM bounding-box estimates back into absolute desktop or window-relative client coordinates, validated against current window bounds before passing to `CapabilityGuardedAutomationBackend`.

---

## Implementation Plan

| Milestone | Key Deliverable | Success Criteria |
|---|---|---|
| **Phase 1: Capture Adapter** | `IScreenCaptureBackend` and Win32 `PrintWindow` implementation in `MahmoudAI.Vision`. | Successful frame capture of target HWND without desktop-wide screenshot permissions where unnecessary. |
| **Phase 2: OCR & Indexing** | Windows Media OCR integration with spatial bounding-box tokenization. | Accurate text and coordinate extraction from standard WinUI/Win32 application windows. |
| **Phase 3: VLM Grounding** | Vision router integration for screenshot analysis and semantic element lookup. | Accurate identification of interactive buttons or text fields by visual description. |
| **Phase 4: End-to-End Integration** | Mission loop test: Capture window $\rightarrow$ OCR/Vision $\rightarrow$ UIA3/Win32 Action. | Complete closed-loop autonomous task execution anchored on visual feedback. |

---

## References

[1]: https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr "Windows.Media.Ocr Namespace"
[2]: https://learn.microsoft.com/en-us/windows/win32/gdi/printwindow "PrintWindow function (winuser.h)"
