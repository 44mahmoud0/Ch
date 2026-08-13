using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using WinRT;
using System.Threading;
using System.Threading.Tasks;
using MahmoudAI.Core.Automation;
using Microsoft.Graphics.Canvas;
using Windows.Graphics.Capture;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.WinRT.Graphics.Capture;

namespace MahmoudAI.WindowsIntegration.Automation
{
    [SupportedOSPlatform("windows10.0.17763.0")]
    internal sealed class WindowsGraphicsCaptureBackend : IScreenCaptureBackend, IDisposable
    {
        private const int FrameBufferCount = 2;
        private readonly CanvasDevice _device;
        private int _disposed;

        public WindowsGraphicsCaptureBackend()
        {
            if (!GraphicsCaptureSession.IsSupported())
            {
                throw new PlatformNotSupportedException("Windows.Graphics.Capture is not supported on this device.");
            }

            _device = CanvasDevice.GetSharedDevice();
        }

        public async Task<CapturedScreenFrame> CaptureAsync(
            ScreenCaptureRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ThrowIfDisposed();

            if (request.Target.Kind != ScreenCaptureTargetKind.Window
                || request.Target.Hwnd is not nint hwnd
                || hwnd == nint.Zero)
            {
                return Failure(ScreenCaptureStatus.UnsupportedTarget, "Screen Capture V1 supports only a non-zero window HWND target.");
            }

            if (request.Target.ProcessId is not int expectedProcessId || expectedProcessId <= 0)
            {
                return Failure(ScreenCaptureStatus.ProcessMismatch, "Screen Capture V1 requires an explicit positive target process ID.");
            }

            if (!TryValidateTarget(hwnd, expectedProcessId, out var originX, out var originY, out var targetStatus, out var targetError))
            {
                return Failure(targetStatus, targetError);
            }

            cancellationToken.ThrowIfCancellationRequested();
            GraphicsCaptureItem? item;
            try
            {
                var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
                interop.CreateForWindow((HWND)hwnd, out item);
            }
            catch (Exception ex)
            {
                return Failure(ScreenCaptureStatus.ProviderError, $"GraphicsCaptureItem creation failed: {ex.Message}");
            }

            if (item is null)
            {
                return Failure(ScreenCaptureStatus.NotFound, "Windows.Graphics.Capture could not create an item for the target window.");
            }

            using var framePool = Direct3D11CaptureFramePool.Create(
                _device,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                FrameBufferCount,
                item.Size);
            using var session = framePool.CreateCaptureSession(item);
            session.IsCursorCaptureEnabled = request.IncludeCursor;

            var frameTask = WaitForFrameAsync(framePool, cancellationToken);
            session.StartCapture();
            using var frame = await frameTask.ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateTarget(hwnd, expectedProcessId, out originX, out originY, out targetStatus, out targetError))
            {
                return Failure(targetStatus, targetError);
            }

            using var bitmap = CanvasBitmap.CreateFromDirect3D11Surface(_device, frame.Surface);
            var contentSize = frame.ContentSize;
            var pixelWidth = Math.Max(1, contentSize.Width);
            var pixelHeight = Math.Max(1, contentSize.Height);
            var bytesPerPixel = 4;
            var sourcePixels = bitmap.GetPixelBytes();
            var expectedLength = checked(pixelWidth * pixelHeight * bytesPerPixel);
            var pixels = sourcePixels.Length == expectedLength
                ? sourcePixels
                : CopyContentRegion(sourcePixels, checked((int)bitmap.SizeInPixels.Width), pixelWidth, pixelHeight, bytesPerPixel);

            var metadata = new ScreenFrameMetadata(
                Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
                DateTimeOffset.UtcNow,
                pixelWidth,
                pixelHeight,
                checked(pixelWidth * bytesPerPixel),
                1.0f,
                1.0f,
                originX,
                originY,
                expectedProcessId,
                hwnd);
            return new CapturedScreenFrame(ScreenCaptureStatus.Captured, metadata, pixels);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _device.Dispose();
            }
        }

        private static async Task<Windows.Graphics.Capture.Direct3D11CaptureFrame> WaitForFrameAsync(
            Direct3D11CaptureFramePool framePool,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<Windows.Graphics.Capture.Direct3D11CaptureFrame>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            TypedEventHandler<Direct3D11CaptureFramePool, object>? handler = null;
            handler = (_, _) =>
            {
                try
                {
                    var frame = framePool.TryGetNextFrame();
                    if (frame is not null && completion.TrySetResult(frame))
                    {
                        framePool.FrameArrived -= handler;
                    }
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            };

            framePool.FrameArrived += handler;
            try
            {
                return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                framePool.FrameArrived -= handler;
            }
        }

        private static byte[] CopyContentRegion(
            byte[] source,
            int sourceWidth,
            int width,
            int height,
            int bytesPerPixel)
        {
            var sourceStride = checked(sourceWidth * bytesPerPixel);
            var targetStride = checked(width * bytesPerPixel);
            var target = new byte[checked(targetStride * height)];
            for (var row = 0; row < height; row++)
            {
                Buffer.BlockCopy(source, checked(row * sourceStride), target, checked(row * targetStride), targetStride);
            }

            return target;
        }

        private static bool TryValidateTarget(
            nint hwnd,
            int expectedProcessId,
            out int originX,
            out int originY,
            out ScreenCaptureStatus status,
            out string error)
        {
            originX = 0;
            originY = 0;
            status = ScreenCaptureStatus.NotFound;
            error = "Target window was not found.";

            var window = (HWND)hwnd;
            if (!PInvoke.IsWindow(window) || !PInvoke.IsWindowVisible(window))
            {
                return false;
            }

            PInvoke.GetWindowThreadProcessId(window, out var actualProcessId);
            if (actualProcessId != (uint)expectedProcessId)
            {
                status = ScreenCaptureStatus.ProcessMismatch;
                error = $"Target process mismatch: expected {expectedProcessId}, actual {actualProcessId}.";
                return false;
            }

            if (!PInvoke.GetWindowRect(window, out var bounds))
            {
                status = ScreenCaptureStatus.NotFound;
                error = "Target window bounds could not be resolved.";
                return false;
            }

            if (bounds.right <= bounds.left || bounds.bottom <= bounds.top)
            {
                status = ScreenCaptureStatus.NotFound;
                error = "Target window has no visible bounds.";
                return false;
            }

            originX = bounds.left;
            originY = bounds.top;
            status = ScreenCaptureStatus.Captured;
            error = string.Empty;
            return true;
        }

        private static CapturedScreenFrame Failure(ScreenCaptureStatus status, string error)
        {
            return new CapturedScreenFrame(status, null, null, error);
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        }
    }
}
