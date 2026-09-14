using System.Text.RegularExpressions;
using Devos.Protocol;

namespace Devos.Logging;

public sealed class SensitiveValueRedactor
{
    private static readonly Regex SecretAssignmentPattern = new(
        @"(?i)\b(password|passwd|pwd|otp|mfa|2fa|captcha|token|refresh_token|access_token|api[_-]?key|authorization|cookie|session)\b\s*[:=]\s*([^\s,;]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] SensitiveFieldTerms =
    {
        "password",
        "passwd",
        "pwd",
        "otp",
        "one-time password",
        "verification code",
        "mfa",
        "2fa",
        "captcha",
        "token",
        "cookie",
        "authorization",
        "api key",
        "secret"
    };

    public string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return SecretAssignmentPattern.Replace(value, match => $"{match.Groups[1].Value}=<redacted>");
    }

    public BrowserObservation Sanitize(BrowserObservation observation)
    {
        var elements = observation.Elements
            .Select(element => IsSensitiveElement(element)
                ? element with { Text = "<redacted>", Href = null }
                : element with { Text = Redact(element.Text), Href = Redact(element.Href) })
            .ToArray();

        return observation with
        {
            Title = Redact(observation.Title),
            VisibleText = Redact(observation.VisibleText),
            Elements = elements
        };
    }

    public bool IsSensitiveElement(BrowserElement element)
    {
        var combined = string.Join(' ', element.Role, element.Text, element.Type, element.Href).ToLowerInvariant();
        return SensitiveFieldTerms.Any(term => combined.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
