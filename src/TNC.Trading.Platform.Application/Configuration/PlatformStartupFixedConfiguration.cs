namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record PlatformStartupFixedConfiguration(
    PlatformEnvironmentKind PlatformEnvironment,
    BrokerEnvironmentKind BrokerEnvironment);