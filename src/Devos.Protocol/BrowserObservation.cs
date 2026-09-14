namespace Devos.Protocol;

public sealed record BrowserObservation(
    string ProtocolVersion,
    string? Url,
    string? Title,
    string? TabId,
    string? VisibleText,
    IReadOnlyList<BrowserElement> Elements,
    IReadOnlyList<BrowserForm> Forms,
    IReadOnlyList<BrowserTable> Tables,
    IReadOnlyList<BrowserFrame> Frames,
    string? NetworkState);

public sealed record BrowserElement(
    string Ref,
    string? Role,
    string? Text,
    string? Type,
    string? Href,
    bool Visible);

public sealed record BrowserForm(string Ref, IReadOnlyList<string> ElementRefs);
public sealed record BrowserTable(string Ref, int RowCount, int ColumnCount);
public sealed record BrowserFrame(string Ref, string? Url, string? Title);
