using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MahmoudAI.Core.Integration;
using MahmoudAI.Core.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MahmoudAI.Core.Tests
{
    public sealed class AutomationLeaseTests
    {
        [Fact]
        public async Task RevokingLease_ShouldCancelInFlightAutomation()
        {
            var broker = ApprovedBroker();
            var backend = new BlockingAutomationBackend();
            var guarded = new CapabilityGuardedAutomationBackend(broker, backend);
            var request = new AutomationRequest(
                CapabilityType.MouseControl,
                "window:test",
                AutomationOperation.Pointer,
                "10,10");

            var execution = guarded.ExecuteAsync(request, CancellationToken.None);
            await backend.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var lease = await broker.RequestCapabilityLeaseAsync(
                CapabilityType.MouseControl,
                "window:test",
                TimeSpan.FromMinutes(1),
                CancellationToken.None);

            lease.Should().NotBeNull();
            broker.RevokeLease(lease!.Lease.LeaseId).Should().BeTrue();

            Func<Task> act = async () => await execution;
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task EmergencyStop_ShouldCancelInFlightAutomation()
        {
            var broker = ApprovedBroker();
            var backend = new BlockingAutomationBackend();
            var guarded = new CapabilityGuardedAutomationBackend(broker, backend);
            var request = new AutomationRequest(
                CapabilityType.KeyboardControl,
                "window:test",
                AutomationOperation.Keyboard,
                "foreground",
                "safe text");

            var execution = guarded.ExecuteAsync(request, CancellationToken.None);
            await backend.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            broker.TriggerEmergencyStop();

            Func<Task> act = async () => await execution;
            await act.Should().ThrowAsync<OperationCanceledException>();
            broker.EmergencyStopTriggered.Should().BeTrue();
            broker.SafeModeActive.Should().BeTrue();
        }

        [Fact]
        public async Task LeaseExpiry_ShouldCancelInFlightAutomation()
        {
            var broker = ApprovedBroker();
            var backend = new BlockingAutomationBackend();
            var guarded = new CapabilityGuardedAutomationBackend(broker, backend, TimeSpan.FromMilliseconds(40));
            var request = new AutomationRequest(
                CapabilityType.MouseControl,
                "window:test",
                AutomationOperation.Pointer,
                "10,10");

            var execution = guarded.ExecuteAsync(request, CancellationToken.None);
            await backend.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Func<Task> act = async () => await execution;
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task RevokedToken_ShouldReachBackendBeforePotentialSideEffect()
        {
            var broker = ApprovedBroker();
            var backend = new CancellationObservingBackend();
            var guarded = new CapabilityGuardedAutomationBackend(broker, backend);
            var request = new AutomationRequest(
                CapabilityType.MouseControl,
                "window:test",
                AutomationOperation.Pointer,
                "10,10");

            var execution = guarded.ExecuteAsync(request, CancellationToken.None);
            await backend.Ready.Task.WaitAsync(TimeSpan.FromSeconds(2));
            broker.TriggerEmergencyStop();
            backend.Release.SetResult(true);

            Func<Task> act = async () => await execution;
            await act.Should().ThrowAsync<OperationCanceledException>();
            backend.SideEffectAttempted.Should().BeFalse();
        }

        private static AdvancedPermissionBroker ApprovedBroker()
        {
            return new AdvancedPermissionBroker(NullLogger<AdvancedPermissionBroker>.Instance)
            {
                ApprovalDelegate = (_, _, _) => Task.FromResult(true)
            };
        }

        private sealed class BlockingAutomationBackend : IWindowsAutomationBackend
        {
            public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public async Task<AutomationResult> ExecuteAsync(AutomationRequest request, CancellationToken cancellationToken)
            {
                Started.TrySetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new AutomationResult(true, "unreachable");
            }
        }

        private sealed class CancellationObservingBackend : IWindowsAutomationBackend
        {
            public TaskCompletionSource<bool> Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public bool SideEffectAttempted { get; private set; }

            public async Task<AutomationResult> ExecuteAsync(AutomationRequest request, CancellationToken cancellationToken)
            {
                Ready.TrySetResult(true);
                await Release.Task.WaitAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                SideEffectAttempted = true;
                return new AutomationResult(true, "side-effect");
            }
        }
    }
}
