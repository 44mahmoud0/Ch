using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MahmoudAI.Core.Integration;

namespace MahmoudAI.Core.Automation
{
    public enum ScreenCaptureTargetKind
    {
        Window,
        Display
    }

    public sealed record ScreenCaptureTarget(
        ScreenCaptureTargetKind Kind,
        nint? Hwnd = null,
        int? ProcessId = null,
        string? DisplayId = null);

    public sealed record ScreenCaptureRegion(
        int X,
        int Y,
        int Width,
        int Height);

    public sealed record ScreenCaptureRequest(
        ScreenCaptureTarget Target,
        ScreenCaptureRegion? Region = null,
        int? MaxWidth = null,
        int? MaxHeight = null,
        bool IncludeCursor = false,
        AutomationContext? Context = null);

    public sealed record ScreenFrameMetadata(
        string FrameId,
        DateTimeOffset CapturedAt,
        int PixelWidth,
        int PixelHeight,
        int Stride,
        float DpiScaleX,
        float DpiScaleY,
        int ScreenOriginX,
        int ScreenOriginY,
        int SourceProcessId,
        nint SourceHwnd);

    public enum ScreenCaptureStatus
    {
        Captured,
        NotFound,
        ProcessMismatch,
        Denied,
        Cancelled,
        UnsupportedTarget,
        ProviderError,
        Timeout
    }

    public sealed record CapturedScreenFrame(
        ScreenCaptureStatus Status,
        ScreenFrameMetadata? Metadata,
        byte[]? PixelBuffer,
        FrameCoordinateTransform? Transform = null,
        string? Error = null) : IDisposable
    {
        public bool Succeeded => Status == ScreenCaptureStatus.Captured;

        public void Dispose()
        {
            if (PixelBuffer is not null)
            {
                Array.Clear(PixelBuffer, 0, PixelBuffer.Length);
            }
        }
    }

    public enum ScreenPrivacySensitivity
    {
        Public,
        Normal,
        Sensitive,
        Restricted
    }

    public sealed record ScreenPrivacyContext(
        ScreenPrivacySensitivity Sensitivity,
        bool AllowCloudVlm,
        bool AllowPersistence,
        bool AllowDebugScreenshot);

    public sealed record RedactedScreenFrame(
        ScreenCaptureStatus Status,
        ScreenFrameMetadata? Metadata,
        byte[]? PixelBuffer,
        int RedactionCount,
        FrameCoordinateTransform? Transform = null,
        string? Error = null) : IDisposable
    {
        public bool Succeeded => Status == ScreenCaptureStatus.Captured;

        public void Dispose()
        {
            if (PixelBuffer is not null)
            {
                Array.Clear(PixelBuffer, 0, PixelBuffer.Length);
            }
        }
    }

    public interface IScreenCaptureBackend
    {
        Task<CapturedScreenFrame> CaptureAsync(
            ScreenCaptureRequest request,
            CancellationToken cancellationToken);
    }

    public interface IScreenPrivacyFilter
    {
        Task<RedactedScreenFrame> RedactAsync(
            CapturedScreenFrame frame,
            ScreenPrivacyContext context,
            CancellationToken cancellationToken);
    }
}
