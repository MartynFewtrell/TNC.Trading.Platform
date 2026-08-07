using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.AccountDetails;

internal sealed class CaptureDailyAccountDetailsHandler(
    RefreshAccountDetailsHandler refreshHandler,
    PlatformConfigurationService configurationService,
    IPlatformEventStore eventStore,
    TimeProvider timeProvider) : IAccountDetailsDailyCapture
{
    public async Task<CaptureDailyAccountDetailsResponse> HandleAsync(CaptureDailyAccountDetailsRequest request, CancellationToken cancellationToken)
    {
        AccountDetailsRefreshOutcome outcome;
        try
        {
            outcome = await refreshHandler.CaptureAutomaticAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await RecordFailureAsync(AccountDetailsFailureCategory.Unavailable, "Automatic account details capture failed.", cancellationToken).ConfigureAwait(false);
            throw;
        }

        if (outcome is AccountDetailsRefreshOutcome.Failed failed)
        {
            await RecordFailureAsync(failed.Category, "Automatic account details capture failed.", cancellationToken).ConfigureAwait(false);
        }

        return new CaptureDailyAccountDetailsResponse(outcome);
    }

    private async Task RecordFailureAsync(AccountDetailsFailureCategory category, string summary, CancellationToken cancellationToken)
    {
        var configuration = await configurationService.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        await eventStore.AddAsync(new PlatformEventRecord(
            "account-details",
            "AccountDetailsCaptureFailed",
            configuration.PlatformEnvironment,
            configuration.BrokerEnvironment,
            "Warning",
            summary,
            new { Trigger = AccountDetailsTriggerSource.Automatic.ToString(), Outcome = category.ToString() },
            Guid.NewGuid().ToString("N"),
            null,
            timeProvider.GetUtcNow()), cancellationToken).ConfigureAwait(false);
    }
}
