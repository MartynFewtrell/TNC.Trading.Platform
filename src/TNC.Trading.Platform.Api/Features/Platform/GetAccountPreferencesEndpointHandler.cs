using Microsoft.AspNetCore.Http.HttpResults;
using TNC.Trading.Platform.Application.Features.AccountPreferences;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class GetAccountPreferencesEndpointHandler
{
    public static async Task<IResult> HandleAsync(GetAccountPreferencesHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetAccountPreferencesRequest(), cancellationToken);
        return result.Outcome.ToHttpResult();
    }
}
