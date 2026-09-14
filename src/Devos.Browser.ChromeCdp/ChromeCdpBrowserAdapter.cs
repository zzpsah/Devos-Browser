using Devos.Browser.Abstractions;
using Devos.Protocol;

namespace Devos.Browser.ChromeCdp;

public sealed class ChromeCdpBrowserAdapter : IBrowserAdapter
{
    private readonly IChromeCdpBridgeTransport _transport;
    private int _actionCounter;

    public ChromeCdpBrowserAdapter(IChromeCdpBridgeTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public string ProviderId => "chrome-cdp";
    public IReadOnlySet<string> Capabilities => ChromeCdpBridgeCapabilities.Default;
    public bool IsAvailable => _transport.IsConnected;

    public Task<BrowserObservation> GetObservationAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var text = IsAvailable
            ? "Chrome CDP bridge connected. Real DOM observation is intentionally not implemented until the CDP executor slice."
            : "Chrome CDP bridge disconnected. Awaiting approved extension/native bridge handshake.";

        return Task.FromResult(new BrowserObservation(
            ProtocolVersion: "1.0",
            Url: null,
            Title: "Chrome CDP Bridge",
            TabId: "chrome-cdp-stub",
            VisibleText: text,
            Elements: Array.Empty<BrowserElement>(),
            Forms: Array.Empty<BrowserForm>(),
            Tables: Array.Empty<BrowserTable>(),
            Frames: Array.Empty<BrowserFrame>(),
            NetworkState: IsAvailable ? "connected" : "disconnected"));
    }

    public Task<string?> GetCurrentUrlAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<string?>(null);
    }

    public Task<BrowserActionResult> ExecuteAsync(BrowserAction action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(action);

        var error = IsAvailable
            ? "Chrome/CDP bridge transport is connected, but real CDP browser execution is not implemented in this skeleton."
            : "Chrome/CDP bridge is not connected. Action rejected fail-closed.";

        return Task.FromResult(new BrowserActionResult(
            ActionId: $"chrome-cdp-{++_actionCounter:0000}",
            Provider: ProviderId,
            Capability: action.RequiredCapability ?? CapabilityFor(action.Kind),
            Success: false,
            UrlBefore: null,
            UrlAfter: null,
            Verification: VerificationState.Fail,
            Attempts: 1,
            Error: error));
    }

    private static string CapabilityFor(BrowserActionKind kind) => kind switch
    {
        BrowserActionKind.Navigate => "browser.navigate",
        BrowserActionKind.Back => "browser.back",
        BrowserActionKind.Forward => "browser.forward",
        BrowserActionKind.Reload => "browser.reload",
        BrowserActionKind.OpenTab => "browser.openTab",
        BrowserActionKind.CloseTab => "browser.closeTab",
        BrowserActionKind.SwitchTab => "browser.switchTab",
        BrowserActionKind.Click => "browser.click",
        BrowserActionKind.DoubleClick => "browser.doubleClick",
        BrowserActionKind.Hover => "browser.hover",
        BrowserActionKind.Scroll => "browser.scroll",
        BrowserActionKind.Type => "browser.type",
        BrowserActionKind.KeyPress => "browser.keypress",
        BrowserActionKind.Select => "browser.select",
        BrowserActionKind.ReadText => "browser.dom.read",
        BrowserActionKind.ExtractTable => "browser.extract.table",
        BrowserActionKind.ExtractLinks => "browser.extract.links",
        BrowserActionKind.Wait => "browser.wait.selector",
        BrowserActionKind.Download => "browser.download",
        BrowserActionKind.Upload => "browser.upload",
        BrowserActionKind.Screenshot => "browser.screenshot",
        BrowserActionKind.Submit => "browser.form.submit",
        _ => "browser." + kind.ToString().ToLowerInvariant()
    };
}
