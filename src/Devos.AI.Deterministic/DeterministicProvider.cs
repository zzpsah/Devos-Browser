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
            return Continue($"Navigate to {url}", new BrowserAction(BrowserActionKind.Navigate, Value: url, RequiredCapability: "browser.navigate"));
        }

        if (goal.StartsWith("click ", StringComparison.OrdinalIgnoreCase))
        {
            var target = goal[6..].Trim();
            return Continue($"Click {target}", new BrowserAction(BrowserActionKind.Click, Target: target, RequiredCapability: "browser.click"));
        }

        if (goal.StartsWith("read ", StringComparison.OrdinalIgnoreCase))
        {
            var target = goal[5..].Trim();
            return Continue($"Read {target}", new BrowserAction(BrowserActionKind.ReadText, Target: target, RequiredCapability: "browser.dom.read"));
        }

        if (goal.StartsWith("wait for ", StringComparison.OrdinalIgnoreCase))
        {
            var target = goal[9..].Trim();
            return Continue($"Wait for {target}", new BrowserAction(BrowserActionKind.Wait, Target: target, RequiredCapability: "browser.wait.selector"));
        }

        if (goal.StartsWith("extract table ", StringComparison.OrdinalIgnoreCase))
        {
            var target = goal[14..].Trim();
            return Continue($"Extract table {target}", new BrowserAction(BrowserActionKind.ExtractTable, Target: target, RequiredCapability: "browser.extract.table"));
        }

        if (goal.StartsWith("download ", StringComparison.OrdinalIgnoreCase))
        {
            var target = goal[9..].Trim();
            return Continue($"Download from {target}", new BrowserAction(BrowserActionKind.Download, Target: target, RequiredCapability: "browser.download"));
        }

        if (goal.StartsWith("submit ", StringComparison.OrdinalIgnoreCase))
        {
            var target = goal[7..].Trim();
            return Continue($"Submit {target}", new BrowserAction(BrowserActionKind.Submit, Target: target, RequiredCapability: "browser.form.submit"));
        }

        var typeCommand = ParseTypeCommand(goal);
        if (typeCommand is not null)
        {
            return Continue($"Type into {typeCommand.Value.Target}", new BrowserAction(BrowserActionKind.Type, Target: typeCommand.Value.Target, Value: typeCommand.Value.Text, RequiredCapability: "browser.type"));
        }

        return Task.FromResult(new PlannerDecision("unsupported", "Deterministic provider supports only bounded DEVOS commands at this stage."));
    }

    private static Task<PlannerDecision> Continue(string reason, BrowserAction action)
    {
        return Task.FromResult(new PlannerDecision("continue", reason, action));
    }

    private static (string Text, string Target)? ParseTypeCommand(string goal)
    {
        const string prefix = "type ";
        const string separator = " into ";

        if (!goal.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var payload = goal[prefix.Length..];
        var split = payload.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
        if (split <= 0)
        {
            return null;
        }

        var text = payload[..split].Trim();
        var target = payload[(split + separator.Length)..].Trim();
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        return (text, target);
    }

    private static string ResolveUrl(string target)
    {
        return target.ToLowerInvariant() switch
        {
            "eshikshakosh" or "e-shikshakosh" or "e shikshakosh" or "e shiksha kosh" => "https://eshikshakosh.bihar.gov.in/",
            _ when target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) => target,
            _ when target.Contains('.') => "https://" + target,
            _ => throw new InvalidOperationException($"Unknown direct site name: {target}. Use a full URL or configured alias.")
        };
    }
}
