using System.Text.Json;
using System.Text.Json.Serialization;
using Devos.Protocol;

namespace Devos.Browser.ChromeCdp;

public sealed class ChromeCdpBridgeSession
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly ChromeCdpBridgeOptions _options;

    public ChromeCdpBridgeSession(ChromeCdpBridgeOptions? options = null)
    {
        _options = options ?? new ChromeCdpBridgeOptions();
    }

    public bool IsConnected { get; private set; }
    public string? ExtensionId { get; private set; }
    public string? ExtensionVersion { get; private set; }
    public IReadOnlySet<string> GrantedCapabilities { get; private set; } = EmptyCapabilities();
    public ChromeCdpBridgeEnvelope? LastEvent { get; private set; }

    public ChromeCdpBridgeEnvelope? Handle(ChromeCdpBridgeEnvelope? message)
    {
        var validation = ChromeCdpBridgeEnvelopeValidator.Validate(message);
        if (!validation.IsValid || message is null)
        {
            return Error(message, "INVALID_MESSAGE", validation.Error ?? "Invalid bridge message.");
        }

        if (message.Kind == ChromeCdpBridgeMessageKinds.Hello)
        {
            return HandleHello(message);
        }

        if (!IsConnected)
        {
            return Error(message, "HANDSHAKE_REQUIRED", "A successful hello/helloAck handshake is required before bridge traffic.");
        }

        if (!ProtocolCompatible(message.ProtocolVersion))
        {
            return Error(message, "BRIDGE_UPDATE_REQUIRED", "Bridge message protocol is incompatible with the runtime.");
        }

        return message.Kind switch
        {
            ChromeCdpBridgeMessageKinds.Ping => Reply(message, ChromeCdpBridgeMessageKinds.Pong),
            ChromeCdpBridgeMessageKinds.Pong => null,
            ChromeCdpBridgeMessageKinds.Event => CaptureEvent(message),
            ChromeCdpBridgeMessageKinds.Error => CaptureEvent(message),
            _ => Error(message, "DIRECTION_NOT_ALLOWED", $"Message kind '{message.Kind}' is runtime-to-extension only in this bridge slice.")
        };
    }

    private ChromeCdpBridgeEnvelope HandleHello(ChromeCdpBridgeEnvelope message)
    {
        ChromeCdpBridgeHandshakePayload? payload;

        try
        {
            payload = message.Payload?.Deserialize<ChromeCdpBridgeHandshakePayload>(JsonOptions);
        }
        catch (JsonException ex)
        {
            return Error(message, "INVALID_HELLO", ex.Message);
        }

        if (payload is null)
        {
            return Error(message, "INVALID_HELLO", "Hello payload is required.");
        }

        var request = new ChromeCdpBridgeHandshakeRequest(
            ExtensionId: payload.ExtensionId,
            ExtensionVersion: payload.ExtensionVersion,
            ProtocolVersion: payload.ProtocolVersion,
            TransportMode: payload.TransportMode,
            RuntimeEndpoint: payload.RuntimeEndpoint,
            RequestedCapabilities: (payload.RequestedCapabilities ?? Array.Empty<string>())
                .ToHashSet(StringComparer.OrdinalIgnoreCase));

        var result = ChromeCdpBridgeHandshake.Negotiate(request, _options);
        IsConnected = result.IsConnected;
        ExtensionId = result.IsConnected ? request.ExtensionId : null;
        ExtensionVersion = result.IsConnected ? request.ExtensionVersion : null;
        GrantedCapabilities = result.IsConnected ? result.GrantedCapabilities : EmptyCapabilities();

        var responsePayload = JsonSerializer.SerializeToElement(new
        {
            state = result.Message,
            message = result.IsConnected ? "DEVOS runtime accepted Chrome bridge handshake." : "DEVOS runtime rejected Chrome bridge handshake.",
            runtimeProtocolVersion = result.RuntimeProtocolVersion,
            grantedCapabilities = result.GrantedCapabilities,
            reason = result.Reason
        }, JsonOptions);

        return new ChromeCdpBridgeEnvelope(
            ProtocolVersion: _options.RequiredProtocolVersion,
            MessageId: NewMessageId(),
            Kind: ChromeCdpBridgeMessageKinds.HelloAck,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: message.MessageId,
            Payload: responsePayload);
    }

    private ChromeCdpBridgeEnvelope? CaptureEvent(ChromeCdpBridgeEnvelope message)
    {
        LastEvent = message;
        return null;
    }

    private bool ProtocolCompatible(string supportedVersion)
    {
        try
        {
            return ProtocolNegotiator.Negotiate(_options.RequiredProtocolVersion, supportedVersion).IsCompatible;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return false;
        }
    }

    private ChromeCdpBridgeEnvelope Reply(ChromeCdpBridgeEnvelope request, string kind, JsonElement? payload = null) =>
        new(
            ProtocolVersion: _options.RequiredProtocolVersion,
            MessageId: NewMessageId(),
            Kind: kind,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: request.MessageId,
            TabId: request.TabId,
            Payload: payload);

    private ChromeCdpBridgeEnvelope Error(ChromeCdpBridgeEnvelope? request, string code, string detail)
    {
        var payload = JsonSerializer.SerializeToElement(new { code, message = detail }, JsonOptions);
        return new ChromeCdpBridgeEnvelope(
            ProtocolVersion: _options.RequiredProtocolVersion,
            MessageId: NewMessageId(),
            Kind: ChromeCdpBridgeMessageKinds.Error,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: request?.MessageId,
            TabId: request?.TabId,
            Payload: payload);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static string NewMessageId() => $"runtime-{Guid.NewGuid():N}";

    private static IReadOnlySet<string> EmptyCapabilities() => new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private sealed record ChromeCdpBridgeHandshakePayload(
        string ExtensionId,
        string ExtensionVersion,
        string ProtocolVersion,
        ChromeCdpTransportMode TransportMode,
        string RuntimeEndpoint,
        string[]? RequestedCapabilities);
}
