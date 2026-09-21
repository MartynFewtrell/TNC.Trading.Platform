using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal static class AccountPreferencesEnvironmentPolicy
{
    public static bool IsSupported(PlatformEnvironmentKind platformEnvironment, BrokerEnvironmentKind brokerEnvironment) =>
        platformEnvironment == PlatformEnvironmentKind.Test
        && brokerEnvironment == BrokerEnvironmentKind.Demo;

    public static string UnsupportedReason => "Account preferences are supported for the Test account only; this feature does not authorize real orders or monetary exposure.";
}