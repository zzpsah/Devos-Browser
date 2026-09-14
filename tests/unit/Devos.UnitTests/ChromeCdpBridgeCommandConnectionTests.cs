using Devos.Browser.ChromeCdp;
using Xunit;

namespace Devos.UnitTests;

public sealed class ChromeCdpBridgeCommandConnectionTests
{
    [Fact]
    public async Task StartsDisconnectedAndRejectsCommands()
    {
        var connection = new ChromeCdpBridgeCommandConnection();

        Assert.False(connection.IsConnected);
        Assert.Null(connection.CurrentConnectionId);
        await Assert.ThrowsAsync<ChromeCdpBridgeDisconnectedException>(() =>
            connection.SendAsync(ChromeCdpBridgeMessageKinds.Ping));
    }

    [Fact]
    public async Task BoundConnectionSendsAndCorrelatesResponse()
    {
        var connection = new ChromeCdpBridgeCommandConnection();
        var binding = connection.Bind((command, _) =>
        {
            var response = new ChromeCdpBridgeEnvelope(
                ProtocolVersion: "1.0",
                MessageId: "pong-1",
                Kind: ChromeCdpBridgeMessageKinds.Pong,
                Timestamp: DateTimeOffset.UtcNow,
                CorrelationId: command.MessageId);

            Assert.True(connection.TryHandleInbound(response));
            return Task.CompletedTask;
        });

        var result = await connection.SendAsync(ChromeCdpBridgeMessageKinds.Ping);

        Assert.True(connection.IsConnected);
        Assert.Equal(binding, connection.CurrentConnectionId);
        Assert.Equal(ChromeCdpBridgeMessageKinds.Pong, result.Kind);
    }

    [Fact]
    public void StaleUnbindCannotDisconnectNewerBinding()
    {
        var connection = new ChromeCdpBridgeCommandConnection();
        var first = connection.Bind((_, _) => Task.CompletedTask);
        Assert.True(connection.Unbind(first, "first closed"));

        var second = connection.Bind((_, _) => Task.CompletedTask);

        Assert.False(connection.Unbind(first, "stale close"));
        Assert.True(connection.IsConnected);
        Assert.Equal(second, connection.CurrentConnectionId);
    }

    [Fact]
    public void RejectsSecondConcurrentBinding()
    {
        var connection = new ChromeCdpBridgeCommandConnection();
        connection.Bind((_, _) => Task.CompletedTask);

        Assert.Throws<InvalidOperationException>(() =>
            connection.Bind((_, _) => Task.CompletedTask));
    }
}
