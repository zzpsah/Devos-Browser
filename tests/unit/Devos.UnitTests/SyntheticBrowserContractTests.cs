using Devos.Browser.Synthetic;
using Xunit;

namespace Devos.UnitTests;

public sealed class SyntheticBrowserContractTests
{
    [Fact]
    public async Task SyntheticBrowserAdapterSatisfiesCoreContractHarness()
    {
        await BrowserAdapterContractHarness.AssertCoreContractAsync(() => new SyntheticBrowserAdapter());
    }
}
