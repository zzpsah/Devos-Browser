using Devos.Governance;
using Devos.Protocol;
using Xunit;

namespace Devos.UnitTests;

public sealed class GovernanceSemanticTests
{
    [Theory]
    [InlineData("save")]
    [InlineData("delete selected record")]
    [InlineData("send message")]
    [InlineData("payment button")]
    [InlineData("register student")]
    public void CommitLikeClickRequiresApproval(string target)
    {
        var policy = new GovernancePolicy();

        var decision = policy.Classify(new BrowserAction(BrowserActionKind.Click, Target: target));

        Assert.Equal(GovernanceDecision.ApprovalRequired, decision);
    }

    [Theory]
    [InlineData("otp")]
    [InlineData("captcha")]
    [InlineData("verification code")]
    [InlineData("mfa")]
    public void ChallengeLikeActionsAreHumanOnly(string target)
    {
        var policy = new GovernancePolicy();

        var decision = policy.Classify(new BrowserAction(BrowserActionKind.Type, Target: target, Value: "123456"));

        Assert.Equal(GovernanceDecision.HumanOnly, decision);
    }
}
