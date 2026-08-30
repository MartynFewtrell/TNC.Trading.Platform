using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal interface ITrailingStopsPreferenceObservationStore
{
    Task AppendAsync(TrailingStopsPreferenceObservation observation, CancellationToken cancellationToken);
    Task<TrailingStopsPreferenceObservationPage> ListAsync(PlatformEnvironmentKind platformEnvironment, BrokerEnvironmentKind brokerEnvironment, string? cursor, int pageSize, CancellationToken cancellationToken);
}

internal sealed record TrailingStopsPreferenceObservationPage(IReadOnlyList<TrailingStopsPreferenceObservation> Observations, string? NextCursor, bool HasInvalidCursor);