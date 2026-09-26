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
}
