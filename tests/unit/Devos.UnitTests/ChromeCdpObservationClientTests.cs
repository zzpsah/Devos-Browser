using System.Text.Json;
using Devos.Browser.ChromeCdp;
using Xunit;

namespace Devos.UnitTests;

public sealed class ChromeCdpObservationClientTests
{
    [Fact]
    public async Task AttachAndObserveMapsNormalizedObservation()
    {
        var commands = new FakeCommandClient();
        var client = new ChromeCdpObservationClient(commands);

        var observation = await client.AttachAndObserveAsync("42");

        Assert.Equal(new[]
        {
            ChromeCdpBridgeMessageKinds.AttachTab,
            ChromeCdpBridgeMessageKinds.Observe
        }, commands.Kinds);
        Assert.Equal("42", observation.TabId);
        Assert.Equal("https://school.example/dashboard", observation.Url);
        Assert.Equal("Dashboard", observation.Title);
        Assert.Equal("ready", observation.NetworkState);
        Assert.Equal(2, observation.Elements.Count);
        Assert.Equal("d1", observation.Elements[0].Ref);
        Assert.Equal("button", observation.Elements[0].Role);
        Assert.Equal("Open Students", observation.Elements[0].Text);
        Assert.True(observation.Elements[0].Visible);
        Assert.Single(observation.Forms);
        Assert.Equal(new[] { "d2" }, observation.Forms[0].ElementRefs);
        Assert.Single(observation.Tables);
        Assert.Equal(12, observation.Tables[0].RowCount);
        Assert.Single(observation.Frames);
        Assert.Equal("fr1", observation.Frames[0].Ref);
    }

    [Fact]
    public async Task BridgeErrorFailsClosedWithCode()
    {
        var commands = new ErrorCommandClient();
        var client = new ChromeCdpObservationClient(commands);

        var error = await Assert.ThrowsAsync<ChromeCdpBridgeCommandException>(
            () => client.AttachAndObserveAsync("7"));

        Assert.Equal("ATTACH_FAILED", error.Code);
        Assert.Contains("denied", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MalformedObservationFailsClosed()
    {
        var commands = new MalformedObservationCommandClient();
        var client = new ChromeCdpObservationClient(commands);

        await Assert.ThrowsAsync<ChromeCdpObservationException>(
            () => client.AttachAndObserveAsync("9"));
    }

    [Fact]
    public async Task InvalidTabIdIsRejectedBeforeBridgeCall()
    {
        var commands = new FakeCommandClient();
        var client = new ChromeCdpObservationClient(commands);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.AttachAndObserveAsync("not-a-tab"));

        Assert.Empty(commands.Kinds);
    }

    private sealed class FakeCommandClient : IChromeCdpBridgeCommandClient
    {
        public List<string> Kinds { get; } = new();
        public bool IsConnected => true;

        public Task<ChromeCdpBridgeEnvelope> SendAsync(
            string kind,
            string? tabId = null,
            JsonElement? payload = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            Kinds.Add(kind);

            if (kind == ChromeCdpBridgeMessageKinds.AttachTab)
            {
                return Task.FromResult(Event(tabId, new
                {
                    @event = "tabAttached",
                    alreadyAttached = false
                }));
            }

            if (kind == ChromeCdpBridgeMessageKinds.Observe)
            {
                return Task.FromResult(Event(tabId, new
                {
                    @event = "observation",
                    observation = new
                    {
                        url = "https://school.example/dashboard",
                        title = "Dashboard",
                        visibleText = "Student portal dashboard",
                        networkState = "ready",
                        elements = new object[]
                        {
                            new { @ref = "d1", role = "button", text = "Open Students", type = "button", href = (string?)null, visible = true },
                            new { @ref = "d2", role = "textbox", text = "Search", type = "text", href = (string?)null, visible = true }
                        },
                        forms = new object[]
                        {
                            new { @ref = "f1", elementRefs = new[] { "d2" } }
                        },
                        tables = new object[]
                        {
                            new { @ref = "t1", rowCount = 12, columnCount = 4 }
                        },
                        frames = new object[]
                        {
                            new { @ref = "fr1", url = "https://school.example/frame", title = "Report" }
                        }
                    }
                }));
            }

            throw new InvalidOperationException($"Unexpected command '{kind}'.");
        }
    }

    private sealed class ErrorCommandClient : IChromeCdpBridgeCommandClient
    {
        public bool IsConnected => true;

        public Task<ChromeCdpBridgeEnvelope> SendAsync(
            string kind,
            string? tabId = null,
            JsonElement? payload = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChromeCdpBridgeEnvelope(
                ProtocolVersion: "1.0",
                MessageId: "error-1",
                Kind: ChromeCdpBridgeMessageKinds.Error,
                Timestamp: DateTimeOffset.UtcNow,
                CorrelationId: "cmd-1",
                TabId: tabId,
                Payload: JsonSerializer.SerializeToElement(new
                {
                    code = "ATTACH_FAILED",
                    message = "Debugger attach denied."
                })));
        }
    }

    private sealed class MalformedObservationCommandClient : IChromeCdpBridgeCommandClient
    {
        public bool IsConnected => true;

        public Task<ChromeCdpBridgeEnvelope> SendAsync(
            string kind,
            string? tabId = null,
            JsonElement? payload = null,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (kind == ChromeCdpBridgeMessageKinds.AttachTab)
            {
                return Task.FromResult(Event(tabId, new { @event = "tabAttached" }));
            }

            return Task.FromResult(Event(tabId, new
            {
                @event = "observation",
                observation = (object?)null
            }));
        }
    }

    private static ChromeCdpBridgeEnvelope Event(string? tabId, object payload) =>
        new(
            ProtocolVersion: "1.0",
            MessageId: $"event-{Guid.NewGuid():N}",
            Kind: ChromeCdpBridgeMessageKinds.Event,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "cmd-test",
            TabId: tabId,
            Payload: JsonSerializer.SerializeToElement(payload));
}
