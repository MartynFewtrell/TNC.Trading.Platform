using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.ReconcilePlatformAuthentication;

internal sealed class PlatformAuthenticationReconcilerSideEffects(
    IPlatformRetryCycleStore retryCycleStore,
    IPlatformEventStore eventStore,
    INotificationDispatcher notificationDispatcher,
    TimeProvider timeProvider,
    IPlatformApplicationLogger logger)
{
    public Task UpsertRetryCycleAsync(
        Guid? retryCycleId,
        PlatformConfigurationSnapshot currentConfiguration,
        PlatformRuntimeState currentState,
        string cycleType,
        bool failureNotificationSent,
        int? lastDelaySeconds,
        CancellationToken cancellationToken)
    {
        if (retryCycleId is null)
        {
            return Task.CompletedTask;
        }

        return retryCycleStore.UpsertAsync(
            new PlatformRetryCycle
            {
                RetryCycleId = retryCycleId.Value,
                CycleType = cycleType,
                PlatformEnvironment = currentConfiguration.PlatformEnvironment.ToString(),
                BrokerEnvironment = currentConfiguration.BrokerEnvironment.ToString(),
                RetryPhase = currentState.RetryPhase,
                AutomaticAttemptNumber = currentState.AutomaticAttemptNumber,
                NextRetryAtUtc = currentState.NextRetryAtUtc,
                LastDelaySeconds = lastDelaySeconds,
                PeriodicDelayMinutes = currentConfiguration.RetryPolicy.PeriodicDelayMinutes,
                MaxAutomaticRetries = currentConfiguration.RetryPolicy.MaxAutomaticRetries,
                RetryLimitReached = currentState.RetryLimitReached,
                FailureNotificationSent = failureNotificationSent,
                StartedAtUtc = timeProvider.GetUtcNow(),
                UpdatedAtUtc = timeProvider.GetUtcNow()
            },
            cancellationToken);
    }

    public async Task WriteOperationalEventAsync(
        PlatformConfigurationSnapshot currentConfiguration,
        string category,
        string eventType,
        string summary,
        object details,
        string severity,
        string correlationId,
        Guid? retryCycleId,
        CancellationToken cancellationToken)
    {
        await eventStore.AddAsync(
            new PlatformEventRecord(
                category,
                eventType,
                currentConfiguration.PlatformEnvironment,
                currentConfiguration.BrokerEnvironment,
                severity,
                summary,
                details,
                correlationId,
                retryCycleId,
                timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Operational event recorded: {Category}/{EventType} - {Summary}",
            category,
            eventType,
            summary);
    }

    public Task DispatchRecoveryAsync(
        PlatformConfigurationSnapshot currentConfiguration,
        string summary,
        string correlationId,
        Guid? retryCycleId,
        CancellationToken cancellationToken)
    {
        return notificationDispatcher.DispatchRecoveryAsync(currentConfiguration, summary, correlationId, retryCycleId, cancellationToken);
    }

    public Task DispatchFailureAsync(
        PlatformConfigurationSnapshot currentConfiguration,
        string summary,
        string correlationId,
        Guid? retryCycleId,
        CancellationToken cancellationToken)
    {
        return notificationDispatcher.DispatchFailureAsync(currentConfiguration, summary, correlationId, retryCycleId, cancellationToken);
    }

    public Task DispatchBlockedLiveAsync(
        PlatformConfigurationSnapshot currentConfiguration,
        string summary,
        string correlationId,
        Guid? retryCycleId,
        CancellationToken cancellationToken)
    {
        return notificationDispatcher.DispatchBlockedLiveAsync(currentConfiguration, summary, correlationId, retryCycleId, cancellationToken);
    }
}