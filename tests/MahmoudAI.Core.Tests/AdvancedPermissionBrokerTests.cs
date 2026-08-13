using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MahmoudAI.Core.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MahmoudAI.Core.Tests
{
    public class AdvancedPermissionBrokerTests
    {
        [Fact]
        public async Task PermissionBroker_ShouldGrantAndEnforceLeases()
        {
            var broker = new AdvancedPermissionBroker(NullLogger<AdvancedPermissionBroker>.Instance);
            broker.ApprovalDelegate = (cap, scope, ct) => Task.FromResult(true);

            bool granted = await broker.RequestCapabilityAsync(
                CapabilityType.FilesRead,
                "workspace/*",
                TimeSpan.FromMinutes(5),
                CancellationToken.None);
            granted.Should().BeTrue();

            // Emergency stop should deny everything
            broker.TriggerEmergencyStop();
            bool denied = await broker.RequestCapabilityAsync(
                CapabilityType.FilesRead,
                "workspace/*",
                TimeSpan.FromMinutes(5),
                CancellationToken.None);
            denied.Should().BeFalse();
        }

        [Fact]
        public async Task LeaseExpiry_ShouldStartAfterApprovalCompletes()
        {
            var clock = new ManualTimeProvider(DateTimeOffset.Parse("2030-01-01T00:00:00Z"));
            var approvalStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseApproval = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var broker = new AdvancedPermissionBroker(
                NullLogger<AdvancedPermissionBroker>.Instance,
                timeProvider: clock)
            {
                ApprovalDelegate = async (_, _, _) =>
                {
                    approvalStarted.SetResult(true);
                    await releaseApproval.Task;
                    return true;
                }
            };

            var request = broker.RequestCapabilityLeaseAsync(
                CapabilityType.FilesRead,
                "workspace",
                TimeSpan.FromMinutes(5),
                CancellationToken.None);
            await approvalStarted.Task;
            clock.Advance(TimeSpan.FromSeconds(30));
            releaseApproval.SetResult(true);
            var lease = await request;

            lease.Should().NotBeNull();
            lease!.Lease.GrantedAt.Should().Be(clock.GetUtcNow().UtcDateTime);
            lease.Lease.ExpiresAt.Should().Be(clock.GetUtcNow().UtcDateTime.AddMinutes(5));
        }

        [Fact]
        public async Task LeaseHandle_ShouldKeepTokenSnapshotReadableAfterBrokerDisposal()
        {
            var broker = new AdvancedPermissionBroker(NullLogger<AdvancedPermissionBroker>.Instance)
            {
                ApprovalDelegate = (_, _, _) => Task.FromResult(true)
            };
            var lease = await broker.RequestCapabilityLeaseAsync(
                CapabilityType.FilesRead,
                "workspace",
                TimeSpan.FromMinutes(5),
                CancellationToken.None);
            lease.Should().NotBeNull();
            var token = lease!.RevocationToken;

            broker.RevokeLease(lease.Lease.LeaseId).Should().BeTrue();

            token.IsCancellationRequested.Should().BeTrue();
            lease.RevocationToken.IsCancellationRequested.Should().BeTrue();
        }

        private sealed class ManualTimeProvider : TimeProvider
        {
            private DateTimeOffset _utcNow;

            public ManualTimeProvider(DateTimeOffset utcNow)
            {
                _utcNow = utcNow;
            }

            public override DateTimeOffset GetUtcNow() => _utcNow;

            public void Advance(TimeSpan amount)
            {
                _utcNow = _utcNow.Add(amount);
            }
        }
    }
}
