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
        "browser.wait.selector"
    };

    private readonly IChromeCdpBridgeCommandClient _commands;
    private readonly IChromeCdpObservationClient _observations;
    private readonly string _tabId;
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

    public Task<BrowserObservation> GetObservationAsync(CancellationToken cancellationToken = default) =>
        _observations.AttachAndObserveAsync(_tabId, cancellationToken);

    public async Task<string?> GetCurrentUrlAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return null;
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

        if (action.Kind is not (BrowserActionKind.Navigate or BrowserActionKind.Wait))
        {
            return Failure(
                actionId,
                capability,
                $"Chrome action '{action.Kind}' is not enabled in the governed executor slice.");
        }

        BrowserObservation before;
        try
        {
            before = await _observations.AttachAndObserveAsync(_tabId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ChromeCdpBridgeCommandException or ChromeCdpObservationException or ChromeCdpBridgeDisconnectedException or TimeoutException)
        {
            return Failure(actionId, capability, $"Pre-action observation failed: {ex.Message}");
        }

        var validationError = ValidateAction(action);
        if (validationError is not null)
        {
            return Failure(actionId, capability, validationError, before.Url, before.Url);
        }

        var payload = JsonSerializer.SerializeToElement(new
        {
            action = new
            {
                kind = action.Kind == BrowserActionKind.Navigate ? "navigate" : "wait",
                target = action.Target,
                value = action.Value
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
            return Failure(actionId, capability, ex.Message, before.Url, before.Url);
        }

        if (response.Kind == ChromeCdpBridgeMessageKinds.Error)
        {
            return Failure(
                actionId,
                capability,
                FormatBridgeError(response),
                before.Url,
                before.Url);
        }

        if (response.Kind != ChromeCdpBridgeMessageKinds.Event || response.Payload is not { } responsePayload)
        {
            return Failure(
                actionId,
                capability,
                $"Expected correlated actionResult event but received '{response.Kind}'.",
                before.Url,
                before.Url);
        }

        var eventName = GetString(responsePayload, "event");
        var success = GetBool(responsePayload, "success");
        if (!string.Equals(eventName, "actionResult", StringComparison.Ordinal) || success != true)
        {
            return Failure(
                actionId,
                capability,
                $"Chrome action did not return a successful actionResult event (event={eventName ?? "unknown"}).",
                before.Url,
                before.Url);
        }

        var urlAfter = GetString(responsePayload, "url") ?? before.Url;
        return new BrowserActionResult(
            ActionId: actionId,
            Provider: ProviderId,
            Capability: capability,
            Success: true,
            UrlBefore: before.Url,
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

    private static string? ValidateAction(BrowserAction action)
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

        return null;
    }

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
