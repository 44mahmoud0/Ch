using System;

namespace MahmoudAI.Core.Automation
{
    public enum CoordinateSpace
    {
        SourceWindowPhysicalPixels,
        CroppedRegionPhysicalPixels,
        NormalizedOutputPixels,
        AbsoluteDesktopPhysicalPixels
    }

    public sealed record ScreenRect(
        int X,
        int Y,
        int Width,
        int Height);

    public sealed record FrameCoordinateTransform(
        ScreenRect SourceWindowBoundsPx,
        ScreenRect SourceRegionPx,
        int OutputWidthPx,
        int OutputHeightPx,
        double OutputToSourceScaleX,
        double OutputToSourceScaleY,
        CoordinateSpace CoordinateSpace)
    {
        public ScreenPoint MapOutputToAbsoluteDesktop(float outputX, float outputY)
        {
            // Step 1: Undo proportional output scaling to get source region coordinates
            double regionX = outputX * OutputToSourceScaleX;
            double regionY = outputY * OutputToSourceScaleY;

            // Step 2: Add region offset relative to source window
            double windowPixelX = SourceRegionPx.X + regionX;
            double windowPixelY = SourceRegionPx.Y + regionY;

            // Step 3: Add source window screen origin to get absolute desktop physical pixels
            double desktopX = SourceWindowBoundsPx.X + windowPixelX;
            double desktopY = SourceWindowBoundsPx.Y + windowPixelY;

            return new ScreenPoint((float)desktopX, (float)desktopY);
        }

        public ScreenPolygon MapOutputPolygonToAbsoluteDesktop(ScreenPolygon outputPolygon)
        {
            var tl = MapOutputToAbsoluteDesktop(outputPolygon.TopLeft.X, outputPolygon.TopLeft.Y);
            var tr = MapOutputToAbsoluteDesktop(outputPolygon.TopRight.X, outputPolygon.TopRight.Y);
            var br = MapOutputToAbsoluteDesktop(outputPolygon.BottomRight.X, outputPolygon.BottomRight.Y);
            var bl = MapOutputToAbsoluteDesktop(outputPolygon.BottomLeft.X, outputPolygon.BottomLeft.Y);

            return new ScreenPolygon(
                TopLeft: outputPolygon.TopLeft,
                TopRight: outputPolygon.TopRight,
                BottomRight: outputPolygon.BottomRight,
                BottomLeft: outputPolygon.BottomLeft,
                AbsoluteTopLeft: tl,
                AbsoluteBottomRight: br);
        }

        public static FrameCoordinateTransform FromMetadata(
            ScreenFrameMetadata metadata,
            ScreenCaptureRequest request,
            int sourceWindowWidth,
            int sourceWindowHeight)
        {
            int regionX = request.Region?.X ?? 0;
            int regionY = request.Region?.Y ?? 0;
            int regionWidth = request.Region?.Width > 0 ? request.Region.Width : sourceWindowWidth;
            int regionHeight = request.Region?.Height > 0 ? request.Region.Height : sourceWindowHeight;

            double scaleX = (double)regionWidth / metadata.PixelWidth;
            double scaleY = (double)regionHeight / metadata.PixelHeight;

            return new FrameCoordinateTransform(
                SourceWindowBoundsPx: new ScreenRect(metadata.ScreenOriginX - regionX, metadata.ScreenOriginY - regionY, sourceWindowWidth, sourceWindowHeight),
                SourceRegionPx: new ScreenRect(regionX, regionY, regionWidth, regionHeight),
                OutputWidthPx: metadata.PixelWidth,
                OutputHeightPx: metadata.PixelHeight,
                OutputToSourceScaleX: scaleX,
                OutputToSourceScaleY: scaleY,
                CoordinateSpace: CoordinateSpace.AbsoluteDesktopPhysicalPixels);
        }
    }
}
