using Devos.AI.Abstractions;
using Devos.AI.Deterministic;
using Devos.Protocol;
using Xunit;

namespace Devos.UnitTests;

public sealed class DeterministicProviderTests
{
    [Fact]
    public async Task OpenKnownAliasCreatesNavigationAction()
    {
        var provider = new DeterministicProvider();
        var observation = new BrowserObservation("1.0", null, null, null, null, Array.Empty<BrowserElement>(), Array.Empty<BrowserForm>(), Array.Empty<BrowserTable>(), Array.Empty<BrowserFrame>(), null);

        var decision = await provider.PlanAsync(new PlannerRequest("open eshikshakosh", observation));

        Assert.Equal("continue", decision.Status);
        Assert.NotNull(decision.Action);
        Assert.Equal(BrowserActionKind.Navigate, decision.Action!.Kind);
        Assert.Equal("https://eshikshakosh.bihar.gov.in/", decision.Action.Value);
    }

    [Fact]
    public async Task UnknownBareOpenFailsClosed()
    {
        var provider = new DeterministicProvider();
        var observation = new BrowserObservation("1.0", null, null, null, null, Array.Empty<BrowserElement>(), Array.Empty<BrowserForm>(), Array.Empty<BrowserTable>(), Array.Empty<BrowserFrame>(), null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.PlanAsync(new PlannerRequest("open unknown-school-portal", observation)));
    }
}
