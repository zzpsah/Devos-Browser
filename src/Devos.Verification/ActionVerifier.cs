using Devos.Protocol;

namespace Devos.Verification;

public interface IActionVerifier
{
    BrowserActionResult Verify(
        BrowserAction action,
        BrowserObservation before,
        BrowserObservation after,
        string provider,
        bool executionSuccess = true,
        int attempts = 1,
        string? error = null);
}

public sealed class ActionVerifier : IActionVerifier
{
    public BrowserActionResult Verify(
        BrowserAction action,
        BrowserObservation before,
        BrowserObservation after,
        string provider,
        bool executionSuccess = true,
        int attempts = 1,
        string? error = null)
    {
        var capability = action.RequiredCapability ?? CapabilityFor(action.Kind);
        var state = executionSuccess
            ? DetermineState(action, before, after)
            : VerificationState.Fail;

        return new BrowserActionResult(
            ActionId: $"a-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Provider: provider,
            Capability: capability,
            Success: executionSuccess && state != VerificationState.Fail,
            UrlBefore: before.Url,
            UrlAfter: after.Url,
            Verification: state,
            Attempts: attempts,
            Error: error);
    }

    private static VerificationState DetermineState(BrowserAction action, BrowserObservation before, BrowserObservation after)
    {
        return action.Kind switch
        {
            BrowserActionKind.Navigate => UrlMatches(action.Value, after.Url) ? VerificationState.Pass : VerificationState.Fail,
            BrowserActionKind.Back or BrowserActionKind.Forward or BrowserActionKind.Reload => VerificationState.Pass,
            BrowserActionKind.Observe or BrowserActionKind.ReadText or BrowserActionKind.ExtractTable or BrowserActionKind.ExtractLinks or BrowserActionKind.Screenshot => VerificationState.Pass,
            BrowserActionKind.Click => HasMeaningfulChange(before, after) ? VerificationState.Pass : VerificationState.NotVerified,
            BrowserActionKind.Type or BrowserActionKind.Select => VerificationState.NotVerified,
            BrowserActionKind.Download => VerificationState.NotVerified,
            BrowserActionKind.Submit or BrowserActionKind.Upload => CommitHasPositiveEvidence(after) ? VerificationState.Pass : VerificationState.HoldReadbackRequired,
            _ => VerificationState.NotVerified
        };
    }

    private static bool UrlMatches(string? expected, string? actual)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
        {
            return false;
        }

        return actual.StartsWith(expected, StringComparison.OrdinalIgnoreCase) || string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasMeaningfulChange(BrowserObservation before, BrowserObservation after)
    {
        return !string.Equals(before.Url, after.Url, StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(before.Title, after.Title, StringComparison.Ordinal) ||
               !string.Equals(before.VisibleText, after.VisibleText, StringComparison.Ordinal);
    }

    private static bool CommitHasPositiveEvidence(BrowserObservation after)
    {
        var evidence = string.Join(' ', after.Title, after.VisibleText).ToLowerInvariant();
        return evidence.Contains("success", StringComparison.Ordinal) ||
               evidence.Contains("saved", StringComparison.Ordinal) ||
               evidence.Contains("submitted", StringComparison.Ordinal) ||
               evidence.Contains("complete", StringComparison.Ordinal) ||
               evidence.Contains("reference", StringComparison.Ordinal);
    }

    private static string CapabilityFor(BrowserActionKind kind)
    {
        return kind switch
        {
            BrowserActionKind.Navigate => "browser.navigate",
            BrowserActionKind.Click => "browser.click",
            BrowserActionKind.Type => "browser.type",
            BrowserActionKind.ReadText => "browser.dom.read",
            BrowserActionKind.ExtractTable => "browser.extract.table",
            BrowserActionKind.ExtractLinks => "browser.extract.links",
            BrowserActionKind.Wait => "browser.wait",
            BrowserActionKind.Download => "browser.download",
            BrowserActionKind.Upload => "browser.upload",
            BrowserActionKind.Screenshot => "browser.screenshot",
            BrowserActionKind.Submit => "browser.form.submit",
            _ => "browser." + kind.ToString().ToLowerInvariant()
        };
    }
}
