using Devos.Protocol;

namespace Devos.Governance;

public enum GovernanceDecision
{
    AutoAllow,
    Controlled,
    ApprovalRequired,
    HumanOnly
}

public sealed class GovernancePolicy
{
    private static readonly string[] CommitKeywords =
    {
        "submit",
        "save",
        "delete",
        "remove",
        "send",
        "upload",
        "approve",
        "publish",
        "register",
        "payment",
        "pay",
        "purchase",
        "checkout",
        "confirm",
        "final"
    };

    private static readonly string[] HumanOnlyKeywords =
    {
        "captcha",
        "otp",
        "one-time password",
        "verification code",
        "mfa",
        "2fa",
        "security key",
        "biometric"
    };

    public GovernanceDecision Classify(BrowserAction action)
    {
        var semanticText = string.Join(
            ' ',
            action.Target,
            action.Value,
            action.RequiredCapability,
            action.SemanticHint).ToLowerInvariant();

        if (ContainsAny(semanticText, HumanOnlyKeywords))
        {
            return GovernanceDecision.HumanOnly;
        }

        if (IsExplicitCommitAction(action.Kind) || ContainsAny(semanticText, CommitKeywords))
        {
            return GovernanceDecision.ApprovalRequired;
        }

        return action.Kind switch
        {
            BrowserActionKind.Navigate or BrowserActionKind.Observe or BrowserActionKind.ReadText or BrowserActionKind.Wait or BrowserActionKind.Scroll or BrowserActionKind.ExtractTable or BrowserActionKind.ExtractLinks or BrowserActionKind.Screenshot or BrowserActionKind.SwitchTab => GovernanceDecision.AutoAllow,
            BrowserActionKind.Click or BrowserActionKind.Type or BrowserActionKind.Select or BrowserActionKind.Download => GovernanceDecision.Controlled,
            _ => GovernanceDecision.Controlled
        };
    }

    private static bool IsExplicitCommitAction(BrowserActionKind kind)
    {
        return kind is BrowserActionKind.Submit or BrowserActionKind.Upload;
    }

    private static bool ContainsAny(string text, IEnumerable<string> terms)
    {
        return terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
