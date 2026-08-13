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

            if (!TryValidateTarget(hwnd, expectedProcessId, out var originX, out var originY, out _, out _, out var targetStatus, out var targetError))
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

            var itemSize = item.Size;
            if (itemSize.Width <= 0 || itemSize.Height <= 0)
            {
                return Failure(ScreenCaptureStatus.NotFound, "Target window has zero or negative capture dimensions.");
            }

            using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                _device,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                FrameBufferCount,
                itemSize);
            using var session = framePool.CreateCaptureSession(item);
            session.IsCursorCaptureEnabled = request.IncludeCursor;

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            Windows.Graphics.Capture.Direct3D11CaptureFrame frame;
            try
            {
                var frameTask = WaitForFrameAsync(framePool, linkedCts.Token);
                session.StartCapture();
                frame = await frameTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return Failure(ScreenCaptureStatus.Timeout, "Windows.Graphics.Capture frame acquisition timed out after 5 seconds.");
            }
            catch (OperationCanceledException)
            {
                return Failure(ScreenCaptureStatus.Cancelled, "Screen capture was cancelled.");
            }
            catch (Exception ex)
            {
                return Failure(ScreenCaptureStatus.ProviderError, $"Frame capture failed: {ex.Message}");
            }

            using (frame)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryValidateTarget(hwnd, expectedProcessId, out originX, out originY, out var dpiScaleX, out var dpiScaleY, out targetStatus, out targetError))
                {
                    return Failure(targetStatus, targetError);
                }

                using var bitmap = CanvasBitmap.CreateFromDirect3D11Surface(_device, frame.Surface);
                var sourceWidth = (int)bitmap.SizeInPixels.Width;
                var sourceHeight = (int)bitmap.SizeInPixels.Height;
                var sourcePixels = bitmap.GetPixelBytes();

                var region = request.Region;
                var cropX = region?.X ?? 0;
                var cropY = region?.Y ?? 0;
                var cropWidth = region?.Width > 0 ? region.Width : sourceWidth;
                var cropHeight = region?.Height > 0 ? region.Height : sourceHeight;

                cropX = Math.Clamp(cropX, 0, Math.Max(0, sourceWidth - 1));
                cropY = Math.Clamp(cropY, 0, Math.Max(0, sourceHeight - 1));
                cropWidth = Math.Clamp(cropWidth, 1, sourceWidth - cropX);
                cropHeight = Math.Clamp(cropHeight, 1, sourceHeight - cropY);

                var finalWidth = cropWidth;
                var finalHeight = cropHeight;
                if (request.MaxWidth is int maxWidth && maxWidth > 0 && finalWidth > maxWidth)
                {
                    finalHeight = (int)((double)maxWidth / finalWidth * finalHeight);
                    finalWidth = maxWidth;
                }
                if (request.MaxHeight is int maxHeight && maxHeight > 0 && finalHeight > maxHeight)
                {
                    finalWidth = (int)((double)maxHeight / finalHeight * finalWidth);
                    finalHeight = maxHeight;
                }

                var bytesPerPixel = 4;
                var pixels = ExtractAndProcessRegion(
                    sourcePixels,
                    sourceWidth,
                    sourceHeight,
                    cropX,
                    cropY,
                    cropWidth,
                    cropHeight,
                    finalWidth,
                    finalHeight,
                    bytesPerPixel);

                var metadata = new ScreenFrameMetadata(
                    Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
                    DateTimeOffset.UtcNow,
                    finalWidth,
                    finalHeight,
                    checked(finalWidth * bytesPerPixel),
                    dpiScaleX,
                    dpiScaleY,
                    originX + cropX,
                    originY + cropY,
                    expectedProcessId,
                    hwnd);
                return new CapturedScreenFrame(ScreenCaptureStatus.Captured, metadata, pixels);
            }
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

        private static byte[] ExtractAndProcessRegion(
            byte[] source,
            int sourceWidth,
            int sourceHeight,
            int cropX,
            int cropY,
            int cropWidth,
            int cropHeight,
            int finalWidth,
            int finalHeight,
            int bytesPerPixel)
        {
            var sourceStride = checked(sourceWidth * bytesPerPixel);
            var croppedStride = checked(cropWidth * bytesPerPixel);
            var cropped = new byte[checked(croppedStride * cropHeight)];

            for (var row = 0; row < cropHeight; row++)
            {
                var srcRow = cropY + row;
                if (srcRow >= sourceHeight) break;
                var srcOffset = checked(srcRow * sourceStride + cropX * bytesPerPixel);
                var dstOffset = checked(row * croppedStride);
                Buffer.BlockCopy(source, srcOffset, cropped, dstOffset, Math.Min(croppedStride, source.Length - srcOffset));
            }

            if (cropWidth == finalWidth && cropHeight == finalHeight)
            {
                return cropped;
            }

            var finalStride = checked(finalWidth * bytesPerPixel);
            var final = new byte[checked(finalStride * finalHeight)];
            for (var y = 0; y < finalHeight; y++)
            {
                var srcY = (int)((double)y / finalHeight * cropHeight);
                srcY = Math.Clamp(srcY, 0, cropHeight - 1);
                var srcRowOffset = checked(srcY * croppedStride);
                var dstRowOffset = checked(y * finalStride);

                for (var x = 0; x < finalWidth; x++)
                {
                    var srcX = (int)((double)x / finalWidth * cropWidth);
                    srcX = Math.Clamp(srcX, 0, cropWidth - 1);
                    var srcPixelOffset = checked(srcRowOffset + srcX * bytesPerPixel);
                    var dstPixelOffset = checked(dstRowOffset + x * bytesPerPixel);

                    if (srcPixelOffset + bytesPerPixel <= cropped.Length && dstPixelOffset + bytesPerPixel <= final.Length)
                    {
                        final[dstPixelOffset] = cropped[srcPixelOffset];
                        final[dstPixelOffset + 1] = cropped[srcPixelOffset + 1];
                        final[dstPixelOffset + 2] = cropped[srcPixelOffset + 2];
                        final[dstPixelOffset + 3] = cropped[srcPixelOffset + 3];
                    }
                }
            }

            return final;
        }

        private static bool TryValidateTarget(
            nint hwnd,
            int expectedProcessId,
            out int originX,
            out int originY,
            out float dpiScaleX,
            out float dpiScaleY,
            out ScreenCaptureStatus status,
            out string error)
        {
            originX = 0;
            originY = 0;
            dpiScaleX = 1.0f;
            dpiScaleY = 1.0f;
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

            var dpi = PInvoke.GetDpiForWindow(window);
            if (dpi > 0)
            {
                dpiScaleX = dpi / 96.0f;
                dpiScaleY = dpi / 96.0f;
            }

            status = ScreenCaptureStatus.Captured;
            error = string.Empty;
            return true;
        }

        private static CapturedScreenFrame Failure(ScreenCaptureStatus status, string error)
        {
            return new CapturedScreenFrame(status, null, null, null, error);
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        }
    }
}
