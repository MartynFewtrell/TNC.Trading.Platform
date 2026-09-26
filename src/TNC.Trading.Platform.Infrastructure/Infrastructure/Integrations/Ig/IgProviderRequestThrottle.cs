using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal sealed class IgProviderRequestThrottle
{
    private static readonly object ProcessSync = new();
    private static DateTimeOffset processNextRequestAtUtc;
    private readonly PlatformDbContext? dbContext;
    private readonly TimeProvider clock;
    private readonly TimeSpan minimumInterval;
    private readonly int accountRequestsPerMinute;
    private readonly int applicationRequestsPerMinute;

    internal IgProviderRequestThrottle(TimeSpan? minimumInterval = null)
    {
        ValidateArguments(minimumInterval, 30, 60);
        clock = TimeProvider.System;
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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        ArgumentNullException.ThrowIfNull(accountIdentifier);
        if (windowEndUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The request window end must be UTC.", nameof(windowEndUtc));
        }

        if (dbContext is null)
        {
            await WaitAsync(cancellationToken).ConfigureAwait(false);
            return clock.GetUtcNow().ToUniversalTime() < windowEndUtc;
        }

        var applicationHash = ComputeHash(apiKey);
        var accountHash = ComputeHash(accountIdentifier.Trim().ToUpperInvariant());
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reservedAtUtc = ReserveLocalSlot();
            var delay = reservedAtUtc - clock.GetUtcNow().ToUniversalTime();
            if (delay > TimeSpan.Zero)
            {
                if (reservedAtUtc >= windowEndUtc)
                {
                    return false;
                }

                await Task.Delay(delay, clock, cancellationToken).ConfigureAwait(false);
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
            }
        }
    }

    private DateTimeOffset ReserveLocalSlot()
    {
        if (minimumInterval == TimeSpan.Zero)
        {
            return clock.GetUtcNow().ToUniversalTime();
        }

        lock (ProcessSync)
        {
            var nowUtc = clock.GetUtcNow().ToUniversalTime();
            var reservedAtUtc = processNextRequestAtUtc > nowUtc ? processNextRequestAtUtc : nowUtc;
            processNextRequestAtUtc = reservedAtUtc.Add(minimumInterval);
            return reservedAtUtc;
        }
    }

    private async Task<DateTimeOffset?> TryReserveDistributedSlotAsync(
        string applicationHash,
        string accountHash,
        DateTimeOffset nowUtc,
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

        if (nextAllowedAtUtc.Count > 0)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return nextAllowedAtUtc.Min();
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
}
