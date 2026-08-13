using System;
using MahmoudAI.Core.Automation;

namespace MahmoudAI.WindowsIntegration.Automation
{
    public static class WindowsGraphicsCaptureBackendTransformExtensions
    {
        public static FrameCoordinateTransform CreateAuthoritativeTransform(
            ScreenFrameMetadata metadata,
            int sourceWindowWidth,
            int sourceWindowHeight,
            ScreenRect actualRegionPx)
        {
            ArgumentNullException.ThrowIfNull(metadata);

            double scaleX = (double)actualRegionPx.Width / metadata.PixelWidth;
            double scaleY = (double)actualRegionPx.Height / metadata.PixelHeight;

            return new FrameCoordinateTransform(
                SourceWindowBoundsPx: new ScreenRect(metadata.ScreenOriginX - actualRegionPx.X, metadata.ScreenOriginY - actualRegionPx.Y, sourceWindowWidth, sourceWindowHeight),
                SourceRegionPx: actualRegionPx,
                OutputWidthPx: metadata.PixelWidth,
                OutputHeightPx: metadata.PixelHeight,
                OutputToSourceScaleX: scaleX,
                OutputToSourceScaleY: scaleY,
                CoordinateSpace: CoordinateSpace.AbsoluteDesktopPhysicalPixels);
        }
    }
}
