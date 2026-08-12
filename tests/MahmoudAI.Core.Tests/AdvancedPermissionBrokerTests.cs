using System;
using System.Threading;
using FluentAssertions;
using MahmoudAI.Core.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MahmoudAI.Core.Tests
{
    public class AdvancedPermissionBrokerTests
    {
        [Fact]
        public void PermissionBroker_ShouldGrantAndEnforceLeases()
        {
            var broker = new AdvancedPermissionBroker(NullLogger<AdvancedPermissionBroker>.Instance);
            
            bool granted = broker.RequestCapability(CapabilityType.FilesRead, "workspace/*", TimeSpan.FromMinutes(5));
            granted.Should().BeTrue();

            // Emergency stop should deny everything
            broker.TriggerEmergencyStop();
            bool denied = broker.RequestCapability(CapabilityType.FilesRead, "workspace/*", TimeSpan.FromMinutes(5));
            denied.Should().BeFalse();
        }
    }
}
