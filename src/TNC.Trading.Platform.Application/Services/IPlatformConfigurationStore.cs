using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Services;

internal interface IPlatformConfigurationStore
{
    Task<PlatformConfigurationSnapshot> ApplyStartupConfigurationAsync(CancellationToken cancellationToken);

    Task<PlatformConfigurationSnapshot> GetCurrentAsync(CancellationToken cancellationToken);

    Task<PlatformConfigurationSnapshot> GetRuntimeAsync(
        PlatformEnvironmentKind? platformEnvironment,
        BrokerEnvironmentKind? brokerEnvironment,
        CancellationToken cancellationToken);

    Task<UpdatePlatformConfigurationResult> UpdateAsync(PlatformConfigurationUpdate update, CancellationToken cancellationToken);
}
