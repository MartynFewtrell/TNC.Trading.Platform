using TNC.Trading.Platform.Application.Features.MarketCategories;

namespace TNC.Trading.Platform.Application.UnitTests.Features.MarketCategories;

public sealed class MarketCategoriesHandlerTests
{
    /// <summary>
    /// Trace: Market Categories Work Item 1 query contract.
    /// Verifies: saved categories are sorted by ordinal code without consulting the provider.
    /// Expected: the response is ordered ordinally and the gateway is never called.
    /// Why: Viewer reads must be deterministic and provider-free.
    /// </summary>
    [Fact]
    public async Task GetAsync_ShouldSortSavedCategoriesOrdinallyWithoutCallingGateway()
    {
        var store = new SnapshotStore
        {
            Snapshot = new(
                [new("zulu", false), new("Alpha", true), new("beta", false)],
                new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero))
        };

        var result = await new GetMarketCategoriesHandler(store)
            .HandleAsync(new(), CancellationToken.None);

        Assert.Equal(["Alpha", "beta", "zulu"], result.Categories.Select(category => category.Code));
        Assert.True(result.HasSavedSnapshot);
    }

    /// <summary>
    /// Trace: Market Categories Work Item 1 empty-state contract.
    /// Verifies: no persisted row is represented explicitly with an empty catalogue and nullable timestamp.
    /// Expected: HasSavedSnapshot is false and LastRefreshedAtUtc is null.
    /// Why: the UI and API must distinguish never-refreshed from a populated snapshot.
    /// </summary>
    [Fact]
    public async Task GetAsync_ShouldReturnExplicitNeverRefreshedState_WhenStoreIsEmpty()
    {
        var result = await new GetMarketCategoriesHandler(new SnapshotStore())
            .HandleAsync(new(), CancellationToken.None);

        Assert.False(result.HasSavedSnapshot);
        Assert.Empty(result.Categories);
        Assert.Null(result.LastRefreshedAtUtc);
    }

    /// <summary>
    /// Trace: Market Categories Work Item 1 refresh persistence contract.
    /// Verifies: a successful gateway result is mapped to one complete replacement save.
    /// Expected: exactly one store call receives the gateway categories and refresh timestamp.
    /// Why: replacing the complete catalogue prevents stale provider codes from surviving.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_ShouldSaveExactlyOneReplacement_WhenGatewaySucceeds()
    {
        var categories = (IReadOnlyList<MarketCategory>)[new("FX", false), new("INDICES", true)];
        var store = new SnapshotStore();
        var gateway = new Gateway { Result = new MarketCategoriesGatewayResult.Succeeded(categories) };
        var result = await new RefreshMarketCategoriesHandler(gateway, store, TimeProvider.System)
            .HandleAsync(new(), CancellationToken.None);

        var saved = Assert.IsType<MarketCategoriesRefreshOutcome.Saved>(result.Outcome);
        Assert.Equal(categories, saved.Snapshot.Categories);
        Assert.Equal(1, store.ReplaceCalls);
        Assert.Same(saved.Snapshot, store.Snapshot);
        Assert.True(saved.Snapshot.LastRefreshedAtUtc <= DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Trace: Market Categories Work Item 1 typed-failure contract.
    /// Verifies: each provider failure is returned as safe typed data without persistence.
    /// Expected: the failure category is preserved and the store is not called.
    /// Why: provider faults must never erase a previously saved catalogue.
    /// </summary>
    [Theory]
    [InlineData(MarketCategoriesFailureCategory.UnsupportedEnvironment)]
    [InlineData(MarketCategoriesFailureCategory.Unauthorized)]
    [InlineData(MarketCategoriesFailureCategory.RateLimited)]
    [InlineData(MarketCategoriesFailureCategory.Unavailable)]
    [InlineData(MarketCategoriesFailureCategory.Timeout)]
    [InlineData(MarketCategoriesFailureCategory.MalformedProviderData)]
    [InlineData(MarketCategoriesFailureCategory.Rejected)]
    [InlineData(MarketCategoriesFailureCategory.Transient)]
    public async Task RefreshAsync_ShouldNotWrite_WhenGatewayFails(MarketCategoriesFailureCategory category)
    {
        var store = new SnapshotStore();
        var gateway = new Gateway { Result = new MarketCategoriesGatewayResult.Failed(category, "safe") };

        var result = await new RefreshMarketCategoriesHandler(gateway, store, TimeProvider.System)
            .HandleAsync(new(), CancellationToken.None);

        var failure = Assert.IsType<MarketCategoriesRefreshOutcome.Failed>(result.Outcome);
        Assert.Equal(category, failure.Category);
        Assert.Equal(0, store.ReplaceCalls);
    }

    /// <summary>
    /// Trace: Market Categories Work Item 1 cancellation contract.
    /// Verifies: caller cancellation is passed to the gateway and not translated into a provider failure.
    /// Expected: OperationCanceledException propagates and no write occurs.
    /// Why: request cancellation must remain observable to the hosting pipeline.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_ShouldPropagateCallerCancellation_WhenGatewayIsCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var store = new SnapshotStore();
        var gateway = new Gateway { ThrowCancellation = true };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new RefreshMarketCategoriesHandler(gateway, store, TimeProvider.System)
                .HandleAsync(new(), cancellation.Token));

        Assert.Equal(cancellation.Token, gateway.ReceivedToken);
        Assert.Equal(0, store.ReplaceCalls);
    }

    private sealed class Gateway : IMarketCategoriesGateway
    {
        public MarketCategoriesGatewayResult Result { get; set; } =
            new MarketCategoriesGatewayResult.Succeeded([]);
        public bool ThrowCancellation { get; set; }
        public CancellationToken ReceivedToken { get; private set; }

        public Task<MarketCategoriesGatewayResult> GetAsync(CancellationToken cancellationToken)
        {
            ReceivedToken = cancellationToken;
            if (ThrowCancellation)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return Task.FromResult(Result);
        }
    }

    private sealed class SnapshotStore : IMarketCategorySnapshotStore
    {
        public MarketCategorySnapshot? Snapshot { get; set; }
        public int ReplaceCalls { get; private set; }
        public Task<MarketCategorySnapshot?> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Snapshot);

        public Task<MarketCategorySnapshot> ReplaceAsync(
            MarketCategorySnapshot snapshot,
            CancellationToken cancellationToken)
        {
            ReplaceCalls++;
            Snapshot = snapshot;
            return Task.FromResult(snapshot);
        }
    }
}
