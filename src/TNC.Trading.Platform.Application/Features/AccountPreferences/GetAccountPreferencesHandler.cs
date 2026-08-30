using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed class GetAccountPreferencesHandler(
    PlatformConfigurationService configurationService,
    IAccountPreferencesGateway gateway,
    ITrailingStopsPreferenceObservationStore observationStore,
    IPlatformEventStore eventStore,
    TimeProvider timeProvider)
{
    public async Task<GetAccountPreferencesResponse> HandleAsync(GetAccountPreferencesRequest request, CancellationToken cancellationToken)
    {
        var configuration = await configurationService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (!AccountPreferencesEnvironmentPolicy.IsSupported(configuration.PlatformEnvironment, configuration.BrokerEnvironment))
        {
            return new(await GetFailure(AccountPreferencesFailureCategory.UnsupportedEnvironment, AccountPreferencesEnvironmentPolicy.UnsupportedReason, request, configuration, cancellationToken).ConfigureAwait(false));
        }

        var outcome = await gateway.GetAsync(cancellationToken).ConfigureAwait(false);
        if (outcome is AccountPreferencesGatewayOutcome.Succeeded succeeded)
        {
            await observationStore.AppendAsync(CreateObservation(succeeded.Preferences, "ReadObserved", request, configuration, timeProvider.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        }
        else if (outcome is AccountPreferencesGatewayOutcome.Failed failed)
        {
            await RecordFailureAsync("GetFailed", failed.Category, failed.SafeReason, request, configuration, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }
        else if (outcome is AccountPreferencesGatewayOutcome.Indeterminate indeterminate)
        {
            await RecordFailureAsync("GetIndeterminate", indeterminate.Category, indeterminate.SafeReason, request, configuration, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }

        return new(outcome);
    }

    internal static TrailingStopsPreferenceObservation CreateObservation(AccountPreferences preferences, string kind, GetAccountPreferencesRequest request, PlatformConfigurationSnapshot configuration, DateTimeOffset recordedAtUtc) =>
        new(Guid.NewGuid(), preferences.TrailingStopsEnabled, preferences.ObservedAtUtc, recordedAtUtc, configuration.PlatformEnvironment, configuration.BrokerEnvironment, kind, "AccountPreferences", request.Actor, request.CorrelationId ?? Guid.NewGuid().ToString("N"));

    internal static async Task RecordFailureAsync(string eventType, AccountPreferencesFailureCategory category, string safeReason, GetAccountPreferencesRequest request, PlatformConfigurationSnapshot configuration, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken, IPlatformEventStore? eventStore = null)
    {
        if (eventStore is null) return;
        await eventStore.AddAsync(new PlatformEventRecord("AccountPreferences", eventType, configuration.PlatformEnvironment, configuration.BrokerEnvironment, "Warning", "Account preferences provider operation failed.", new { Category = category.ToString(), Reason = safeReason[..Math.Min(safeReason.Length, 160)] }, request.CorrelationId ?? Guid.NewGuid().ToString("N"), null, occurredAtUtc), cancellationToken).ConfigureAwait(false);
    }

    private async Task<AccountPreferencesGatewayOutcome> GetFailure(AccountPreferencesFailureCategory category, string reason, GetAccountPreferencesRequest request, PlatformConfigurationSnapshot configuration, CancellationToken cancellationToken)
    {
        await RecordFailureAsync("GetFailed", category, reason, request, configuration, timeProvider.GetUtcNow(), cancellationToken, eventStore).ConfigureAwait(false);
        return new AccountPreferencesGatewayOutcome.Failed(category, reason);
    }
}