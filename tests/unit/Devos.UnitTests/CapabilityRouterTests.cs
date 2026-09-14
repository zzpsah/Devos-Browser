using Devos.Capabilities.Abstractions;
using Devos.Capabilities.Router;
using Xunit;

namespace Devos.UnitTests;

public sealed class CapabilityRouterTests
{
    [Fact]
    public void SelectsAvailableProviderWithCapability()
    {
        var provider = new FakeProvider("chrome-cdp", new[] { "browser.click" }, true);
        var router = new CapabilityRouter(new[] { provider });

        var selected = router.Select(new CapabilityRequest("browser.click"));

        Assert.Equal("chrome-cdp", selected.ProviderId);
    }

    [Fact]
    public void UnavailableProviderIsSkipped()
    {
        var provider = new FakeProvider("chrome-cdp", new[] { "browser.click" }, false);
        var router = new CapabilityRouter(new[] { provider });

        Assert.Throws<InvalidOperationException>(() => router.Select(new CapabilityRequest("browser.click")));
    }

    private sealed class FakeProvider : ICapabilityProvider
    {
        public FakeProvider(string providerId, IEnumerable<string> capabilities, bool isAvailable)
        {
            ProviderId = providerId;
            Capabilities = capabilities.ToHashSet();
            IsAvailable = isAvailable;
        }

        public string ProviderId { get; }
        public IReadOnlySet<string> Capabilities { get; }
        public bool IsAvailable { get; }
    }
}
