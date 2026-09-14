using Devos.Browser.ChromeCdp;
using Devos.Protocol;
using Xunit;

namespace Devos.UnitTests;

public sealed class ChromeCdpBridgeTests
{
    [Fact]
    public void HandshakeRejectsRemoteEndpointByDefault()
    {
        var request = NewHandshake(runtimeEndpoint: "https://example.com:8787");

        var result = ChromeCdpBridgeHandshake.Negotiate(request);

        Assert.Equal(ChromeCdpBridgeState.Rejected, result.State);
        Assert.False(result.IsConnected);
        Assert.Contains("loopback", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HandshakeRequiresCompatibleProtocol()
    {
        var request = NewHandshake(protocolVersion: "2.0");

        var result = ChromeCdpBridgeHandshake.Negotiate(request);

        Assert.Equal(ChromeCdpBridgeState.ProtocolMismatch, result.State);
        Assert.Equal("BRIDGE_UPDATE_REQUIRED", result.Message);
    }

    [Fact]
    public void HandshakeGrantsOnlyKnownCapabilities()
    {
        var request = NewHandshake(requestedCapabilities: new[]
        {
            "browser.navigate",
            "browser.click",
            "unsafe.secret.read"
        });

        var result = ChromeCdpBridgeHandshake.Negotiate(request);

        Assert.True(result.IsConnected);
        Assert.Contains("browser.navigate", result.GrantedCapabilities);
        Assert.Contains("browser.click", result.GrantedCapabilities);
        Assert.DoesNotContain(result.GrantedCapabilities, capability => capability == "unsafe.secret.read");
    }

    [Fact]
    public async Task AdapterFailsClosedEvenAfterBridgeHandshakeUntilRealExecutorExists()
    {
        var transport = new InMemoryChromeCdpBridgeTransport();
        var handshake = await transport.HandshakeAsync(NewHandshake());
        var adapter = new ChromeCdpBrowserAdapter(transport);

        var result = await adapter.ExecuteAsync(new BrowserAction(
            BrowserActionKind.Navigate,
            Value: "https://example.com",
            RequiredCapability: "browser.navigate"));

        Assert.True(handshake.IsConnected);
        Assert.True(adapter.IsAvailable);
        Assert.False(result.Success);
        Assert.Equal(VerificationState.Fail, result.Verification);
        Assert.Contains("not implemented", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DisconnectedAdapterReportsSafeObservation()
    {
        var adapter = new ChromeCdpBrowserAdapter(new InMemoryChromeCdpBridgeTransport());

        var observation = await adapter.GetObservationAsync();

        Assert.False(adapter.IsAvailable);
        Assert.Equal("1.0", observation.ProtocolVersion);
        Assert.Equal("Chrome CDP Bridge", observation.Title);
        Assert.Equal("disconnected", observation.NetworkState);
    }

    private static ChromeCdpBridgeHandshakeRequest NewHandshake(
        string protocolVersion = "1.0",
        string runtimeEndpoint = "http://127.0.0.1:8787",
        IEnumerable<string>? requestedCapabilities = null) =>
        new(
            ExtensionId: "devos-test-extension",
            ExtensionVersion: "0.1.0",
            ProtocolVersion: protocolVersion,
            TransportMode: ChromeCdpTransportMode.Localhost,
            RuntimeEndpoint: runtimeEndpoint,
            RequestedCapabilities: (requestedCapabilities ?? ChromeCdpBridgeCapabilities.Default).ToHashSet(StringComparer.OrdinalIgnoreCase));
}
