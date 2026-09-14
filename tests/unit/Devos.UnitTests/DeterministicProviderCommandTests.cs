using Devos.AI.Abstractions;
using Devos.AI.Deterministic;
using Devos.Protocol;
using Xunit;

namespace Devos.UnitTests;

public sealed class DeterministicProviderCommandTests
{
    private static readonly BrowserObservation EmptyObservation = new(
        ProtocolVersion: "1.0",
        Url: "https://synthetic-portal.local/dashboard",
        Title: "Dashboard",
        TabId: "tab-1",
        VisibleText: string.Empty,
        Elements: Array.Empty<BrowserElement>(),
        Forms: Array.Empty<BrowserForm>(),
        Tables: Array.Empty<BrowserTable>(),
        Frames: Array.Empty<BrowserFrame>(),
        NetworkState: "idle");

    [Theory]
    [InlineData("click d12", BrowserActionKind.Click, "d12", null, "browser.click")]
    [InlineData("read d1", BrowserActionKind.ReadText, "d1", null, "browser.dom.read")]
    [InlineData("wait for #ready", BrowserActionKind.Wait, "#ready", null, "browser.wait.selector")]
    [InlineData("extract table students", BrowserActionKind.ExtractTable, "students", null, "browser.extract.table")]
    [InlineData("download export", BrowserActionKind.Download, "export", null, "browser.download")]
    [InlineData("submit final-form", BrowserActionKind.Submit, "final-form", null, "browser.form.submit")]
    [InlineData("type Prashant into name", BrowserActionKind.Type, "name", "Prashant", "browser.type")]
    public async Task ParsesBoundedCommands(string goal, BrowserActionKind kind, string target, string? value, string capability)
    {
        var provider = new DeterministicProvider();
        var decision = await provider.PlanAsync(new PlannerRequest(goal, EmptyObservation));

        Assert.Equal("continue", decision.Status);
        Assert.NotNull(decision.Action);
        Assert.Equal(kind, decision.Action!.Kind);
        Assert.Equal(target, decision.Action.Target);
        Assert.Equal(value, decision.Action.Value);
        Assert.Equal(capability, decision.Action.RequiredCapability);
    }
}
