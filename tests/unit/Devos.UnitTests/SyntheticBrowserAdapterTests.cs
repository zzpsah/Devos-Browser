using Devos.Browser.Synthetic;
using Devos.Governance;
using Devos.Protocol;
using Devos.Verification;
using Xunit;

namespace Devos.UnitTests;

public sealed class SyntheticBrowserAdapterTests
{
    [Fact]
    public void ExposesBrowserContractCapabilities()
    {
        var adapter = new SyntheticBrowserAdapter();

        Assert.True(adapter.IsAvailable);
        Assert.Contains("browser.navigate", adapter.Capabilities);
        Assert.Contains("browser.click", adapter.Capabilities);
        Assert.Contains("browser.form.submit", adapter.Capabilities);
        Assert.Contains("browser.extract.table", adapter.Capabilities);
    }

    [Fact]
    public async Task NavigateToDashboardProducesNormalizedObservation()
    {
        var adapter = new SyntheticBrowserAdapter();

        await adapter.ExecuteAsync(new BrowserAction(BrowserActionKind.Navigate, Value: "https://synthetic-portal.local/dashboard", RequiredCapability: "browser.navigate"));
        var observation = await adapter.GetObservationAsync();

        Assert.Equal("1.0", observation.ProtocolVersion);
        Assert.Equal("https://synthetic-portal.local/dashboard", observation.Url);
        Assert.Equal("Synthetic Portal Dashboard", observation.Title);
        Assert.Contains(observation.Elements, element => element.Ref == "d1" && element.Text == "Student Records");
    }

    [Fact]
    public async Task StudentRecordsExposeTableAndSyntheticRows()
    {
        var adapter = new SyntheticBrowserAdapter();

        await adapter.ExecuteAsync(new BrowserAction(BrowserActionKind.Navigate, Value: "/students", RequiredCapability: "browser.navigate"));
        var observation = await adapter.GetObservationAsync();

        Assert.Contains("Total synthetic records: 100", observation.VisibleText);
        var table = Assert.Single(observation.Tables);
        Assert.Equal("t-students", table.Ref);
        Assert.Equal(100, table.RowCount);
        Assert.Equal(4, table.ColumnCount);
    }

    [Fact]
    public async Task ChallengePagesExposeVerifiedSyntheticFixtureMarkers()
    {
        var adapter = new SyntheticBrowserAdapter();
        var policy = new SecurityChallengePolicy();

        await adapter.ExecuteAsync(new BrowserAction(BrowserActionKind.Navigate, Value: "/test/captcha", RequiredCapability: "browser.navigate"));
        var observation = await adapter.GetObservationAsync();
        var challenge = policy.Detect(observation);

        Assert.NotNull(challenge);
        Assert.Equal(SecurityChallengeKind.Captcha, challenge!.Kind);
        Assert.True(challenge.IsSyntheticFixture);
        Assert.Equal("captcha", challenge.FixtureId);
    }

    [Fact]
    public async Task SubmitSuccessHasPositiveVerificationEvidence()
    {
        var adapter = new SyntheticBrowserAdapter();
        var verifier = new ActionVerifier();

        await adapter.ExecuteAsync(new BrowserAction(BrowserActionKind.Navigate, Value: "/students", RequiredCapability: "browser.navigate"));
        var before = await adapter.GetObservationAsync();
        var submit = new BrowserAction(BrowserActionKind.Submit, Target: "d3", RequiredCapability: "browser.form.submit");
        await adapter.ExecuteAsync(submit);
        var after = await adapter.GetObservationAsync();
        var result = verifier.Verify(submit, before, after, adapter.ProviderId);

        Assert.True(result.Success);
        Assert.Equal(VerificationState.Pass, result.Verification);
        Assert.Contains("reference", after.VisibleText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UncertainCommitHoldsUntilProviderReadback()
    {
        var adapter = new SyntheticBrowserAdapter();
        var verifier = new ActionVerifier();

        await adapter.ExecuteAsync(new BrowserAction(BrowserActionKind.Navigate, Value: "/uncertain-commit", RequiredCapability: "browser.navigate"));
        var before = await adapter.GetObservationAsync();
        var submit = new BrowserAction(BrowserActionKind.Submit, Target: "d1", RequiredCapability: "browser.form.submit");
        await adapter.ExecuteAsync(submit);
        var ambiguous = await adapter.GetObservationAsync();
        var immediate = verifier.Verify(submit, before, ambiguous, adapter.ProviderId);

        Assert.Equal(VerificationState.HoldReadbackRequired, immediate.Verification);

        await adapter.ExecuteAsync(new BrowserAction(BrowserActionKind.Navigate, Value: "/uncertain-commit/readback", RequiredCapability: "browser.navigate"));
        var readback = await adapter.GetObservationAsync();

        Assert.Contains("SYN-UNCERTAIN-0001", readback.VisibleText);
        Assert.Contains("Success", readback.VisibleText);
    }
}
