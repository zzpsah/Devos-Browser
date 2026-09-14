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
        Assert.Contains("browser.click", adapter.Capabilities);
        Assert.Contains("browser.type", adapter.Capabilities);
        Assert.Contains("browser.select", adapter.Capabilities);
        Assert.DoesNotContain("browser.form.submit", adapter.Capabilities);
    }

    [Fact]
    public async Task NavigateSendsBoundedExecuteActionPayload()
    {
        var commands = new FakeCommandClient();
        var observations = new FakeObservationClient();
        var adapter = NewAdapter(commands, observations);

        var result = await adapter.ExecuteAsync(new BrowserAction(
            BrowserActionKind.Navigate,
            Value: "https://school.example/students",
            RequiredCapability: "browser.navigate"));

        Assert.True(result.Success);
        Assert.Equal(VerificationState.NotVerified, result.Verification);
        Assert.Equal("https://school.example/dashboard", result.UrlBefore);
        Assert.Equal("https://school.example/students", result.UrlAfter);
        Assert.Equal(1, observations.CallCount);
        Assert.Single(commands.Calls);
        Assert.Equal(ChromeCdpBridgeMessageKinds.ExecuteAction, commands.Calls[0].Kind);
        Assert.Equal("42", commands.Calls[0].TabId);

        var action = commands.Calls[0].Payload!.Value.GetProperty("action");
        Assert.Equal("navigate", action.GetProperty("kind").GetString());
        Assert.Equal("https://school.example/students", action.GetProperty("value").GetString());
    }

    [Fact]
    public async Task WaitMapsSuccessfulActionResult()
    {
        var commands = new FakeCommandClient();
        var observations = new FakeObservationClient();
        var adapter = NewAdapter(commands, observations);

        var result = await adapter.ExecuteAsync(new BrowserAction(
            BrowserActionKind.Wait,
            Target: "#ready",
            RequiredCapability: "browser.wait.selector"));

        Assert.True(result.Success);
        Assert.Equal("https://school.example/dashboard", result.UrlBefore);
        Assert.Equal("https://school.example/dashboard", result.UrlAfter);
        Assert.Equal(VerificationState.NotVerified, result.Verification);
        Assert.Equal(1, observations.CallCount);
        Assert.Single(commands.Calls);

        var action = commands.Calls[0].Payload!.Value.GetProperty("action");
        Assert.Equal("wait", action.GetProperty("kind").GetString());
        Assert.Equal("#ready", action.GetProperty("target").GetString());
    }

    [Fact]
    public async Task ClickWithFreshSnapshotSendsSnapshotToken()
    {
        var commands = new FakeCommandClient();
        var adapter = NewAdapter(commands);
        var observation = await adapter.GetObservationAsync();

        var result = await adapter.ExecuteAsync(new BrowserAction(
            BrowserActionKind.Click,
            Target: "d1",
            RequiredCapability: "browser.click",
            SnapshotToken: observation.SnapshotToken,
            SemanticHint: "button Open students button"));

        Assert.True(result.Success);
        Assert.Single(commands.Calls);
        var action = commands.Calls[0].Payload!.Value.GetProperty("action");
        Assert.Equal("click", action.GetProperty("kind").GetString());
        Assert.Equal("d1", action.GetProperty("target").GetString());
        Assert.Equal("snapshot-1", action.GetProperty("snapshotToken").GetString());
    }

    [Fact]
    public async Task TypeWithFreshSnapshotSendsValue()
    {
        var commands = new FakeCommandClient();
        var adapter = NewAdapter(commands);
        var observation = await adapter.GetObservationAsync();

        var result = await adapter.ExecuteAsync(new BrowserAction(
            BrowserActionKind.Type,
            Target: "d2",
            Value: "Prashant",
            RequiredCapability: "browser.type",
            SnapshotToken: observation.SnapshotToken,
            SemanticHint: "textbox Student name text"));

        Assert.True(result.Success);
        var action = Assert.Single(commands.Calls).Payload!.Value.GetProperty("action");
        Assert.Equal("type", action.GetProperty("kind").GetString());
        Assert.Equal("Prashant", action.GetProperty("value").GetString());
    }

    [Fact]
    public async Task SelectWithFreshSnapshotSendsValue()
    {
        var commands = new FakeCommandClient();
        var adapter = NewAdapter(commands);
        var observation = await adapter.GetObservationAsync();

        var result = await adapter.ExecuteAsync(new BrowserAction(
            BrowserActionKind.Select,
            Target: "d3",
            Value: "11",
            RequiredCapability: "browser.select",
            SnapshotToken: observation.SnapshotToken,
            SemanticHint: "combobox Class select"));

        Assert.True(result.Success);
        var action = Assert.Single(commands.Calls).Payload!.Value.GetProperty("action");
        Assert.Equal("select", action.GetProperty("kind").GetString());
        Assert.Equal("11", action.GetProperty("value").GetString());
    }

    [Fact]
    public async Task ElementActionWithoutCachedMatchingSnapshotFailsClosed()
    {
        var commands = new FakeCommandClient();
        var adapter = NewAdapter(commands);

        var result = await adapter.ExecuteAsync(new BrowserAction(
            BrowserActionKind.Click,
            Target: "d1",
            SnapshotToken: "snapshot-1",
            SemanticHint: "button Open students button"));

        Assert.False(result.Success);
        Assert.Contains("stale", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(commands.Calls);
    }

    [Fact]
    public async Task StaleSnapshotTokenFailsClosedAfterFreshObservation()
    {
        var commands = new FakeCommandClient();
        var adapter = NewAdapter(commands);
        _ = await adapter.GetObservationAsync();

        var result = await adapter.ExecuteAsync(new BrowserAction(
            BrowserActionKind.Click,
            Target: "d1",
            SnapshotToken: "older-snapshot",
            SemanticHint: "button Open students button"));

        Assert.False(result.Success);
        Assert.Contains("stale", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(commands.Calls);
    }

    [Fact]
    public async Task SemanticContextMismatchFailsClosed()
    {
        var commands = new FakeCommandClient();
        var adapter = NewAdapter(commands);
        var observation = await adapter.GetObservationAsync();

        var result = await adapter.ExecuteAsync(new BrowserAction(
            BrowserActionKind.Click,
            Target: "d1",
            SnapshotToken: observation.SnapshotToken,
            SemanticHint: "button Harmless button"));

        Assert.False(result.Success);
        Assert.Contains("semantic", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(commands.Calls);
    }

    [Fact]
    public async Task SuccessfulElementMutationInvalidatesCachedSnapshot()
    {
        var commands = new FakeCommandClient();
        var adapter = NewAdapter(commands);
        var observation = await adapter.GetObservationAsync();
        var action = new BrowserAction(
            BrowserActionKind.Click,
            Target: "d1",
            SnapshotToken: observation.SnapshotToken,
            SemanticHint: "button Open students button");

        var first = await adapter.ExecuteAsync(action);
        var second = await adapter.ExecuteAsync(action);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Contains("stale", second.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Single(commands.Calls);
    }

    [Fact]
    public async Task SubmitRemainsFailClosed()
    {
        var commands = new FakeCommandClient();
        var adapter = NewAdapter(commands);
        var observation = await adapter.GetObservationAsync();

        var result = await adapter.ExecuteAsync(new BrowserAction(
            BrowserActionKind.Submit,
            Target: "d1",
            SnapshotToken: observation.SnapshotToken,
            SemanticHint: "button Submit"));

        Assert.False(result.Success);
        Assert.Contains("not enabled", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(commands.Calls);
    }

    [Fact]
    public async Task MissingSemanticContextRejectsElementAction()
    {
        var commands = new FakeCommandClient();
        var adapter = NewAdapter(commands);
        var observation = await adapter.GetObservationAsync();

        var result = await adapter.ExecuteAsync(new BrowserAction(
            BrowserActionKind.Click,
            Target: "d1",
            SnapshotToken: observation.SnapshotToken));

        Assert.False(result.Success);
        Assert.Contains("semantic", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(commands.Calls);
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
        var observations = new FakeObservationClient();
        var adapter = NewAdapter(commands, observations);

        var result = await adapter.ExecuteAsync(new BrowserAction(
            BrowserActionKind.Navigate,
            Value: "javascript:alert(1)"));

        Assert.False(result.Success);
        Assert.Contains("HTTP or HTTPS", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(commands.Calls);
        Assert.Equal(0, observations.CallCount);
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
                Elements: new[]
                {
                    new BrowserElement("d1", "button", "Open students", "button", null, true),
                    new BrowserElement("d2", "textbox", "Student name", "text", null, true),
                    new BrowserElement("d3", "combobox", "Class", "select", null, true)
                },
                Forms: Array.Empty<BrowserForm>(),
                Tables: Array.Empty<BrowserTable>(),
                Frames: Array.Empty<BrowserFrame>(),
                NetworkState: "complete",
                SnapshotToken: "snapshot-1");
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
