using Devos.Protocol;
using Devos.Verification;
using Xunit;

namespace Devos.UnitTests;

public sealed class ActionVerifierTests
{
    [Fact]
    public void NavigatePassesWhenUrlMatches()
    {
        var verifier = new ActionVerifier();
        var before = Observation("https://start.local", "Start");
        var after = Observation("https://example.com/dashboard", "Dashboard");
        var action = new BrowserAction(BrowserActionKind.Navigate, Value: "https://example.com");

        var result = verifier.Verify(action, before, after, "fake-browser");

        Assert.True(result.Success);
        Assert.Equal(VerificationState.Pass, result.Verification);
    }

    [Fact]
    public void SubmitWithoutEvidenceHoldsForReadback()
    {
        var verifier = new ActionVerifier();
        var before = Observation("https://portal.local/form", "Form");
        var after = Observation("https://portal.local/form", "Still processing");
        var action = new BrowserAction(BrowserActionKind.Submit, Target: "d9");

        var result = verifier.Verify(action, before, after, "fake-browser");

        Assert.True(result.Success);
        Assert.Equal(VerificationState.HoldReadbackRequired, result.Verification);
    }

    private static BrowserObservation Observation(string url, string text) => new(
        ProtocolVersion: "1.0",
        Url: url,
        Title: text,
        TabId: "tab-1",
        VisibleText: text,
        Elements: Array.Empty<BrowserElement>(),
        Forms: Array.Empty<BrowserForm>(),
        Tables: Array.Empty<BrowserTable>(),
        Frames: Array.Empty<BrowserFrame>(),
        NetworkState: "idle");
}
