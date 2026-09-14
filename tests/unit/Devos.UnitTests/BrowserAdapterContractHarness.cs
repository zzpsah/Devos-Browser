using Devos.Browser.Abstractions;
using Devos.Protocol;
using Xunit;

namespace Devos.UnitTests;

internal static class BrowserAdapterContractHarness
{
    public static async Task AssertCoreContractAsync(Func<IBrowserAdapter> adapterFactory)
    {
        var adapter = adapterFactory();

        Assert.True(adapter.IsAvailable);
        Assert.Contains("browser.observe", adapter.Capabilities, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("browser.navigate", adapter.Capabilities, StringComparer.OrdinalIgnoreCase);

        var initial = await adapter.GetObservationAsync();
        AssertNormalizedObservation(initial);

        var navigate = await adapter.ExecuteAsync(new BrowserAction(
            BrowserActionKind.Navigate,
            Value: "/dashboard",
            RequiredCapability: "browser.navigate"));
        Assert.True(navigate.Success, navigate.Error);

        var dashboard = await adapter.GetObservationAsync();
        AssertNormalizedObservation(dashboard);
        Assert.EndsWith("/dashboard", dashboard.Url, StringComparison.OrdinalIgnoreCase);

        var currentUrl = await adapter.GetCurrentUrlAsync();
        Assert.Equal(dashboard.Url, currentUrl);

        var missingWait = await adapter.ExecuteAsync(new BrowserAction(
            BrowserActionKind.Wait,
            Target: "contract-missing-target",
            RequiredCapability: "browser.wait.selector"));
        Assert.False(missingWait.Success);
    }

    private static void AssertNormalizedObservation(BrowserObservation observation)
    {
        Assert.Equal("1.0", observation.ProtocolVersion);
        Assert.False(string.IsNullOrWhiteSpace(observation.Url));
        Assert.False(string.IsNullOrWhiteSpace(observation.Title));
        Assert.False(string.IsNullOrWhiteSpace(observation.TabId));
        Assert.NotNull(observation.Elements);
        Assert.NotNull(observation.Forms);
        Assert.NotNull(observation.Tables);
        Assert.NotNull(observation.Frames);

        var refs = observation.Elements.Select(element => element.Ref).ToArray();
        Assert.All(refs, reference => Assert.False(string.IsNullOrWhiteSpace(reference)));
        Assert.Equal(refs.Length, refs.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        Assert.All(observation.Tables, table =>
        {
            Assert.False(string.IsNullOrWhiteSpace(table.Ref));
            Assert.True(table.RowCount >= 0);
            Assert.True(table.ColumnCount >= 0);
        });
    }
}
