using System;
using MahmoudAI.Core.Automation;
using Xunit;

namespace MahmoudAI.Core.Tests
{
    public sealed class CoordinateTransformTests
    {
        [Fact]
        public void FrameCoordinateTransform_MapsOutputToAbsoluteDesktopAccurately()
        {
            var windowBounds = new ScreenRect(100, 200, 800, 600);
            var region = new ScreenRect(10, 20, 400, 300);
            var transform = new FrameCoordinateTransform(
                SourceWindowBoundsPx: windowBounds,
                SourceRegionPx: region,
                OutputWidthPx: 200,
                OutputHeightPx: 150,
                OutputToSourceScaleX: 2.0,
                OutputToSourceScaleY: 2.0,
                CoordinateSpace: CoordinateSpace.AbsoluteDesktopPhysicalPixels);

            // Output coordinate (50, 50)
            // Region relative = 50 * 2.0 = 100
            // Window relative = 10 (region X) + 100 = 110
            // Desktop absolute = 100 (window X) + 110 = 210
            var desktopPoint = transform.MapOutputToAbsoluteDesktop(50f, 50f);

            Assert.Equal(210f, desktopPoint.X);
            Assert.Equal(320f, desktopPoint.Y); // 200 (window Y) + 20 (region Y) + (50 * 2.0 = 100) = 320
        }
    }
}
