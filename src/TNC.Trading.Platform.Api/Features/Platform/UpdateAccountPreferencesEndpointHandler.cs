using Microsoft.AspNetCore.Http.HttpResults;
using TNC.Trading.Platform.Application.Features.AccountPreferences;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class UpdateAccountPreferencesEndpointHandler
{
    public static async Task<IResult> HandleAsync(UpdateAccountPreferencesHttpRequest request, UpdateAccountPreferencesValidator validator, UpdateAccountPreferencesHandler handler, CancellationToken cancellationToken)
    {
        var errors = validator.Validate(new UpdateAccountPreferencesRequest(request.TrailingStopsEnabled));
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.TrailingStopsEnabled)] = errors.ToArray() });
        }

        var result = await handler.HandleAsync(new UpdateAccountPreferencesRequest(request.TrailingStopsEnabled), cancellationToken);
        return result.Outcome.ToHttpResult();
    }
}
