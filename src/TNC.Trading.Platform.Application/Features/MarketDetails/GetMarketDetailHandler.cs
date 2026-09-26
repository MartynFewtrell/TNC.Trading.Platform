using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed class GetMarketDetailHandler(
    IAppliedBrokerEnvironmentContextResolver appliedEnvironmentResolver,
    IMarketDetailReader detailReader)
{
    public async Task<GetMarketDetailResponse> HandleAsync(
        GetMarketDetailRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.CategoryCode)
            || request.CategoryCode.Length > 128
            || request.CategoryCode.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(request.Epic)
            || request.Epic.Length > 64
            || request.Epic.Any(char.IsControl)
            || request.ExpectedListingVersion is < 1)
        {
            throw new ArgumentException("The market-detail read request is invalid.", nameof(request));
        }

        var applied = await appliedEnvironmentResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        if (!MarketCategoryInstrumentEnvironment.TryGetSupported(applied, out var environment))
        {
            return new(
                GetMarketDetailStatus.AppliedEnvironmentUnavailable,
                null,
                request.CategoryCode,
                request.Epic,
                null);
        }

        var detail = await detailReader.ReadAsync(
            new(environment, request.CategoryCode, request.Epic, request.ExpectedListingVersion),
            cancellationToken).ConfigureAwait(false);
        if (!detail.CurrentMembershipExists)
        {
            return new(GetMarketDetailStatus.CurrentMembershipNotFound, environment, request.CategoryCode, request.Epic, null);
        }

        if (!detail.ListingVersionMatches)
        {
            return new(GetMarketDetailStatus.StaleListingVersion, environment, request.CategoryCode, request.Epic, null);
        }

        return new(GetMarketDetailStatus.Found, environment, request.CategoryCode, request.Epic, detail);
    }
}
