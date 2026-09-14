using System.Collections.Concurrent;
using System.Text.Json;

namespace Devos.Browser.ChromeCdp;

public sealed class ChromeCdpBridgeCommandBroker
{
    private static readonly IReadOnlySet<string> OutboundCommandKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        ChromeCdpBridgeMessageKinds.AttachTab,
        ChromeCdpBridgeMessageKinds.DetachTab,
        ChromeCdpBridgeMessageKinds.Observe,
        ChromeCdpBridgeMessageKinds.ExecuteAction,
        ChromeCdpBridgeMessageKinds.Ping
    };

    private readonly ConcurrentDictionary<string, TaskCompletionSource<ChromeCdpBridgeEnvelope>> _pending = new(StringComparer.Ordinal);
    private readonly TimeSpan _defaultTimeout;

    public ChromeCdpBridgeCommandBroker(TimeSpan? defaultTimeout = null)
    {
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(15);
        if (_defaultTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultTimeout), "Command timeout must be positive.");
        }
    }

    public int PendingCount => _pending.Count;

    public async Task<ChromeCdpBridgeEnvelope> SendAsync(
        string kind,
        string? tabId,
        JsonElement? payload,
        Func<ChromeCdpBridgeEnvelope, CancellationToken, Task> sender,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);

        if (!OutboundCommandKinds.Contains(kind))
        {
            throw new ArgumentException($"Bridge message kind '{kind}' is not a runtime command.", nameof(kind));
        }

        if (RequiresTab(kind) && string.IsNullOrWhiteSpace(tabId))
        {
            throw new ArgumentException($"Bridge command kind '{kind}' requires a tab id.", nameof(tabId));
        }

        var command = new ChromeCdpBridgeEnvelope(
            ProtocolVersion: "1.0",
            MessageId: $"cmd-{Guid.NewGuid():N}",
            Kind: kind,
            Timestamp: DateTimeOffset.UtcNow,
            TabId: tabId,
            Payload: payload);

        var completion = new TaskCompletionSource<ChromeCdpBridgeEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(command.MessageId, completion))
        {
            throw new InvalidOperationException("Unable to register bridge command correlation id.");
        }

        try
        {
            await sender(command, cancellationToken).ConfigureAwait(false);

            var effectiveTimeout = timeout ?? _defaultTimeout;
            if (effectiveTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Command timeout must be positive.");
            }

            return await completion.Task.WaitAsync(effectiveTimeout, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(command.MessageId, out _);
        }
    }

    public bool TryHandleInbound(ChromeCdpBridgeEnvelope? message)
    {
        if (message is null || string.IsNullOrWhiteSpace(message.CorrelationId))
        {
            return false;
        }

        if (message.Kind is not (
            ChromeCdpBridgeMessageKinds.Event or
            ChromeCdpBridgeMessageKinds.Error or
            ChromeCdpBridgeMessageKinds.Pong))
        {
            return false;
        }

        return _pending.TryGetValue(message.CorrelationId, out var completion) &&
               completion.TrySetResult(message);
    }

    public void CancelAll(string reason)
    {
        var exception = new ChromeCdpBridgeDisconnectedException(reason);
        foreach (var entry in _pending)
        {
            entry.Value.TrySetException(exception);
        }
    }

    private static bool RequiresTab(string kind) => kind is
        ChromeCdpBridgeMessageKinds.AttachTab or
        ChromeCdpBridgeMessageKinds.DetachTab or
        ChromeCdpBridgeMessageKinds.Observe or
        ChromeCdpBridgeMessageKinds.ExecuteAction;
}

public sealed class ChromeCdpBridgeDisconnectedException : Exception
{
    public ChromeCdpBridgeDisconnectedException(string message) : base(message)
    {
    }
}
