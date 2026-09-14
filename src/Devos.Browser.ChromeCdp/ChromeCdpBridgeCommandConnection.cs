using System.Text.Json;

namespace Devos.Browser.ChromeCdp;

public interface IChromeCdpBridgeCommandClient
{
    bool IsConnected { get; }

    Task<ChromeCdpBridgeEnvelope> SendAsync(
        string kind,
        string? tabId = null,
        JsonElement? payload = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}

public sealed class ChromeCdpBridgeCommandConnection : IChromeCdpBridgeCommandClient
{
    private readonly object _sync = new();
    private readonly ChromeCdpBridgeCommandBroker _broker;
    private Func<ChromeCdpBridgeEnvelope, CancellationToken, Task>? _sender;
    private string? _connectionId;

    public ChromeCdpBridgeCommandConnection(ChromeCdpBridgeCommandBroker? broker = null)
    {
        _broker = broker ?? new ChromeCdpBridgeCommandBroker();
    }

    public bool IsConnected
    {
        get
        {
            lock (_sync)
            {
                return _sender is not null;
            }
        }
    }

    public string? CurrentConnectionId
    {
        get
        {
            lock (_sync)
            {
                return _connectionId;
            }
        }
    }

    public string Bind(Func<ChromeCdpBridgeEnvelope, CancellationToken, Task> sender)
    {
        ArgumentNullException.ThrowIfNull(sender);

        lock (_sync)
        {
            if (_sender is not null)
            {
                throw new InvalidOperationException("A Chrome bridge command connection is already bound.");
            }

            _connectionId = $"bridge-{Guid.NewGuid():N}";
            _sender = sender;
            return _connectionId;
        }
    }

    public bool Unbind(string connectionId, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        lock (_sync)
        {
            if (!string.Equals(_connectionId, connectionId, StringComparison.Ordinal))
            {
                return false;
            }

            _sender = null;
            _connectionId = null;
        }

        _broker.CancelAll(reason);
        return true;
    }

    public bool TryHandleInbound(ChromeCdpBridgeEnvelope? message) => _broker.TryHandleInbound(message);

    public Task<ChromeCdpBridgeEnvelope> SendAsync(
        string kind,
        string? tabId = null,
        JsonElement? payload = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        Func<ChromeCdpBridgeEnvelope, CancellationToken, Task> sender;

        lock (_sync)
        {
            sender = _sender ?? throw new ChromeCdpBridgeDisconnectedException(
                "Chrome bridge command connection is not bound to an active extension session.");
        }

        return _broker.SendAsync(kind, tabId, payload, sender, timeout, cancellationToken);
    }
}
