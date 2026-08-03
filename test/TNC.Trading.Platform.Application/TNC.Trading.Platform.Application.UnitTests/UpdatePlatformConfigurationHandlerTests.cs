using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.ReconcilePlatformAuthentication;
using TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;

namespace TNC.Trading.Platform.Application.UnitTests;

public sealed class UpdatePlatformConfigurationHandlerTests
{
    /// <summary>
    /// Trace: FR20, FR21. Verifies invalid use-case input is rejected before persistence or reconciliation.
    /// Expected: validation throws and neither downstream collaborator is called. Why: callers outside HTTP cannot bypass configuration invariants.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldRejectInvalidConfiguration_WithoutPersistingChanges()
    {
        var committer = new FakeCommitter();
        var reconciler = new FakeReconciler();
        var handler = CreateHandler(committer, reconciler);
        var request = new UpdatePlatformConfigurationRequest(CreateUpdate());

        await Assert.ThrowsAsync<ConfigurationValidationException>(() => handler.HandleAsync(request, CancellationToken.None));
        Assert.Empty(committer.Updates);
        Assert.Equal(0, reconciler.CallCount);
    }

    /// <summary>
    /// Trace: FR20, OR7. Verifies a valid update is committed before the explicit reconciliation command is dispatched.
    /// Expected: the handler returns the committer result, records one update intent, and invokes reconciliation once after commit. Why: runtime state must never reconcile against an update that was not durably accepted.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldCommitAndReconcile_WhenConfigurationIsValid()
    {
        var update = CreateUpdate(tradingDays: [DayOfWeek.Monday]);
        var expected = new UpdatePlatformConfigurationResult(CreateSnapshot(), RestartRequired: true);
        var committer = new FakeCommitter(expected);
        var workflow = new List<string>();
        var reconciler = new FakeReconciler(workflow);
        committer.Workflow = workflow;
        var handler = CreateHandler(committer, reconciler);

        var response = await handler.HandleAsync(new UpdatePlatformConfigurationRequest(update), CancellationToken.None);

        Assert.Same(expected, response.Result);
        Assert.Single(committer.Updates);
        Assert.Same(update, committer.Updates[0]);
        Assert.Equal(1, reconciler.CallCount);
        Assert.Equal(["commit", "reconcile"], workflow);
    }

    /// <summary>
    /// Trace: FR20, SR2, SR3. Verifies credential replacement values cross the Application boundary as update intent without exposing or rewriting omitted values.
    /// Expected: the committer receives all supplied credentials and the handler performs no credential-specific processing. Why: protected storage and presence semantics belong to the outward adapter.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldPreserveCredentialPresenceIntent_WhenCredentialsAreSupplied()
    {
        var update = CreateUpdate(
            tradingDays: [DayOfWeek.Monday],
            apiKey: "new-api-key",
            identifier: null,
            password: "new-password");
        var committer = new FakeCommitter(new UpdatePlatformConfigurationResult(CreateSnapshot(), false));
        var handler = CreateHandler(committer, new FakeReconciler());

        await handler.HandleAsync(new UpdatePlatformConfigurationRequest(update), CancellationToken.None);

        var committed = Assert.Single(committer.Updates);
        Assert.Equal("new-api-key", committed.ApiKey);
        Assert.Null(committed.Identifier);
        Assert.Equal("new-password", committed.Password);
    }

    /// <summary>
    /// Trace: Phase 0 consistency decision. Verifies cancellation while committing prevents reconciliation dispatch and preserves the caller cancellation.
    /// Expected: OperationCanceledException propagates and the command is not reported as a successful update. Why: cancellation must not produce a false post-update runtime refresh.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldPropagateCancellationWithoutReconciliation_WhenCommitIsCancelled()
    {
        using var cancellationSource = new CancellationTokenSource();
        var committer = new FakeCommitter { CancellationToken = cancellationSource.Token };
        var reconciler = new FakeReconciler();
        var handler = CreateHandler(committer, reconciler);

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.HandleAsync(new UpdatePlatformConfigurationRequest(CreateUpdate(tradingDays: [DayOfWeek.Monday])), cancellationSource.Token));

        Assert.Equal(0, reconciler.CallCount);
    }

    /// <summary>
    /// Trace: FR20, TR12. Verifies persistence failures are not translated into a successful configuration response or a reconciliation command.
    /// Expected: the original failure propagates and no later workflow step runs. Why: atomic local writes must fail closed at the Application boundary.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldPropagateCommitFailureWithoutReconciliation_WhenPersistenceFails()
    {
        var failure = new InvalidOperationException("commit failed");
        var committer = new FakeCommitter { Failure = failure };
        var reconciler = new FakeReconciler();
        var handler = CreateHandler(committer, reconciler);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new UpdatePlatformConfigurationRequest(CreateUpdate(tradingDays: [DayOfWeek.Monday])), CancellationToken.None));

        Assert.Same(failure, actual);
        Assert.Equal(0, reconciler.CallCount);
    }

    private static UpdatePlatformConfigurationHandler CreateHandler(FakeCommitter committer, FakeReconciler reconciler) =>
        new(committer, new ReconcilePlatformAuthenticationHandler(reconciler), new UpdatePlatformConfigurationValidator());

    private static PlatformConfigurationUpdate CreateUpdate(
        IReadOnlyList<DayOfWeek>? tradingDays = null,
        string? apiKey = "api-key",
        string? identifier = "identifier",
        string? password = "password")
        => new(
            PlatformEnvironmentKind.Live,
            BrokerEnvironmentKind.Demo,
            new TradingScheduleConfiguration(new TimeOnly(8, 0), new TimeOnly(16, 30), tradingDays ?? [], WeekendBehavior.ExcludeWeekends, [], "UTC"),
            new RetryPolicyConfiguration(1, 5, 2, 60, 5),
            new NotificationSettingsConfiguration("RecordedOnly", "operator@example.com"),
            apiKey,
            identifier,
            password,
            "unit-test");

    private static PlatformConfigurationSnapshot CreateSnapshot() =>
        new(
            PlatformEnvironmentKind.Live,
            BrokerEnvironmentKind.Demo,
            new TradingScheduleConfiguration(new TimeOnly(8, 0), new TimeOnly(16, 30), [DayOfWeek.Monday], WeekendBehavior.ExcludeWeekends, [], "UTC"),
            new RetryPolicyConfiguration(1, 5, 2, 60, 5),
            new NotificationSettingsConfiguration("RecordedOnly", "operator@example.com"),
            new CredentialPresence(true, true, true),
            true,
            true,
            DateTimeOffset.UtcNow,
            false);

    private sealed class FakeCommitter(UpdatePlatformConfigurationResult? result = null) : IUpdatePlatformConfigurationCommitter
    {
        private readonly UpdatePlatformConfigurationResult result = result ?? new UpdatePlatformConfigurationResult(CreateSnapshot(), false);

        public List<PlatformConfigurationUpdate> Updates { get; } = [];

        public Exception? Failure { get; init; }

        public CancellationToken CancellationToken { get; init; }

        public List<string>? Workflow { get; set; }

        public Task<UpdatePlatformConfigurationResult> CommitAsync(PlatformConfigurationUpdate update, CancellationToken cancellationToken)
        {
            Updates.Add(update);
            Workflow?.Add("commit");
            if (Failure is not null)
            {
                return Task.FromException<UpdatePlatformConfigurationResult>(Failure);
            }

            if (CancellationToken.CanBeCanceled)
            {
                return Task.FromCanceled<UpdatePlatformConfigurationResult>(CancellationToken);
            }

            return Task.FromResult(result);
        }
    }

    private sealed class FakeReconciler(List<string>? workflow = null) : IPlatformAuthenticationReconciler
    {
        public int CallCount { get; private set; }

        public Task<PlatformRuntimeState> ReconcileAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            workflow?.Add("reconcile");
            return Task.FromResult(new PlatformRuntimeState { SessionStatus = PlatformSessionStatus.Active });
        }
    }
}