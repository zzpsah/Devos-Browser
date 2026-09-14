using Devos.Governance;
using Devos.Protocol;
using Xunit;

namespace Devos.UnitTests;

public sealed class SecurityChallengePolicyTests
{
    [Fact]
    public void RealCaptchaRequiresHumanIntervention()
    {
        var policy = new SecurityChallengePolicy();
        var observation = Observation("Please complete CAPTCHA to continue");

        var challenge = policy.Detect(observation);
        var handling = policy.Decide(challenge, new ChallengeHandlingContext(true, "example.com", true, "test"));

        Assert.NotNull(challenge);
        Assert.Equal(SecurityChallengeKind.Captcha, challenge!.Kind);
        Assert.Equal(SecurityChallengeHandling.HumanInterventionRequired, handling);
    }

    [Fact]
    public void VerifiedSyntheticFixtureCanAutoCompleteInTestMode()
    {
        var policy = new SecurityChallengePolicy();
        var observation = Observation("DEVOS-SYNTHETIC-FIXTURE fixture:captcha-001 captcha");

        var challenge = policy.Detect(observation);
        var handling = policy.Decide(challenge, new ChallengeHandlingContext(true, "localhost", true, "test"));

        Assert.NotNull(challenge);
        Assert.True(challenge!.IsSyntheticFixture);
        Assert.Equal("captcha-001", challenge.FixtureId);
        Assert.Equal(SecurityChallengeHandling.SyntheticAutoCompletionAllowed, handling);
    }

    [Fact]
    public void SpoofedSyntheticMarkerOnUnknownDomainFailsClosed()
    {
        var policy = new SecurityChallengePolicy();
        var observation = Observation("DEVOS-SYNTHETIC-FIXTURE fixture:captcha-001 captcha");

        var challenge = policy.Detect(observation);
        var handling = policy.Decide(challenge, new ChallengeHandlingContext(true, "evil.example", true, "test"));

        Assert.Equal(SecurityChallengeHandling.HumanInterventionRequired, handling);
    }

    private static BrowserObservation Observation(string visibleText) => new(
        ProtocolVersion: "1.0",
        Url: "https://example.com",
        Title: "Challenge",
        TabId: "tab-1",
        VisibleText: visibleText,
        Elements: Array.Empty<BrowserElement>(),
        Forms: Array.Empty<BrowserForm>(),
        Tables: Array.Empty<BrowserTable>(),
        Frames: Array.Empty<BrowserFrame>(),
        NetworkState: "idle");
}
