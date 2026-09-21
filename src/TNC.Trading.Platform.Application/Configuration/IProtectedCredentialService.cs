namespace TNC.Trading.Platform.Application.Configuration;

internal interface IProtectedCredentialService
{
    Task<CredentialPresence> GetPresenceAsync(Guid brokerEnvironmentId, CancellationToken cancellationToken) =>
        Task.FromException<CredentialPresence>(new NotSupportedException("Catalog-scoped credential access is not implemented by this provider."));

    Task<IgCredentials> GetCredentialsAsync(Guid brokerEnvironmentId, CancellationToken cancellationToken) =>
        Task.FromException<IgCredentials>(new NotSupportedException("Catalog-scoped credential access is not implemented by this provider."));

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
