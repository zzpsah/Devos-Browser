using System.Text.Json;
using Devos.Browser.Abstractions;
using Devos.Protocol;

namespace Devos.Browser.ChromeCdp;

public sealed class ChromeCdpTabBrowserAdapter : IBrowserAdapter
{
    private static readonly IReadOnlySet<string> EnabledCapabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "browser.observe",
        "browser.navigate",
        "browser.wait.selector",
        "browser.click",
        "browser.type",
        "browser.select"
    };

    private readonly IChromeCdpBridgeCommandClient _commands;
    private readonly IChromeCdpObservationClient _observations;
    private readonly string _tabId;
    private BrowserObservation? _lastObservation;
    private int _actionCounter;

    public ChromeCdpTabBrowserAdapter(
        IChromeCdpBridgeCommandClient commands,
        string tabId,
        IChromeCdpObservationClient? observations = null)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _tabId = ValidateTabId(tabId);
        _observations = observations ?? new ChromeCdpObservationClient(commands);
    }

    public string ProviderId => "chrome-cdp";
    public IReadOnlySet<string> Capabilities => EnabledCapabilities;
    public bool IsAvailable => _commands.IsConnected;

    public async Task<BrowserObservation> GetObservationAsync(CancellationToken cancellationToken = default)
    {
        var observation = await _observations.AttachAndObserveAsync(_tabId, cancellationToken).ConfigureAwait(false);
        _lastObservation = observation;
        return observation;
    }

    public async Task<string?> GetCurrentUrlAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return null;
        }

        if (_lastObservation is not null)
        {
            return _lastObservation.Url;
        }

        var observation = await GetObservationAsync(cancellationToken).ConfigureAwait(false);
        return observation.Url;
    }

    public async Task<BrowserActionResult> ExecuteAsync(
        BrowserAction action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        var actionId = $"chrome-cdp-tab-{Interlocked.Increment(ref _actionCounter):0000}";
        var capability = action.RequiredCapability ?? CapabilityFor(action.Kind);

        if (!IsAvailable)
        {
            return Failure(
                actionId,
                capability,
                "Chrome/CDP bridge is not connected. Action rejected fail-closed.");
        }

        if (!IsEnabled(action.Kind))
        {
            return Failure(
                actionId,
                capability,
                $"Chrome action '{action.Kind}' is not enabled in the governed executor slice.");
        }

        var validationError = ValidateAction(action, _lastObservation);
        if (validationError is not null)
        {
            return Failure(
                actionId,
                capability,
                validationError,
                _lastObservation?.Url,
                _lastObservation?.Url);
        }

        BrowserObservation? before = _lastObservation;
        if (before is null && !IsElementTargeted(action.Kind))
        {
            try
            {
                before = await GetObservationAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ChromeCdpBridgeCommandException or ChromeCdpObservationException or ChromeCdpBridgeDisconnectedException or TimeoutException)
            {
                return Failure(actionId, capability, $"Pre-action observation failed: {ex.Message}");
            }
        }

        var payload = JsonSerializer.SerializeToElement(new
        {
            action = new
            {
                kind = ActionKindName(action.Kind),
                target = action.Target,
                value = action.Value,
                snapshotToken = action.SnapshotToken
            }
        });

        ChromeCdpBridgeEnvelope response;
        try
        {
            response = await _commands.SendAsync(
                ChromeCdpBridgeMessageKinds.ExecuteAction,
                _tabId,
                payload,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ChromeCdpBridgeDisconnectedException or TimeoutException)
        {
            return Failure(actionId, capability, ex.Message, before?.Url, before?.Url);
        }

        if (response.Kind == ChromeCdpBridgeMessageKinds.Error)
        {
            return Failure(
                actionId,
                capability,
                FormatBridgeError(response),
                before?.Url,
                before?.Url);
        }

        if (response.Kind != ChromeCdpBridgeMessageKinds.Event || response.Payload is not { } responsePayload)
        {
            return Failure(
                actionId,
                capability,
                $"Expected correlated actionResult event but received '{response.Kind}'.",
                before?.Url,
                before?.Url);
        }

        var eventName = GetString(responsePayload, "event");
        var success = GetBool(responsePayload, "success");
        if (!string.Equals(eventName, "actionResult", StringComparison.Ordinal) || success != true)
        {
            return Failure(
                actionId,
                capability,
                $"Chrome action did not return a successful actionResult event (event={eventName ?? "unknown"}).",
                before?.Url,
                before?.Url);
        }

        var urlAfter = GetString(responsePayload, "url") ?? before?.Url;
        if (action.Kind is BrowserActionKind.Navigate or BrowserActionKind.Click or BrowserActionKind.Type or BrowserActionKind.Select)
        {
            _lastObservation = null;
        }

        return new BrowserActionResult(
            ActionId: actionId,
            Provider: ProviderId,
            Capability: capability,
            Success: true,
            UrlBefore: before?.Url,
            UrlAfter: urlAfter,
            Verification: VerificationState.NotVerified,
            Attempts: 1,
            Error: null);
    }

    private BrowserActionResult Failure(
        string actionId,
        string capability,
        string error,
        string? urlBefore = null,
        string? urlAfter = null) =>
        new(
            ActionId: actionId,
            Provider: ProviderId,
            Capability: capability,
            Success: false,
            UrlBefore: urlBefore,
            UrlAfter: urlAfter,
            Verification: VerificationState.Fail,
            Attempts: 1,
            Error: error);

    private static string? ValidateAction(BrowserAction action, BrowserObservation? lastObservation)
    {
        if (action.Kind == BrowserActionKind.Navigate)
        {
            var target = action.Value ?? action.Target;
            if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https"))
            {
                return "Navigate requires an absolute HTTP or HTTPS URL.";
            }
        }

        if (action.Kind == BrowserActionKind.Wait && string.IsNullOrWhiteSpace(action.Target))
        {
            return "Wait requires a non-empty CSS selector target.";
        }

        if (!IsElementTargeted(action.Kind))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(action.Target) ||
            action.Target.Length < 2 ||
            action.Target[0] != 'd' ||
            !int.TryParse(action.Target[1..], out var refNumber) ||
            refNumber <= 0)
        {
            return "Element action requires a normalized DEVOS element ref such as d1.";
        }

        if (string.IsNullOrWhiteSpace(action.SnapshotToken))
        {
            return "Element action requires a fresh observation snapshot token.";
        }

        if (lastObservation is null ||
            !string.Equals(lastObservation.SnapshotToken, action.SnapshotToken, StringComparison.Ordinal))
        {
            return "Element action snapshot is stale. Take a fresh observation before acting.";
        }

        var element = lastObservation.Elements.FirstOrDefault(candidate =>
            string.Equals(candidate.Ref, action.Target, StringComparison.Ordinal));
        if (element is null || !element.Visible)
        {
            return $"Element ref '{action.Target}' is not present and visible in the active observation snapshot.";
        }

        if (string.IsNullOrWhiteSpace(action.SemanticHint))
        {
            return "Element action is missing resolved semantic context required by governance.";
        }

        if (action.Kind is BrowserActionKind.Type or BrowserActionKind.Select && action.Value is null)
        {
            return $"{action.Kind} requires a value.";
        }

        return null;
    }

    private static bool IsEnabled(BrowserActionKind kind) => kind is
        BrowserActionKind.Navigate or
        BrowserActionKind.Wait or
        BrowserActionKind.Click or
        BrowserActionKind.Type or
        BrowserActionKind.Select;

    private static bool IsElementTargeted(BrowserActionKind kind) => kind is
        BrowserActionKind.Click or
        BrowserActionKind.Type or
        BrowserActionKind.Select;

    private static string ActionKindName(BrowserActionKind kind) => kind switch
    {
        BrowserActionKind.Navigate => "navigate",
        BrowserActionKind.Wait => "wait",
        BrowserActionKind.Click => "click",
        BrowserActionKind.Type => "type",
        BrowserActionKind.Select => "select",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Action kind is not enabled.")
    };

    private static string FormatBridgeError(ChromeCdpBridgeEnvelope response)
    {
        if (response.Payload is not { } payload)
        {
            return "Chrome bridge returned an unspecified error.";
        }

        var code = GetString(payload, "code") ?? "BRIDGE_ERROR";
        var message = GetString(payload, "message") ?? "Chrome bridge command failed.";
        return $"{code}: {message}";
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static bool? GetBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static string CapabilityFor(BrowserActionKind kind) => kind switch
    {
        BrowserActionKind.Navigate => "browser.navigate",
        BrowserActionKind.Wait => "browser.wait.selector",
        BrowserActionKind.Observe => "browser.observe",
        BrowserActionKind.ReadText => "browser.dom.read",
        BrowserActionKind.Click => "browser.click",
        BrowserActionKind.Type => "browser.type",
        BrowserActionKind.Select => "browser.select",
        BrowserActionKind.Submit => "browser.form.submit",
        _ => "browser." + kind.ToString().ToLowerInvariant()
    };

    private static string ValidateTabId(string tabId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tabId);
        if (!int.TryParse(tabId, out var parsed) || parsed <= 0)
        {
            throw new ArgumentException("Tab id must be a positive integer.", nameof(tabId));
        }

        return parsed.ToString();
    }
}
