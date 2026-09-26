using System.Net;

namespace TNC.Trading.Platform.Api.IntegrationTests.Authentication;

[Collection(AuthenticationIntegrationTestCollection.Name)]
public sealed class MarketDetailAuthorizationIntegrationTests(SyntheticTokenIntegrationTestFixture fixture)
{
    /// <summary>
    /// Trace: Market Details Work Item 5, step 4.
    /// Verifies: direct saved-detail reads are protected before any database or provider work.
    /// Expected: an anonymous request receives HTTP 401 Unauthorized.
    /// Why: saved prices and instrument terms remain inside the delegated Viewer boundary.
    /// </summary>
    [Fact]
    public async Task MarketDetailEndpoint_ShouldReturnUnauthorized_WhenRequestIsAnonymous()
    {
        using var client = fixture.CreateApiClient();
        using var response = await client.GetAsync(
            "/api/platform/market-categories/FX/instruments/CS.D.ADAUSD.CFD.IP/market-details");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Trace: Market Details Work Item 5, step 4.
    /// Verifies: a valid delegated Viewer can cross the protected detail route even when the requested current membership is absent.
    /// Expected: the route returns its safe 404/503 read outcome rather than an authentication or authorization failure.
    /// Why: authorization must allow Viewer reads without granting a provider refresh or exposing internal database failures.
    /// </summary>
    [Theory]
    [InlineData("Viewer", "platform.viewer")]
    [InlineData("Operator", "platform.operator")]
    public async Task MarketDetailEndpoint_ShouldPassViewerAuthorization_WhenViewerOrOperatorTokenIsValid(
        string role,
        string scope)
    {
        using var client = fixture.CreateApiClient();
        using var request = TestJwtTokenFactory.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/platform/market-categories/FX/instruments/CS.D.ADAUSD.CFD.IP/market-details",
            $"market-detail-{role.ToLowerInvariant()}",
            [role],
            [scope]);
        using var response = await client.SendAsync(request);

        Assert.True(response.StatusCode is not HttpStatusCode.Unauthorized and not HttpStatusCode.Forbidden);
    }
}
