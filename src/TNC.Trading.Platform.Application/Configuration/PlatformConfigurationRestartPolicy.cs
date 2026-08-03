namespace TNC.Trading.Platform.Application.Configuration;

internal static class PlatformConfigurationRestartPolicy
{
    public static bool IsRestartRequired(
        PlatformStartupFixedConfiguration? current,
        PlatformConfigurationUpdate update)
    {
        return current is not null
            && (current.PlatformEnvironment != update.PlatformEnvironment
                || current.BrokerEnvironment != update.BrokerEnvironment);
    }
}