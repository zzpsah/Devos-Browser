namespace Devos.Protocol;

public enum VerificationState
{
    NotVerified,
    Pass,
    Fail,
    HoldReadbackRequired
}

public sealed record BrowserActionResult(
    string ActionId,
    string Provider,
    string Capability,
    bool Success,
    string? UrlBefore,
    string? UrlAfter,
    VerificationState Verification,
    int Attempts,
    string? Error = null);
