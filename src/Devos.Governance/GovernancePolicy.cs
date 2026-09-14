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
    public GovernanceDecision Classify(BrowserAction action)
    {
        return action.Kind switch
        {
            BrowserActionKind.Navigate or BrowserActionKind.Observe or BrowserActionKind.ReadText or BrowserActionKind.Wait or BrowserActionKind.Scroll or BrowserActionKind.ExtractTable or BrowserActionKind.ExtractLinks or BrowserActionKind.Screenshot or BrowserActionKind.SwitchTab => GovernanceDecision.AutoAllow,
            BrowserActionKind.Click or BrowserActionKind.Type or BrowserActionKind.Select or BrowserActionKind.Download => GovernanceDecision.Controlled,
            BrowserActionKind.Submit or BrowserActionKind.Upload => GovernanceDecision.ApprovalRequired,
            _ => GovernanceDecision.Controlled
        };
    }
}
