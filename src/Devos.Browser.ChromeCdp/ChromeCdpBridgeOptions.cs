namespace Devos.Browser.ChromeCdp;

public enum ChromeCdpTransportMode
{
    Localhost,
    NativeMessaging
}

public enum ChromeCdpBridgeState
{
    Disconnected,
    Connected,
    ProtocolMismatch,
    Rejected,
    NotImplemented
}

public sealed record ChromeCdpBridgeOptions(
    string RuntimeEndpoint = "http://127.0.0.1:8787",
    string RequiredProtocolVersion = "1.0",
    ChromeCdpTransportMode TransportMode = ChromeCdpTransportMode.Localhost,
    bool AllowRemoteEndpoints = false);
