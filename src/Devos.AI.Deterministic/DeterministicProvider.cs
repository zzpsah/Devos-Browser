using Devos.AI.Abstractions;
using Devos.Protocol;

namespace Devos.AI.Deterministic;

public sealed class DeterministicProvider : IAiProvider
{
    public string ProviderId => "deterministic";
    public bool IsAvailable => true;

    public Task<PlannerDecision> PlanAsync(PlannerRequest request, CancellationToken cancellationToken = default)
    {
        var goal = request.Goal.Trim();

        if (goal.StartsWith("open ", StringComparison.OrdinalIgnoreCase))
        {
            var target = goal[5..].Trim();
            var url = ResolveUrl(target);
            return Task.FromResult(new PlannerDecision("continue", $"Navigate to {url}", new BrowserAction(BrowserActionKind.Navigate, Value: url, RequiredCapability: "browser.navigate")));
        }

        return Task.FromResult(new PlannerDecision("unsupported", "Deterministic provider supports only bounded commands at this stage."));
    }

    private static string ResolveUrl(string target)
    {
        return target.ToLowerInvariant() switch
        {
            "eshikshakosh" or "e-shikshakosh" or "e shikshakosh" => "https://eshikshakosh.bihar.gov.in/",
            _ when target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) => target,
            _ when target.Contains('.') => "https://" + target,
            _ => throw new InvalidOperationException($"Unknown direct site name: {target}. Use a full URL or configured alias.")
        };
    }
}
