using Microsoft.Extensions.Configuration;

namespace TNC.Trading.Platform.AppHost;

internal sealed record AppHostSettings(
    bool UseSyntheticRuntimeForTests,
    bool EnableInteractiveTestSignIn,
    string? AcsEndpoint,
    string? AcsSenderAddress,
    string? AcsConnectionString)
{
    public static AppHostSettings FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new AppHostSettings(
            UseSyntheticRuntimeForTests: string.Equals(
                configuration["AppHost:UseSyntheticRuntime"],
                bool.TrueString,
                StringComparison.OrdinalIgnoreCase),
            EnableInteractiveTestSignIn: string.Equals(
                configuration["Authentication:Test:EnableInteractiveSignIn"],
                bool.TrueString,
                StringComparison.OrdinalIgnoreCase),
            AcsEndpoint: configuration["NotificationTransports:AzureCommunicationServices:Endpoint"],
            AcsSenderAddress: configuration["NotificationTransports:AzureCommunicationServices:SenderAddress"],
            AcsConnectionString: configuration["NotificationTransports:AzureCommunicationServices:ConnectionString"]);
    }
}
