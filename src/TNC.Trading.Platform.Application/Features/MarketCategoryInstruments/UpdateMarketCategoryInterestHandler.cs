using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal sealed class UpdateMarketCategoryInterestHandler(
    IAppliedBrokerEnvironmentContextResolver appliedEnvironmentResolver,
    IMarketCategorySnapshotStore categorySnapshotStore,
    IMarketCategoryInstrumentInterestReader interestReader,
    IMarketCategoryInstrumentInterestWriter interestWriter)
{
    public async Task<UpdateMarketCategoryInterestResponse> HandleAsync(
        UpdateMarketCategoryInterestRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.CategoryCode)
            || request.CategoryCode.Length > 128
            || request.ExpectedRevision < 0)
        {
            throw new ArgumentException("A valid category code and non-negative expected revision are required.", nameof(request));
        }

        var applied = await appliedEnvironmentResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (!MarketCategoryInstrumentEnvironment.TryGetSupported(applied, out var appliedEnvironment))
        {
            return new(UpdateMarketCategoryInterestStatus.AppliedEnvironmentUnavailable, null);
        }

        var categories = await categorySnapshotStore.GetAsync(cancellationToken).ConfigureAwait(false);
        if (categories is null || !categories.Categories.Any(item =>
                string.Equals(item.Code, request.CategoryCode, StringComparison.Ordinal)))
        {
            return new(UpdateMarketCategoryInterestStatus.CategoryNotFound, null);
        }

        var current = await interestReader.ReadAsync(appliedEnvironment, cancellationToken).ConfigureAwait(false);
        if (current.Revision != request.ExpectedRevision)
        {
            return new(UpdateMarketCategoryInterestStatus.RevisionConflict, current.Revision);
        }

        var interests = current.Interests
            .Select(item => string.Equals(item.CategoryCode, request.CategoryCode, StringComparison.Ordinal)
                ? item with { IsSelected = request.Interested }
                : item)
            .ToArray();
        try
        {
            var revision = await interestWriter.SaveAsync(
                appliedEnvironment,
                interests,
                request.ExpectedRevision,
                cancellationToken).ConfigureAwait(false);
            return new(UpdateMarketCategoryInterestStatus.Saved, revision);
        }
        catch (MarketCategoryInstrumentInterestConflictException)
        {
            var latest = await interestReader.ReadAsync(appliedEnvironment, cancellationToken).ConfigureAwait(false);
            return new(UpdateMarketCategoryInterestStatus.RevisionConflict, latest.Revision);
        }
    }
}
