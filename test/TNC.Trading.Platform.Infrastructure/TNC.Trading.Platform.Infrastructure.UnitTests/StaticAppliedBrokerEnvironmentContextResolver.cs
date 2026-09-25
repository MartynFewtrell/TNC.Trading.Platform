using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

internal sealed class StaticAppliedBrokerEnvironmentContextResolver(Guid? brokerEnvironmentId)
    : IAppliedBrokerEnvironmentContextResolver
{
    public Task<AppliedBrokerEnvironmentContext?> ResolveAppliedAsync(CancellationToken cancellationToken) =>
        Task.FromResult(brokerEnvironmentId is { } id
            ? new AppliedBrokerEnvironmentContext(id, "IG", "Demo", "Active", "Available", "IgDemo", true, true)
            : null);

    public Task<AppliedBrokerEnvironmentContext?> ResolveAsync(
        Guid brokerEnvironmentId,
        CancellationToken cancellationToken) =>
        Task.FromResult<AppliedBrokerEnvironmentContext?>(null);
}
