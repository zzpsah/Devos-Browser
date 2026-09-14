using Devos.Protocol;

namespace Devos.Browser.ChromeCdp;

public sealed record ChromeCdpBridgeHandshakeRequest(
    string ExtensionId,
    string ExtensionVersion,
    string ProtocolVersion,
    ChromeCdpTransportMode TransportMode,
    string RuntimeEndpoint,
    IReadOnlySet<string> RequestedCapabilities);

public sealed record ChromeCdpBridgeHandshakeResult(
    ChromeCdpBridgeState State,
    string Message,
    string RuntimeProtocolVersion,
    IReadOnlySet<string> GrantedCapabilities,
    string? Reason = null)
{
    public bool IsConnected => State == ChromeCdpBridgeState.Connected;
}

public static class ChromeCdpBridgeHandshake
{
    public static ChromeCdpBridgeHandshakeResult Negotiate(
        ChromeCdpBridgeHandshakeRequest request,
        ChromeCdpBridgeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var resolved = options ?? new ChromeCdpBridgeOptions();

        if (string.IsNullOrWhiteSpace(request.ExtensionId))
        {
            return Reject(resolved, "Extension id is required.");
        }

        if (request.TransportMode != resolved.TransportMode)
        {
            return Reject(resolved, $"Transport mismatch. Runtime expects {resolved.TransportMode}; bridge requested {request.TransportMode}.");
        }

        if (!EndpointIsAllowed(request.TransportMode, request.RuntimeEndpoint, resolved.AllowRemoteEndpoints))
        {
            return Reject(resolved, "Localhost transport must use a loopback runtime endpoint unless remote endpoints are explicitly allowed.");
        }

        ProtocolCompatibilityResult compatibility;
        try
        {
            compatibility = ProtocolNegotiator.Negotiate(resolved.RequiredProtocolVersion, request.ProtocolVersion);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return new ChromeCdpBridgeHandshakeResult(
                ChromeCdpBridgeState.ProtocolMismatch,
                "BRIDGE_UPDATE_REQUIRED",
                resolved.RequiredProtocolVersion,
                EmptyCapabilities(),
                ex.Message);
        }

        if (!compatibility.IsCompatible)
        {
            return new ChromeCdpBridgeHandshakeResult(
                ChromeCdpBridgeState.ProtocolMismatch,
                compatibility.State,
                resolved.RequiredProtocolVersion,
                EmptyCapabilities(),
                compatibility.Reason);
        }

        var granted = request.RequestedCapabilities
            .Where(capability => ChromeCdpBridgeCapabilities.Default.Contains(capability))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new ChromeCdpBridgeHandshakeResult(
            ChromeCdpBridgeState.Connected,
            "CONNECTED",
            resolved.RequiredProtocolVersion,
            granted,
            "Chrome/CDP bridge handshake accepted. Real CDP execution remains behind adapter implementation gates.");
    }

    private static ChromeCdpBridgeHandshakeResult Reject(ChromeCdpBridgeOptions options, string reason) =>
        new(
            ChromeCdpBridgeState.Rejected,
            "REJECTED",
            options.RequiredProtocolVersion,
            EmptyCapabilities(),
            reason);

    private static bool EndpointIsAllowed(ChromeCdpTransportMode mode, string endpoint, bool allowRemoteEndpoints)
    {
        if (mode == ChromeCdpTransportMode.NativeMessaging)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(endpoint) || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme is not "http" and not "https")
        {
            return false;
        }

        return allowRemoteEndpoints || uri.IsLoopback;
    }

    private static IReadOnlySet<string> EmptyCapabilities() => new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
