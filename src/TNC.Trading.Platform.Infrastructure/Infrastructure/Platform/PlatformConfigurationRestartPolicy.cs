using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Infrastructure.Persistence;

namespace TNC.Trading.Platform.Infrastructure.Platform;

internal static class PlatformConfigurationRestartPolicy
{
    public static bool IsRestartRequired(PlatformConfigurationEntity entity, PlatformConfigurationUpdate update)
    {
        return entity.PlatformEnvironment != update.PlatformEnvironment.ToString()
            || entity.BrokerEnvironment != update.BrokerEnvironment.ToString();
    }
}