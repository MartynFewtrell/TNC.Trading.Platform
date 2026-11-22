using TNC.Trading.Platform.Infrastructure.Integrations.Ig;

namespace TNC.Trading.Platform.Infrastructure.UnitTests.Integrations.Ig;

public sealed class IgProviderRequestThrottleTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Trace: IG market-data recovery Phase 2, Step 2.5.
    /// Verifies: scoped request admission spaces successive real-send slots by the configured five seconds.
    /// Expected: the second admission remains pending until fake time advances by five seconds, then succeeds at that instant.
    /// Why: session, page, relogin, retry, and detail sends share one pacing gate.
    /// </summary>
    [Fact]
    public async Task WaitAsync_ShouldEnforceConfiguredSpacing_WhenRequestsAreAdmittedSequentially()
    {
        var timeProvider = new ManualTimeProvider(Start);
        var throttle = new IgProviderRequestThrottle(TimeSpan.FromMilliseconds(250), timeProvider);

        Assert.True(await throttle.WaitAsync("api-key", "account", DateTimeOffset.MaxValue, CancellationToken.None, TimeSpan.FromSeconds(5)));
        var secondAdmission = throttle.WaitAsync(
            "api-key",
            "account",
            DateTimeOffset.MaxValue,
            CancellationToken.None,
            TimeSpan.FromSeconds(5));

        Assert.False(secondAdmission.IsCompleted);
        timeProvider.Advance(TimeSpan.FromSeconds(5));

        Assert.True(await secondAdmission);
        Assert.Equal(Start.AddSeconds(5), timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Trace: IG market-data recovery Phase 2, Steps 2.4-2.5.
    /// Verifies: a failed lease checkpoint after a quota/pacing wait prevents the next request from being admitted.
    /// Expected: fake time releases the wait, the second checkpoint reports lease loss, and admission returns false.
    /// Why: a stale owner must stop before sending after a request wait.
    /// </summary>
    [Fact]
    public async Task WaitAsync_ShouldStopAfterLeaseLoss_WhenLeaseExpiresDuringPacingWait()
    {
        var timeProvider = new ManualTimeProvider(Start);
        var throttle = new IgProviderRequestThrottle(TimeSpan.FromMilliseconds(250), timeProvider);
        var checkpointCount = 0;
        Task<bool> RenewAsync(CancellationToken _) => Task.FromResult(++checkpointCount == 1);

        Assert.True(await throttle.WaitAsync("api-key", "account", DateTimeOffset.MaxValue, CancellationToken.None, TimeSpan.FromSeconds(5)));
        var secondAdmission = throttle.WaitAsync(
            "api-key",
            "account",
            DateTimeOffset.MaxValue,
            CancellationToken.None,
            TimeSpan.FromSeconds(5),
            RenewAsync);
        timeProvider.Advance(TimeSpan.FromSeconds(5));

        Assert.False(await secondAdmission);
        Assert.Equal(2, checkpointCount);
    }

    /// <summary>
    /// Trace: IG market-data recovery Phase 2, Step 2.5.
    /// Verifies: caller cancellation interrupts an in-flight pacing wait without admitting a provider send.
    /// Expected: the pending wait throws cancellation and fake time need not advance.
    /// Why: operator shutdown and schedule cancellation must remain responsive while pacing is enforced.
    /// </summary>
    [Fact]
    public async Task WaitAsync_ShouldPropagateCancellation_WhenRequestIsWaitingForItsPacingSlot()
    {
        var timeProvider = new ManualTimeProvider(Start);
        var throttle = new IgProviderRequestThrottle(TimeSpan.FromMilliseconds(250), timeProvider);
        using var cancellation = new CancellationTokenSource();

        Assert.True(await throttle.WaitAsync("api-key", "account", DateTimeOffset.MaxValue, CancellationToken.None, TimeSpan.FromSeconds(5)));
        var secondAdmission = throttle.WaitAsync(
            "api-key",
            "account",
            DateTimeOffset.MaxValue,
            cancellation.Token,
            TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => secondAdmission);
        Assert.Equal(Start, timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Trace: IG market-data recovery Phase 4, Step 4.1.
    /// Verifies: a classified API-key allowance denial installs a cooldown shared by independent market-data throttle callers.
    /// Expected: a second caller cannot pass the shared key scope before one minute has elapsed, then becomes eligible.
    /// Why: one caller must not immediately repeat a known provider limit while another adapter proceeds unaware.
    /// </summary>
    [Fact]
    public async Task WaitAsync_ShouldShareApiKeyCooldownAcrossCallers_WhenProviderReportsKeyAllowanceExceeded()
    {
        var timeProvider = new ManualTimeProvider(Start);
        var deniedCaller = new IgProviderRequestThrottle(TimeSpan.Zero, timeProvider);
        var laterCaller = new IgProviderRequestThrottle(TimeSpan.Zero, timeProvider);
        await deniedCaller.ApplyProviderDenialCooldownAsync(
            "api-key",
            "account-one",
            "error.public-api.exceeded-api-key-allowance",
            CancellationToken.None);

        var nextRequest = laterCaller.WaitAsync(
            "api-key",
            "account-two",
            DateTimeOffset.MaxValue,
            CancellationToken.None,
            honorProviderCooldown: true);

        Assert.False(nextRequest.IsCompleted);
        timeProvider.Advance(TimeSpan.FromMinutes(1));

        Assert.True(await nextRequest);
        Assert.Equal(Start.AddMinutes(1), timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Trace: IG market-data recovery Phase 4, Step 4.1.
    /// Verifies: account allowance cooldown is scoped to the affected account and configuration denials do not create cooldowns.
    /// Expected: another account under the same key remains eligible, while an unknown code produces no cooldown.
    /// Why: safe recovery must avoid both repeated denial and unrelated account starvation.
    /// </summary>
    [Fact]
    public async Task WaitAsync_ShouldLimitCooldownToDeniedAccount_WhenOtherAccountUsesSameApiKey()
    {
        var timeProvider = new ManualTimeProvider(Start);
        var throttle = new IgProviderRequestThrottle(TimeSpan.Zero, timeProvider);
        await throttle.ApplyProviderDenialCooldownAsync(
            "api-key",
            "account-one",
            "error.public-api.exceeded-account-allowance",
            CancellationToken.None);
        await throttle.ApplyProviderDenialCooldownAsync(
            "api-key",
            "account-two",
            "error.security.api-key-disabled",
            CancellationToken.None);

        Assert.True(await throttle.WaitAsync(
            "api-key",
            "account-two",
            DateTimeOffset.MaxValue,
            CancellationToken.None,
            honorProviderCooldown: true));
        Assert.Equal(Start, timeProvider.GetUtcNow());
    }

    private sealed class ManualTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private readonly List<ManualTimer> timers = [];
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
            timers.Add(timer);
            return timer;
        }

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
