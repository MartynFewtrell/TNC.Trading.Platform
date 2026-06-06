using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Infrastructure.Infrastructure.Platform;

internal sealed class InMemoryPlatformIgProofDataStore : IPlatformIgProofDataStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<BrokerEnvironmentKind, IgProofDataSnapshot> _store = new();

    public Task<IgProofDataSnapshot?> GetLatestAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken)
        => Task.FromResult(_store.TryGetValue(brokerEnvironment, out var snapshot) ? snapshot : null);

    public Task SaveAsync(BrokerEnvironmentKind brokerEnvironment, IgProofDataSnapshot snapshot, CancellationToken cancellationToken)
    {
        _store[brokerEnvironment] = snapshot;
        return Task.CompletedTask;
    }
}
    }
}
