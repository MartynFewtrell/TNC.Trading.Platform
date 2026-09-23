using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal static class AccountPreferencesEnvironmentPolicy
{
    public static bool IsSupported(PlatformEnvironmentKind platformEnvironment, BrokerEnvironmentKind brokerEnvironment) =>
        platformEnvironment is PlatformEnvironmentKind.Desktop or PlatformEnvironmentKind.Test
        && brokerEnvironment == BrokerEnvironmentKind.Demo;

    public static string UnsupportedReason => "Account preferences are supported for Demo accounts in local and test environments only; this feature does not authorize real orders or monetary exposure.";
}