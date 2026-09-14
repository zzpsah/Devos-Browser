using System.Text.Json;
using Devos.Browser.ChromeCdp;
using Xunit;

namespace Devos.UnitTests;

public sealed class ChromeCdpBridgeSessionTests
{
    [Fact]
    public void HelloConnectsLoopbackBridgeAndReturnsHelloAck()
    {
        var session = new ChromeCdpBridgeSession();
        var hello = CreateHello();

        var response = session.Handle(hello);

        Assert.NotNull(response);
        Assert.Equal(ChromeCdpBridgeMessageKinds.HelloAck, response!.Kind);
        Assert.True(session.IsConnected);
        Assert.Equal("devos-test-extension", session.ExtensionId);
        Assert.Contains("browser.navigate", session.GrantedCapabilities);
        Assert.Equal(hello.MessageId, response.CorrelationId);
    }

    [Fact]
    public void NonHandshakeTrafficFailsClosedBeforeConnection()
    {
        var session = new ChromeCdpBridgeSession();
        var ping = new ChromeCdpBridgeMessage(
            ProtocolVersion: "1.0",
            MessageId: "ping-1",
            Kind: ChromeCdpBridgeMessageKinds.Ping,
            Timestamp: DateTimeOffset.UtcNow);

        var response = session.Handle(ping);

        Assert.NotNull(response);
        Assert.Equal(ChromeCdpBridgeMessageKinds.Error, response!.Kind);
        Assert.False(session.IsConnected);
        Assert.Equal("HANDSHAKE_REQUIRED", response.Payload?.GetProperty("code").GetString());
    }

    [Fact]
    public void RemoteRuntimeEndpointIsRejectedByDefault()
    {
        var session = new ChromeCdpBridgeSession();
        var hello = CreateHello(runtimeEndpoint: "https://example.com:8787");

        var response = session.Handle(hello);

        Assert.NotNull(response);
        Assert.Equal(ChromeCdpBridgeMessageKinds.HelloAck, response!.Kind);
        Assert.False(session.IsConnected);
        Assert.Equal("REJECTED", response.Payload?.GetProperty("state").GetString());
    }

    [Fact]
    public void PingAfterHandshakeReturnsPong()
    {
        var session = new ChromeCdpBridgeSession();
        _ = session.Handle(CreateHello());
        var ping = new ChromeCdpBridgeMessage(
            ProtocolVersion: "1.0",
            MessageId: "ping-2",
            Kind: ChromeCdpBridgeMessageKinds.Ping,
            Timestamp: DateTimeOffset.UtcNow);

        var response = session.Handle(ping);

        Assert.NotNull(response);
        Assert.Equal(ChromeCdpBridgeMessageKinds.Pong, response!.Kind);
        Assert.Equal("ping-2", response.CorrelationId);
    }

    private static ChromeCdpBridgeMessage CreateHello(string runtimeEndpoint = "http://127.0.0.1:8787")
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            extensionId = "devos-test-extension",
            extensionVersion = "0.1.0",
            protocolVersion = "1.0",
            transportMode = "Localhost",
            runtimeEndpoint,
            requestedCapabilities = new[] { "browser.navigate", "browser.click" }
        });

        return new ChromeCdpBridgeMessage(
            ProtocolVersion: "1.0",
            MessageId: "hello-1",
            Kind: ChromeCdpBridgeMessageKinds.Hello,
            Timestamp: DateTimeOffset.UtcNow,
            Payload: payload);
    }
}
