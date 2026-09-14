namespace Devos.Browser.ChromeCdp;

public interface IChromeCdpBridgeTransport
{
    string TransportId { get; }
    bool IsConnected { get; }
    ChromeCdpBridgeHandshakeResult? LastHandshake { get; }
    Task<ChromeCdpBridgeHandshakeResult> HandshakeAsync(ChromeCdpBridgeHandshakeRequest request, CancellationToken cancellationToken = default);
    Task<ChromeCdpBridgeMessage> SendAsync(ChromeCdpBridgeMessage message, CancellationToken cancellationToken = default);
}

public sealed record ChromeCdpBridgeMessage(
    string Id,
    string Kind,
    string? TabId = null,
    string? PayloadJson = null,
    string? Error = null);

public sealed class InMemoryChromeCdpBridgeTransport : IChromeCdpBridgeTransport
{
    private readonly ChromeCdpBridgeOptions _options;

    public InMemoryChromeCdpBridgeTransport(ChromeCdpBridgeOptions? options = null)
    {
        _options = options ?? new ChromeCdpBridgeOptions();
    }

    public string TransportId => _options.TransportMode == ChromeCdpTransportMode.NativeMessaging
        ? "chrome-cdp.native-messaging"
        : "chrome-cdp.localhost";

    public bool IsConnected => LastHandshake?.IsConnected == true;
    public ChromeCdpBridgeHandshakeResult? LastHandshake { get; private set; }

    public Task<ChromeCdpBridgeHandshakeResult> HandshakeAsync(ChromeCdpBridgeHandshakeRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastHandshake = ChromeCdpBridgeHandshake.Negotiate(request, _options);
        return Task.FromResult(LastHandshake);
    }

    public Task<ChromeCdpBridgeMessage> SendAsync(ChromeCdpBridgeMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsConnected)
        {
            return Task.FromResult(message with
            {
                Kind = "bridge.not_connected",
                Error = "Chrome/CDP bridge is not connected."
            });
        }

        return Task.FromResult(message with
        {
            Kind = "bridge.not_implemented",
            Error = "Chrome/CDP transport is connected, but real CDP execution is not implemented in this skeleton."
        });
    }
}
