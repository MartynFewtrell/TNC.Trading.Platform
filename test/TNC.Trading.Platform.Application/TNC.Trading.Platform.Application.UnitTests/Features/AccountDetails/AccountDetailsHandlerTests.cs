using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountDetails;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.UnitTests.Features.AccountDetails;

public sealed class AccountDetailsHandlerTests
{
    /// <summary>
    /// Trace: Account Details Phase 1.3 validation requirement.
    /// Verifies: malformed cursors are rejected without querying or changing persisted state.
    /// Expected: an empty result and no store or provider calls.
    /// Why: input validation must be complete before any data access occurs.
    /// </summary>
    [Theory]
    [InlineData("not-a-cursor")]
    [InlineData("1:not-a-guid")]
    [InlineData(":00000000000000000000000000000000")]
    public async Task GetAsync_ShouldRejectInvalidCursorWithoutReadsOrWrites(string cursor)
    {
        var fixture = new Fixture();

        var result = await fixture.Get.HandleAsync(new GetAccountDetailsRequest(cursor), CancellationToken.None);

        Assert.Null(result.Retrieval);
        Assert.Equal(0, fixture.Store.ReadCount);
        Assert.Empty(fixture.Store.Snapshots);
    }

    /// <summary>
    /// Trace: Account Details Phase 1.1 no-provider-read requirement.
    /// Verifies: history reads use only the snapshot store.
    /// Expected: the saved retrieval and adjacent cursors are returned while the gateway remains untouched.
    /// Why: Viewer requests must never establish or use a provider session.
    /// </summary>
    [Fact]
    public async Task GetAsync_ShouldTraverseCompositeCursorWithoutCallingGateway()
    {
        var fixture = new Fixture();
        var older = fixture.Snapshot(fixture.UtcNow.AddMinutes(-2), fixture.TradingDay);
        var current = fixture.Snapshot(fixture.UtcNow.AddMinutes(-1), fixture.TradingDay);
        var newer = fixture.Snapshot(fixture.UtcNow, fixture.TradingDay);
        fixture.Store.Snapshots.AddRange([older, current, newer]);

        var result = await fixture.Get.HandleAsync(new GetAccountDetailsRequest(new AccountDetailsCursor(current.RetrievedAtUtc, current.RetrievalId).Encode()), CancellationToken.None);

        Assert.Equal(older, result.Retrieval);
        Assert.Null(result.OlderCursor);
        Assert.Equal(new AccountDetailsCursor(current.RetrievedAtUtc, current.RetrievalId).Encode(), result.NewerCursor);
        Assert.Equal(0, fixture.Gateway.CallCount);
    }

    /// <summary>
    /// Trace: Account Details Phase 1.3 coalescing requirement.
    /// Verifies: concurrent manual requests in one process share one gateway call and one save.
    /// Expected: both callers observe a saved retrieval.
    /// Why: duplicate operator clicks must not create competing provider sessions.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_ShouldCoalesceConcurrentManualRequests()
    {
        var fixture = new Fixture();
        fixture.Gateway.WaitForRelease = true;
        var first = fixture.Refresh.HandleAsync(new RefreshAccountDetailsRequest(), CancellationToken.None);
        await fixture.Gateway.Started.Task;
        var second = fixture.Refresh.HandleAsync(new RefreshAccountDetailsRequest(), CancellationToken.None);
        fixture.Gateway.Release();

        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.IsType<AccountDetailsRefreshOutcome.Saved>(result.Outcome));
        Assert.Equal(1, fixture.Gateway.CallCount);
        Assert.Single(fixture.Store.Snapshots);
    }

    /// <summary>
    /// Trace: Account Details Phase 1.2 trigger-specific lease behavior.
    /// Verifies: automatic contention yields while manual contention reports in-progress and latest retrieval.
    /// Expected: typed Deferred and RefreshInProgress outcomes respectively.
    /// Why: automatic capture must not delay login, while operators need actionable contention state.
    /// </summary>
    [Fact]
    public async Task CaptureAutomaticAsync_ShouldYieldLeaseWhileManualRefreshReportsProgress()
    {
        var fixture = new Fixture();
        fixture.Lease.Result = new AccountDetailsRefreshLeaseResult(false, fixture.UtcNow);

        var automatic = await fixture.Refresh.CaptureAutomaticAsync(CancellationToken.None);
        var manual = await fixture.Refresh.HandleAsync(new RefreshAccountDetailsRequest(), CancellationToken.None);

        Assert.IsType<AccountDetailsRefreshOutcome.Deferred>(automatic);
        var progress = Assert.IsType<AccountDetailsRefreshOutcome.RefreshInProgress>(manual.Outcome);
        Assert.Equal(fixture.Lease.Result.LatestRetrievedAtUtc, progress.LatestRetrievedAtUtc);
    }

    /// <summary>
    /// Trace: Account Details Phase 1.2 daily de-duplication and retry requirement.
    /// Verifies: an existing day is skipped, but a failed automatic capture can be retried later.
    /// Expected: duplicate day is deferred and a failed first attempt is followed by a provider call on retry.
    /// Why: daily capture is at-most-once only after durable success, not after failure.
    /// </summary>
    [Fact]
    public async Task CaptureAutomaticAsync_ShouldDeduplicateSuccessAndRetryAfterFailure()
    {
        var fixture = new Fixture();
        fixture.Store.Snapshots.Add(fixture.Snapshot(fixture.UtcNow, fixture.TradingDay, AccountDetailsTriggerSource.Automatic));

        var duplicate = await fixture.Refresh.CaptureAutomaticAsync(CancellationToken.None);
        Assert.IsType<AccountDetailsRefreshOutcome.Deferred>(duplicate);
        Assert.Equal(0, fixture.Gateway.CallCount);

        fixture.Store.Snapshots.Clear();
        fixture.Gateway.Result = new AccountDetailsGatewayResult.Failed(AccountDetailsFailureCategory.Unavailable, "secret payload");
        var failed = await fixture.Refresh.CaptureAutomaticAsync(CancellationToken.None);
        fixture.Gateway.Result = new AccountDetailsGatewayResult.Succeeded([fixture.Account]);
        var retried = await fixture.Refresh.CaptureAutomaticAsync(CancellationToken.None);

        Assert.IsType<AccountDetailsRefreshOutcome.Failed>(failed);
        Assert.IsType<AccountDetailsRefreshOutcome.Saved>(retried);
        Assert.Equal(2, fixture.Gateway.CallCount);
    }

    /// <summary>
    /// Trace: Account Details Phase 1.2 bounded diagnostic requirement.
    /// Verifies: failed automatic capture emits one fixed, redacted operational event.
    /// Expected: the event contains category and trigger only, with no provider detail.
    /// Why: operators need a bounded signal without credential, token, or raw payload leakage.
    /// </summary>
    [Fact]
    public async Task CaptureDailyAsync_ShouldEmitBoundedRedactedFailureEvent()
    {
        var fixture = new Fixture();
        fixture.Gateway.Result = new AccountDetailsGatewayResult.Failed(AccountDetailsFailureCategory.Unavailable, "secret payload");
        var daily = new CaptureDailyAccountDetailsHandler(fixture.Refresh, fixture.Configuration, fixture.Events, new Fixture.FixedTimeProvider(fixture.UtcNow));

        var result = await daily.HandleAsync(new CaptureDailyAccountDetailsRequest(), CancellationToken.None);

        Assert.IsType<AccountDetailsRefreshOutcome.Failed>(result.Outcome);
        var record = Assert.Single(fixture.Events.Records);
        Assert.Equal("AccountDetailsCaptureFailed", record.EventType);
        Assert.Equal("Automatic account details capture failed.", record.Summary);
        Assert.DoesNotContain("secret", record.Details?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Fixture
    {
        public readonly InMemoryStore Store = new();
        public readonly FakeGateway Gateway = new();
        public readonly FakeLease Lease = new();
        public readonly FakeEventStore Events = new();
        public readonly PlatformConfigurationService Configuration;
        public readonly GetAccountDetailsHandler Get;
        public readonly RefreshAccountDetailsHandler Refresh;
        public readonly DateTimeOffset UtcNow = new(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);
        public readonly DateOnly TradingDay = DateOnly.FromDateTime(new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc));
        public readonly AccountDetailsAccount Account = new("ACC", "Account", null, "ENABLED", "CFD", true, 100, 0, 0, 100, "GBP", true, true);

        public Fixture()
        {
            Gateway.Result = new AccountDetailsGatewayResult.Succeeded([Account]);
            Configuration = new PlatformConfigurationService(new FakeConfigurationStore());
            var gate = new TradingScheduleGate();
            var timeProvider = new FixedTimeProvider(UtcNow);
            Refresh = new RefreshAccountDetailsHandler(Configuration, Gateway, Store, Lease, gate, timeProvider);
            Get = new GetAccountDetailsHandler(Configuration, Store);
        }

        public AccountDetailsSnapshot Snapshot(DateTimeOffset retrievedAt, DateOnly day, AccountDetailsTriggerSource triggerSource = AccountDetailsTriggerSource.Manual) =>
            new(Guid.NewGuid(), BrokerEnvironmentKind.Demo, retrievedAt, day, triggerSource, [Account]);

        internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => utcNow;
        }
    }

    private sealed class FakeGateway : IAccountDetailsGateway
    {
        public int CallCount;
        public bool WaitForRelease;
        public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource ReleaseSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public AccountDetailsGatewayResult Result = null!;
        public Task<AccountDetailsGatewayResult> GetAccountsAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            Started.TrySetResult();
            return WaitForRelease ? AwaitReleaseAsync(cancellationToken) : Task.FromResult(Result);
        }
        private async Task<AccountDetailsGatewayResult> AwaitReleaseAsync(CancellationToken cancellationToken) { await ReleaseSource.Task.WaitAsync(cancellationToken); return Result; }
        public void Release() => ReleaseSource.TrySetResult();
    }

    private sealed class FakeLease : IAccountDetailsRefreshLease
    {
        public AccountDetailsRefreshLeaseResult Result = new(true);
        public Task<AccountDetailsRefreshLeaseResult> AcquireAsync(BrokerEnvironmentKind environment, AccountDetailsTriggerSource trigger, CancellationToken cancellationToken) => Task.FromResult(Result);
    }

    private sealed class InMemoryStore : IAccountDetailsSnapshotStore
    {
        public readonly List<AccountDetailsSnapshot> Snapshots = [];
        public int ReadCount;
        public Task<AccountDetailsSnapshot?> GetLatestAsync(BrokerEnvironmentKind environment, CancellationToken cancellationToken) { ReadCount++; return Task.FromResult(Snapshots.Where(x => x.BrokerEnvironment == environment).OrderByDescending(x => x.RetrievedAtUtc).FirstOrDefault()); }
        public Task<AccountDetailsSnapshot?> GetBeforeAsync(BrokerEnvironmentKind environment, AccountDetailsCursor cursor, CancellationToken cancellationToken) { ReadCount++; return Task.FromResult(Snapshots.Where(x => x.BrokerEnvironment == environment && IsBefore(x, cursor)).OrderByDescending(x => x.RetrievedAtUtc).FirstOrDefault()); }
        public Task<AccountDetailsSnapshot?> GetAfterAsync(BrokerEnvironmentKind environment, AccountDetailsCursor cursor, CancellationToken cancellationToken) { ReadCount++; return Task.FromResult(Snapshots.Where(x => x.BrokerEnvironment == environment && IsAfter(x, cursor)).OrderBy(x => x.RetrievedAtUtc).FirstOrDefault()); }
        private static bool IsBefore(AccountDetailsSnapshot snapshot, AccountDetailsCursor cursor) => snapshot.RetrievedAtUtc < cursor.RetrievedAtUtc || snapshot.RetrievedAtUtc == cursor.RetrievedAtUtc && snapshot.RetrievalId.CompareTo(cursor.RetrievalId) < 0;
        private static bool IsAfter(AccountDetailsSnapshot snapshot, AccountDetailsCursor cursor) => snapshot.RetrievedAtUtc > cursor.RetrievedAtUtc || snapshot.RetrievedAtUtc == cursor.RetrievedAtUtc && snapshot.RetrievalId.CompareTo(cursor.RetrievalId) > 0;
        public Task<bool> ExistsForTradingDayAsync(BrokerEnvironmentKind environment, DateOnly tradingDay, CancellationToken cancellationToken) =>
            Task.FromResult(Snapshots.Any(x => x.BrokerEnvironment == environment && x.TradingDay == tradingDay && x.TriggerSource == AccountDetailsTriggerSource.Automatic));
        public Task<AccountDetailsSnapshot> SaveAsync(AccountDetailsSnapshot snapshot, CancellationToken cancellationToken) { Snapshots.Add(snapshot); return Task.FromResult(snapshot); }
    }

    private sealed class FakeConfigurationStore : IPlatformConfigurationStore
    {
        public Task<PlatformConfigurationSnapshot> ApplyStartupConfigurationAsync(CancellationToken cancellationToken) => Task.FromResult(Current);
        public Task<PlatformConfigurationSnapshot> GetCurrentAsync(CancellationToken cancellationToken) => Task.FromResult(Current);
        public Task<PlatformConfigurationSnapshot> GetRuntimeAsync(PlatformEnvironmentKind? platformEnvironment, BrokerEnvironmentKind? brokerEnvironment, CancellationToken cancellationToken) => Task.FromResult(Current);
        private static PlatformConfigurationSnapshot Current => new(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, new TradingScheduleConfiguration(new TimeOnly(0), new TimeOnly(23, 59), [], WeekendBehavior.IncludeFullWeekend, [], "UTC"), new RetryPolicyConfiguration(1, 1, 1, 1, 1), new NotificationSettingsConfiguration("None", null), new CredentialPresence(false, false, false, false, false, false), false, false, DateTimeOffset.UtcNow, false);
    }

    private sealed class FakeEventStore : IPlatformEventStore
    {
        public readonly List<PlatformEventRecord> Records = [];
        public Task<IReadOnlyList<OperationalEventModel>> GetEventsAsync(string? category, string? environment, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OperationalEventModel>>([]);
        public Task AddAsync(PlatformEventRecord record, CancellationToken cancellationToken) { Records.Add(record); return Task.CompletedTask; }
    }
}