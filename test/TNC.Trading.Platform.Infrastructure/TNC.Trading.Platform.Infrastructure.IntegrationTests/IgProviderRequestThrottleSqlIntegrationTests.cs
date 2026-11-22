using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Infrastructure.Integrations.Ig;

namespace TNC.Trading.Platform.Infrastructure.IntegrationTests;

[Collection("SQL Server")]
public sealed class IgProviderRequestThrottleSqlIntegrationTests(SqlServerDatabaseFixture fixture)
{
    /// <summary>
    /// Trace: Market Details Work Item 3, step 2. Verifies two independent throttle instances share SQL-backed account and application rate reservations.
    /// Expected: only one concurrent request is admitted at a one-request-per-minute test limit, and the database stores only fixed-length hashes rather than API keys or account identifiers.
    /// Why: process-local pacing cannot prevent multiple application replicas from collectively exceeding IG's account or application request limits.
    /// </summary>
    [Fact]
    public async Task WaitAsync_ShouldCoordinateAcrossInstancesAndPersistOnlyHashes_WhenRateLimitIsShared()
    {
        await fixture.ResetDatabaseAsync();
        await using var setupContext = fixture.CreateDbContext();
        await setupContext.Database.MigrateAsync(fixture.CancellationToken);

        await using var firstContext = fixture.CreateDbContext();
        await using var secondContext = fixture.CreateDbContext();
        var firstReplica = new IgProviderRequestThrottle(
            firstContext,
            TimeProvider.System,
            TimeSpan.Zero,
            accountRequestsPerMinute: 1,
            applicationRequestsPerMinute: 1);
        var secondReplica = new IgProviderRequestThrottle(
            secondContext,
            TimeProvider.System,
            TimeSpan.Zero,
            accountRequestsPerMinute: 1,
            applicationRequestsPerMinute: 1);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);

        var results = await Task.WhenAll(
            firstReplica.WaitAsync("fixture-api-key", "fixture-account", deadline, fixture.CancellationToken),
            secondReplica.WaitAsync("fixture-api-key", "fixture-account", deadline, fixture.CancellationToken));

        Assert.Single(results, item => item);
        Assert.Single(results, item => !item);

        var reservations = await setupContext.IgProviderRateReservations.AsNoTracking()
            .ToListAsync(fixture.CancellationToken);
        Assert.Equal(2, reservations.Count);
        Assert.Equal(new[] { "Account", "App" }, reservations.Select(item => item.ScopeType).OrderBy(item => item, StringComparer.Ordinal));
        Assert.All(reservations, item =>
        {
            Assert.Equal(64, item.ScopeHash.Length);
            Assert.DoesNotContain("fixture-api-key", item.ScopeHash, StringComparison.Ordinal);
            Assert.DoesNotContain("fixture-account", item.ScopeHash, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Trace: IG market-data recovery Phase 2, Steps 2.3 and 2.5.
    /// Verifies two throttle instances with independent local clocks honor the same SQL-backed five-second app/account spacing reservation.
    /// Expected: the second admission remains pending until its fake clock reaches the persisted next slot, and both sends are stored five seconds apart.
    /// Why: process-local pacing alone cannot enforce collection request spacing across application replicas.
    /// </summary>
    [Fact]
    public async Task WaitAsync_ShouldShareConfiguredSpacingAcrossInstances_WhenLocalPacingStateIsIndependent()
    {
        await fixture.ResetDatabaseAsync();
        await using var setupContext = fixture.CreateDbContext();
        await setupContext.Database.MigrateAsync(fixture.CancellationToken);

        var start = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        var firstClock = new ManualTimeProvider(start);
        var secondClock = new ManualTimeProvider(start);
        await using var firstContext = fixture.CreateDbContext();
        await using var secondContext = fixture.CreateDbContext();
        var firstReplica = new IgProviderRequestThrottle(firstContext, firstClock);
        var secondReplica = new IgProviderRequestThrottle(secondContext, secondClock);

        Assert.True(await firstReplica.WaitAsync(
            "fixture-api-key",
            "fixture-account",
            start.AddMinutes(1),
            fixture.CancellationToken,
            TimeSpan.FromSeconds(5)));
        var secondAdmission = secondReplica.WaitAsync(
            "fixture-api-key",
            "fixture-account",
            start.AddMinutes(1),
            fixture.CancellationToken,
            TimeSpan.FromSeconds(5));

        Assert.False(secondAdmission.IsCompleted);
        await secondClock.WaitForScheduledDelayAsync();
        secondClock.Advance(TimeSpan.FromSeconds(5));

        Assert.True(await secondAdmission);
        var applicationReservations = await setupContext.IgProviderRateReservations.AsNoTracking()
            .Where(item => item.ScopeType == "App")
            .OrderBy(item => item.ReservedAtUtc)
            .Select(item => item.ReservedAtUtc)
            .ToArrayAsync(fixture.CancellationToken);
        Assert.Equal(2, applicationReservations.Length);
        Assert.Equal(TimeSpan.FromSeconds(5), applicationReservations[1] - applicationReservations[0]);
    }

    /// <summary>
    /// Trace: IG market-data recovery Phase 4, Step 4.1.
    /// Verifies: an API-key allowance denial persists a hashed cooldown shared by independent throttle instances.
    /// Expected: the second replica waits for the bounded cooldown and is admitted after the fake SQL-backed deadline expires.
    /// Why: a process restart or another replica must not immediately repeat a known allowance-denied request.
    /// </summary>
    [Fact]
    public async Task WaitAsync_ShouldPersistAndShareProviderCooldown_WhenApiKeyAllowanceIsExceeded()
    {
        await fixture.ResetDatabaseAsync();
        await using var setupContext = fixture.CreateDbContext();
        await setupContext.Database.MigrateAsync(fixture.CancellationToken);

        var start = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        var firstClock = new ManualTimeProvider(start);
        var secondClock = new ManualTimeProvider(start);
        await using var firstContext = fixture.CreateDbContext();
        await using var secondContext = fixture.CreateDbContext();
        var firstReplica = new IgProviderRequestThrottle(firstContext, firstClock, TimeSpan.Zero);
        var secondReplica = new IgProviderRequestThrottle(secondContext, secondClock, TimeSpan.Zero);

        await firstReplica.ApplyProviderDenialCooldownAsync(
            "fixture-api-key",
            "fixture-account-one",
            "error.public-api.exceeded-api-key-allowance",
            fixture.CancellationToken);
        var persistedCooldown = await setupContext.IgProviderCooldowns.AsNoTracking()
            .SingleAsync(fixture.CancellationToken);
        Assert.Equal("App", persistedCooldown.ScopeType);
        Assert.Equal(64, persistedCooldown.ScopeHash.Length);
        Assert.DoesNotContain("fixture-api-key", persistedCooldown.ScopeHash, StringComparison.Ordinal);
        Assert.Equal(start.AddMinutes(1), persistedCooldown.CooldownUntilUtc);

        var nextRequest = secondReplica.WaitAsync(
            "fixture-api-key",
            "fixture-account-two",
            start.AddMinutes(2),
            fixture.CancellationToken,
            honorProviderCooldown: true);
        Assert.False(nextRequest.IsCompleted);
        await secondClock.WaitForScheduledDelayAsync();
        secondClock.Advance(TimeSpan.FromMinutes(1));

        Assert.True(await nextRequest);
    }

    /// <summary>
    /// Trace: Market Details Work Item 3, step 2. Verifies account and application limits independently across a rolling one-minute window.
    /// Expected: a second account sharing an application is admitted while application capacity remains; later requests are denied when either configured limit is full and the trading deadline is earlier than capacity release.
    /// Why: independent caps protect both IG quota scopes while avoiding waits that would carry scheduled work beyond its trading slot.
    /// </summary>
    [Fact]
    public async Task WaitAsync_ShouldEnforceAccountAndApplicationLimits_WithoutWaitingPastWindow()
    {
        await fixture.ResetDatabaseAsync();
        await using var setupContext = fixture.CreateDbContext();
        await setupContext.Database.MigrateAsync(fixture.CancellationToken);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        await using var firstContext = fixture.CreateDbContext();
        var firstReplica = new IgProviderRequestThrottle(
            firstContext, TimeProvider.System, TimeSpan.Zero,
            accountRequestsPerMinute: 1, applicationRequestsPerMinute: 2);

        Assert.True(await firstReplica.WaitAsync(
            "shared-app-key", "account-one", deadline, fixture.CancellationToken));
        Assert.False(await firstReplica.WaitAsync(
            "shared-app-key", "account-one", deadline, fixture.CancellationToken));

        await using var secondContext = fixture.CreateDbContext();
        var secondReplica = new IgProviderRequestThrottle(
            secondContext, TimeProvider.System, TimeSpan.Zero,
            accountRequestsPerMinute: 1, applicationRequestsPerMinute: 2);
        Assert.True(await secondReplica.WaitAsync(
            "shared-app-key", "account-two", deadline, fixture.CancellationToken));
        Assert.False(await secondReplica.WaitAsync(
            "shared-app-key", "account-three", deadline, fixture.CancellationToken));

        Assert.Equal(4, await setupContext.IgProviderRateReservations.CountAsync(fixture.CancellationToken));
    }

    private sealed class ManualTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private readonly List<ManualTimer> timers = [];
        private readonly SemaphoreSlim scheduledDelays = new(0);
        private long timestamp;

        public override DateTimeOffset GetUtcNow() => initialUtcNow.AddTicks(timestamp);

        public override long GetTimestamp() => timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            timer.Change(dueTime, period);
            if (dueTime > TimeSpan.Zero)
            {
                scheduledDelays.Release();
            }

            timers.Add(timer);
            return timer;
        }

        public Task WaitForScheduledDelayAsync() => scheduledDelays.WaitAsync();

        public void Advance(TimeSpan amount)
        {
            timestamp = checked(timestamp + amount.Ticks);
            while (true)
            {
                var next = timers.FirstOrDefault(timer => timer.IsDue(timestamp));
                if (next is null)
                {
                    return;
                }

                next.Fire(timestamp);
            }
        }

        private sealed class ManualTimer(
            ManualTimeProvider owner,
            TimerCallback callback,
            object? state) : ITimer
        {
            private long dueAt = long.MaxValue;
            private long periodTicks = -1;
            private bool disposed;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (disposed)
                {
                    return false;
                }

                dueAt = dueTime == Timeout.InfiniteTimeSpan
                    ? long.MaxValue
                    : checked(owner.timestamp + Math.Max(0, dueTime.Ticks));
                periodTicks = period == Timeout.InfiniteTimeSpan ? -1 : period.Ticks;
                return true;
            }

            public bool IsDue(long now) => !disposed && dueAt <= now;

            public void Fire(long now)
            {
                dueAt = periodTicks > 0 ? checked(now + periodTicks) : long.MaxValue;
                callback(state);
            }

            public void Dispose()
            {
                disposed = true;
                dueAt = long.MaxValue;
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
