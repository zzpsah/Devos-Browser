using Devos.Protocol;

namespace Devos.AI.Abstractions;

public interface IAiProvider
{
    string ProviderId { get; }
    bool IsAvailable { get; }
    Task<PlannerDecision> PlanAsync(PlannerRequest request, CancellationToken cancellationToken = default);
}

public sealed record PlannerRequest(string Goal, BrowserObservation Observation);

public sealed record PlannerDecision(
    string Status,
    string Reason,
    BrowserAction? Action = null);
