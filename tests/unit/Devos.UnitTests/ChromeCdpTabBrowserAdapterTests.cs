using System.Text.Json;
using Devos.Browser.ChromeCdp;
using Devos.Protocol;
using Xunit;

namespace Devos.UnitTests;

public sealed class ChromeCdpTabBrowserAdapterTests
{
    [Fact]
    public void ExposesOnlyImplementedCapabilities()
    {
        var adapter = NewAdapter(new FakeCommandClient());

        Assert.Contains("browser.observe", adapter.Capabilities);
        Assert.Contains("browser.navigate", adapter.Capabilities);
        Assert.Contains("browser.wait.selector", adapter.Capabilities);
        Assert.DoesNotContain("browser.click", adapter.Capabilities);
        Assert.DoesNotContain("browser.type", adapter.Capabilities);
        Assert.DoesNotContain("browser.form.submit", adapter.Capabilities);
    }

    [Fact]
    public async Task NavigateSendsBoundedExecuteActionPayload()
    {
        var commands = new FakeCommandClient();
        var adapter = NewAdapter(commands);

        var result = await adapter.ExecuteAsync(new BrowserAction(
            BrowserActionKind.Navigate,
            Value: "https://school.example/students",
            RequiredCapability: "browser.navigate"));

        Assert.True(result.Success);
        Assert.Equal(VerificationState.NotVerified, result.Verification);
        Assert.Equal("https://school.example/dashboard", result.UrlBefore);
        Assert.Equal("https://school.example/students", result.UrlAfter);
        Assert.Single(commands.Calls);
        Assert.Equal(ChromeCdpBridgeMessageKinds.ExecuteAction, commands.Calls[0].Kind);
        Assert.Equal("42", commands.Calls[0].TabId);

        var action = commands.Calls[0].Payload!.Value.GetProperty("action");
        Assert.Equal("navigate", action.GetProperty("kind").GetString());
        Assert.Equal("https://school.example/students", action.GetProperty("value").GetString());
    }

    [Fact]
    public async Task WaitMapsSuccessfulActionResultWithoutMutationCapability()
    {
        var commands = new FakeCommandClient();
        var adapter = NewAdapter(commands);

        var result = await adapter.ExecuteAsync(new BrowserAction(
            BrowserActionKind.Wait,
            Target: "#ready",
            RequiredCapability: "browser.wait.selector"));

        Assert.True(result.Success);
        Assert.Equal("https://school.example/dashboard", result.UrlBefore);
        Assert.Equal("https://school.example/dashboard", result.UrlAfter);
        Assert.Equal(VerificationState.NotVerified, result.Verification);
        Assert.Single(commands.Calls);

        var action = commands.Calls[0].Payload!.Value.GetProperty("action");
        Assert.Equal("wait", action.GetProperty("kind").GetString());
        Assert.Equal("#ready", action.GetProperty("target").GetString());
    }

    [Theory]
    [InlineData(BrowserActionKind.Click)]
    [InlineData(BrowserActionKind.Type)]
    [InlineData(BrowserActionKind.Select)]
    [InlineData(BrowserActionKind.Submit)]
    public async Task MutationActionsRemainFailClosed(BrowserActionKind kind)
    {
        var commands = new FakeCommandClient();
        var observations = new FakeObservationClient();
        var adapter = NewAdapter(commands, observations);

        var result = await adapter.ExecuteAsync(new BrowserAction(kind, Target: "d1", Value: "value"));

        Assert.False(result.Success);
        Assert.Equal(VerificationState.Fail, result.Verification);
        Assert.Contains("not enabled", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(commands.Calls);
        Assert.Equal(0, observations.CallCount);
    }

    [Fact]
    public async Task DisconnectedBridgeRejectsBeforeObservationOrCommand()
    {
        var commands = new FakeCommandClient { IsConnected = false };
        var observations = new FakeObservationClient();
        var adapter = NewAdapter(commands, observations);

        var result = await adapter.ExecuteAsync(new BrowserAction(
            BrowserActionKind.Navigate,
            Value: "https://school.example/students"));

        Assert.False(result.Success);
        Assert.Contains("not connected", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(commands.Calls);
        Assert.Equal(0, observations.CallCount);
    }

    [Fact]
    public async Task BridgeErrorMapsToFailedActionResult()
    {
        var commands = new FakeCommandClient
        {
            ResponseFactory = call => new ChromeCdpBridgeEnvelope(
                ProtocolVersion: "1.0",
                MessageId: "error-1",
                Kind: ChromeCdpBridgeMessageKinds.Error,
                Timestamp: DateTimeOffset.UtcNow,
                CorrelationId: "cmd-1",
                TabId: call.TabId,
                Payload: JsonSerializer.SerializeToElement(new
                {
                    code = "NAVIGATE_FAILED",
                    message = "Navigation was rejected."
                }))
        };
        var adapter = NewAdapter(commands);

        var result = await adapter.ExecuteAsync(new BrowserAction(
            BrowserActionKind.Navigate,
            Value: "https://school.example/students"));

        Assert.False(result.Success);
        Assert.Equal(VerificationState.Fail, result.Verification);
        Assert.Contains("NAVIGATE_FAILED", result.Error);
        Assert.Contains("rejected", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnsafeNavigateSchemeFailsBeforeExecuteCommand()
    {
        var commands = new FakeCommandClient();
        var adapter = NewAdapter(commands);

        var result = await adapter.ExecuteAsync(new BrowserAction(
            BrowserActionKind.Navigate,
            Value: "javascript:alert(1)"));

        Assert.False(result.Success);
        Assert.Contains("HTTP or HTTPS", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(commands.Calls);
    }

    private static ChromeCdpTabBrowserAdapter NewAdapter(
        FakeCommandClient commands,
        FakeObservationClient? observations = null) =>
        new(commands, "42", observations ?? new FakeObservationClient());

    private sealed class FakeObservationClient : IChromeCdpObservationClient
    {
        public int CallCount { get; private set; }

        public Task<BrowserObservation> AttachAndObserveAsync(
            string tabId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Observation(tabId));
        }

        public Task<BrowserObservation> ObserveAsync(
            string tabId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Observation(tabId));
        }

        private static BrowserObservation Observation(string tabId) =>
            new(
                ProtocolVersion: "1.0",
                Url: "https://school.example/dashboard",
                Title: "Dashboard",
                TabId: tabId,
                VisibleText: "Ready",
                Elements: Array.Empty<BrowserElement>(),
                Forms: Array.Empty<BrowserForm>(),
                Tables: Array.Empty<BrowserTable>(),
                Frames: Array.Empty<BrowserFrame>(),
                NetworkState: "complete");
    }

    private sealed class FakeCommandClient : IChromeCdpBridgeCommandClient
    {
        public bool IsConnected { get; set; } = true;
        public List<Call> Calls { get; } = new();
        public Func<Call, ChromeCdpBridgeEnvelope>? ResponseFactory { get; init; }

        public Task<ChromeCdpBridgeEnvelope> SendAsync(
            string kind,
            string? tabId = null,
            JsonElement? payload = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var call = new Call(kind, tabId, payload);
            Calls.Add(call);

            if (ResponseFactory is not null)
            {
                return Task.FromResult(ResponseFactory(call));
            }

            var action = payload?.GetProperty("action");
            var actionKind = action?.GetProperty("kind").GetString();
            var url = actionKind == "navigate"
                ? action?.GetProperty("value").GetString()
                : "https://school.example/dashboard";

            return Task.FromResult(new ChromeCdpBridgeEnvelope(
                ProtocolVersion: "1.0",
                MessageId: "event-1",
                Kind: ChromeCdpBridgeMessageKinds.Event,
                Timestamp: DateTimeOffset.UtcNow,
                CorrelationId: "cmd-1",
                TabId: tabId,
                Payload: JsonSerializer.SerializeToElement(new
                {
                    @event = "actionResult",
                    action = actionKind,
                    success = true,
                    url
                })));
        }
    }

    private sealed record Call(string Kind, string? TabId, JsonElement? Payload);
}
