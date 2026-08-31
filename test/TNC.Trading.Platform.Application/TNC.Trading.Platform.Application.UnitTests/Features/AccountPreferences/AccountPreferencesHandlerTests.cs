using System.Text.Json;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountPreferences;
using TNC.Trading.Platform.Application.Services;
using AccountPreferencesModel = TNC.Trading.Platform.Application.Features.AccountPreferences.AccountPreferences;

namespace TNC.Trading.Platform.Application.UnitTests.Features.AccountPreferencesTests;

public sealed class AccountPreferencesHandlerTests
{
    /// <summary>
    /// Trace: Phase 2.4, environment safety. Verifies Test permits the provider read while Live is rejected before any gateway call.
    /// Expected: Test returns the configured provider result; Live returns UnsupportedEnvironment and makes no provider call, protecting live trading.
    /// </summary>
    [Fact]
    public async Task GetAccountPreferences_ShouldAllowTestAndRejectLive_WhenEnvironmentChanges()
    {
        var testFixture = new Fixture(BrokerEnvironmentKind.Demo);
        testFixture.Gateway.GetResults.Enqueue(new AccountPreferencesGatewayOutcome.Succeeded(testFixture.Preferences(true)));

        var testResult = await testFixture.Get.HandleAsync(new GetAccountPreferencesRequest("operator", "test-correlation"), CancellationToken.None);

        Assert.IsType<AccountPreferencesGatewayOutcome.Succeeded>(testResult.Outcome);
        Assert.Equal(1, testFixture.Gateway.GetCallCount);

        var liveFixture = new Fixture(BrokerEnvironmentKind.Live);
        var liveResult = await liveFixture.Get.HandleAsync(new GetAccountPreferencesRequest("operator", "live-correlation"), CancellationToken.None);

        var failure = Assert.IsType<AccountPreferencesGatewayOutcome.Failed>(liveResult.Outcome);
        Assert.Equal(AccountPreferencesFailureCategory.UnsupportedEnvironment, failure.Category);
        Assert.Equal(0, liveFixture.Gateway.GetCallCount);
        Assert.Empty(liveFixture.Store.Observations);
    }

    /// <summary>
    /// Trace: Account preferences load-performance mitigation Phase 1, Steps 1.1-1.3. Verifies a missing SQL projection is a valid unconfigured result.
    /// Expected: the handler returns explicit Unconfigured query state and does not call the provider, preventing a fresh database from becoming a retryable outage.
    /// </summary>
    [Fact]
    public async Task GetAccountPreferences_ShouldReturnUnconfiguredWithoutGatewayCall_WhenCurrentStateRowIsMissing()
    {
        var fixture = new Fixture(BrokerEnvironmentKind.Demo, new FakeCurrentStateStore(null));

        var result = await fixture.Get.HandleAsync(new GetAccountPreferencesRequest(), CancellationToken.None);

        Assert.IsType<AccountPreferencesQueryState.Unconfigured>(result.QueryState);
        Assert.Null(result.State);
        Assert.Equal(0, fixture.Gateway.GetCallCount);
    }

    /// <summary>
    /// Trace: Account preferences load-performance mitigation Phase 1, Steps 1.1-1.3. Verifies a populated SQL projection remains authoritative for the query.
    /// Expected: the complete durable state is returned as Configured and the provider is not called, keeping the initial read independent of IG availability.
    /// </summary>
    [Fact]
    public async Task GetAccountPreferences_ShouldReturnConfiguredStateWithoutGatewayCall_WhenCurrentStateRowIsPopulated()
    {
        var fixture = new Fixture(BrokerEnvironmentKind.Demo);
        var expectedState = new AccountPreferencesCurrentState(Guid.NewGuid(), PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, "account", true, 7, "operator", fixture.Now, false, "account", fixture.Now.AddMinutes(1), "snapshot", "attempt", AccountPreferencesVerificationStatus.Drifted, fixture.Now.AddMinutes(2), fixture.Now.AddMinutes(3), 2, "safe failure", "correlation", [1, 2, 3]);
        fixture = new Fixture(BrokerEnvironmentKind.Demo, new FakeCurrentStateStore(expectedState));

        var result = await fixture.Get.HandleAsync(new GetAccountPreferencesRequest(), CancellationToken.None);

        var configured = Assert.IsType<AccountPreferencesQueryState.Configured>(result.QueryState);
        Assert.Same(expectedState, configured.State);
        Assert.Same(expectedState, result.State);
        Assert.Equal(0, fixture.Gateway.GetCallCount);
    }

    /// <summary>
    /// Trace: Phase 2.4, authoritative confirmation. Verifies a successful PUT is followed by a GET and only the GET state is observed.
    /// Expected: the response and observation contain the authoritative GET value, guarding against trusting an unconfirmed write response.
    /// </summary>
    [Fact]
    public async Task UpdateAccountPreferences_ShouldReturnNotAppliedAndPersistObservation_WhenReadbackDiffers()
    {
        var fixture = new Fixture(BrokerEnvironmentKind.Demo);
        fixture.Gateway.UpdateResults.Enqueue(new AccountPreferencesGatewayOutcome.Succeeded(fixture.Preferences(false)));
        fixture.Gateway.GetResults.Enqueue(new AccountPreferencesGatewayOutcome.Succeeded(fixture.Preferences(true)));

        var result = await fixture.Update.HandleAsync(new UpdateAccountPreferencesRequest(false, "operator", "correlation"), CancellationToken.None);

        var notApplied = Assert.IsType<AccountPreferencesGatewayOutcome.NotApplied>(result.Outcome);
        Assert.False(notApplied.RequestedTrailingStopsEnabled);
        Assert.True(notApplied.ObservedPreferences.TrailingStopsEnabled);
        Assert.Equal(1, fixture.Gateway.UpdateCallCount);
        Assert.Equal(1, fixture.Gateway.GetCallCount);
        Assert.Equal("ReadObserved", Assert.Single(fixture.Store.Observations).ObservationKind);
        Assert.True(fixture.Store.Observations[0].TrailingStopsEnabled);
        Assert.Equal("UpdateNotApplied", Assert.Single(fixture.Events.Records).EventType);
    }

    /// <summary>
    /// Trace: Phase 2.4, reconciliation. Verifies an indeterminate PUT is reconciled with one GET and the PUT is never retried.
    /// Expected: the GET result is returned and recorded as ReconciliationObserved, preventing duplicate provider writes after uncertain delivery.
    /// </summary>
    [Fact]
    public async Task UpdateAccountPreferences_ShouldReconcileWithGetWithoutRetry_WhenPutIsIndeterminate()
    {
        var fixture = new Fixture(BrokerEnvironmentKind.Demo);
        fixture.Gateway.UpdateResults.Enqueue(new AccountPreferencesGatewayOutcome.Indeterminate(AccountPreferencesFailureCategory.Unavailable, "provider body secret"));
        fixture.Gateway.GetResults.Enqueue(new AccountPreferencesGatewayOutcome.Succeeded(fixture.Preferences(true)));

        var result = await fixture.Update.HandleAsync(new UpdateAccountPreferencesRequest(true), CancellationToken.None);

        Assert.IsType<AccountPreferencesGatewayOutcome.Succeeded>(result.Outcome);
        Assert.Equal(1, fixture.Gateway.UpdateCallCount);
        Assert.Equal(1, fixture.Gateway.GetCallCount);
        Assert.Equal("ReconciliationObserved", Assert.Single(fixture.Store.Observations).ObservationKind);
    }

    /// <summary>
    /// Trace: Phase 2.4, observation integrity. Verifies failed and indeterminate terminal operations do not create preference observations.
    /// Expected: no observation rows are appended when PUT or reconciliation cannot establish a successful authoritative state.
    /// </summary>
    [Fact]
    public async Task UpdateAccountPreferences_ShouldCreateNoObservation_WhenPutOrReconciliationFails()
    {
        var failedFixture = new Fixture(BrokerEnvironmentKind.Demo);
        failedFixture.Gateway.UpdateResults.Enqueue(new AccountPreferencesGatewayOutcome.Failed(AccountPreferencesFailureCategory.Unavailable, "secret provider response"));
        await failedFixture.Update.HandleAsync(new UpdateAccountPreferencesRequest(true), CancellationToken.None);

        var indeterminateFixture = new Fixture(BrokerEnvironmentKind.Demo);
        indeterminateFixture.Gateway.UpdateResults.Enqueue(new AccountPreferencesGatewayOutcome.Indeterminate(AccountPreferencesFailureCategory.Unavailable, "secret provider response"));
        indeterminateFixture.Gateway.GetResults.Enqueue(new AccountPreferencesGatewayOutcome.Indeterminate(AccountPreferencesFailureCategory.Unavailable, "diagnostic secret"));
        await indeterminateFixture.Update.HandleAsync(new UpdateAccountPreferencesRequest(true), CancellationToken.None);

        Assert.Empty(failedFixture.Store.Observations);
        Assert.Empty(indeterminateFixture.Store.Observations);
    }

    /// <summary>
    /// Trace: Phase 2.4, idempotent history. Verifies repeated successful writes of the same value remain distinct observations.
    /// Expected: two confirmed rows are retained, preserving an auditable record of each successful operation rather than silently deduplicating history.
    /// </summary>
    [Fact]
    public async Task UpdateAccountPreferences_ShouldRecordRepeatedEqualSuccessfulValues_WhenSameValueIsConfirmed()
    {
        var fixture = new Fixture(BrokerEnvironmentKind.Demo);
        fixture.Gateway.UpdateResults.Enqueue(new AccountPreferencesGatewayOutcome.Succeeded(fixture.Preferences(true)));
        fixture.Gateway.UpdateResults.Enqueue(new AccountPreferencesGatewayOutcome.Succeeded(fixture.Preferences(true)));
        fixture.Gateway.GetResults.Enqueue(new AccountPreferencesGatewayOutcome.Succeeded(fixture.Preferences(true)));
        fixture.Gateway.GetResults.Enqueue(new AccountPreferencesGatewayOutcome.Succeeded(fixture.Preferences(true)));

        await fixture.Update.HandleAsync(new UpdateAccountPreferencesRequest(true), CancellationToken.None);
        await fixture.Update.HandleAsync(new UpdateAccountPreferencesRequest(true), CancellationToken.None);

        Assert.Equal(2, fixture.Store.Observations.Count);
        Assert.All(fixture.Store.Observations, observation => Assert.True(observation.TrailingStopsEnabled));
    }

    /// <summary>
    /// Trace: Phase 2.4, telemetry safety. Verifies failure telemetry is bounded and contains neither secrets, provider bodies, nor arbitrary diagnostics.
    /// Expected: the sanitized event contains only the safe category and bounded reason, preventing sensitive provider data from entering the event store.
    /// </summary>
    [Fact]
    public async Task GetAccountPreferences_ShouldEmitSanitizedBoundedTelemetry_WhenProviderFails()
    {
        var fixture = new Fixture(BrokerEnvironmentKind.Demo);
        var safeReason = "provider unavailable " + new string('x', 300);
        await GetAccountPreferencesHandler.RecordFailureAsync("GetFailed", AccountPreferencesFailureCategory.Unavailable, safeReason, new GetAccountPreferencesRequest("operator", "correlation"), fixture.ConfigurationSnapshot, fixture.Now, CancellationToken.None, fixture.Events);

        var record = Assert.Single(fixture.Events.Records);
        var details = JsonSerializer.Serialize(record.Details);
        Assert.DoesNotContain(new string('x', 161), details, StringComparison.Ordinal);
        Assert.Contains("Unavailable", details, StringComparison.Ordinal);
        Assert.True(details.Length < 300);
    }

    /// <summary>
    /// Trace: Phase 2.4, deterministic history paging. Verifies page size clamping and cursor forwarding are deterministic under the current history contract.
    /// Expected: the requested cursor and bounded page size reach the store, and a full page returns the last observation ID as the next cursor.
    /// </summary>
    [Fact]
    public async Task GetTrailingStopsPreferenceObservations_ShouldReturnDeterministicPageCursor_WhenPageIsFull()
    {
        var fixture = new Fixture(BrokerEnvironmentKind.Demo);
        var first = fixture.Observation(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var second = fixture.Observation(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        fixture.Store.ListResult = new([first, second], second.Id.ToString("N"), false);

        var result = await fixture.History.HandleAsync(new GetTrailingStopsPreferenceObservationsRequest(2, "cursor-1"), CancellationToken.None);

        Assert.Equal("cursor-1", fixture.Store.LastCursor);
        Assert.Equal(2, fixture.Store.LastPageSize);
        Assert.Equal([first, second], result.Observations);
        Assert.Equal(second.Id.ToString("N"), result.NextCursor);
        Assert.False(result.HasInvalidCursor);
    }

    private sealed class Fixture
    {
        public readonly FakeGateway Gateway = new();
        public readonly FakeObservationStore Store = new();
        public readonly FakeEventStore Events = new();
        public readonly UpdateAccountPreferencesHandler Update;
        public readonly GetAccountPreferencesHandler Get;
        public readonly GetTrailingStopsPreferenceObservationsHandler History;
        public readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
        public readonly PlatformConfigurationSnapshot ConfigurationSnapshot;

        public Fixture(BrokerEnvironmentKind brokerEnvironment, IAccountPreferencesCurrentStateStore? currentStateStore = null)
        {
            var configuration = new PlatformConfigurationService(new FakeConfigurationStore(brokerEnvironment));
            ConfigurationSnapshot = new FakeConfigurationStore(brokerEnvironment).Snapshot;
            var timeProvider = new FixedTimeProvider(Now);
            Update = new(configuration, Gateway, Store, Events, timeProvider);
            Get = new(configuration, Gateway, Store, Events, timeProvider, currentStateStore);
            History = new(configuration, Store);
        }

        public AccountPreferencesModel Preferences(bool enabled) => new(enabled, "OK", Now.AddMinutes(-1));
        public TrailingStopsPreferenceObservation Observation(Guid id) => new(id, true, Now, Now, PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, null, "ReadObserved", "AccountPreferences", null, id.ToString("N"));
    }

    private sealed class FakeCurrentStateStore(AccountPreferencesCurrentState? result) : IAccountPreferencesCurrentStateStore
    {
        public Task<AccountPreferencesCurrentState?> GetAsync(PlatformEnvironmentKind platformEnvironment, BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken) => Task.FromResult(result);
        public Task<AccountPreferencesDesiredStateCommitResult> CommitDesiredStateAsync(AccountPreferencesDesiredStateChange change, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> NudgeAuthenticationAsync(PlatformEnvironmentKind platformEnvironment, BrokerEnvironmentKind brokerEnvironment, string accountId, string authenticationSnapshotId, DateTimeOffset authenticatedAtUtc, DateTimeOffset dueAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<AccountPreferencesCurrentState>> ClaimDueWorkAsync(DateTimeOffset nowUtc, int take, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AccountPreferencesReconciliationCompletion> CompleteReconciliationAsync(Guid stateId, long desiredRevision, AccountPreferencesCurrentState state, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeGateway : IAccountPreferencesGateway
    {
        public readonly Queue<AccountPreferencesGatewayOutcome> GetResults = new();
        public readonly Queue<AccountPreferencesGatewayOutcome> UpdateResults = new();
        public int GetCallCount;
        public int UpdateCallCount;
        public Task<AccountPreferencesObservationResult> ObserveAsync(AccountPreferencesObserveRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AccountPreferencesObservationResult(request.TargetAccountId, "test-attempt", DateTimeOffset.UtcNow, null));
        public Task<AccountPreferencesRemediationResult> RemediateAsync(AccountPreferencesRemediateRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AccountPreferencesRemediationResult(request.TargetAccountId, "test-attempt", DateTimeOffset.UtcNow, request.TrailingStopsEnabled, false));
        public Task<AccountPreferencesGatewayOutcome> GetAsync(CancellationToken cancellationToken) { GetCallCount++; return Task.FromResult(GetResults.Dequeue()); }
        public Task<AccountPreferencesGatewayOutcome> UpdateAsync(bool trailingStopsEnabled, CancellationToken cancellationToken) { UpdateCallCount++; return Task.FromResult(UpdateResults.Dequeue()); }
    }

    private sealed class FakeObservationStore : ITrailingStopsPreferenceObservationStore
    {
        public readonly List<TrailingStopsPreferenceObservation> Observations = [];
        public TrailingStopsPreferenceObservationPage ListResult { get; set; } = new([], null, false);
        public string? LastCursor;
        public int LastPageSize;
        public Task AppendAsync(TrailingStopsPreferenceObservation observation, CancellationToken cancellationToken) { Observations.Add(observation); return Task.CompletedTask; }
        public Task<TrailingStopsPreferenceObservationPage> ListAsync(PlatformEnvironmentKind platformEnvironment, BrokerEnvironmentKind brokerEnvironment, string? cursor, int pageSize, CancellationToken cancellationToken) { LastCursor = cursor; LastPageSize = pageSize; return Task.FromResult(ListResult); }
    }

    private sealed class FakeConfigurationStore(BrokerEnvironmentKind brokerEnvironment) : IPlatformConfigurationStore
    {
        public Task<PlatformConfigurationSnapshot> ApplyStartupConfigurationAsync(CancellationToken cancellationToken) => Task.FromResult(Current);
        public Task<PlatformConfigurationSnapshot> GetCurrentAsync(CancellationToken cancellationToken) => Task.FromResult(Current);
        public Task<PlatformConfigurationSnapshot> GetRuntimeAsync(PlatformEnvironmentKind? platformEnvironment, BrokerEnvironmentKind? requestedBrokerEnvironment, CancellationToken cancellationToken) => Task.FromResult(Current);
        public PlatformConfigurationSnapshot Snapshot => Current;
        private PlatformConfigurationSnapshot Current => new(PlatformEnvironmentKind.Test, brokerEnvironment, new TradingScheduleConfiguration(new TimeOnly(0), new TimeOnly(23, 59), [], WeekendBehavior.IncludeFullWeekend, [], "UTC"), new RetryPolicyConfiguration(1, 1, 1, 1, 1), new NotificationSettingsConfiguration("None", null), new CredentialPresence(false, false, false, false, false, false), false, false, DateTimeOffset.UtcNow, false);
    }

    private sealed class FakeEventStore : IPlatformEventStore
    {
        public readonly List<PlatformEventRecord> Records = [];
        public Task<IReadOnlyList<OperationalEventModel>> GetEventsAsync(string? category, string? environment, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OperationalEventModel>>([]);
        public Task AddAsync(PlatformEventRecord record, CancellationToken cancellationToken) { Records.Add(record); return Task.CompletedTask; }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
