using System;

namespace MahmoudAI.Core.Automation
{
    public sealed record TargetIdentityTicket(
        nint Hwnd,
        int ProcessId,
        long ProcessStartTimeTicks,
        string WindowTarget,
        UiaSelectorPath Selector,
        string? AutomationId,
        string? Name,
        string? ControlType,
        ScreenRect ObservedBounds,
        string FrameId,
        DateTimeOffset ObservedAt)
    {
        public bool IsFresh(DateTimeOffset now, TimeSpan maxAge)
        {
            return now - ObservedAt <= maxAge;
        }
    }
}
