namespace Devos.Protocol;

public enum BrowserActionKind
{
    Navigate,
    Back,
    Forward,
    Reload,
    OpenTab,
    CloseTab,
    SwitchTab,
    Observe,
    Click,
    DoubleClick,
    Hover,
    Scroll,
    Type,
    KeyPress,
    Select,
    ReadText,
    ExtractTable,
    ExtractLinks,
    Wait,
    Download,
    Upload,
    Screenshot,
    Submit
}

public sealed record BrowserAction(
    BrowserActionKind Kind,
    string? Target = null,
    string? Value = null,
    string? RequiredCapability = null);
