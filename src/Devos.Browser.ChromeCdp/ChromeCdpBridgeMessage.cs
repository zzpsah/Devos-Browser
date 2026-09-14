using System.Text.Json;
using Devos.Protocol;

namespace Devos.Browser.ChromeCdp;

public static class ChromeCdpBridgeMessageKinds
{
    public const string Hello = "hello";
    public const string HelloAck = "helloAck";
    public const string AttachTab = "attachTab";
    public const string DetachTab = "detachTab";
    public const string Observe = "observe";
    public const string ExecuteAction = "executeAction";
    public const string Event = "event";
    public const string Ping = "ping";
    public const string Pong = "pong";
    public const string Error = "error";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Hello,
        HelloAck,
        AttachTab,
        DetachTab,
        Observe,
        ExecuteAction,
        Event,
        Ping,
        Pong,
        Error
    };
}

public sealed record ChromeCdpBridgeMessage(
    string ProtocolVersion,
    string MessageId,
    string Kind,
    DateTimeOffset Timestamp,
    string? CorrelationId = null,
    string? TabId = null,
    JsonElement? Payload = null);

public sealed record ChromeCdpBridgeMessageValidationResult(bool IsValid, string? Error = null)
{
    public static ChromeCdpBridgeMessageValidationResult Valid { get; } = new(true);

    public static ChromeCdpBridgeMessageValidationResult Invalid(string error) => new(false, error);
}

public static class ChromeCdpBridgeMessageValidator
{
    public static ChromeCdpBridgeMessageValidationResult Validate(ChromeCdpBridgeMessage? message)
    {
        if (message is null)
        {
            return ChromeCdpBridgeMessageValidationResult.Invalid("Bridge message is required.");
        }

        if (string.IsNullOrWhiteSpace(message.MessageId))
        {
            return ChromeCdpBridgeMessageValidationResult.Invalid("Bridge message id is required.");
        }

        if (string.IsNullOrWhiteSpace(message.Kind) || !ChromeCdpBridgeMessageKinds.All.Contains(message.Kind))
        {
            return ChromeCdpBridgeMessageValidationResult.Invalid($"Unsupported bridge message kind '{message.Kind}'.");
        }

        try
        {
            _ = DevosProtocolVersion.Parse(message.ProtocolVersion);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return ChromeCdpBridgeMessageValidationResult.Invalid(ex.Message);
        }

        if (message.Timestamp == default)
        {
            return ChromeCdpBridgeMessageValidationResult.Invalid("Bridge message timestamp is required.");
        }

        if (RequiresTab(message.Kind) && string.IsNullOrWhiteSpace(message.TabId))
        {
            return ChromeCdpBridgeMessageValidationResult.Invalid($"Bridge message kind '{message.Kind}' requires a tab id.");
        }

        return ChromeCdpBridgeMessageValidationResult.Valid;
    }

    private static bool RequiresTab(string kind) => kind is
        ChromeCdpBridgeMessageKinds.AttachTab or
        ChromeCdpBridgeMessageKinds.DetachTab or
        ChromeCdpBridgeMessageKinds.Observe or
        ChromeCdpBridgeMessageKinds.ExecuteAction;
}
