namespace Devos.Browser.ChromeCdp;

public static class ChromeCdpBridgeCapabilities
{
    public static IReadOnlySet<string> Default { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "browser.navigate",
        "browser.back",
        "browser.forward",
        "browser.reload",
        "browser.openTab",
        "browser.closeTab",
        "browser.switchTab",
        "browser.observe",
        "browser.click",
        "browser.doubleClick",
        "browser.hover",
        "browser.scroll",
        "browser.type",
        "browser.keypress",
        "browser.select",
        "browser.dom.read",
        "browser.extract.table",
        "browser.extract.links",
        "browser.wait.selector",
        "browser.wait.text",
        "browser.wait.navigation",
        "browser.wait.networkIdle",
        "browser.download",
        "browser.upload",
        "browser.screenshot",
        "browser.form.inspect",
        "browser.form.fill",
        "browser.form.submit",
        "browser.network.observe",
        "browser.frame.list",
        "browser.frame.switch",
        "browser.accessibility.snapshot"
    };
}
