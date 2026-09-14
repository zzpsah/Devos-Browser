using Devos.Checkpoints;
using Devos.Protocol;

namespace Devos.Core;

public sealed partial class DevosTaskRunner
{
    public async Task<DevosTaskRunResult> ReconcileReadbackAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        var checkpoint = await _checkpointStore.LoadAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (checkpoint is null)
        {
            return new DevosTaskRunResult(taskId, DevosTaskStatus.Failed, "No checkpoint exists for the requested task.");
        }

        if (!string.Equals(checkpoint.ReconciliationState, "readback-required", StringComparison.OrdinalIgnoreCase))
        {
            return new DevosTaskRunResult(
                taskId,
                DevosTaskStatus.Failed,
                "Task is not waiting for provider readback reconciliation.");
        }

        var readback = await _browser.GetObservationAsync(cancellationToken).ConfigureAwait(false);
        var challenge = _challengePolicy.Detect(readback);
        if (challenge is not null)
        {
            await PersistReconciliationCheckpointAsync(
                checkpoint,
                readback,
                "readback-required",
                "human-intervention-required",
                cancellationToken).ConfigureAwait(false);

            return new DevosTaskRunResult(
                taskId,
                DevosTaskStatus.HumanInterventionRequired,
                challenge.Reason);
        }

        var outcome = ClassifyReadback(readback);
        switch (outcome)
        {
            case ReadbackOutcome.Confirmed:
                await PersistReconciliationCheckpointAsync(
                    checkpoint,
                    readback,
                    "readback-confirmed",
                    "approved-executed",
                    cancellationToken).ConfigureAwait(false);

                return new DevosTaskRunResult(
                    taskId,
                    DevosTaskStatus.Continued,
                    "Provider readback contains positive persistence evidence. The prior approved mutation is reconciled without replay.",
                    LastActionResult: checkpoint.CompletedSteps.LastOrDefault());

            case ReadbackOutcome.Absent:
                await PersistReconciliationCheckpointAsync(
                    checkpoint,
                    readback,
                    "readback-absent",
                    "approved-executed",
                    cancellationToken).ConfigureAwait(false);

                return new DevosTaskRunResult(
                    taskId,
                    DevosTaskStatus.Failed,
                    "Provider readback indicates the prior mutation is absent or failed. DEVOS will not replay it automatically; a fresh plan or approval is required.",
                    LastActionResult: checkpoint.CompletedSteps.LastOrDefault());

            default:
                await PersistReconciliationCheckpointAsync(
                    checkpoint,
                    readback,
                    "readback-required",
                    "approved-executed",
                    cancellationToken).ConfigureAwait(false);

                return new DevosTaskRunResult(
                    taskId,
                    DevosTaskStatus.ReconciliationRequired,
                    "Provider readback is still ambiguous. Keep the task on hold and do not replay the mutation.",
                    LastActionResult: checkpoint.CompletedSteps.LastOrDefault());
        }
    }

    private Task PersistReconciliationCheckpointAsync(
        TaskCheckpoint checkpoint,
        BrowserObservation observation,
        string reconciliationState,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var updated = checkpoint with
        {
            ActiveUrl = observation.Url,
            ActiveTab = observation.TabId,
            LastObservation = observation,
            PendingAction = null,
            ApprovalState = approvalState,
            ReconciliationState = reconciliationState,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        return _checkpointStore.SaveAsync(updated, cancellationToken);
    }

    private static ReadbackOutcome ClassifyReadback(BrowserObservation observation)
    {
        var evidence = string.Join(' ', observation.Title, observation.VisibleText).ToLowerInvariant();

        var absenceMarkers = new[]
        {
            "absent",
            "not found",
            "not saved",
            "not submitted",
            "failed",
            "failure",
            "rolled back",
            "rollback"
        };

        if (absenceMarkers.Any(marker => evidence.Contains(marker, StringComparison.Ordinal)))
        {
            return ReadbackOutcome.Absent;
        }

        var confirmationMarkers = new[]
        {
            "success",
            "saved",
            "submitted",
            "completed",
            "complete",
            "reference",
            "receipt",
            "confirmation"
        };

        return confirmationMarkers.Any(marker => evidence.Contains(marker, StringComparison.Ordinal))
            ? ReadbackOutcome.Confirmed
            : ReadbackOutcome.Ambiguous;
    }

    private enum ReadbackOutcome
    {
        Ambiguous,
        Confirmed,
        Absent
    }
}
