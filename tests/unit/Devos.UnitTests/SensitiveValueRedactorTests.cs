using Devos.Logging;
using Devos.Protocol;
using Xunit;

namespace Devos.UnitTests;

public sealed class SensitiveValueRedactorTests
{
    [Fact]
    public void RedactsSecretAssignments()
    {
        var redactor = new SensitiveValueRedactor();

        var result = redactor.Redact("user=demo password=hunter2 otp=123456 token=abc");

        Assert.Contains("user=demo", result);
        Assert.Contains("password=<redacted>", result);
        Assert.Contains("otp=<redacted>", result);
        Assert.Contains("token=<redacted>", result);
        Assert.DoesNotContain("hunter2", result);
        Assert.DoesNotContain("123456", result);
    }

    [Fact]
    public void SanitizesSensitiveElements()
    {
        var redactor = new SensitiveValueRedactor();
        var observation = new BrowserObservation(
            ProtocolVersion: "1.0",
            Url: "https://example.test/login",
            Title: "Login",
            TabId: "tab-1",
            VisibleText: "password=secret token=abc",
            Elements: new[]
            {
                new BrowserElement("d1", "textbox", "secret", "password", null, true),
                new BrowserElement("d2", "button", "Login", "button", null, true)
            },
            Forms: Array.Empty<BrowserForm>(),
            Tables: Array.Empty<BrowserTable>(),
            Frames: Array.Empty<BrowserFrame>(),
            NetworkState: "idle");

        var safe = redactor.Sanitize(observation);

        Assert.Equal("<redacted>", safe.Elements[0].Text);
        Assert.DoesNotContain("secret", safe.VisibleText ?? string.Empty);
        Assert.Equal("Login", safe.Elements[1].Text);
    }
}
