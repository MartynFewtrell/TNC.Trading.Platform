using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed class UpdateAccountPreferencesHandler(
    PlatformConfigurationService configurationService,
    IAccountPreferencesGateway gateway,
    ITrailingStopsPreferenceObservationStore observationStore,
    IPlatformEventStore eventStore,
    TimeProvider timeProvider,
    IAccountPreferencesCurrentStateStore? currentStateStore = null)
{
    public async Task<UpdateAccountPreferencesResponse> HandleAsync(UpdateAccountPreferencesRequest request, CancellationToken cancellationToken)
    {
        var configuration = await configurationService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (currentStateStore is not null)
        {
            if (!AccountPreferencesEnvironmentPolicy.IsSupported(configuration.PlatformEnvironment, configuration.BrokerEnvironment))
                return new(new AccountPreferencesGatewayOutcome.Failed(AccountPreferencesFailureCategory.UnsupportedEnvironment, AccountPreferencesEnvironmentPolicy.UnsupportedReason));

            var change = new AccountPreferencesDesiredStateChange(configuration.PlatformEnvironment, configuration.BrokerEnvironment, request.TargetAccountId!, request.TrailingStopsEnabled!.Value, request.ExpectedRevision, request.Actor!, timeProvider.GetUtcNow(), request.CorrelationId ?? Guid.NewGuid().ToString("N"));
            var commit = await currentStateStore.CommitDesiredStateAsync(change, cancellationToken).ConfigureAwait(false);
            if (!commit.Committed)
                return new(new AccountPreferencesGatewayOutcome.Failed(AccountPreferencesFailureCategory.Rejected, "The account preferences revision is stale."), commit.State);

            var state = commit.State;
            var outcome = new AccountPreferencesGatewayOutcome.Succeeded(new AccountPreferences(request.TrailingStopsEnabled.Value, state.VerificationStatus.ToString(), state.DesiredChangedAtUtc ?? timeProvider.GetUtcNow()));
            return new(outcome, state);
        }
        if (!AccountPreferencesEnvironmentPolicy.IsSupported(configuration.PlatformEnvironment, configuration.BrokerEnvironment))
        {
            return new(new AccountPreferencesGatewayOutcome.Failed(AccountPreferencesFailureCategory.UnsupportedEnvironment, AccountPreferencesEnvironmentPolicy.UnsupportedReason));
        }

        var requested = request.TrailingStopsEnabled!.Value;
        var update = await gateway.UpdateAsync(requested, cancellationToken).ConfigureAwait(false);
        if (update is AccountPreferencesGatewayOutcome.Failed failedUpdate)
        {
            await RecordFailureAsync("UpdateFailed", failedUpdate, request, configuration, cancellationToken).ConfigureAwait(false);
            return new(update);
        }

        if (update is AccountPreferencesGatewayOutcome.Indeterminate indeterminateUpdate)
        {
            await RecordFailureAsync("UpdateIndeterminate", indeterminateUpdate, request, configuration, cancellationToken).ConfigureAwait(false);
        }

        var confirmation = await gateway.GetAsync(cancellationToken).ConfigureAwait(false);

        if (confirmation is AccountPreferencesGatewayOutcome.Succeeded succeeded)
        {
            if (succeeded.Preferences.TrailingStopsEnabled == requested)
            {
                var kind = update is AccountPreferencesGatewayOutcome.Indeterminate ? "ReconciliationObserved" : "UpdateConfirmed";
                await observationStore.AppendAsync(GetAccountPreferencesHandler.CreateObservation(succeeded.Preferences, kind, new GetAccountPreferencesRequest(request.Actor, request.CorrelationId), configuration, timeProvider.GetUtcNow()), cancellationToken).ConfigureAwait(false);
                return new(confirmation);
            }

            await observationStore.AppendAsync(GetAccountPreferencesHandler.CreateObservation(succeeded.Preferences, "ReadObserved", new GetAccountPreferencesRequest(request.Actor, request.CorrelationId), configuration, timeProvider.GetUtcNow()), cancellationToken).ConfigureAwait(false);
            var notApplied = new AccountPreferencesGatewayOutcome.NotApplied(requested, succeeded.Preferences);
            await RecordFailureAsync("UpdateNotApplied", AccountPreferencesFailureCategory.Rejected, "IG account preferences update was not applied.", request, configuration, cancellationToken).ConfigureAwait(false);
            return new(notApplied);
        }

        if (confirmation is AccountPreferencesGatewayOutcome.Failed failedConfirmation)
        {
            await RecordFailureAsync("ReconciliationFailed", failedConfirmation, request, configuration, cancellationToken).ConfigureAwait(false);
        }
        else if (confirmation is AccountPreferencesGatewayOutcome.Indeterminate indeterminateConfirmation)
        {
            await RecordFailureAsync("ReconciliationIndeterminate", indeterminateConfirmation, request, configuration, cancellationToken).ConfigureAwait(false);
        }
        return new(confirmation);
    }

    private Task RecordFailureAsync(string eventType, AccountPreferencesGatewayOutcome.Failed failure, UpdateAccountPreferencesRequest request, PlatformConfigurationSnapshot configuration, CancellationToken cancellationToken) =>
        GetAccountPreferencesHandler.RecordFailureAsync(eventType, failure.Category, failure.SafeReason, new GetAccountPreferencesRequest(request.Actor, request.CorrelationId), configuration, timeProvider.GetUtcNow(), cancellationToken, eventStore);

    private Task RecordFailureAsync(string eventType, AccountPreferencesGatewayOutcome.Indeterminate failure, UpdateAccountPreferencesRequest request, PlatformConfigurationSnapshot configuration, CancellationToken cancellationToken) =>
        GetAccountPreferencesHandler.RecordFailureAsync(eventType, failure.Category, failure.SafeReason, new GetAccountPreferencesRequest(request.Actor, request.CorrelationId), configuration, timeProvider.GetUtcNow(), cancellationToken, eventStore);

    private Task RecordFailureAsync(string eventType, AccountPreferencesFailureCategory category, string reason, UpdateAccountPreferencesRequest request, PlatformConfigurationSnapshot configuration, CancellationToken cancellationToken) =>
        GetAccountPreferencesHandler.RecordFailureAsync(eventType, category, reason, new GetAccountPreferencesRequest(request.Actor, request.CorrelationId), configuration, timeProvider.GetUtcNow(), cancellationToken, eventStore);
}