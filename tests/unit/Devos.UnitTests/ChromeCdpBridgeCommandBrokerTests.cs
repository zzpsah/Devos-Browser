using Devos.Browser.ChromeCdp;
using Xunit;

namespace Devos.UnitTests;

public sealed class ChromeCdpBridgeCommandBrokerTests
{
    [Fact]
    public async Task CorrelatedEventCompletesPendingCommand()
    {
        var broker = new ChromeCdpBridgeCommandBroker();

        var result = await broker.SendAsync(
            ChromeCdpBridgeMessageKinds.Observe,
            tabId: "42",
            payload: null,
            sender: (command, _) =>
            {
                var response = new ChromeCdpBridgeEnvelope(
                    ProtocolVersion: "1.0",
                    MessageId: "event-1",
                    Kind: ChromeCdpBridgeMessageKinds.Event,
                    Timestamp: DateTimeOffset.UtcNow,
                    CorrelationId: command.MessageId,
                    TabId: command.TabId);

                Assert.True(broker.TryHandleInbound(response));
                return Task.CompletedTask;
            });

        Assert.Equal(ChromeCdpBridgeMessageKinds.Event, result.Kind);
        Assert.Equal("42", result.TabId);
        Assert.Equal(0, broker.PendingCount);
    }

    [Fact]
    public async Task CorrelatedErrorCompletesCommandWithoutBlindRetry()
    {
        var broker = new ChromeCdpBridgeCommandBroker();

        var result = await broker.SendAsync(
            ChromeCdpBridgeMessageKinds.AttachTab,
            tabId: "7",
            payload: null,
            sender: (command, _) =>
            {
                var response = new ChromeCdpBridgeEnvelope(
                    ProtocolVersion: "1.0",
                    MessageId: "error-1",
                    Kind: ChromeCdpBridgeMessageKinds.Error,
                    Timestamp: DateTimeOffset.UtcNow,
                    CorrelationId: command.MessageId,
                    TabId: command.TabId);

                broker.TryHandleInbound(response);
                return Task.CompletedTask;
            });

        Assert.Equal(ChromeCdpBridgeMessageKinds.Error, result.Kind);
        Assert.Equal(0, broker.PendingCount);
    }

    [Fact]
    public async Task CommandTimesOutAndCleansCorrelation()
    {
        var broker = new ChromeCdpBridgeCommandBroker(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAsync<TimeoutException>(() => broker.SendAsync(
            ChromeCdpBridgeMessageKinds.Ping,
            tabId: null,
            payload: null,
            sender: (_, _) => Task.CompletedTask));

        Assert.Equal(0, broker.PendingCount);
    }

    [Fact]
    public async Task DisconnectFailsPendingCommands()
    {
        var broker = new ChromeCdpBridgeCommandBroker(TimeSpan.FromSeconds(2));
        var commandSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var pending = broker.SendAsync(
            ChromeCdpBridgeMessageKinds.Observe,
            tabId: "99",
            payload: null,
            sender: (_, _) =>
            {
                commandSent.SetResult();
                return Task.CompletedTask;
            });

        await commandSent.Task;
        broker.CancelAll("bridge disconnected");

        var exception = await Assert.ThrowsAsync<ChromeCdpBridgeDisconnectedException>(() => pending);
        Assert.Contains("disconnected", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, broker.PendingCount);
    }

    [Fact]
    public async Task TabScopedCommandRequiresTabId()
    {
        var broker = new ChromeCdpBridgeCommandBroker();

        await Assert.ThrowsAsync<ArgumentException>(() => broker.SendAsync(
            ChromeCdpBridgeMessageKinds.ExecuteAction,
            tabId: null,
            payload: null,
            sender: (_, _) => Task.CompletedTask));
    }

    [Fact]
    public async Task RuntimeCannotQueueInboundOnlyMessageKind()
    {
        var broker = new ChromeCdpBridgeCommandBroker();

        await Assert.ThrowsAsync<ArgumentException>(() => broker.SendAsync(
            ChromeCdpBridgeMessageKinds.Event,
            tabId: null,
            payload: null,
            sender: (_, _) => Task.CompletedTask));
    }
}
