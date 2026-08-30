using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed class GetTrailingStopsPreferenceObservationsHandler(PlatformConfigurationService configurationService, ITrailingStopsPreferenceObservationStore store)
{
    public async Task<GetTrailingStopsPreferenceObservationsResponse> HandleAsync(GetTrailingStopsPreferenceObservationsRequest request, CancellationToken cancellationToken)
    {
        var configuration = await configurationService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (!AccountPreferencesEnvironmentPolicy.IsSupported(configuration.PlatformEnvironment, configuration.BrokerEnvironment))
        {
            return new([], null, false);
        }
        var page = await store.ListAsync(configuration.PlatformEnvironment, configuration.BrokerEnvironment, request.Cursor, Math.Clamp(request.PageSize, 1, 100), cancellationToken).ConfigureAwait(false);
        return new(page.Observations, page.NextCursor, page.HasInvalidCursor);
    }
}