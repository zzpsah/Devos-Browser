namespace Devos.Host.Server;

public sealed record BrowserBridgeStatusSnapshot(
    bool Connected,
    string Provider,
    string State,
    string? ExtensionId,
    string? ExtensionVersion,
    string ProtocolVersion,
    IReadOnlyCollection<string> GrantedCapabilities,
    string? LastEventKind,
    DateTimeOffset? ConnectedAt,
    DateTimeOffset UpdatedAt,
    string? Message);

public sealed class BrowserBridgeRuntimeState
{
    private readonly object _sync = new();
    private BrowserBridgeStatusSnapshot _snapshot = new(
        Connected: false,
        Provider: "ChromeCDP",
        State: "DISCONNECTED",
        ExtensionId: null,
        ExtensionVersion: null,
        ProtocolVersion: "1.0",
        GrantedCapabilities: Array.Empty<string>(),
        LastEventKind: null,
        ConnectedAt: null,
        UpdatedAt: DateTimeOffset.UtcNow,
        Message: "Waiting for DEVOS Chrome extension bridge.");

    public BrowserBridgeStatusSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return _snapshot with
            {
                GrantedCapabilities = _snapshot.GrantedCapabilities.ToArray()
            };
        }
    }

    public void MarkSocketAccepted()
    {
        Mutate(snapshot => snapshot with
        {
            State = "HANDSHAKE_REQUIRED",
            UpdatedAt = DateTimeOffset.UtcNow,
            Message = "Loopback WebSocket accepted; waiting for bridge handshake."
        });
    }

    public void MarkConnected(
        string? extensionId,
        string? extensionVersion,
        string protocolVersion,
        IEnumerable<string> grantedCapabilities)
    {
        var now = DateTimeOffset.UtcNow;
        Mutate(snapshot => snapshot with
        {
            Connected = true,
            State = "CONNECTED",
            ExtensionId = extensionId,
            ExtensionVersion = extensionVersion,
            ProtocolVersion = protocolVersion,
            GrantedCapabilities = grantedCapabilities.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            ConnectedAt = snapshot.ConnectedAt ?? now,
            UpdatedAt = now,
            Message = "DEVOS Chrome extension bridge connected."
        });
    }

    public void RecordEvent(string eventKind)
    {
        Mutate(snapshot => snapshot with
        {
            LastEventKind = eventKind,
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    public void MarkDisconnected(string message)
    {
        Mutate(snapshot => snapshot with
        {
            Connected = false,
            State = "DISCONNECTED",
            ExtensionId = null,
            ExtensionVersion = null,
            GrantedCapabilities = Array.Empty<string>(),
            ConnectedAt = null,
            UpdatedAt = DateTimeOffset.UtcNow,
            Message = message
        });
    }

    private void Mutate(Func<BrowserBridgeStatusSnapshot, BrowserBridgeStatusSnapshot> mutation)
    {
        lock (_sync)
        {
            _snapshot = mutation(_snapshot);
        }
    }
}
