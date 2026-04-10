using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Services;

internal sealed class PlatformConfigurationService(IPlatformConfigurationStore store)
{
    public Task<PlatformConfigurationSnapshot> ApplyStartupConfigurationAsync(CancellationToken cancellationToken) =>
        store.ApplyStartupConfigurationAsync(cancellationToken);

    public Task<PlatformConfigurationSnapshot> GetCurrentAsync(CancellationToken cancellationToken) =>
        store.GetCurrentAsync(cancellationToken);

    public Task<PlatformConfigurationSnapshot> GetRuntimeAsync(
        PlatformEnvironmentKind? platformEnvironment,
        BrokerEnvironmentKind? brokerEnvironment,
        CancellationToken cancellationToken) =>
        store.GetRuntimeAsync(platformEnvironment, brokerEnvironment, cancellationToken);

    public Task<UpdatePlatformConfigurationResult> UpdateAsync(PlatformConfigurationUpdate update, CancellationToken cancellationToken) =>
        store.UpdateAsync(update, cancellationToken);
}
