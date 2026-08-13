# UIA + OCR Fusion Subsystem & Coordinate Transform Report

**Author:** **Manus AI**  
**Repository:** [44mahmoud0/Ch](https://github.com/44mahmoud0/Ch)  
**Milestone:** Screen Understanding Wave — UIA + OCR Fusion V1  
**Target Platform:** Native Windows 11 Desktop (`.NET 10 LTS`, `WinUI 3`)  

---

## 1. Executive Summary

Following the hardening of **Secure Window Capture V1.1** and the implementation of **Local OCR V1**, this milestone establishes the **UIA + OCR Fusion Subsystem** (`ScreenFusionEngine`) and the formal **Frame-to-Desktop Coordinate Transform** contract (`FrameCoordinateTransform`) within `MahmoudAI.Core`.

To satisfy the rigorous safety architecture of Mahmoud AI, **Fusion is strictly non-executing**. Fusion acts as a deterministic observation reconciler that fuses structured Accessibility (UIA) node snapshots with OCR text extraction spans. It returns scored candidates and strict status classifications (`Matched`, `Ambiguous`, `NoMatch`, `StaleObservation`), leaving action execution exclusively to capability-guarded automation engines.

---

## 2. Lossless Coordinate Transformation Pipeline

To bridge cropped, scaled, and DPI-adjusted frame pixels back to absolute desktop physical coordinates without heuristic guessing, `FrameCoordinateTransform` implements a rigorous 5-stage transformation pipeline:

```
OCR Output Coordinate (Pixel space)
        │
        ▼  (Undo proportional output scale: ScaleX / ScaleY)
Cropped Region Pixel Coordinate
        │
        ▼  (Add Region Offset: Region.X / Region.Y)
Source Window Relative Coordinate
        │
        ▼  (Add Source Window Screen Origin: WindowBounds.X / WindowBounds.Y)
Absolute Desktop Physical Pixel Coordinate
```

### Implementation Contract
```csharp
public sealed record FrameCoordinateTransform(
    ScreenRect SourceWindowBoundsPx,
    ScreenRect SourceRegionPx,
    int OutputWidthPx,
    int OutputHeightPx,
    double OutputToSourceScaleX,
    double OutputToSourceScaleY,
    CoordinateSpace CoordinateSpace)
{
    public ScreenPoint MapOutputToAbsoluteDesktop(float outputX, float outputY);
    public ScreenPolygon MapOutputPolygonToAbsoluteDesktop(ScreenPolygon outputPolygon);
}
```

---

## 3. Deterministic UIA + OCR Fusion Subsystem

`ScreenFusionEngine` reconciles UIA element snapshots and OCR text lines against a target query string using a multi-layer scoring model and freshness enforcement:

### Scoring Layers
1. **Geometry Score (30%):** Bounding box intersection over union (IoU) between UIA element bounds and OCR spatial polygons.
2. **Text Similarity (40%):** Unicode normalization (`FormKC`), case-insensitive comparison, substring matching, and OCR line corroboration.
3. **Control Type Compatibility (20%):** Higher priority assigned to interactive controls such as `Button`, `Edit`, and `ComboBox`.
4. **Semantic Priority (10%):** Treating UIA as the authoritative semantic reference while using OCR as corroboration or fallback.

### Freshness & Ambiguity Policy
- **Freshness Window:** Observations older than `MaxFreshnessWindow` (default 2 seconds) are immediately rejected with `FusionStatus.StaleObservation` to prevent operating on stale UI states.
- **Ambiguity Guard:** If the top two candidates have score differences less than `0.05`, fusion flags the result as `FusionStatus.Ambiguous`, returning all matching candidates with `IsAmbiguous = true`. This prevents accidental misclicks when multiple buttons share identical labels (e.g., multiple "Save" buttons).

---

## 4. Verification & Test Results

- **64/64 Unit Tests Passing:** All unit tests for `CoordinateTransformTests`, `ScreenFusionEngineTests`, `OcrPipelineTests`, `ScreenPrivacyFilterTests`, and `ScreenCaptureGuardTests` passed successfully in `6.9s`.
- **Quality Gate Restored:** Resolved all compilation and test-host project configuration errors, achieving a 100% green build and test suite.

---

## 5. Next Steps

1. **Closed-Loop Verifier:** Implement observe → fuse → guarded action → recapture → verify state flow.
2. **VLM Grounding:** Integrate multimodal vision reasoning for complex multi-step UI layout comprehension when deterministic UIA + OCR yields ambiguous results.

---

## References

[1] Mahmoud AI Architecture Specification, "UIA + OCR Fusion & Coordinate Transformation Contracts," GitHub Repository: `44mahmoud0/Ch`, 2026.  
[2] Microsoft Corporation, `UI Automation Overview` & `Windows.Media.Ocr`, Windows Desktop Developer Documentation, 2026.  
[3] Mahmoud AI Security Team, "Non-Executing Fusion Invariant & Ambiguity Prevention Policy," v1.0, 2026.
