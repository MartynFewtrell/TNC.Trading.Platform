namespace TNC.Trading.Platform.Application.Configuration;

internal static class BrokerEnvironmentPresentation
{
    public static string ForAccountPreferences(BrokerEnvironmentKind environment) =>
        environment switch
        {
            BrokerEnvironmentKind.Demo => "Test",
            BrokerEnvironmentKind.Live => "Live",
            _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, null)
        };
}