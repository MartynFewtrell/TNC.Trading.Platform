using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Features.MarketDetails;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal sealed class IgProviderRequestThrottle
{
    private static readonly ConditionalWeakTable<TimeProvider, LocalPacingState> ProcessSlots = new();
    private readonly PlatformDbContext? dbContext;
    private readonly TimeProvider clock;
    private readonly TimeSpan minimumInterval;
    private readonly int accountRequestsPerMinute;
    private readonly int applicationRequestsPerMinute;

    internal IgProviderRequestThrottle(TimeSpan? minimumInterval = null, TimeProvider? timeProvider = null)
    {
        ValidateArguments(minimumInterval, 30, 60);
        clock = timeProvider ?? TimeProvider.System;
        this.minimumInterval = minimumInterval ?? TimeSpan.FromMilliseconds(250);
        accountRequestsPerMinute = 30;
        applicationRequestsPerMinute = 60;
    }

    public IgProviderRequestThrottle(
        PlatformDbContext dbContext,
        TimeProvider? timeProvider = null,
        TimeSpan? minimumInterval = null,
        int accountRequestsPerMinute = 30,
        int applicationRequestsPerMinute = 60)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        ValidateArguments(minimumInterval, accountRequestsPerMinute, applicationRequestsPerMinute);
        clock = timeProvider ?? TimeProvider.System;
        this.minimumInterval = minimumInterval ?? TimeSpan.FromMilliseconds(250);
        this.accountRequestsPerMinute = accountRequestsPerMinute;
        this.applicationRequestsPerMinute = applicationRequestsPerMinute;
    }

    private static void ValidateArguments(
        TimeSpan? minimumInterval,
        int accountRequestsPerMinute,
        int applicationRequestsPerMinute)
    {
        if ((minimumInterval is { } configuredInterval && configuredInterval < TimeSpan.Zero)
            || accountRequestsPerMinute < 1
            || applicationRequestsPerMinute < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumInterval));
        }
    }

    internal async Task WaitAsync(CancellationToken cancellationToken)
    {
        var reservedAtUtc = ReserveLocalSlot();
        var delay = reservedAtUtc - clock.GetUtcNow().ToUniversalTime();
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, clock, cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task<bool> WaitAsync(
        string apiKey,
        string accountIdentifier,
        DateTimeOffset windowEndUtc,
        CancellationToken cancellationToken,
        TimeSpan? requestedInterval = null,
        Func<CancellationToken, Task<bool>>? renewLeaseAsync = null,
        bool honorProviderCooldown = false)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        ArgumentNullException.ThrowIfNull(accountIdentifier);
        var interval = requestedInterval ?? minimumInterval;
        if (interval < TimeSpan.Zero || interval > TimeSpan.FromMinutes(1))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedInterval));
        }
        if (windowEndUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The request window end must be UTC.", nameof(windowEndUtc));
        }

        if (renewLeaseAsync is not null
            && !await renewLeaseAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (dbContext is null)
        {
            if (honorProviderCooldown
                && !await WaitForLocalCooldownAsync(
                    applicationHash: ComputeHash(apiKey),
                    accountHash: ComputeHash(accountIdentifier.Trim().ToUpperInvariant()),
                    windowEndUtc,
                    cancellationToken,
                    renewLeaseAsync).ConfigureAwait(false))
            {
                return false;
            }

            var localSlot = ReserveLocalSlot(interval);
            var delay = localSlot - clock.GetUtcNow().ToUniversalTime();
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, clock, cancellationToken).ConfigureAwait(false);
                if (renewLeaseAsync is not null
                    && !await renewLeaseAsync(cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }
            }
            return clock.GetUtcNow().ToUniversalTime() < windowEndUtc;
        }

        var applicationHash = ComputeHash(apiKey);
        var accountHash = ComputeHash(accountIdentifier.Trim().ToUpperInvariant());
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (honorProviderCooldown)
            {
                var cooldownUntilUtc = await GetCooldownUntilAsync(
                    applicationHash,
                    accountHash,
                    cancellationToken).ConfigureAwait(false);
                var cooldownNowUtc = clock.GetUtcNow().ToUniversalTime();
                if (cooldownUntilUtc is { } cooldown
                    && cooldown > cooldownNowUtc)
                {
                    if (cooldown >= windowEndUtc)
                    {
                        return false;
                    }

                    await Task.Delay(cooldown - cooldownNowUtc, clock, cancellationToken).ConfigureAwait(false);
                    if (renewLeaseAsync is not null
                        && !await renewLeaseAsync(cancellationToken).ConfigureAwait(false))
                    {
                        return false;
                    }

                    continue;
                }
            }

            var reservedAtUtc = ReserveLocalSlot(interval);
            var delay = reservedAtUtc - clock.GetUtcNow().ToUniversalTime();
            if (delay > TimeSpan.Zero)
            {
                if (reservedAtUtc >= windowEndUtc)
                {
                    return false;
                }

                await Task.Delay(delay, clock, cancellationToken).ConfigureAwait(false);
                if (renewLeaseAsync is not null
                    && !await renewLeaseAsync(cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }
            }

            var nowUtc = clock.GetUtcNow().ToUniversalTime();
            if (nowUtc >= windowEndUtc)
            {
                return false;
            }

            var retryAtUtc = await TryReserveDistributedSlotAsync(
                applicationHash,
                accountHash,
                nowUtc,
                interval,
                cancellationToken).ConfigureAwait(false);
            if (retryAtUtc is null)
            {
                return true;
            }

            if (retryAtUtc.Value >= windowEndUtc)
            {
                return false;
            }

            var retryDelay = retryAtUtc.Value - clock.GetUtcNow().ToUniversalTime();
            if (retryDelay > TimeSpan.Zero)
            {
                await Task.Delay(retryDelay, clock, cancellationToken).ConfigureAwait(false);
                if (renewLeaseAsync is not null
                    && !await renewLeaseAsync(cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }
            }
        }
    }

    internal async Task ApplyProviderDenialCooldownAsync(
        string apiKey,
        string accountIdentifier,
        string? providerErrorCode,
        CancellationToken cancellationToken)
    {
        var scopeType = MarketDetailProviderErrorCodes.GetCooldownScope(providerErrorCode);
        var duration = MarketDetailProviderErrorCodes.GetCooldownDuration(providerErrorCode);
        if (scopeType is null || duration is null)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(apiKey);
        ArgumentNullException.ThrowIfNull(accountIdentifier);
        var scopeHash = scopeType == "App"
            ? ComputeHash(apiKey)
            : ComputeHash(accountIdentifier.Trim().ToUpperInvariant());
        var cooldownUntilUtc = clock.GetUtcNow().ToUniversalTime().Add(duration.Value);
        if (dbContext is null)
        {
            var state = ProcessSlots.GetValue(clock, static _ => new LocalPacingState());
            lock (state.Sync)
            {
                var key = (scopeType, scopeHash);
                if (!state.Cooldowns.TryGetValue(key, out var existing)
                    || existing < cooldownUntilUtc)
                {
                    state.Cooldowns[key] = cooldownUntilUtc;
                }
            }

            return;
        }

        if (!dbContext.Database.IsRelational())
        {
            throw new InvalidOperationException("Distributed IG cooldowns require a relational database.");
        }

        var resource = $"IGRate/{scopeType}/{scopeHash}";
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        await MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext,
            resource,
            "Exclusive",
            cancellationToken).ConfigureAwait(false);
        var existingCooldown = await dbContext.IgProviderCooldowns.SingleOrDefaultAsync(
            item => item.ScopeType == scopeType && item.ScopeHash == scopeHash,
            cancellationToken).ConfigureAwait(false);
        if (existingCooldown is null)
        {
            dbContext.IgProviderCooldowns.Add(new IgProviderCooldownEntity
            {
                ScopeType = scopeType,
                ScopeHash = scopeHash,
                CooldownUntilUtc = cooldownUntilUtc
            });
        }
        else if (existingCooldown.CooldownUntilUtc < cooldownUntilUtc)
        {
            existingCooldown.CooldownUntilUtc = cooldownUntilUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<DateTimeOffset?> GetCooldownUntilAsync(
        string applicationHash,
        string accountHash,
        CancellationToken cancellationToken)
    {
        var context = dbContext ?? throw new InvalidOperationException("Distributed IG cooldowns require the SQL request store.");
        var cooldowns = await context.IgProviderCooldowns.AsNoTracking()
            .Where(item => (item.ScopeType == "App" && item.ScopeHash == applicationHash)
                || (item.ScopeType == "Account" && item.ScopeHash == accountHash))
            .Select(item => item.CooldownUntilUtc)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return cooldowns.Length == 0 ? null : cooldowns.Max();
    }

    private async Task<bool> WaitForLocalCooldownAsync(
        string applicationHash,
        string accountHash,
        DateTimeOffset windowEndUtc,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<bool>>? renewLeaseAsync)
    {
        var state = ProcessSlots.GetValue(clock, static _ => new LocalPacingState());
        while (true)
        {
            DateTimeOffset? cooldownUntilUtc;
            lock (state.Sync)
            {
                var appCooldown = state.Cooldowns.GetValueOrDefault(("App", applicationHash));
                var accountCooldown = state.Cooldowns.GetValueOrDefault(("Account", accountHash));
                var latest = appCooldown > accountCooldown ? appCooldown : accountCooldown;
                cooldownUntilUtc = latest == default ? null : latest;
            }

            var nowUtc = clock.GetUtcNow().ToUniversalTime();
            if (cooldownUntilUtc is not { } cooldown || cooldown <= nowUtc)
            {
                return true;
            }

            if (cooldown >= windowEndUtc)
            {
                return false;
            }

            await Task.Delay(cooldown - nowUtc, clock, cancellationToken).ConfigureAwait(false);
            if (renewLeaseAsync is not null
                && !await renewLeaseAsync(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }
    }

    private DateTimeOffset ReserveLocalSlot(TimeSpan? requestedInterval = null)
    {
        var interval = requestedInterval ?? minimumInterval;
        if (interval == TimeSpan.Zero)
        {
            return clock.GetUtcNow().ToUniversalTime();
        }

        var state = ProcessSlots.GetValue(clock, static _ => new LocalPacingState());
        lock (state.Sync)
        {
            var nowUtc = clock.GetUtcNow().ToUniversalTime();
            var reservedAtUtc = state.NextRequestAtUtc > nowUtc ? state.NextRequestAtUtc : nowUtc;
            state.NextRequestAtUtc = reservedAtUtc.Add(interval);
            return reservedAtUtc;
        }
    }

    private async Task<DateTimeOffset?> TryReserveDistributedSlotAsync(
        string applicationHash,
        string accountHash,
        DateTimeOffset nowUtc,
        TimeSpan minimumSpacing,
        CancellationToken cancellationToken)
    {
        var context = dbContext ?? throw new InvalidOperationException("Distributed rate limiting requires the SQL request store.");
        if (!context.Database.IsRelational())
        {
            throw new InvalidOperationException("Distributed IG rate limiting requires a relational database.");
        }

        var appLock = $"IGRate/App/{applicationHash}";
        var accountLock = $"IGRate/Account/{accountHash}";
        await using var transaction = await context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        foreach (var resource in new[] { appLock, accountLock }.Order(StringComparer.Ordinal))
        {
            await MarketCategoryInstrumentSqlLock.AcquireAsync(
                context, resource, "Exclusive", cancellationToken).ConfigureAwait(false);
        }

        var windowStartUtc = nowUtc.AddMinutes(-1);
        await context.IgProviderRateReservations
            .Where(item => item.ReservedAtUtc <= windowStartUtc
                && ((item.ScopeType == "App" && item.ScopeHash == applicationHash)
                    || (item.ScopeType == "Account" && item.ScopeHash == accountHash)))
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        var appTimes = await context.IgProviderRateReservations.AsNoTracking()
            .Where(item => item.ScopeType == "App"
                && item.ScopeHash == applicationHash
                && item.ReservedAtUtc > windowStartUtc)
            .OrderBy(item => item.ReservedAtUtc)
            .Select(item => item.ReservedAtUtc)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var accountTimes = await context.IgProviderRateReservations.AsNoTracking()
            .Where(item => item.ScopeType == "Account"
                && item.ScopeHash == accountHash
                && item.ReservedAtUtc > windowStartUtc)
            .OrderBy(item => item.ReservedAtUtc)
            .Select(item => item.ReservedAtUtc)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);

        var nextAllowedAtUtc = new List<DateTimeOffset>(2);
        if (appTimes.Length >= applicationRequestsPerMinute)
        {
            nextAllowedAtUtc.Add(appTimes[0].AddMinutes(1));
        }

        if (accountTimes.Length >= accountRequestsPerMinute)
        {
            nextAllowedAtUtc.Add(accountTimes[0].AddMinutes(1));
        }

        var lastApplicationReservation = appTimes.LastOrDefault();
        var lastAccountReservation = accountTimes.LastOrDefault();
        if (lastApplicationReservation != default)
        {
            nextAllowedAtUtc.Add(lastApplicationReservation.Add(minimumSpacing));
        }

        if (lastAccountReservation != default)
        {
            nextAllowedAtUtc.Add(lastAccountReservation.Add(minimumSpacing));
        }

        if (nextAllowedAtUtc.Count > 0)
        {
            var nextAllowed = nextAllowedAtUtc.Max();
            if (nextAllowed > nowUtc)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return nextAllowed;
            }
        }

        var requestId = Guid.NewGuid();
        context.IgProviderRateReservations.AddRange(
            new IgProviderRateReservationEntity
            {
                RequestId = requestId,
                ScopeType = "App",
                ScopeHash = applicationHash,
                ReservedAtUtc = nowUtc
            },
            new IgProviderRateReservationEntity
            {
                RequestId = requestId,
                ScopeType = "Account",
                ScopeHash = accountHash,
                ReservedAtUtc = nowUtc
            });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return null;
    }

    private static string ComputeHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class LocalPacingState
    {
        internal object Sync { get; } = new();

        internal DateTimeOffset NextRequestAtUtc { get; set; }

        internal Dictionary<(string ScopeType, string ScopeHash), DateTimeOffset> Cooldowns { get; } = [];
    }
}
