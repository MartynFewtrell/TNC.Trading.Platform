using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountPreferences;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfTrailingStopsPreferenceObservationStore(PlatformDbContext dbContext) : ITrailingStopsPreferenceObservationStore
{
    public async Task AppendAsync(TrailingStopsPreferenceObservation observation, CancellationToken cancellationToken)
    {
        dbContext.TrailingStopsPreferenceObservations.Add(new TrailingStopsPreferenceObservationEntity
        {
            TrailingStopsPreferenceObservationId = observation.Id,
            TrailingStopsEnabled = observation.TrailingStopsEnabled,
            ObservedAtUtc = observation.ObservedAtUtc,
            RecordedAtUtc = observation.RecordedAtUtc,
            PlatformEnvironment = observation.PlatformEnvironment.ToString(),
            BrokerEnvironment = observation.BrokerEnvironment.ToString(),
            AccountId = observation.AccountId,
            ObservationKind = observation.ObservationKind,
            Source = observation.Source,
            Actor = observation.Actor,
            CorrelationId = observation.CorrelationId
        });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TrailingStopsPreferenceObservationPage> ListAsync(PlatformEnvironmentKind platformEnvironment, BrokerEnvironmentKind brokerEnvironment, string? cursor, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.TrailingStopsPreferenceObservations.AsNoTracking()
            .Where(item => item.PlatformEnvironment == platformEnvironment.ToString() && item.BrokerEnvironment == brokerEnvironment.ToString())
            .OrderByDescending(item => item.ObservedAtUtc)
            .ThenByDescending(item => item.TrailingStopsPreferenceObservationId);
        if (cursor is not null)
        {
            if (!Guid.TryParse(cursor, out var cursorId)) return new([], null, true);
            var cursorItem = await dbContext.TrailingStopsPreferenceObservations.AsNoTracking()
                .SingleOrDefaultAsync(item => item.TrailingStopsPreferenceObservationId == cursorId && item.PlatformEnvironment == platformEnvironment.ToString() && item.BrokerEnvironment == brokerEnvironment.ToString(), cancellationToken).ConfigureAwait(false);
            if (cursorItem is null) return new([], null, true);
            if (cursorItem is not null)
            {
                query = query.Where(item => item.ObservedAtUtc < cursorItem.ObservedAtUtc
                    || item.ObservedAtUtc == cursorItem.ObservedAtUtc
                    && item.TrailingStopsPreferenceObservationId.CompareTo(cursorItem.TrailingStopsPreferenceObservationId) < 0)
                    .OrderByDescending(item => item.ObservedAtUtc)
                    .ThenByDescending(item => item.TrailingStopsPreferenceObservationId);
            }
        }

        var entities = await query.Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        var observations = entities.Select(item => new TrailingStopsPreferenceObservation(
            item.TrailingStopsPreferenceObservationId, item.TrailingStopsEnabled, item.ObservedAtUtc, item.RecordedAtUtc,
            Enum.Parse<PlatformEnvironmentKind>(item.PlatformEnvironment), Enum.Parse<BrokerEnvironmentKind>(item.BrokerEnvironment),
            item.AccountId, item.ObservationKind, item.Source, item.Actor, item.CorrelationId)).ToList();
        return new(observations, observations.Count == pageSize ? observations[^1].Id.ToString("N") : null, false);
    }
}