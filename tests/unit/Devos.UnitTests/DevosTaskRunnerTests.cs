using Devos.AI.Abstractions;
using Devos.Browser.Abstractions;
using Devos.Checkpoints;
using Devos.Core;
using Devos.Governance;
using Devos.Protocol;
using Devos.Verification;
using Xunit;

namespace Devos.UnitTests;

public sealed class DevosTaskRunnerTests
{
    [Fact]
    public async Task NavigateStepExecutesAndCheckpoints()
    {
        var browser = new FakeBrowserAdapter(Observation("https://start.local", "Start"));
        var planner = new FakePlanner(new PlannerDecision(
            "continue",
            "Open target",
            new BrowserAction(BrowserActionKind.Navigate, Value: "https://example.com", RequiredCapability: "browser.navigate")));
        var store = new InMemoryCheckpointStore();
        var runner = new DevosTaskRunner(planner, browser, new GovernancePolicy(), new SecurityChallengePolicy(), new ActionVerifier(), store);

        var result = await runner.RunOneStepAsync("task-1", "open example.com");
        var checkpoint = await store.LoadAsync("task-1");

        Assert.Equal(DevosTaskStatus.Continued, result.Status);
        Assert.Equal(VerificationState.Pass, result.LastActionResult!.Verification);
        Assert.NotNull(checkpoint);
        Assert.Equal("https://example.com", checkpoint!.ActiveUrl);
    }

    [Fact]
    public async Task SubmitStepPausesForApproval()
    {
        var browser = new FakeBrowserAdapter(Observation("https://portal.local/form", "Form"));
        var planner = new FakePlanner(new PlannerDecision(
            "continue",
            "Submit form",
            new BrowserAction(BrowserActionKind.Submit, Target: "d9", RequiredCapability: "browser.form.submit")));
        var store = new InMemoryCheckpointStore();
        var runner = new DevosTaskRunner(planner, browser, new GovernancePolicy(), new SecurityChallengePolicy(), new ActionVerifier(), store);

        var result = await runner.RunOneStepAsync("task-2", "submit form");
        var checkpoint = await store.LoadAsync("task-2");

        Assert.Equal(DevosTaskStatus.AwaitingApproval, result.Status);
        Assert.NotNull(result.PendingAction);
        Assert.Equal("approval-required", checkpoint!.ApprovalState);
        Assert.False(browser.Executed);
    }

    [Fact]
    public async Task ChallengeObservationPausesBeforePlanning()
    {
        var browser = new FakeBrowserAdapter(Observation("https://portal.local/test/captcha", "captcha required"));
        var planner = new FakePlanner(new PlannerDecision("continue", "Should not run", new BrowserAction(BrowserActionKind.Click, Target: "d1")));
        var store = new InMemoryCheckpointStore();
        var runner = new DevosTaskRunner(planner, browser, new GovernancePolicy(), new SecurityChallengePolicy(), new ActionVerifier(), store);

        var result = await runner.RunOneStepAsync("task-3", "continue");

        Assert.Equal(DevosTaskStatus.HumanInterventionRequired, result.Status);
        Assert.False(planner.Called);
    }

    [Fact]
    public async Task ElementSemanticsCanEscalateOpaqueRefClickToApproval()
    {
        var observation = Observation(
            "https://portal.local/form",
            "Form",
            elements: new[]
            {
                new BrowserElement("d1", "button", "Save registration", "button", null, true)
            },
            snapshotToken: "snapshot-123");
        var browser = new FakeBrowserAdapter(observation);
        var planner = new FakePlanner(new PlannerDecision(
            "continue",
            "Click the save control",
            new BrowserAction(BrowserActionKind.Click, Target: "d1", RequiredCapability: "browser.click")));
        var store = new InMemoryCheckpointStore();
        var runner = new DevosTaskRunner(planner, browser, new GovernancePolicy(), new SecurityChallengePolicy(), new ActionVerifier(), store);

        var result = await runner.RunOneStepAsync("task-4", "save registration");

        Assert.Equal(DevosTaskStatus.AwaitingApproval, result.Status);
        Assert.False(browser.Executed);
        Assert.NotNull(result.PendingAction);
        Assert.Equal("snapshot-123", result.PendingAction!.SnapshotToken);
        Assert.Contains("Save registration", result.PendingAction.SemanticHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApprovedPendingCommitExecutesOnceAndRequiresReadbackWithoutPositiveEvidence()
    {
        var observation = Observation(
            "https://portal.local/form",
            "Form",
            elements: new[]
            {
                new BrowserElement("d1", "button", "Save registration", "button", null, true)
            },
            snapshotToken: "snapshot-approve-1");
        var browser = new FakeBrowserAdapter(observation);
        var planner = new FakePlanner(new PlannerDecision(
            "continue",
            "Save registration",
            new BrowserAction(BrowserActionKind.Click, Target: "d1", RequiredCapability: "browser.click")));
        var store = new InMemoryCheckpointStore();
        var runner = new DevosTaskRunner(planner, browser, new GovernancePolicy(), new SecurityChallengePolicy(), new ActionVerifier(), store);

        var pending = await runner.RunOneStepAsync("task-5", "save registration");
        var approved = await runner.ExecuteApprovedPendingActionAsync("task-5");
        var checkpoint = await store.LoadAsync("task-5");

        Assert.Equal(DevosTaskStatus.AwaitingApproval, pending.Status);
        Assert.Equal(DevosTaskStatus.ReconciliationRequired, approved.Status);
        Assert.True(browser.Executed);
        Assert.Equal(1, browser.ExecutionCount);
        Assert.Equal("approved-executed", checkpoint!.ApprovalState);
        Assert.Equal("readback-required", checkpoint.ReconciliationState);
        Assert.Null(checkpoint.PendingAction);
    }

    [Fact]
    public async Task ApprovedPendingActionRequiresFreshApprovalWhenSemanticContextChanges()
    {
        var initial = Observation(
            "https://portal.local/form",
            "Form",
            elements: new[]
            {
                new BrowserElement("d1", "button", "Save registration", "button", null, true)
            },
            snapshotToken: "snapshot-before");
        var browser = new FakeBrowserAdapter(initial);
        var planner = new FakePlanner(new PlannerDecision(
            "continue",
            "Save registration",
            new BrowserAction(BrowserActionKind.Click, Target: "d1", RequiredCapability: "browser.click")));
        var store = new InMemoryCheckpointStore();
        var runner = new DevosTaskRunner(planner, browser, new GovernancePolicy(), new SecurityChallengePolicy(), new ActionVerifier(), store);

        _ = await runner.RunOneStepAsync("task-6", "save registration");
        browser.SetObservation(Observation(
            "https://portal.local/form",
            "Form changed",
            elements: new[]
            {
                new BrowserElement("d1", "button", "Delete registration", "button", null, true)
            },
            snapshotToken: "snapshot-after"));

        var approved = await runner.ExecuteApprovedPendingActionAsync("task-6");
        var checkpoint = await store.LoadAsync("task-6");

        Assert.Equal(DevosTaskStatus.AwaitingApproval, approved.Status);
        Assert.False(browser.Executed);
        Assert.Equal("approval-context-changed", checkpoint!.ReconciliationState);
        Assert.NotNull(checkpoint.PendingAction);
        Assert.Contains("Delete registration", checkpoint.PendingAction!.SemanticHint, StringComparison.OrdinalIgnoreCase);
    }

    private static BrowserObservation Observation(
        string url,
        string text,
        IReadOnlyList<BrowserElement>? elements = null,
        string? snapshotToken = null) => new(
        ProtocolVersion: "1.0",
        Url: url,
        Title: text,
        TabId: "tab-1",
        VisibleText: text,
        Elements: elements ?? Array.Empty<BrowserElement>(),
        Forms: Array.Empty<BrowserForm>(),
        Tables: Array.Empty<BrowserTable>(),
        Frames: Array.Empty<BrowserFrame>(),
        NetworkState: "idle",
        SnapshotToken: snapshotToken);

    private sealed class FakePlanner : IAiProvider
    {
        private readonly PlannerDecision _decision;

        public FakePlanner(PlannerDecision decision)
        {
            _decision = decision;
        }

        public string ProviderId => "fake-planner";
        public bool IsAvailable => true;
        public bool Called { get; private set; }

        public Task<PlannerDecision> PlanAsync(PlannerRequest request, CancellationToken cancellationToken = default)
        {
            Called = true;
            return Task.FromResult(_decision);
        }
    }

    private sealed class FakeBrowserAdapter : IBrowserAdapter
    {
        private BrowserObservation _observation;

        public FakeBrowserAdapter(BrowserObservation observation)
        {
            _observation = observation;
        }

        public string ProviderId => "fake-browser";
        public IReadOnlySet<string> Capabilities { get; } = new HashSet<string> { "browser.navigate", "browser.form.submit", "browser.click" };
        public bool IsAvailable => true;
        public bool Executed { get; private set; }
        public int ExecutionCount { get; private set; }

        public void SetObservation(BrowserObservation observation)
        {
            _observation = observation;
        }

        public Task<BrowserObservation> GetObservationAsync(CancellationToken cancellationToken = default) => Task.FromResult(_observation);

        public Task<BrowserActionResult> ExecuteAsync(BrowserAction action, CancellationToken cancellationToken = default)
        {
            Executed = true;
            ExecutionCount++;
            if (action.Kind == BrowserActionKind.Navigate && action.Value is not null)
            {
                _observation = Observation(action.Value, "Navigated");
            }

            return Task.FromResult(new BrowserActionResult(
                ActionId: "adapter-action",
                Provider: ProviderId,
                Capability: action.RequiredCapability ?? "browser.unknown",
                Success: true,
                UrlBefore: null,
                UrlAfter: _observation.Url,
                Verification: VerificationState.NotVerified,
                Attempts: 1));
        }

        public Task<string?> GetCurrentUrlAsync(CancellationToken cancellationToken = default) => Task.FromResult(_observation.Url);
    }
}
