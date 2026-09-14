using Devos.Capabilities.Abstractions;
using Devos.Capabilities.Router;
using Xunit;

namespace Devos.UnitTests;

public sealed class CapabilityRouterScoringTests
{
    [Fact]
    public void ExistingSessionConstraintPrefersChromeCdpOverPlaywright()
    {
        var router = new CapabilityRouter(new ICapabilityProvider[]
        {
            new FakeProvider("playwright", new[] { "browser.click" }),
            new FakeProvider("chrome-cdp", new[] { "browser.click" })
        });

        var result = router.SelectRoute(new CapabilityRequest(
            "browser.click",
            new Dictionary<string, string> { ["requiresExistingSession"] = "true" }));

        Assert.Equal("chrome-cdp", result.Provider.ProviderId);
        Assert.True(result.Confidence > 0.50);
        Assert.Contains(result.Candidates, candidate => candidate.ProviderId == "chrome-cdp" && candidate.IsEligible);
    }

    [Fact]
    public void IsolationConstraintPrefersPlaywright()
    {
        var router = new CapabilityRouter(new ICapabilityProvider[]
        {
            new FakeProvider("chrome-cdp", new[] { "browser.navigate" }),
            new FakeProvider("playwright", new[] { "browser.navigate" })
        });

        var result = router.SelectRoute(new CapabilityRequest(
            "browser.navigate",
            new Dictionary<string, string> { ["requiresIsolation"] = "true" }));

        Assert.Equal("playwright", result.Provider.ProviderId);
    }

    [Fact]
    public void ProviderConstraintOverridesDefaultScoring()
    {
        var router = new CapabilityRouter(new ICapabilityProvider[]
        {
            new FakeProvider("chrome-cdp", new[] { "browser.extract.links" }),
            new FakeProvider("firecrawl", new[] { "browser.extract.links" })
        });

        var result = router.SelectRoute(new CapabilityRequest(
            "browser.extract.links",
            new Dictionary<string, string> { ["provider"] = "firecrawl" }));

        Assert.Equal("firecrawl", result.Provider.ProviderId);
        Assert.Contains(result.Candidates, candidate => candidate.ProviderId == "chrome-cdp" && !candidate.IsEligible);
    }

    [Fact]
    public void ExperimentalProviderIsBlockedByDefault()
    {
        var router = new CapabilityRouter(new ICapabilityProvider[]
        {
            new FakeProvider("browseract", new[] { "browser.click" })
        });

        var exception = Assert.Throws<InvalidOperationException>(() => router.Select(new CapabilityRequest("browser.click")));

        Assert.Contains("experimental", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExperimentalProviderCanBeAllowedByPolicy()
    {
        var router = new CapabilityRouter(
            new ICapabilityProvider[] { new FakeProvider("browseract", new[] { "browser.click" }) },
            new CapabilityRoutingPolicy(AllowExperimentalProviders: true));

        var result = router.SelectRoute(new CapabilityRequest("browser.click"));

        Assert.Equal("browseract", result.Provider.ProviderId);
    }

    private sealed class FakeProvider : ICapabilityProvider
    {
        public FakeProvider(string providerId, IEnumerable<string> capabilities, bool isAvailable = true)
        {
            ProviderId = providerId;
            Capabilities = capabilities.ToHashSet(StringComparer.OrdinalIgnoreCase);
            IsAvailable = isAvailable;
        }

        public string ProviderId { get; }
        public IReadOnlySet<string> Capabilities { get; }
        public bool IsAvailable { get; }
    }
}
