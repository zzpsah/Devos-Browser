using Devos.AI.Abstractions;
using Devos.Browser.Abstractions;
using Devos.Checkpoints;
using Devos.Governance;
using Devos.Protocol;
using Devos.Verification;

namespace Devos.Core;

public enum DevosTaskStatus
{
    Completed,
    Unsupported,
    AwaitingApproval,
    HumanInterventionRequired,
    ReconciliationRequired,
    Failed,
    Continued
}

public sealed record DevosTaskRunResult(
    string TaskId,
    DevosTaskStatus Status,
    string Reason,
    BrowserAction? PendingAction = null,
    BrowserActionResult? LastActionResult = null);

public sealed partial class DevosTaskRunner
{
    private readonly IAiProvider _planner;
    private readonly IBrowserAdapter _browser;
    private readonly GovernancePolicy _governance;
    private readonly SecurityChallengePolicy _challengePolicy;
    private readonly IActionVerifier _verifier;
    private readonly ICheckpointStore _checkpointStore;

    public DevosTaskRunner(
        IAiProvider planner,
        IBrowserAdapter browser,
        GovernancePolicy governance,
        SecurityChallengePolicy challengePolicy,
        IActionVerifier verifier,
        ICheckpointStore checkpointStore)
    {
        _planner = planner;
        _browser = browser;
        _governance = governance;
        _challengePolicy = challengePolicy;
        _verifier = verifier;
        _checkpointStore = checkpointStore;
    }

    public async Task<DevosTaskRunResult> RunOneStepAsync(string taskId, string goal, CancellationToken cancellationToken = default)
    {
        var before = await _browser.GetObservationAsync(cancellationToken);
        var challenge = _challengePolicy.Detect(before);
        if (challenge is not null)
        {
            await SaveCheckpointAsync(taskId, goal, before, null, "human-intervention-required", "challenge-detected", cancellationToken);
            return new DevosTaskRunResult(taskId, DevosTaskStatus.HumanInterventionRequired, challenge.Reason);
        }

        var decision = await _planner.PlanAsync(new PlannerRequest(goal, before), cancellationToken);
        if (decision.Action is null)
        {
            var status = string.Equals(decision.Status, "done", StringComparison.OrdinalIgnoreCase)
                ? DevosTaskStatus.Completed
                : DevosTaskStatus.Unsupported;

            await SaveCheckpointAsync(taskId, goal, before, null, status.ToString().ToLowerInvariant(), "none", cancellationToken);
            return new DevosTaskRunResult(taskId, status, decision.Reason);
        }

        var plannedAction = BindObservationContext(decision.Action, before);
        var governanceDecision = _governance.Classify(plannedAction);
        if (governanceDecision == GovernanceDecision.HumanOnly)
        {
            await SaveCheckpointAsync(taskId, goal, before, plannedAction, "human-intervention-required", "human-only-action", cancellationToken);
            return new DevosTaskRunResult(taskId, DevosTaskStatus.HumanInterventionRequired, decision.Reason, plannedAction);
        }

        if (governanceDecision == GovernanceDecision.ApprovalRequired)
        {
            await SaveCheckpointAsync(taskId, goal, before, plannedAction, "approval-required", "pending-user-approval", cancellationToken);
            return new DevosTaskRunResult(taskId, DevosTaskStatus.AwaitingApproval, decision.Reason, plannedAction);
        }

        var adapterResult = await _browser.ExecuteAsync(plannedAction, cancellationToken);
        var after = await _browser.GetObservationAsync(cancellationToken);
        var verified = _verifier.Verify(plannedAction, before, after, _browser.ProviderId, adapterResult.Success, adapterResult.Attempts, adapterResult.Error);

        await SaveCheckpointAsync(taskId, goal, after, null, "continued", verified.Verification.ToString(), cancellationToken, verified);
        return new DevosTaskRunResult(taskId, verified.Success ? DevosTaskStatus.Continued : DevosTaskStatus.Failed, decision.Reason, LastActionResult: verified);
    }

    public async Task<DevosTaskRunResult> ExecuteApprovedPendingActionAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        var checkpoint = await _checkpointStore.LoadAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (checkpoint is null)
        {
            return new DevosTaskRunResult(taskId, DevosTaskStatus.Failed, "No checkpoint exists for the requested task.");
        }

        var pendingAction = checkpoint.PendingAction;
        if (pendingAction is null || !string.Equals(checkpoint.ApprovalState, "approval-required", StringComparison.OrdinalIgnoreCase))
        {
            return new DevosTaskRunResult(taskId, DevosTaskStatus.Failed, "No approval-required pending action exists for this task.");
        }

        if (_governance.Classify(pendingAction) != GovernanceDecision.ApprovalRequired)
        {
            return new DevosTaskRunResult(taskId, DevosTaskStatus.Failed, "Checkpoint pending action is not approval-gated under the current governance policy.");
        }

        var freshObservation = await _browser.GetObservationAsync(cancellationToken).ConfigureAwait(false);
        var challenge = _challengePolicy.Detect(freshObservation);
        if (challenge is not null)
        {
            await SaveCheckpointAsync(
                taskId,
                checkpoint.Goal,
                freshObservation,
                pendingAction,
                "human-intervention-required",
                "challenge-detected-before-approved-action",
                cancellationToken).ConfigureAwait(false);

            return new DevosTaskRunResult(
                taskId,
                DevosTaskStatus.HumanInterventionRequired,
                challenge.Reason,
                pendingAction);
        }

        var reboundAction = BindObservationContext(pendingAction, freshObservation);
        if (ApprovalContextChanged(pendingAction, reboundAction))
        {
            await SaveCheckpointAsync(
                taskId,
                checkpoint.Goal,
                freshObservation,
                reboundAction,
                "approval-required",
                "approval-context-changed",
                cancellationToken).ConfigureAwait(false);

            return new DevosTaskRunResult(
                taskId,
                DevosTaskStatus.AwaitingApproval,
                "The page or target changed after approval was requested. Fresh user approval is required.",
                reboundAction);
        }

        var adapterResult = await _browser.ExecuteAsync(reboundAction, cancellationToken).ConfigureAwait(false);
        var after = await _browser.GetObservationAsync(cancellationToken).ConfigureAwait(false);
        var verified = _verifier.Verify(
            reboundAction,
            freshObservation,
            after,
            _browser.ProviderId,
            adapterResult.Success,
            adapterResult.Attempts,
            adapterResult.Error);

        var requiresReadback = verified.Verification == VerificationState.HoldReadbackRequired;
        var reconciliationState = requiresReadback ? "readback-required" : verified.Verification.ToString();
        var status = requiresReadback
            ? DevosTaskStatus.ReconciliationRequired
            : verified.Success
                ? DevosTaskStatus.Continued
                : DevosTaskStatus.Failed;

        await SaveCheckpointAsync(
            taskId,
            checkpoint.Goal,
            after,
            null,
            "approved-executed",
            reconciliationState,
            cancellationToken,
            verified).ConfigureAwait(false);

        return new DevosTaskRunResult(
            taskId,
            status,
            requiresReadback
                ? "Approved action executed, but positive persistence evidence is missing. Provider readback is required before retry or continuation."
                : "Approved action executed and verification completed.",
            LastActionResult: verified);
    }

    private static bool ApprovalContextChanged(BrowserAction approvedAction, BrowserAction reboundAction)
    {
        if (string.IsNullOrWhiteSpace(approvedAction.Target))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(reboundAction.SnapshotToken) || string.IsNullOrWhiteSpace(reboundAction.SemanticHint))
        {
            return true;
        }

        return !string.Equals(approvedAction.SemanticHint, reboundAction.SemanticHint, StringComparison.Ordinal);
    }

    private static BrowserAction BindObservationContext(BrowserAction action, BrowserObservation observation)
    {
        if (string.IsNullOrWhiteSpace(action.Target))
        {
            return action;
        }

        var element = observation.Elements.FirstOrDefault(candidate =>
            string.Equals(candidate.Ref, action.Target, StringComparison.Ordinal));

        if (element is null)
        {
            return action with
            {
                SnapshotToken = null,
                SemanticHint = null
            };
        }

        var semanticHint = string.Join(
            ' ',
            element.Role,
            element.Text,
            element.Type,
            element.Href).Trim();

        return action with
        {
            SnapshotToken = observation.SnapshotToken,
            SemanticHint = semanticHint
        };
    }

    private Task SaveCheckpointAsync(
        string taskId,
        string goal,
        BrowserObservation observation,
        BrowserAction? pendingAction,
        string approvalState,
        string reconciliationState,
        CancellationToken cancellationToken,
        BrowserActionResult? completedStep = null)
    {
        var now = DateTimeOffset.UtcNow;
        var completed = completedStep is null ? Array.Empty<BrowserActionResult>() : new[] { completedStep };
        var checkpoint = new TaskCheckpoint(
            TaskId: taskId,
            Goal: goal,
            BrowserBackend: _browser.ProviderId,
            AiProvider: _planner.ProviderId,
            CurrentStep: completed.Length,
            CompletedSteps: completed,
            ActiveUrl: observation.Url,
            ActiveTab: observation.TabId,
            LastObservation: observation,
            PendingAction: pendingAction,
            ApprovalState: approvalState,
            ReconciliationState: reconciliationState,
            CreatedAt: now,
            UpdatedAt: now);

        return _checkpointStore.SaveAsync(checkpoint, cancellationToken);
    }
}
