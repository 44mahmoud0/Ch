using System;
using System.Threading;
using System.Threading.Tasks;
using MahmoudAI.Core.Security;
using Microsoft.Extensions.Logging;

namespace MahmoudAI.Core.Automation
{
    public enum VerificationStatus
    {
        Verified,
        NotVerified,
        OutcomeUnknown,
        TargetChanged,
        Ambiguous,
        StaleObservation,
        ProcessMismatch,
        ActionDenied,
        ActionFailedBeforeCommit,
        ObservationFailedAfterAction,
        CancelledBeforeAction,
        CancelledAfterAction,
        TimeoutBeforeAction,
        TimeoutAfterAction,
        ProviderError
    }

    public sealed record VerificationResult(
        VerificationStatus Status,
        string Message,
        ScreenObservation? PostActionObservation = null,
        ScreenFusionResult? PostActionFusion = null);

    public sealed class ClosedLoopVerifier
    {
        private readonly IScreenObservationService _observationService;
        private readonly IUiaSemanticAutomation _uiaAutomation;
        private readonly AdvancedPermissionBroker _permissionBroker;
        private readonly ILogger<ClosedLoopVerifier> _logger;

        public ClosedLoopVerifier(
            IScreenObservationService observationService,
            IUiaSemanticAutomation uiaAutomation,
            AdvancedPermissionBroker permissionBroker,
            ILogger<ClosedLoopVerifier> logger)
        {
            _observationService = observationService ?? throw new ArgumentNullException(nameof(observationService));
            _uiaAutomation = uiaAutomation ?? throw new ArgumentNullException(nameof(uiaAutomation));
            _permissionBroker = permissionBroker ?? throw new ArgumentNullException(nameof(permissionBroker));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<VerificationResult> ExecuteAndVerifyAsync(
            TargetIdentityTicket ticket,
            VerificationExpectation expectation,
            Func<CancellationToken, Task> action,
            ScreenCaptureRequest captureRequest,
            ScreenPrivacyContext privacyContext,
            OcrRequest ocrRequest,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(ticket);
            ArgumentNullException.ThrowIfNull(expectation);
            ArgumentNullException.ThrowIfNull(action);
            ArgumentNullException.ThrowIfNull(captureRequest);
            ArgumentNullException.ThrowIfNull(privacyContext);
            ArgumentNullException.ThrowIfNull(ocrRequest);

            if (cancellationToken.IsCancellationRequested)
            {
                return new VerificationResult(VerificationStatus.CancelledBeforeAction, "Action cancelled before execution.");
            }

            // 1. Pre-Action Revalidation (Zero-side-effect check)
            var now = DateTimeOffset.UtcNow;
            if (!ticket.IsFresh(now, TimeSpan.FromSeconds(5)))
            {
                _logger.LogWarning("Target identity ticket is stale. Aborting action with zero side effects.");
                return new VerificationResult(VerificationStatus.StaleObservation, "Target identity ticket exceeded maximum age.");
            }

            // Perform fresh observation to revalidate target identity
            var freshObsResult = await _observationService.ObserveAndFuseAsync(
                captureRequest, privacyContext, ocrRequest, ticket.Name ?? "target", cancellationToken).ConfigureAwait(false);

            if (freshObsResult.Observation == null || freshObsResult.FusionResult.Status != FusionStatus.Matched)
            {
                _logger.LogWarning("Pre-action revalidation failed: target element no longer matches or is missing.");
                return new VerificationResult(VerificationStatus.TargetChanged, "Target element changed or disappeared during pre-action revalidation.");
            }

            var observation = freshObsResult.Observation;
            var bestCandidate = freshObsResult.FusionResult.BestCandidate;
            if (bestCandidate == null)
            {
                _logger.LogWarning("Pre-action revalidation failed: best candidate is null.");
                return new VerificationResult(VerificationStatus.TargetChanged, "Target element changed or disappeared during pre-action revalidation.");
            }

            if (observation.ProcessId != ticket.ProcessId || bestCandidate.SourceProcessId != ticket.ProcessId)
            {
                _logger.LogWarning("Process ID mismatch detected during pre-action revalidation (ticket PID: {TicketPid}, obs PID: {ObsPid}).", ticket.ProcessId, observation.ProcessId);
                return new VerificationResult(VerificationStatus.ProcessMismatch, "Target process ID changed before action execution.");
            }

            // 2. Capability-Guarded Action via Permission Broker Lease Request
            using var leaseHandle = await _permissionBroker.RequestCapabilityLeaseAsync(
                CapabilityType.MouseControl, "Execute closed-loop verified automation action", TimeSpan.FromMinutes(2), cancellationToken).ConfigureAwait(false);

            if (leaseHandle is null)
            {
                _logger.LogWarning("Action denied by Capability Broker lease revocation or denial.");
                return new VerificationResult(VerificationStatus.ActionDenied, "Action execution denied by permission broker.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return new VerificationResult(VerificationStatus.CancelledBeforeAction, "Action cancelled right before invocation.");
            }

            // 3. Side-Effect Commit Boundary
            bool actionCommitted = false;
            try
            {
                // Execute action with linked revocation token
                using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    leaseHandle.RevocationToken);

                await action(linkedCancellation.Token).ConfigureAwait(false);
                actionCommitted = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new VerificationResult(
                    actionCommitted ? VerificationStatus.CancelledAfterAction : VerificationStatus.CancelledBeforeAction,
                    "Action execution was cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Action execution failed with exception (committed: {Committed})", actionCommitted);
                return new VerificationResult(
                    actionCommitted ? VerificationStatus.OutcomeUnknown : VerificationStatus.ActionFailedBeforeCommit,
                    $"Action execution failed: {ex.Message}");
            }

            // 4. Fresh Post-Action Observation (Never reuse old observations)
            ScreenObservationResult postObservationResult;
            try
            {
                postObservationResult = await _observationService.ObserveAndFuseAsync(
                    captureRequest, privacyContext, ocrRequest, ticket.Name ?? "target", cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to capture fresh post-action observation.");
                return new VerificationResult(VerificationStatus.ObservationFailedAfterAction, $"Post-action observation failed: {ex.Message}");
            }

            if (postObservationResult.Observation == null)
            {
                return new VerificationResult(VerificationStatus.ObservationFailedAfterAction, "Post-action observation returned null.");
            }

            // 5. Verification Expectation Evaluation
            bool expectationSatisfied = expectation.Evaluate(postObservationResult.Observation, postObservationResult.FusionResult);

            if (expectationSatisfied)
            {
                return new VerificationResult(VerificationStatus.Verified, "Verification expectation successfully satisfied.", postObservationResult.Observation, postObservationResult.FusionResult);
            }
            else
            {
                return new VerificationResult(VerificationStatus.NotVerified, "Post-action observation did not satisfy verification expectation.", postObservationResult.Observation, postObservationResult.FusionResult);
            }
        }
    }
}
