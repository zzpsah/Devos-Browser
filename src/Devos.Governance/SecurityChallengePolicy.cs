using Devos.Protocol;

namespace Devos.Governance;

public enum SecurityChallengeKind
{
    None,
    Captcha,
    Recaptcha,
    Hcaptcha,
    Turnstile,
    Otp,
    Mfa,
    SessionExpired,
    RateLimit
}

public enum SecurityChallengeHandling
{
    None,
    HumanInterventionRequired,
    SyntheticAutoCompletionAllowed
}

public sealed record SecurityChallenge(
    SecurityChallengeKind Kind,
    string Reason,
    bool IsSyntheticFixture = false,
    string? FixtureId = null);

public sealed record ChallengeHandlingContext(
    bool TestMode,
    string TargetHost,
    bool HasVerifiedTestFixture,
    string RuntimeProfile);

public sealed class SecurityChallengePolicy
{
    private static readonly HashSet<string> TestHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "127.0.0.1",
        "devos.test",
        "synthetic-portal.local"
    };

    public SecurityChallenge? Detect(BrowserObservation observation)
    {
        var text = string.Join(' ', new[] { observation.Title, observation.VisibleText }
            .Where(value => !string.IsNullOrWhiteSpace(value)))
            .ToLowerInvariant();

        foreach (var element in observation.Elements)
        {
            text += " " + string.Join(' ', new[] { element.Role, element.Text, element.Type, element.Href }
                .Where(value => !string.IsNullOrWhiteSpace(value)))
                .ToLowerInvariant();
        }

        if (text.Contains("turnstile", StringComparison.Ordinal))
        {
            return new SecurityChallenge(SecurityChallengeKind.Turnstile, "Turnstile challenge detected.", IsSynthetic(text), ExtractFixtureId(text));
        }

        if (text.Contains("recaptcha", StringComparison.Ordinal))
        {
            return new SecurityChallenge(SecurityChallengeKind.Recaptcha, "reCAPTCHA challenge detected.", IsSynthetic(text), ExtractFixtureId(text));
        }

        if (text.Contains("hcaptcha", StringComparison.Ordinal))
        {
            return new SecurityChallenge(SecurityChallengeKind.Hcaptcha, "hCaptcha challenge detected.", IsSynthetic(text), ExtractFixtureId(text));
        }

        if (text.Contains("captcha", StringComparison.Ordinal))
        {
            return new SecurityChallenge(SecurityChallengeKind.Captcha, "CAPTCHA challenge detected.", IsSynthetic(text), ExtractFixtureId(text));
        }

        if (text.Contains("one-time password", StringComparison.Ordinal) || text.Contains("otp", StringComparison.Ordinal) || text.Contains("verification code", StringComparison.Ordinal))
        {
            return new SecurityChallenge(SecurityChallengeKind.Otp, "OTP or verification-code challenge detected.", IsSynthetic(text), ExtractFixtureId(text));
        }

        if (text.Contains("mfa", StringComparison.Ordinal) || text.Contains("2fa", StringComparison.Ordinal) || text.Contains("multi-factor", StringComparison.Ordinal))
        {
            return new SecurityChallenge(SecurityChallengeKind.Mfa, "MFA challenge detected.", IsSynthetic(text), ExtractFixtureId(text));
        }

        if (text.Contains("session expired", StringComparison.Ordinal) || text.Contains("sign in again", StringComparison.Ordinal))
        {
            return new SecurityChallenge(SecurityChallengeKind.SessionExpired, "Session-expiry challenge detected.", IsSynthetic(text), ExtractFixtureId(text));
        }

        if (text.Contains("rate limit", StringComparison.Ordinal) || text.Contains("too many requests", StringComparison.Ordinal))
        {
            return new SecurityChallenge(SecurityChallengeKind.RateLimit, "Rate-limit page detected.", IsSynthetic(text), ExtractFixtureId(text));
        }

        return null;
    }

    public SecurityChallengeHandling Decide(SecurityChallenge? challenge, ChallengeHandlingContext context)
    {
        if (challenge is null || challenge.Kind == SecurityChallengeKind.None)
        {
            return SecurityChallengeHandling.None;
        }

        if (context.TestMode &&
            context.HasVerifiedTestFixture &&
            challenge.IsSyntheticFixture &&
            string.Equals(context.RuntimeProfile, "test", StringComparison.OrdinalIgnoreCase) &&
            TestHosts.Contains(context.TargetHost))
        {
            return SecurityChallengeHandling.SyntheticAutoCompletionAllowed;
        }

        return SecurityChallengeHandling.HumanInterventionRequired;
    }

    private static bool IsSynthetic(string text) => text.Contains("devos-synthetic-fixture", StringComparison.Ordinal);

    private static string? ExtractFixtureId(string text)
    {
        const string marker = "fixture:";
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var start = index + marker.Length;
        var end = start;
        while (end < text.Length && !char.IsWhiteSpace(text[end]))
        {
            end++;
        }

        return text[start..end].Trim();
    }
}
