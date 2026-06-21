namespace TNC.Trading.Platform.Application.Configuration;

internal interface IProtectedCredentialService
{
    Task<CredentialPresence> GetPresenceAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken);

    Task<IgCredentials> GetCredentialsAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken);

    Task UpdateAsync(
        BrokerEnvironmentKind brokerEnvironment,
        string? apiKey,
        string? identifier,
        string? password,
        string changedBy,
        CancellationToken cancellationToken);
}
