using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

namespace TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;

internal sealed class SqlAppliedBrokerEnvironmentContextResolver(PlatformDbContext dbContext) : IAppliedBrokerEnvironmentContextResolver
{
    public async Task<AppliedBrokerEnvironmentContext?> ResolveAppliedAsync(CancellationToken cancellationToken)
    {
        var selection = await dbContext.BrokerEnvironmentSelections.AsNoTracking().SingleAsync(cancellationToken).ConfigureAwait(false);
        return selection.AppliedBrokerEnvironmentId is { } id ? await ResolveAsync(id, cancellationToken).ConfigureAwait(false) : null;
    }

    public async Task<AppliedBrokerEnvironmentContext?> ResolveAsync(Guid brokerEnvironmentId, CancellationToken cancellationToken)
    {
        var item = await dbContext.BrokerEnvironments.AsNoTracking().SingleOrDefaultAsync(item => item.BrokerEnvironmentId == brokerEnvironmentId, cancellationToken).ConfigureAwait(false);
        return item is null ? null : new(
            item.BrokerEnvironmentId,
            item.Provider,
            item.Kind,
            item.Lifecycle,
            item.Availability,
            item.EndpointProfile,
            string.Equals(item.Provider, "IG", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Kind, "Demo", StringComparison.OrdinalIgnoreCase),
            string.Equals(item.Provider, "IG", StringComparison.OrdinalIgnoreCase) &&
            ((string.Equals(item.Kind, "Demo", StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.EndpointProfile, "IgDemo", StringComparison.Ordinal))
            || (string.Equals(item.Kind, "Live", StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.EndpointProfile, "IgLive", StringComparison.Ordinal))));
    }
}