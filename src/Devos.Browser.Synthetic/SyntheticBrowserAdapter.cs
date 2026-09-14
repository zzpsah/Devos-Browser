using Devos.Browser.Abstractions;
using Devos.Protocol;

namespace Devos.Browser.Synthetic;

public sealed class SyntheticBrowserAdapter : IBrowserAdapter
{
    private readonly object _sync = new();
    private readonly string _host;
    private readonly List<string> _backStack = new();
    private readonly IReadOnlyList<SyntheticStudentRecord> _records;
    private readonly Dictionary<string, string> _typedValues = new(StringComparer.OrdinalIgnoreCase);
    private string _path = "/login";
    private string? _lastMessage;
    private bool _uncertainCommitCompleted;
    private int _actionCounter;

    public SyntheticBrowserAdapter(string? host = null)
    {
        _host = string.IsNullOrWhiteSpace(host) ? "https://synthetic-portal.local" : host.TrimEnd('/');
        _records = Enumerable.Range(1, 100)
            .Select(index => new SyntheticStudentRecord(index, $"REG{index:0000}", $"Synthetic Student {index:000}", index % 3 == 0 ? "Pending" : "Ready"))
            .ToArray();
    }

    public string ProviderId => "browser.synthetic";

    public IReadOnlySet<string> Capabilities { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "browser.navigate",
        "browser.back",
        "browser.forward",
        "browser.reload",
        "browser.observe",
        "browser.click",
        "browser.type",
        "browser.dom.read",
        "browser.extract.table",
        "browser.extract.links",
        "browser.wait.selector",
        "browser.wait.text",
        "browser.download",
        "browser.upload",
        "browser.screenshot",
        "browser.form.submit"
    };

    public bool IsAvailable => true;

    public Task<BrowserObservation> GetObservationAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            return Task.FromResult(BuildObservation());
        }
    }

    public Task<string?> GetCurrentUrlAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            return Task.FromResult<string?>(CurrentUrl());
        }
    }

    public Task<BrowserActionResult> ExecuteAsync(BrowserAction action, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var before = CurrentUrl();
            var success = true;
            string? error = null;

            switch (action.Kind)
            {
                case BrowserActionKind.Navigate:
                case BrowserActionKind.OpenTab:
                    NavigateTo(action.Value ?? action.Target ?? "/login");
                    break;
                case BrowserActionKind.Back:
                    Back();
                    break;
                case BrowserActionKind.Forward:
                case BrowserActionKind.Reload:
                case BrowserActionKind.Observe:
                case BrowserActionKind.ReadText:
                case BrowserActionKind.ExtractTable:
                case BrowserActionKind.ExtractLinks:
                case BrowserActionKind.Screenshot:
                    break;
                case BrowserActionKind.Click:
                    HandleClick(action.Target);
                    break;
                case BrowserActionKind.Type:
                    HandleType(action.Target, action.Value);
                    break;
                case BrowserActionKind.Wait:
                    success = CanSee(action.Target ?? action.Value);
                    error = success ? null : "Target was not visible in the synthetic observation.";
                    break;
                case BrowserActionKind.Download:
                    _lastMessage = $"Download ready: {action.Target ?? action.Value ?? "synthetic-export.csv"}";
                    break;
                case BrowserActionKind.Upload:
                    _lastMessage = $"Upload accepted: {action.Value ?? action.Target ?? "synthetic-file"}";
                    break;
                case BrowserActionKind.Submit:
                    HandleSubmit(action.Target);
                    break;
                default:
                    success = false;
                    error = $"Synthetic adapter does not implement action kind {action.Kind}.";
                    break;
            }

            return Task.FromResult(new BrowserActionResult(
                ActionId: $"synthetic-{++_actionCounter:0000}",
                Provider: ProviderId,
                Capability: action.RequiredCapability ?? CapabilityFor(action.Kind),
                Success: success,
                UrlBefore: before,
                UrlAfter: CurrentUrl(),
                Verification: VerificationState.NotVerified,
                Attempts: 1,
                Error: error));
        }
    }

    private void NavigateTo(string target)
    {
        var next = NormalizePath(target);
        if (string.Equals(_path, next, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _backStack.Add(_path);
        _path = next;
    }

    private void Back()
    {
        if (_backStack.Count == 0)
        {
            return;
        }

        _path = _backStack[^1];
        _backStack.RemoveAt(_backStack.Count - 1);
    }

    private void HandleClick(string? target)
    {
        var normalized = (target ?? string.Empty).Trim().ToLowerInvariant();
        var page = _path.ToLowerInvariant();

        if (page == "/login" && (normalized is "d3" or "sign in" or "login"))
        {
            NavigateTo("/dashboard");
        }
        else if (normalized.Contains("student", StringComparison.Ordinal) || (page == "/dashboard" && normalized == "d1"))
        {
            NavigateTo("/students");
        }
        else if (normalized.Contains("captcha", StringComparison.Ordinal) || (page == "/dashboard" && normalized == "d2"))
        {
            NavigateTo("/test/captcha");
        }
        else if (normalized.Contains("otp", StringComparison.Ordinal) || (page == "/dashboard" && normalized == "d3"))
        {
            NavigateTo("/test/otp");
        }
        else if (normalized.Contains("mfa", StringComparison.Ordinal) || (page == "/dashboard" && normalized == "d4"))
        {
            NavigateTo("/test/mfa");
        }
        else if (normalized.Contains("uncertain", StringComparison.Ordinal) || (page == "/dashboard" && normalized == "d5"))
        {
            NavigateTo("/uncertain-commit");
        }
        else if (normalized.Contains("next", StringComparison.Ordinal))
        {
            NavigateTo("/students?page=2");
        }
        else if (normalized.Contains("dashboard", StringComparison.Ordinal))
        {
            NavigateTo("/dashboard");
        }
        else
        {
            _lastMessage = string.IsNullOrWhiteSpace(target) ? "Synthetic click executed." : $"Synthetic click executed: {target}";
        }
    }

    private void HandleType(string? target, string? value)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            _lastMessage = "Type action ignored because no target was supplied.";
            return;
        }

        _typedValues[target] = value ?? string.Empty;
        _lastMessage = $"Typed into {target}.";
    }

    private void HandleSubmit(string? target)
    {
        if (_path.StartsWith("/uncertain-commit", StringComparison.OrdinalIgnoreCase))
        {
            _uncertainCommitCompleted = true;
            _lastMessage = "Synthetic provider returned ambiguous commit result. Readback required.";
            return;
        }

        NavigateTo("/submit-success");
        _lastMessage = $"Submitted {target ?? "synthetic form"}.";
    }

    private bool CanSee(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return true;
        }

        var observation = BuildObservation();
        return (observation.VisibleText?.Contains(target, StringComparison.OrdinalIgnoreCase) ?? false) ||
               observation.Elements.Any(element =>
                   string.Equals(element.Ref, target, StringComparison.OrdinalIgnoreCase) ||
                   (element.Text?.Contains(target, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    private BrowserObservation BuildObservation()
    {
        var pathOnly = _path.Split('?', 2)[0].ToLowerInvariant();
        var elements = new List<BrowserElement>();
        var forms = new List<BrowserForm>();
        var tables = new List<BrowserTable>();
        var frames = new List<BrowserFrame>();
        var title = "Synthetic Portal";
        var visibleText = "Synthetic Portal";

        if (pathOnly is "/" or "/login")
        {
            title = "Synthetic Portal Login";
            visibleText = "Synthetic Portal Login Username Password Sign in";
            elements.Add(new BrowserElement("d1", "textbox", "Username", "text", null, true));
            elements.Add(new BrowserElement("d2", "textbox", "Password", "password", null, true));
            elements.Add(new BrowserElement("d3", "button", "Sign in", "button", null, true));
            forms.Add(new BrowserForm("f-login", new[] { "d1", "d2", "d3" }));
        }
        else if (pathOnly == "/dashboard")
        {
            title = "Synthetic Portal Dashboard";
            visibleText = "Dashboard Student Records Mock CAPTCHA Mock OTP Mock MFA Uncertain Commit Downloads";
            elements.Add(new BrowserElement("d1", "link", "Student Records", "link", "/students", true));
            elements.Add(new BrowserElement("d2", "link", "Mock CAPTCHA", "link", "/test/captcha", true));
            elements.Add(new BrowserElement("d3", "link", "Mock OTP", "link", "/test/otp", true));
            elements.Add(new BrowserElement("d4", "link", "Mock MFA", "link", "/test/mfa", true));
            elements.Add(new BrowserElement("d5", "link", "Uncertain Commit", "link", "/uncertain-commit", true));
        }
        else if (pathOnly == "/students")
        {
            title = "Synthetic Student Records";
            visibleText = StudentRecordsText();
            elements.Add(new BrowserElement("d1", "textbox", "Search students", "search", null, true));
            elements.Add(new BrowserElement("d2", "button", "Next page", "button", null, true));
            elements.Add(new BrowserElement("d3", "button", "Submit selected student", "button", null, true));
            elements.Add(new BrowserElement("d4", "button", "Download export", "button", null, true));
            elements.Add(new BrowserElement("d5", "button", "Upload document", "button", null, true));
            elements.Add(new BrowserElement("d6", "link", "Dashboard", "link", "/dashboard", true));
            tables.Add(new BrowserTable("t-students", _records.Count, 4));
        }
        else if (pathOnly == "/test/captcha")
        {
            title = "Synthetic CAPTCHA";
            visibleText = "CAPTCHA required devos-synthetic-fixture fixture:captcha synthetic only. Human intervention normally required.";
            elements.Add(new BrowserElement("d1", "textbox", "Synthetic CAPTCHA answer", "text", null, true));
            elements.Add(new BrowserElement("d2", "button", "Continue", "button", null, true));
        }
        else if (pathOnly == "/test/otp")
        {
            title = "Synthetic OTP";
            visibleText = "One-time password OTP required devos-synthetic-fixture fixture:otp synthetic code 000111.";
            elements.Add(new BrowserElement("d1", "textbox", "OTP", "text", null, true));
            elements.Add(new BrowserElement("d2", "button", "Verify", "button", null, true));
        }
        else if (pathOnly == "/test/mfa")
        {
            title = "Synthetic MFA";
            visibleText = "MFA multi-factor approval required devos-synthetic-fixture fixture:mfa synthetic approval pending.";
            elements.Add(new BrowserElement("d1", "button", "Synthetic approve", "button", null, true));
        }
        else if (pathOnly == "/test/turnstile")
        {
            title = "Synthetic Turnstile";
            visibleText = "Turnstile challenge devos-synthetic-fixture fixture:turnstile synthetic only.";
        }
        else if (pathOnly == "/test/recaptcha")
        {
            title = "Synthetic reCAPTCHA";
            visibleText = "reCAPTCHA challenge devos-synthetic-fixture fixture:recaptcha synthetic only.";
        }
        else if (pathOnly == "/session-expired")
        {
            title = "Synthetic Session Expired";
            visibleText = "Session expired. Please sign in again.";
        }
        else if (pathOnly == "/rate-limit")
        {
            title = "Synthetic Rate Limit";
            visibleText = "Rate limit reached. Too many requests.";
        }
        else if (pathOnly == "/iframe")
        {
            title = "Synthetic Iframe Page";
            visibleText = "Outer page with embedded synthetic frame.";
            frames.Add(new BrowserFrame("frame-1", $"{_host}/frame-content", "Synthetic Inner Frame"));
        }
        else if (pathOnly == "/uncertain-commit")
        {
            title = "Synthetic Uncertain Commit";
            visibleText = "Uncertain commit simulator. Submit may have completed but there is no immediate success evidence.";
            elements.Add(new BrowserElement("d1", "button", "Submit uncertain transaction", "button", null, true));
            elements.Add(new BrowserElement("d2", "link", "Readback", "link", "/uncertain-commit/readback", true));
        }
        else if (pathOnly == "/uncertain-commit/readback")
        {
            title = "Synthetic Readback";
            visibleText = _uncertainCommitCompleted
                ? "Success submitted complete reference SYN-UNCERTAIN-0001 after provider readback."
                : "ABSENT: no synthetic uncertain commit was found during readback.";
        }
        else if (pathOnly == "/submit-success")
        {
            title = "Synthetic Submit Success";
            visibleText = "Success submitted complete reference SYN-SUBMIT-0001.";
        }
        else
        {
            title = "Synthetic Not Found";
            visibleText = $"Synthetic page not found: {_path}";
        }

        if (!string.IsNullOrWhiteSpace(_lastMessage))
        {
            visibleText += " " + _lastMessage;
        }

        return new BrowserObservation(
            ProtocolVersion: "1.0",
            Url: CurrentUrl(),
            Title: title,
            TabId: "synthetic-tab-1",
            VisibleText: visibleText,
            Elements: elements,
            Forms: forms,
            Tables: tables,
            Frames: frames,
            NetworkState: "idle");
    }

    private string StudentRecordsText()
    {
        var page = _path.Contains("page=2", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
        var start = (page - 1) * 10;
        var rows = _records.Skip(start).Take(10).Select(record => $"{record.Serial}:{record.RegistrationNo}:{record.Name}:{record.Status}");
        return $"Student Records page {page}. Total synthetic records: {_records.Count}. " + string.Join(" ", rows);
    }

    private string CurrentUrl() => _host + _path;

    private static string NormalizePath(string target)
    {
        var value = target.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Equals("home", StringComparison.OrdinalIgnoreCase))
        {
            return "/login";
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return string.IsNullOrWhiteSpace(uri.PathAndQuery) ? "/login" : uri.PathAndQuery;
        }

        return value.StartsWith('/', StringComparison.Ordinal) ? value : "/" + value;
    }

    private static string CapabilityFor(BrowserActionKind kind)
    {
        return kind switch
        {
            BrowserActionKind.Navigate => "browser.navigate",
            BrowserActionKind.Back => "browser.back",
            BrowserActionKind.Forward => "browser.forward",
            BrowserActionKind.Reload => "browser.reload",
            BrowserActionKind.Click => "browser.click",
            BrowserActionKind.Type => "browser.type",
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
}

public sealed record SyntheticStudentRecord(int Serial, string RegistrationNo, string Name, string Status);
