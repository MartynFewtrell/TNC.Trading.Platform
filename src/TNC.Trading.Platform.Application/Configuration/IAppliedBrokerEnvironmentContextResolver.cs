namespace TNC.Trading.Platform.Application.Configuration;

internal interface IAppliedBrokerEnvironmentContextResolver
{
    Task<AppliedBrokerEnvironmentContext?> ResolveAppliedAsync(CancellationToken cancellationToken);
    Task<AppliedBrokerEnvironmentContext?> ResolveAsync(Guid brokerEnvironmentId, CancellationToken cancellationToken);
}