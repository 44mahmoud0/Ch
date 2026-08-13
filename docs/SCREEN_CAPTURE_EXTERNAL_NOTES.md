# External implementation notes for Secure Window Capture V1

The official Microsoft screen-capture guidance states that `Windows.Graphics.Capture` uses a `GraphicsCaptureItem`, a `Direct3D11CaptureFramePool`, a capture session, and `FrameArrived` or `TryGetNextFrame` to acquire frames. The documented SDR path uses `DirectXPixelFormat.B8G8R8A8UIntNormalized`; each checked-out frame must be disposed so it returns to the pool, and callers should copy the content region rather than retain the underlying surface. The same guidance notes that WinUI 3 composition updates must be dispatched to the UI thread, while frame acquisition and bitmap creation can occur on the frame-pool background thread.

Source: [Microsoft Learn — Screen capture](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture)

The Windows Runtime API reference documents `Direct3D11CaptureFramePool.Create(IDirect3DDevice, DirectXPixelFormat, int, SizeInt32)` and the `GraphicsCaptureItem.TryCreateFromWindowId(WindowId)` factory used for window-targeted capture.

Sources: [Direct3D11CaptureFramePool.Create](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.direct3d11captureframepool.create?view=winrt-28000), [GraphicsCaptureItem.TryCreateFromWindowId](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.graphicscaptureitem.trycreatefromwindowid?view=winrt-28000)

Implementation implication: the V1 backend must remain Windows-only and must not claim that Linux sandbox builds or headless CI prove interactive pixel capture. The capture PR should validate compilation and contracts in CI, then use an interactive Windows TestHost E2E for HWND/PID targeting, dimensions, DPI/origin metadata, window movement, and ephemeral frame disposal.
The Win32 interop API `IGraphicsCaptureItemInterop::CreateForWindow(HWND, REFIID, void**)` is the supported HWND-to-GraphicsCaptureItem path for desktop window capture, with Windows 10 version 1903 as the documented minimum. The current Windows SDK projection used by this project does not expose `GraphicsCaptureItem.TryCreateFromWindowId` in the target reference, so the implementation should use the COM interop boundary rather than a non-existent projected static method.

Source: [IGraphicsCaptureItemInterop::CreateForWindow](https://learn.microsoft.com/en-us/windows/win32/api/windows.graphics.capture.interop/nf-windows-graphics-capture-interop-igraphicscaptureiteminterop-createforwindow)

CsWin32 supports generated Win32 P/Invoke and COM interop projections from Windows SDK metadata, which matches the project's isolation strategy for the native Windows integration assembly.

Source: [microsoft/CsWin32](https://github.com/microsoft/CsWin32)
For modern .NET/CsWinRT, the current guidance is to obtain the activation-factory interop through the projected runtime class (`GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>()`) and create the item from the returned ABI pointer, rather than using the removed `WindowsRuntimeMarshal.GetActivationFactory` API. The official Microsoft Win32 composition sample shows the older `WindowsRuntimeMarshal` approach for .NET Framework; the current CsWinRT discussion explicitly notes that this method is unavailable in modern .NET and recommends the projected `As<T>`/`FromAbi` path with a generated COM interface.

Sources: [CsWinRT issue #2017](https://github.com/microsoft/CsWinRT/issues/2017), [Microsoft Windows.UI.Composition-Win32-Samples CaptureHelper](https://github.com/microsoft/Windows.UI.Composition-Win32-Samples/blob/master/dotnet/WPF/ScreenCapture/Composition.WindowsRuntimeHelpers/CaptureHelper.cs)

The maintained Win32CaptureSample documents that `Direct3D11CaptureFramePool.CreateFreeThreaded` avoids a DispatcherQueue requirement and delivers `FrameArrived` on the frame-pool internal thread, which is the safer choice for a non-UI backend. It also documents that checked-out frames must be disposed and that Win32 capture uses `IGraphicsCaptureItemInterop` from the GraphicsCaptureItem factory.

Source: [robmikh/Win32CaptureSample](https://github.com/robmikh/Win32CaptureSample)
