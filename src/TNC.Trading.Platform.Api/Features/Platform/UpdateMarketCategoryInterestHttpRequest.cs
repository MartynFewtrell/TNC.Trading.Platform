using System.Text.Json.Serialization;

namespace TNC.Trading.Platform.Api.Features.Platform;

/// <summary>Request to update a category interest at a specific environment-wide revision.</summary>
internal sealed record UpdateMarketCategoryInterestHttpRequest
{
    [JsonRequired]
    public required bool Interested { get; init; }

    [JsonRequired]
    public required long ExpectedRevision { get; init; }
}
