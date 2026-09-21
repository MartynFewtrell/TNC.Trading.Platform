using Microsoft.Extensions.Configuration;

namespace TNC.Trading.Platform.AppHost;

internal sealed record AppHostSettings(
    string? ApiAuthenticationProvider,
    bool EnableInteractiveTestSignIn,
    string? AcsEndpoint,
    string? AcsSenderAddress,
    string? AcsConnectionString,
    string? AccountPreferencesBaseUrl = null,
    bool AccountPreferencesReconciliationEnabled = true)
{
    public static AppHostSettings FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new AppHostSettings(
            ApiAuthenticationProvider: configuration["Authentication:ApiProvider"],
            EnableInteractiveTestSignIn: string.Equals(
                configuration["Authentication:Test:EnableInteractiveSignIn"],
                bool.TrueString,
                StringComparison.OrdinalIgnoreCase),
            AcsEndpoint: configuration["NotificationTransports:AzureCommunicationServices:Endpoint"],
            AcsSenderAddress: configuration["NotificationTransports:AzureCommunicationServices:SenderAddress"],
            AcsConnectionString: configuration["NotificationTransports:AzureCommunicationServices:ConnectionString"],
            AccountPreferencesBaseUrl: configuration["Ig:AccountPreferencesBaseUrl"],
            AccountPreferencesReconciliationEnabled: !string.Equals(
                configuration["AccountPreferences:Reconciliation:Enabled"],
                bool.FalseString,
                StringComparison.OrdinalIgnoreCase));
    }
}
