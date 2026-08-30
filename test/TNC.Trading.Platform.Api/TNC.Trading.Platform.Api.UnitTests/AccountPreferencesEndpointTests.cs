using Microsoft.AspNetCore.Http;
using TNC.Trading.Platform.Application.Features.AccountPreferences;
using TNC.Trading.Platform.Api.Features.Platform;

namespace TNC.Trading.Platform.Api.UnitTests;

public sealed class AccountPreferencesEndpointTests
{
    /// <summary>Trace: FR3, SR1. Verifies malformed history paging input is rejected at the HTTP boundary before application dispatch.</summary>
    [Theory]
    [InlineData(0, null)]
    [InlineData(101, null)]
    [InlineData(null, "")]
    public async Task HandleAsync_ShouldReturnValidationProblem_WhenHistoryPagingIsInvalid(int? pageSize, string? cursor)
    {
        var result = await GetTrailingStopsPreferenceObservationsEndpointHandler.HandleAsync(pageSize, cursor, null!, CancellationToken.None);

        var problem = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ValidationProblem>(result);
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(problem);
    }

    /// <summary>Trace: FR3, SR1. Verifies omitted PUT input is rejected before the account-preferences handler is invoked.</summary>
    [Fact]
    public async Task HandleAsync_ShouldReturnValidationProblem_WhenTrailingStopsInputIsMissing()
    {
        var result = await UpdateAccountPreferencesEndpointHandler.HandleAsync(
            new UpdateAccountPreferencesHttpRequest(null),
            new UpdateAccountPreferencesValidator(),
            null!,
            CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ValidationProblem>(result);
    }
}
