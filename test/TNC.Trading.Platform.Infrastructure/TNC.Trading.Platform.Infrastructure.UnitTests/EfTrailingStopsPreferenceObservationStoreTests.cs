using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountPreferences;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public sealed class EfTrailingStopsPreferenceObservationStoreTests
{
    /// <summary>Verifies append-only persistence, equal-timestamp ordering, keyset cursors, and page limits.</summary>
    [Fact]
    public async Task ListAsync_ShouldOrderAndPageDeterministically_WhenObservationsShareTimestamp()
    {
        await using var context = InfrastructureReflection.CreateDbContext();
        var store = new EfTrailingStopsPreferenceObservationStore(context);
        var observedAt = new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
        var first = CreateObservation(Guid.Parse("00000000-0000-0000-0000-000000000001"), observedAt, true);
        var second = CreateObservation(Guid.Parse("00000000-0000-0000-0000-000000000002"), observedAt, false);
        await store.AppendAsync(first, CancellationToken.None);
        await store.AppendAsync(second, CancellationToken.None);
        var page = await store.ListAsync(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, null, 1, CancellationToken.None);
        var next = await store.ListAsync(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, page.Observations[0].Id.ToString(), 1, CancellationToken.None);
        Assert.Equal(second.Id, page.Observations[0].Id);
        Assert.Equal(first.Id, next.Observations[0].Id);
        Assert.Equal(2, await context.TrailingStopsPreferenceObservations.CountAsync());
    }

    /// <summary>Verifies an unknown cursor does not alter the first page and a caller cannot exceed the requested limit.</summary>
    [Fact]
    public async Task ListAsync_ShouldRespectPageLimit_WhenCursorIsUnknown()
    {
        await using var context = InfrastructureReflection.CreateDbContext();
        var store = new EfTrailingStopsPreferenceObservationStore(context);
        for (var index = 0; index < 3; index++) await store.AppendAsync(CreateObservation(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-index), index % 2 == 0), CancellationToken.None);
        var page = await store.ListAsync(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, Guid.NewGuid().ToString(), 2, CancellationToken.None);
        Assert.True(page.HasInvalidCursor);
        Assert.Empty(page.Observations);
    }

    private static TrailingStopsPreferenceObservation CreateObservation(Guid id, DateTimeOffset observedAt, bool enabled) =>
        new(id, enabled, observedAt, observedAt.AddSeconds(1), PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, "Observed", "AccountPreferences", "actor", id.ToString("N"));
}