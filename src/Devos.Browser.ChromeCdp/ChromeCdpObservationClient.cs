using System.Text.Json;
using Devos.Protocol;

namespace Devos.Browser.ChromeCdp;

public interface IChromeCdpObservationClient
{
    Task<BrowserObservation> AttachAndObserveAsync(
        string tabId,
        CancellationToken cancellationToken = default);

    Task<BrowserObservation> ObserveAsync(
        string tabId,
        CancellationToken cancellationToken = default);
}

public sealed class ChromeCdpObservationClient : IChromeCdpObservationClient
{
    private const int MaxElements = 500;
    private const int MaxForms = 100;
    private const int MaxTables = 100;
    private const int MaxFrames = 100;

    private readonly IChromeCdpBridgeCommandClient _commands;

    public ChromeCdpObservationClient(IChromeCdpBridgeCommandClient commands)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public async Task<BrowserObservation> AttachAndObserveAsync(
        string tabId,
        CancellationToken cancellationToken = default)
    {
        ValidateTabId(tabId);

        var attach = await _commands.SendAsync(
            ChromeCdpBridgeMessageKinds.AttachTab,
            tabId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        RequireEvent(attach, "tabAttached");

        return await ObserveAsync(tabId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<BrowserObservation> ObserveAsync(
        string tabId,
        CancellationToken cancellationToken = default)
    {
        ValidateTabId(tabId);

        var response = await _commands.SendAsync(
            ChromeCdpBridgeMessageKinds.Observe,
            tabId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        ThrowIfBridgeError(response);

        if (!string.Equals(response.Kind, ChromeCdpBridgeMessageKinds.Event, StringComparison.Ordinal))
        {
            throw new ChromeCdpObservationException(
                $"Expected an observation event but received bridge message kind '{response.Kind}'.");
        }

        var payload = response.Payload ?? throw new ChromeCdpObservationException(
            "Observation event payload is missing.");

        if (!TryGetString(payload, "event", out var eventName) ||
            !string.Equals(eventName, "observation", StringComparison.Ordinal))
        {
            throw new ChromeCdpObservationException(
                $"Expected observation event payload but received '{eventName ?? "unknown"}'.");
        }

        if (!payload.TryGetProperty("observation", out var observation) ||
            observation.ValueKind != JsonValueKind.Object)
        {
            throw new ChromeCdpObservationException(
                "Observation event did not contain a normalized observation object.");
        }

        return ParseObservation(observation, tabId);
    }

    private static void RequireEvent(ChromeCdpBridgeEnvelope response, string expectedEvent)
    {
        ThrowIfBridgeError(response);

        if (!string.Equals(response.Kind, ChromeCdpBridgeMessageKinds.Event, StringComparison.Ordinal))
        {
            throw new ChromeCdpObservationException(
                $"Expected bridge event '{expectedEvent}' but received '{response.Kind}'.");
        }

        var payload = response.Payload ?? throw new ChromeCdpObservationException(
            $"Bridge event '{expectedEvent}' payload is missing.");

        if (!TryGetString(payload, "event", out var eventName) ||
            !string.Equals(eventName, expectedEvent, StringComparison.Ordinal))
        {
            throw new ChromeCdpObservationException(
                $"Expected bridge event '{expectedEvent}' but received '{eventName ?? "unknown"}'.");
        }
    }

    private static void ThrowIfBridgeError(ChromeCdpBridgeEnvelope response)
    {
        if (!string.Equals(response.Kind, ChromeCdpBridgeMessageKinds.Error, StringComparison.Ordinal))
        {
            return;
        }

        var code = "BRIDGE_ERROR";
        var message = "Chrome bridge command failed.";

        if (response.Payload is { } payload)
        {
            if (TryGetString(payload, "code", out var parsedCode) && !string.IsNullOrWhiteSpace(parsedCode))
            {
                code = parsedCode;
            }

            if (TryGetString(payload, "message", out var parsedMessage) && !string.IsNullOrWhiteSpace(parsedMessage))
            {
                message = parsedMessage;
            }
        }

        throw new ChromeCdpBridgeCommandException(code, message);
    }

    private static BrowserObservation ParseObservation(JsonElement observation, string tabId)
    {
        return new BrowserObservation(
            ProtocolVersion: "1.0",
            Url: GetOptionalString(observation, "url"),
            Title: GetOptionalString(observation, "title"),
            TabId: tabId,
            VisibleText: GetOptionalString(observation, "visibleText"),
            Elements: ParseElements(observation),
            Forms: ParseForms(observation),
            Tables: ParseTables(observation),
            Frames: ParseFrames(observation),
            NetworkState: GetOptionalString(observation, "networkState"),
            SnapshotToken: GetOptionalString(observation, "snapshotToken"));
    }

    private static IReadOnlyList<BrowserElement> ParseElements(JsonElement observation)
    {
        if (!TryGetArray(observation, "elements", out var array))
        {
            return Array.Empty<BrowserElement>();
        }

        var result = new List<BrowserElement>();
        var refs = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in array.EnumerateArray().Take(MaxElements))
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !TryGetString(item, "ref", out var elementRef) ||
                string.IsNullOrWhiteSpace(elementRef) ||
                !refs.Add(elementRef))
            {
                continue;
            }

            var visible = item.TryGetProperty("visible", out var visibleElement) &&
                          visibleElement.ValueKind == JsonValueKind.True;

            result.Add(new BrowserElement(
                Ref: elementRef,
                Role: GetOptionalString(item, "role"),
                Text: GetOptionalString(item, "text"),
                Type: GetOptionalString(item, "type"),
                Href: GetOptionalString(item, "href"),
                Visible: visible));
        }

        return result;
    }

    private static IReadOnlyList<BrowserForm> ParseForms(JsonElement observation)
    {
        if (!TryGetArray(observation, "forms", out var array))
        {
            return Array.Empty<BrowserForm>();
        }

        var result = new List<BrowserForm>();
        foreach (var item in array.EnumerateArray().Take(MaxForms))
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !TryGetString(item, "ref", out var formRef) ||
                string.IsNullOrWhiteSpace(formRef))
            {
                continue;
            }

            var elementRefs = new List<string>();
            if (TryGetArray(item, "elementRefs", out var refs))
            {
                foreach (var element in refs.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String &&
                        element.GetString() is { Length: > 0 } value)
                    {
                        elementRefs.Add(value);
                    }
                }
            }

            result.Add(new BrowserForm(formRef, elementRefs));
        }

        return result;
    }

    private static IReadOnlyList<BrowserTable> ParseTables(JsonElement observation)
    {
        if (!TryGetArray(observation, "tables", out var array))
        {
            return Array.Empty<BrowserTable>();
        }

        var result = new List<BrowserTable>();
        foreach (var item in array.EnumerateArray().Take(MaxTables))
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !TryGetString(item, "ref", out var tableRef) ||
                string.IsNullOrWhiteSpace(tableRef))
            {
                continue;
            }

            result.Add(new BrowserTable(
                Ref: tableRef,
                RowCount: GetNonNegativeInt32(item, "rowCount"),
                ColumnCount: GetNonNegativeInt32(item, "columnCount")));
        }

        return result;
    }

    private static IReadOnlyList<BrowserFrame> ParseFrames(JsonElement observation)
    {
        if (!TryGetArray(observation, "frames", out var array))
        {
            return Array.Empty<BrowserFrame>();
        }

        var result = new List<BrowserFrame>();
        foreach (var item in array.EnumerateArray().Take(MaxFrames))
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !TryGetString(item, "ref", out var frameRef) ||
                string.IsNullOrWhiteSpace(frameRef))
            {
                continue;
            }

            result.Add(new BrowserFrame(
                Ref: frameRef,
                Url: GetOptionalString(item, "url"),
                Title: GetOptionalString(item, "title")));
        }

        return result;
    }

    private static int GetNonNegativeInt32(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var parsed))
        {
            return Math.Max(0, parsed);
        }

        return 0;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName) =>
        TryGetString(element, propertyName, out var value) ? value : null;

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static bool TryGetArray(JsonElement element, string propertyName, out JsonElement array)
    {
        if (element.TryGetProperty(propertyName, out array) && array.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        array = default;
        return false;
    }

    private static void ValidateTabId(string tabId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);
        if (!int.TryParse(tabId, out var parsed) || parsed <= 0)
        {
            throw new ArgumentException("Tab id must be a positive integer.", nameof(tabId));
        }
    }
}

public sealed class ChromeCdpBridgeCommandException : Exception
{
    public ChromeCdpBridgeCommandException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class ChromeCdpObservationException : Exception
{
    public ChromeCdpObservationException(string message) : base(message)
    {
    }
}
