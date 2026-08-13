using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MahmoudAI.Core.Automation;
using MahmoudAI.Core.Integration;
using MahmoudAI.Core.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MahmoudAI.Core.Tests
{
    public sealed class SemanticAutomationGuardTests
    {
        [Fact]
        public async Task QueryDeniedByCapabilityBroker_DoesNotReachSemanticBackend()
        {
            var broker = new AdvancedPermissionBroker(NullLogger<AdvancedPermissionBroker>.Instance)
            {
                ApprovalDelegate = (_, _, _) => Task.FromResult(false)
            };
            var fake = new RecordingSemanticAutomation();
            using var guarded = new CapabilityGuardedUiaSemanticAutomation(broker, fake);
            var request = CreateQuery();

            var result = await guarded.QueryAsync(request, CancellationToken.None);

            Assert.Equal(UiaMatchStatus.Denied, result.Status);
            Assert.False(fake.QueryCalled);
        }

        [Fact]
        public async Task ApprovedSetValue_ReachesSemanticBackendWithCancellationBoundary()
        {
            var broker = new AdvancedPermissionBroker(NullLogger<AdvancedPermissionBroker>.Instance)
            {
                ApprovalDelegate = (_, _, _) => Task.FromResult(true)
            };
            var fake = new RecordingSemanticAutomation();
            using var guarded = new CapabilityGuardedUiaSemanticAutomation(broker, fake);
            var request = new UiaActionRequest(CreateQuery(), UiaActionType.SetValue, "hello");

            var result = await guarded.ExecuteAsync(request, CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.True(fake.ActionCalled);
            Assert.Equal(UiaActionType.SetValue, fake.LastAction);
        }

        private static UiaQueryRequest CreateQuery()
        {
            return new UiaQueryRequest(
                "hwnd:123",
                new UiaSelectorPath(new UiaSelector(AutomationId: "SaveButton")));
        }

        private sealed class RecordingSemanticAutomation : IUiaSemanticAutomation
        {
            public bool QueryCalled { get; private set; }
            public bool ActionCalled { get; private set; }
            public UiaActionType? LastAction { get; private set; }

            public Task<UiaQueryResult> QueryAsync(UiaQueryRequest request, CancellationToken cancellationToken)
            {
                QueryCalled = true;
                return Task.FromResult(new UiaQueryResult(
                    UiaMatchStatus.Found,
                    Array.Empty<UiaElementSnapshot>()));
            }

            public Task<UiaActionResult> ExecuteAsync(UiaActionRequest request, CancellationToken cancellationToken)
            {
                ActionCalled = true;
                LastAction = request.Action;
                return Task.FromResult(new UiaActionResult(true, UiaMatchStatus.Found, "executed"));
            }
        }
    }
}
