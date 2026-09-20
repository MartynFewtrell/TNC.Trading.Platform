using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountPreferences;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.UnitTests.Features.AccountPreferences;

public sealed class CheckAccountPreferencesStatusHandlerTests
{
    /// <summary>Trace: FR4. Verifies an unconfigured Check is SQL-only and returns no operation.</summary>
    [Fact]
    public async Task HandleAsync_ShouldReturnUnconfiguredWithoutIgIo_WhenStateIsMissing()
    {
        var fixture = new Fixture(null);
        var result = await fixture.Handler.HandleAsync(new(), "correlation", CancellationToken.None);
        Assert.Null(result.Phase);
        Assert.Equal(0, fixture.Gateway.ObserveCallCount);
    }

    /// <summary>Trace: FR5. Verifies a session-account mismatch returns the exact warning before preference I/O.</summary>
    [Fact]
    public async Task HandleAsync_ShouldReturnExactAccountMismatchWarningWithoutPreferenceIo_WhenSessionAccountDiffers()
    {
        var fixture = new Fixture(fixtureState: Fixture.State("TARGET"), snapshotAccount: "OTHER");
        var result = await fixture.Handler.HandleAsync(new(), "correlation", CancellationToken.None);
        Assert.Equal(AccountPreferencesAccountMismatch.Detail, result.SafeReason);
        Assert.Equal(0, fixture.Gateway.ObserveCallCount);
    }

    /// <summary>Trace: FR6. Verifies equal remote and desired values converge the projection to InSync.</summary>
    [Fact]
    public async Task HandleAsync_ShouldMarkProjectionInSync_WhenIgValueEqualsDesiredValue()
    {
        var fixture = new Fixture(Fixture.State("TARGET", true));
        var result = await fixture.Handler.HandleAsync(new(), "correlation", CancellationToken.None);
        Assert.Equal(AccountPreferencesOperationPhase.Completed, result.Phase);
        Assert.Equal(AccountPreferencesVerificationStatus.InSync, result.State!.VerificationStatus);
        Assert.Equal(1, fixture.Gateway.ObserveCallCount);
    }

    /// <summary>Trace: FR7. Verifies detected drift is remediated to the persisted desired value.</summary>
    [Fact]
    public async Task HandleAsync_ShouldRemediateDriftAndConverge_WhenIgValueDiffers()
    {
        var fixture = new Fixture(Fixture.State("TARGET", true));
        fixture.Gateway.Observed = false;
        var result = await fixture.Handler.HandleAsync(new(), "correlation", CancellationToken.None);
        Assert.Equal(1, fixture.Gateway.RemediateCallCount);
        Assert.Equal(AccountPreferencesVerificationStatus.InSync, result.State!.VerificationStatus);
    }

    /// <summary>Trace: FR8. Verifies provider failure is projected durably with retry metadata and a safe reason.</summary>
    [Fact]
    public async Task HandleAsync_ShouldProjectProcessedFailure_WhenIgObservationFails()
    {
        var fixture = new Fixture(Fixture.State("TARGET", true));
        fixture.Gateway.Failure = AccountPreferencesFailureCategory.Unavailable;
        var result = await fixture.Handler.HandleAsync(new(), "correlation", CancellationToken.None);
        Assert.Equal(AccountPreferencesOperationPhase.VerificationFailed, result.Phase);
        Assert.Equal("safe failure", result.State!.FailureSummary);
        Assert.Equal(1, result.State.RetryCount);
    }

    private sealed class Fixture
    {
        public readonly FakeGateway Gateway = new();
        public readonly FakeStore Store;
        public readonly CheckAccountPreferencesStatusHandler Handler;
        public Fixture(AccountPreferencesCurrentState? fixtureState, string snapshotAccount = "TARGET") { Store = new(fixtureState); Handler = new(new PlatformConfigurationService(new Config()), new Snapshot(snapshotAccount), Store, new Operations(), Gateway, new Lease(), TimeProvider.System); }
        public static AccountPreferencesCurrentState State(string account, bool desired = false) => new(Guid.NewGuid(), PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, account, desired, 1, "operator", DateTimeOffset.UtcNow, !desired, account, DateTimeOffset.UtcNow, null, null, AccountPreferencesVerificationStatus.Drifted, null, null, 0, null, null, []);
    }
    private sealed class FakeGateway : IAccountPreferencesGateway { public bool Observed = true; public int ObserveCallCount; public int RemediateCallCount; public AccountPreferencesFailureCategory? Failure; public Task<AccountPreferencesObservationResult> ObserveAsync(AccountPreferencesObserveRequest r, CancellationToken c) { ObserveCallCount++; return Task.FromResult(new AccountPreferencesObservationResult(r.TargetAccountId, "attempt", DateTimeOffset.UtcNow, Observed, Failure, Failure is null ? null : "safe failure")); } public Task<AccountPreferencesRemediationResult> RemediateAsync(AccountPreferencesRemediateRequest r, CancellationToken c) { RemediateCallCount++; return Task.FromResult(new AccountPreferencesRemediationResult(r.TargetAccountId, "attempt", DateTimeOffset.UtcNow, r.TrailingStopsEnabled, true)); } public Task<AccountPreferencesGatewayOutcome> GetAsync(CancellationToken c) => throw new NotSupportedException(); public Task<AccountPreferencesGatewayOutcome> UpdateAsync(bool e, CancellationToken c) => throw new NotSupportedException(); }
    private sealed class FakeStore(AccountPreferencesCurrentState? state) : IAccountPreferencesCurrentStateStore { public Task<AccountPreferencesCurrentState?> GetAsync(PlatformEnvironmentKind p, BrokerEnvironmentKind b, CancellationToken c) => Task.FromResult(state); public Task<AccountPreferencesReconciliationCompletion> CompleteObservedReconciliationAsync(Guid id, long rev, AccountPreferencesCurrentState s, TrailingStopsPreferenceObservation o, CancellationToken c) => Task.FromResult(new AccountPreferencesReconciliationCompletion(true, s)); public Task<AccountPreferencesReconciliationCompletion> CompleteReconciliationAsync(Guid id, long rev, AccountPreferencesCurrentState s, CancellationToken c) => Task.FromResult(new AccountPreferencesReconciliationCompletion(true, s)); public Task<AccountPreferencesDesiredStateCommitResult> CommitDesiredStateAsync(AccountPreferencesDesiredStateChange c, CancellationToken t) => throw new NotSupportedException(); public Task<AccountPreferencesDesiredStateCommitResult> FinalizeConfirmedSaveAsync(AccountPreferencesOperation o, AccountPreferencesDesiredStateChange c, TrailingStopsPreferenceObservation x, CancellationToken t) => throw new NotSupportedException(); public Task<bool> NudgeAuthenticationAsync(PlatformEnvironmentKind a, BrokerEnvironmentKind b, string c, string d, DateTimeOffset e, DateTimeOffset f, CancellationToken g) => throw new NotSupportedException(); public Task<IReadOnlyList<AccountPreferencesCurrentState>> ClaimDueWorkAsync(DateTimeOffset a, int b, CancellationToken c) => throw new NotSupportedException(); }
    private sealed class Snapshot(string account) : IPlatformIgLoginSnapshotStore { public Task<IgLoginSnapshot?> GetLatestSnapshotAsync(BrokerEnvironmentKind b, CancellationToken c) => Task.FromResult<IgLoginSnapshot?>(new(Guid.NewGuid(), b, DateTimeOffset.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), IgLoginSnapshotKind.Latest, account, null, null, new Dictionary<string, string>(), "{}")); public Task CaptureSuccessfulSnapshotAsync(IgLoginSnapshot a, CancellationToken b) => throw new NotSupportedException(); public Task<IReadOnlyList<IgLoginSnapshot>> GetRetainedDailySnapshotsAsync(BrokerEnvironmentKind a, CancellationToken b) => throw new NotSupportedException(); }
    private sealed class Config : IPlatformConfigurationStore { public Task<PlatformConfigurationSnapshot> GetCurrentAsync(CancellationToken c) => Task.FromResult(new PlatformConfigurationSnapshot(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, null!, null!, null!, null!, false, false, DateTimeOffset.UtcNow, false)); public Task<PlatformConfigurationSnapshot> ApplyStartupConfigurationAsync(CancellationToken c) => GetCurrentAsync(c); public Task<PlatformConfigurationSnapshot> GetRuntimeAsync(PlatformEnvironmentKind? a, BrokerEnvironmentKind? b, CancellationToken c) => GetCurrentAsync(c); }
    private sealed class Operations : IAccountPreferencesOperationStore { public Task<AccountPreferencesOperation?> FindByIdempotencyKeyAsync(PlatformEnvironmentKind a, BrokerEnvironmentKind b, string c, CancellationToken d) => Task.FromResult<AccountPreferencesOperation?>(null); public Task<AccountPreferencesOperation> StartAsync(AccountPreferencesOperation a, CancellationToken b) => Task.FromResult(a); public Task<AccountPreferencesOperation?> SetPhaseAsync(Guid a, AccountPreferencesOperationPhase b, DateTimeOffset c, CancellationToken d) => Task.FromResult<AccountPreferencesOperation?>(null); public Task<IReadOnlyList<AccountPreferencesOperation>> FindRemoteAppliedAsync(PlatformEnvironmentKind a, BrokerEnvironmentKind b, string c, CancellationToken d) => Task.FromResult<IReadOnlyList<AccountPreferencesOperation>>([]); }
    private sealed class Lease : IAccountPreferencesReconciliationLease { public Task<IAsyncDisposable?> AcquireAsync(PlatformEnvironmentKind a, BrokerEnvironmentKind b, string c, CancellationToken d) => Task.FromResult<IAsyncDisposable?>(new Noop()); } private sealed class Noop : IAsyncDisposable { public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
}