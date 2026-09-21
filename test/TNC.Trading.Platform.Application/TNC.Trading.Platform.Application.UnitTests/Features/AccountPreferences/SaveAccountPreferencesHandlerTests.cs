using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountPreferences;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.UnitTests.Features.AccountPreferences;

public sealed class SaveAccountPreferencesHandlerTests
{
    /// <summary>Trace: FR1. Verifies Save confirms the IG value before finalising the SQL projection.</summary>
    [Fact]
    public async Task HandleAsync_ShouldConfirmWithIgBeforePersisting_WhenSaveIsRequested()
    {
        var fixture = new Fixture();
        var result = await fixture.Handler.HandleAsync(new(true, 4, "key-1"), "operator", "correlation", CancellationToken.None);
        Assert.Equal(AccountPreferencesOperationPhase.Completed, result.Phase);
        Assert.Equal(1, fixture.Gateway.RemediateCallCount);
        Assert.Equal(1, fixture.Store.FinalizeCallCount);
    }

    /// <summary>Trace: FR2. Verifies identical idempotent commands replay without another IG write.</summary>
    [Fact]
    public async Task HandleAsync_ShouldReplayMatchingIdempotencyKey_WhenCommandWasAlreadyCompleted()
    {
        var fixture = new Fixture();
        fixture.ExistingOperation = fixture.Operation(AccountPreferencesOperationPhase.Completed);
        var result = await fixture.Handler.HandleAsync(new(true, 4, "key-1"), "operator", "correlation", CancellationToken.None);
        Assert.Equal(AccountPreferencesOperationPhase.Completed, result.Phase);
        Assert.Equal(0, fixture.Gateway.RemediateCallCount);
    }

    /// <summary>Trace: FR5. Verifies a durable-target mismatch returns the safe typed reason without changing state or contacting IG.</summary>
    [Fact]
    public async Task HandleAsync_ShouldReturnAccountMismatchWithoutPreferenceIo_WhenDurableTargetDiffers()
    {
        var fixture = new Fixture();
        fixture.Store.State = fixture.Store.State! with { AccountId = "OTHER" };

        var result = await fixture.Handler.HandleAsync(new(true, 4, "key-1"), "operator", "correlation", CancellationToken.None);

        Assert.Equal(AccountPreferencesAccountMismatch.Category, result.FailureCategory);
        Assert.Equal(AccountPreferencesAccountMismatch.Detail, result.SafeReason);
        Assert.True(result.Conflict);
        Assert.Equal("OTHER", result.State!.AccountId);
        Assert.Equal(0, fixture.Gateway.RemediateCallCount);
        Assert.Equal(0, fixture.Store.FinalizeCallCount);
    }

    /// <summary>Trace: FR2. Verifies incompatible idempotency reuse conflicts before gateway I/O.</summary>
    [Fact]
    public async Task HandleAsync_ShouldReturnConflictBeforeGatewayIo_WhenIdempotencyKeyIsIncompatible()
    {
        var fixture = new Fixture();
        fixture.ExistingOperation = fixture.Operation(AccountPreferencesOperationPhase.Completed) with { RequestedTrailingStopsEnabled = false };
        var result = await fixture.Handler.HandleAsync(new(true, 4, "key-1"), "operator", "correlation", CancellationToken.None);
        Assert.True(result.Conflict);
        Assert.Equal(AccountPreferencesFailureCategory.Rejected, result.FailureCategory);
        Assert.Equal(0, fixture.Gateway.RemediateCallCount);
    }

    /// <summary>Trace: FR3. Verifies an indeterminate provider response is returned as unknown without retry.</summary>
    [Fact]
    public async Task HandleAsync_ShouldReturnUnknownOutcomeWithoutRetry_WhenIgResultIsIndeterminate()
    {
        var fixture = new Fixture(true);
        var result = await fixture.Handler.HandleAsync(new(true, 4, "key-1"), "operator", "correlation", CancellationToken.None);
        Assert.True(result.OutcomeUnknown);
        Assert.Equal(1, fixture.Store.FinalizeCallCount);
        Assert.Equal(1, fixture.Gateway.RemediateCallCount);
    }

    private sealed class Fixture
    {
        public FakeGateway Gateway { get; init; } = new();
        public FakeStore Store { get; }
        public AccountPreferencesOperation? ExistingOperation { get; set; }
        public SaveAccountPreferencesHandler Handler { get; }
        public Fixture(bool throwOnFinalize = false) { Store = new() { ThrowOnFinalize = throwOnFinalize }; Store.State = new(Guid.NewGuid(), PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, "IG-ACCOUNT", false, 4, "operator", DateTimeOffset.UtcNow, false, "IG-ACCOUNT", DateTimeOffset.UtcNow, null, null, AccountPreferencesVerificationStatus.Drifted, null, null, 0, null, null, []); Handler = new(new PlatformConfigurationService(new FakeConfigurationStore()), new FakeSnapshotStore(), Store, new FakeOperationStore(this), Gateway, new FakeLease(), TimeProvider.System); }
        public AccountPreferencesOperation Operation(AccountPreferencesOperationPhase phase) => new(Guid.NewGuid(), "key-1", PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, "IG-ACCOUNT", 4, true, "operator", "correlation", phase, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    }

    private sealed class FakeGateway : IAccountPreferencesGateway
    {
        public AccountPreferencesRemediationResult Remediation { get; init; } = new("IG-ACCOUNT", "attempt", DateTimeOffset.UtcNow, true, false);
        public int RemediateCallCount { get; private set; }
        public Task<AccountPreferencesRemediationResult> RemediateAsync(AccountPreferencesRemediateRequest request, CancellationToken cancellationToken) { RemediateCallCount++; return Task.FromResult(Remediation); }
        public Task<AccountPreferencesObservationResult> ObserveAsync(AccountPreferencesObserveRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AccountPreferencesGatewayOutcome> GetAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AccountPreferencesGatewayOutcome> UpdateAsync(bool trailingStopsEnabled, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeStore : IAccountPreferencesCurrentStateStore
    {
        public int FinalizeCallCount { get; private set; }
        public AccountPreferencesCurrentState? State { get; set; }
        public bool ThrowOnFinalize { get; init; }
        public Task<AccountPreferencesCurrentState?> GetAsync(PlatformEnvironmentKind p, BrokerEnvironmentKind b, CancellationToken c) => Task.FromResult(State);
        public Task<AccountPreferencesDesiredStateCommitResult> FinalizeConfirmedSaveAsync(AccountPreferencesOperation o, AccountPreferencesDesiredStateChange c, TrailingStopsPreferenceObservation x, CancellationToken t) { FinalizeCallCount++; if (ThrowOnFinalize) throw new InvalidOperationException(); return Task.FromResult(new AccountPreferencesDesiredStateCommitResult(true, 5, new(Guid.NewGuid(), o.PlatformEnvironment, o.BrokerEnvironment, o.AccountId, c.TrailingStopsEnabled, 5, c.Actor, c.ChangedAtUtc, c.TrailingStopsEnabled, o.AccountId, c.ChangedAtUtc, null, null, AccountPreferencesVerificationStatus.InSync, c.ChangedAtUtc, null, 0, null, c.CorrelationId, []))); }
        public Task<AccountPreferencesDesiredStateCommitResult> CommitDesiredStateAsync(AccountPreferencesDesiredStateChange c, CancellationToken t) => throw new NotSupportedException();
        public Task<AccountPreferencesReconciliationCompletion> CompleteObservedReconciliationAsync(Guid a, long b, AccountPreferencesCurrentState c, TrailingStopsPreferenceObservation d, CancellationToken e) => throw new NotSupportedException();
        public Task<bool> NudgeAuthenticationAsync(PlatformEnvironmentKind a, BrokerEnvironmentKind b, string c, string d, DateTimeOffset e, DateTimeOffset f, CancellationToken g) => throw new NotSupportedException();
        public Task<IReadOnlyList<AccountPreferencesCurrentState>> ClaimDueWorkAsync(DateTimeOffset a, int b, CancellationToken c) => throw new NotSupportedException();
        public Task<AccountPreferencesReconciliationCompletion> CompleteReconciliationAsync(Guid a, long b, AccountPreferencesCurrentState c, CancellationToken d) => throw new NotSupportedException();
    }

    private sealed class FakeOperationStore(Fixture f) : IAccountPreferencesOperationStore
    { public Task<AccountPreferencesOperation?> FindByIdempotencyKeyAsync(PlatformEnvironmentKind a, BrokerEnvironmentKind b, string c, CancellationToken d) => Task.FromResult(f.ExistingOperation); public Task<AccountPreferencesOperation> StartAsync(AccountPreferencesOperation a, CancellationToken b) => Task.FromResult(a); public Task<AccountPreferencesOperation?> SetPhaseAsync(Guid a, AccountPreferencesOperationPhase b, DateTimeOffset c, CancellationToken d) => Task.FromResult<AccountPreferencesOperation?>(null); public Task<IReadOnlyList<AccountPreferencesOperation>> FindRemoteAppliedAsync(PlatformEnvironmentKind a, BrokerEnvironmentKind b, string c, CancellationToken d) => Task.FromResult<IReadOnlyList<AccountPreferencesOperation>>([]); }
    private sealed class FakeSnapshotStore : IPlatformIgLoginSnapshotStore { public Task<IgLoginSnapshot?> GetLatestSnapshotAsync(BrokerEnvironmentKind b, CancellationToken c) => Task.FromResult<IgLoginSnapshot?>(new(Guid.NewGuid(), b, DateTimeOffset.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), IgLoginSnapshotKind.Latest, "IG-ACCOUNT", null, null, new Dictionary<string, string>(), "{}")); public Task CaptureSuccessfulSnapshotAsync(IgLoginSnapshot a, CancellationToken b) => throw new NotSupportedException(); public Task<IReadOnlyList<IgLoginSnapshot>> GetRetainedDailySnapshotsAsync(BrokerEnvironmentKind a, CancellationToken b) => throw new NotSupportedException(); }
    private sealed class FakeConfigurationStore : IPlatformConfigurationStore { public Task<PlatformConfigurationSnapshot> GetCurrentAsync(CancellationToken c) => Task.FromResult(new PlatformConfigurationSnapshot(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, null!, null!, null!, null!, false, false, DateTimeOffset.UtcNow, false)); public Task<PlatformConfigurationSnapshot> ApplyStartupConfigurationAsync(CancellationToken c) => GetCurrentAsync(c); public Task<PlatformConfigurationSnapshot> GetRuntimeAsync(PlatformEnvironmentKind? a, BrokerEnvironmentKind? b, CancellationToken c) => GetCurrentAsync(c); }
    private sealed class FakeLease : IAccountPreferencesReconciliationLease { public Task<IAsyncDisposable?> AcquireAsync(PlatformEnvironmentKind a, BrokerEnvironmentKind b, string c, CancellationToken d) => Task.FromResult<IAsyncDisposable?>(new Lease()); }
    private sealed class Lease : IAsyncDisposable { public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
}