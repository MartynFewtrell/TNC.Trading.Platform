using Microsoft.AspNetCore.Http.HttpResults;
using TNC.Trading.Platform.Application.Features.AccountPreferences;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class GetTrailingStopsPreferenceObservationsEndpointHandler
{
    public static async Task<IResult> HandleAsync(int? pageSize, string? cursor, GetTrailingStopsPreferenceObservationsHandler handler, CancellationToken cancellationToken)
    {
        if (pageSize is < 1 or > 100)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(pageSize)] = ["Page size must be between 1 and 100."] });
        }

        if (cursor is not null && (cursor.Length == 0 || cursor.Length > 128))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(cursor)] = ["Cursor is invalid."] });
        }

        var result = await handler.HandleAsync(new GetTrailingStopsPreferenceObservationsRequest(pageSize ?? 25, cursor), cancellationToken);
        if (result.HasInvalidCursor)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(cursor)] = ["Cursor is invalid or does not belong to the selected environment."] });
        }
        return TypedResults.Ok(new AccountPreferencesHistoryResponse(result.Observations.Select(item => item.ToResponse()).ToList(), result.NextCursor));
    }
}
