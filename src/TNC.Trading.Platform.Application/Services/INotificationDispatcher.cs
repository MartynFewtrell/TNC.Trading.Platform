using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Services;

internal interface INotificationDispatcher
{
    Task DispatchFailureAsync(PlatformConfigurationSnapshot configuration, string summary, string correlationId, Guid? retryCycleId, CancellationToken cancellationToken);

    Task DispatchRetryLimitReachedAsync(PlatformConfigurationSnapshot configuration, string summary, string correlationId, Guid? retryCycleId, CancellationToken cancellationToken);

    Task DispatchRecoveryAsync(PlatformConfigurationSnapshot configuration, string summary, string correlationId, Guid? retryCycleId, CancellationToken cancellationToken);

    Task DispatchBlockedLiveAsync(PlatformConfigurationSnapshot configuration, string summary, string correlationId, Guid? retryCycleId, CancellationToken cancellationToken);
}
