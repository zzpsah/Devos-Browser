using Devos.Governance;
using Devos.Protocol;
using Xunit;

namespace Devos.UnitTests;

public sealed class GovernancePolicyTests
{
    [Fact]
    public void NavigateIsAutoAllowed()
    {
        var policy = new GovernancePolicy();
        Assert.Equal(GovernanceDecision.AutoAllow, policy.Classify(new BrowserAction(BrowserActionKind.Navigate, Value: "https://example.com")));
    }

    [Fact]
    public void ClickIsControlled()
    {
        var policy = new GovernancePolicy();
        Assert.Equal(GovernanceDecision.Controlled, policy.Classify(new BrowserAction(BrowserActionKind.Click, Target: "d1")));
    }

    [Fact]
    public void SubmitRequiresApproval()
    {
        var policy = new GovernancePolicy();
        Assert.Equal(GovernanceDecision.ApprovalRequired, policy.Classify(new BrowserAction(BrowserActionKind.Submit, Target: "d1")));
    }
}
