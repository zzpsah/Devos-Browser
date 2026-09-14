using Devos.Protocol;

namespace Devos.Checkpoints;

public sealed record TaskCheckpoint(
    string TaskId,
    string Goal,
    string? BrowserBackend,
    string? AiProvider,
    int CurrentStep,
    IReadOnlyList<BrowserActionResult> CompletedSteps,
    string? ActiveUrl,
    string? ActiveTab,
    BrowserObservation? LastObservation,
    BrowserAction? PendingAction,
    string ApprovalState,
    string ReconciliationState,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
