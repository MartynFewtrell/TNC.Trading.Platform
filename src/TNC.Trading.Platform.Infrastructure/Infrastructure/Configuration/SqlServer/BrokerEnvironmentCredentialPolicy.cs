using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;

internal static class BrokerEnvironmentCredentialPolicy
{
    internal static bool CanStoreIgCredentials(BrokerEnvironmentEntity entity) =>
        entity.Lifecycle is not ("Retired" or "Unavailable") &&
        IsSupportedIgCredentialProfile(entity.Provider, entity.Kind, entity.EndpointProfile);

    internal static bool IsSupportedIgCredentialProfile(string provider, string kind, string endpointProfile) =>
        string.Equals(provider, "IG", StringComparison.OrdinalIgnoreCase) &&
        ((string.Equals(kind, "Demo", StringComparison.OrdinalIgnoreCase)
          && string.Equals(endpointProfile, "IgDemo", StringComparison.Ordinal))
         || (string.Equals(kind, "Live", StringComparison.OrdinalIgnoreCase)
             && string.Equals(endpointProfile, "IgLive", StringComparison.Ordinal)));
}
