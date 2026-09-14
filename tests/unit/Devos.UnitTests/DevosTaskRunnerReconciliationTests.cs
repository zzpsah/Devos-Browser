using Devos.AI.Abstractions;
using Devos.Browser.Abstractions;
using Devos.Checkpoints;
using Devos.Core;
using Devos.Governance;
using Devos.Protocol;
using Devos.Verification;
using Xunit;

namespace Devos.UnitTests;

public sealed class DevosTaskRunnerReconciliationTests
{
    [Fact]
    public async Task PositiveReadbackReconcilesWithoutReplay()
    {
        var browser = new ReadbackBrowser(Observation("Success saved reference REG-42"));
        var store = await StoreReadbackCheckpointAsync();
        var runner = NewRunner(browser, store);

        var result = await runner.ReconcileReadbackAsync("task-r1");
        var checkpoint = await store.LoadAsync("task-r1");

        Assert.Equal(DevosTaskStatus.Continued, result.Status);
        Assert.Equal("readback-confirmed", checkpoint!.ReconciliationState);
        Assert.Equal(0, browser.ExecutionCount);
    }

    [Fact]
    public async Task ExplicitAbsentReadbackDoesNotReplayMutation()
    {
        var browser = new ReadbackBrowser(Observation("ABSENT: registration was not found"));
        var store = await StoreReadbackCheckpointAsync();
        var runner = NewRunner(browser, store);

        var result = await runner.ReconcileReadbackAsync("task-r1");
        var checkpoint = await store.LoadAsync("task-r1");

        Assert.Equal(DevosTaskStatus.Failed, result.Status);
        Assert.Equal("readback-absent", checkpoint!.ReconciliationState);
        Assert.Equal(0, browser.ExecutionCount);
        Assert.Contains("not replay", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AmbiguousReadbackStaysOnHoldWithoutReplay()
    {
        var browser = new ReadbackBrowser(Observation("Registration details page loaded"));
        var store = await StoreReadbackCheckpointAsync();
        var runner = NewRunner(browser, store);

        var result = await runner.ReconcileReadbackAsync("task-r1");
        var checkpoint = await store.LoadAsync("task-r1");

        Assert.Equal(DevosTaskStatus.ReconciliationRequired, result.Status);
        Assert.Equal("readback-required", checkpoint!.ReconciliationState);
        Assert.Equal(0, browser.ExecutionCount);
    }

    [Fact]
    public async Task ChallengeDuringReadbackRequiresHumanIntervention()
    {
        var browser = new ReadbackBrowser(Observation("captcha required"));
        var store = await StoreReadbackCheckpointAsync();
        var runner = NewRunner(browser, store);

        var result = await runner.ReconcileReadbackAsync("task-r1");
        var checkpoint = await store.LoadAsync("task-r1");

        Assert.Equal(DevosTaskStatus.HumanInterventionRequired, result.Status);
        Assert.Equal("readback-required", checkpoint!.ReconciliationState);
        Assert.Equal("human-intervention-required", checkpoint.ApprovalState);
        Assert.Equal(0, browser.ExecutionCount);
    }

    private static DevosTaskRunner NewRunner(ReadbackBrowser browser, InMemoryCheckpointStore store) =>
        new(
            new NoopPlanner(),
            browser,
            new GovernancePolicy(),
            new SecurityChallengePolicy(),
            new ActionVerifier(),
            store);

    private static async Task<InMemoryCheckpointStore> StoreReadbackCheckpointAsync()
    {
        var store = new InMemoryCheckpointStore();
        var now = DateTimeOffset.UtcNow;
        var actionResult = new BrowserActionResult(
            ActionId: "approved-1",
            Provider: "fake-browser",
            Capability: "browser.click",
            Success: true,
            UrlBefore: "https://portal.local/form",
            UrlAfter: "https://portal.local/form",
            Verification: VerificationState.HoldReadbackRequired,
            Attempts: 1);

        await store.SaveAsync(new TaskCheckpoint(
            TaskId: "task-r1",
            Goal: "save registration",
            BrowserBackend: "fake-browser",
            AiProvider: "noop",
            CurrentStep: 1,
            CompletedSteps: new[] { actionResult },
            ActiveUrl: "https://portal.local/form",
            ActiveTab: "tab-1",
            LastObservation: Observation("Mutation response ambiguous"),
            PendingAction: null,
            ApprovalState: "approved-executed",
            ReconciliationState: "readback-required",
            CreatedAt: now,
            UpdatedAt: now));

        return store;
    }

    private static BrowserObservation Observation(string text) => new(
        ProtocolVersion: "1.0",
        Url: "https://portal.local/form",
        Title: "Portal",
        TabId: "tab-1",
        VisibleText: text,
        Elements: Array.Empty<BrowserElement>(),
        Forms: Array.Empty<BrowserForm>(),
        Tables: Array.Empty<BrowserTable>(),
        Frames: Array.Empty<BrowserFrame>(),
        NetworkState: "idle",
        SnapshotToken: "snapshot-readback");

    private sealed class ReadbackBrowser : IBrowserAdapter
    {
        private readonly BrowserObservation _observation;

        public ReadbackBrowser(BrowserObservation observation)
        {
            _observation = observation;
        }

        public string ProviderId => "fake-browser";
        public IReadOnlySet<string> Capabilities { get; } = new HashSet<string> { "browser.observe", "browser.click" };
        public bool IsAvailable => true;
        public int ExecutionCount { get; private set; }

        public Task<BrowserObservation> GetObservationAsync(CancellationToken cancellationToken = default) => Task.FromResult(_observation);

        public Task<BrowserActionResult> ExecuteAsync(BrowserAction action, CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            throw new InvalidOperationException("Reconciliation must never replay the mutation.");
        }

        public Task<string?> GetCurrentUrlAsync(CancellationToken cancellationToken = default) => Task.FromResult(_observation.Url);
    }

    private sealed class NoopPlanner : IAiProvider
    {
        public string ProviderId => "noop";
        public bool IsAvailable => true;

        public Task<PlannerDecision> PlanAsync(PlannerRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PlannerDecision("done", "No planning during reconciliation.", null));
    }
}
