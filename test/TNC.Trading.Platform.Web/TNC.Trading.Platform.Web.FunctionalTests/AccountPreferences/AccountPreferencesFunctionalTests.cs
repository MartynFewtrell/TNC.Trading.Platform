using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TNC.Trading.Platform.Web.FunctionalTests.AccountPreferences;

[Collection(Authentication.AuthenticationFunctionalTestCollection.Name)]
public sealed class AccountPreferencesFunctionalTests
{
    private readonly Authentication.RealAuthenticationFunctionalTestFixture fixture;

    public AccountPreferencesFunctionalTests(Authentication.RealAuthenticationFunctionalTestFixture fixture)
    {
        this.fixture = fixture;
    }

    /// <summary>
    /// Trace: Phase 5.2. Verifies a fresh migrated database returns successful Unconfigured state while the external provider is unavailable and is not contacted by the initial read.
    /// Expected: the authenticated public GET returns HTTP 200 with verificationStatus Unconfigured and no preferences GET appears in the reset provider ledger.
    /// Why: a missing durable projection is valid first-run state and must remain available during provider outage without retry amplification or a misleading warning.
    /// </summary>
    [Fact]
    public async Task AccountPreferences_ShouldReturnUnconfiguredWithoutProviderRead_WhenFreshInstallProviderIsUnavailable()
    {
        fixture.Provider.ClearRequests();
        fixture.Provider.Available = false;
        using var client = new HttpClient { BaseAddress = fixture.ApiBaseUri };
        using var request = await TNC.Trading.Platform.Api.IntegrationTests.Authentication.RealKeycloakAccessTokenFactory.CreateAuthenticatedRequestAsync(
            fixture.TokenEndpoint,
            "/api/platform/account-preferences",
            "local-operator",
            "platform.operator");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Unconfigured", state.GetProperty("verificationStatus").GetString());
        Assert.DoesNotContain("GET /gateway/deal/preferences", fixture.Provider.Requests);
    }

    /// <summary>
    /// Trace: account-preferences state verification availability and convergence requirements.
    /// Verifies: the public platform API reads durable SQL state while the provider is unavailable, accepts desired intent, and later converges after provider recovery.
    /// Expected: GET remains successful during outage, PUT persists the requested value, and a subsequent verification retry reports the recovered observed state.
    /// Why: operator intent and current database state must remain available independently of provider availability.
    /// </summary>
    [Fact]
    public async Task AccountPreferences_ShouldPersistAndConverge_WhenProviderRecovers()
    {
        fixture.Provider.Available = false;
        using var client = new HttpClient { BaseAddress = fixture.ApiBaseUri };

        using var unavailableRequest = await TNC.Trading.Platform.Api.IntegrationTests.Authentication.RealKeycloakAccessTokenFactory.CreateAuthenticatedRequestAsync(
            fixture.TokenEndpoint,
            "/api/platform/account-preferences",
            "local-operator",
            "platform.operator");
        using var unavailableRead = await client.SendAsync(unavailableRequest);
        Assert.Equal(HttpStatusCode.OK, unavailableRead.StatusCode);
        var initialState = await unavailableRead.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(initialState.TryGetProperty("desiredTrailingStopsEnabled", out _));

        using var updateRequest = await TNC.Trading.Platform.Api.IntegrationTests.Authentication.RealKeycloakAccessTokenFactory.CreateAuthenticatedRequestAsync(
            fixture.TokenEndpoint,
            HttpMethod.Put,
            "/api/platform/account-preferences",
            "local-operator",
            "platform.operator");
        updateRequest.Content = JsonContent.Create(new { trailingStopsEnabled = true, accountId = "test-account", actor = "local-operator" });
        using var update = await client.SendAsync(updateRequest);
        var updateBody = await update.Content.ReadAsStringAsync();
        Assert.True(update.StatusCode == HttpStatusCode.OK, $"Expected OK but received {update.StatusCode}: {updateBody}");

        fixture.Provider.Available = true;
        fixture.Provider.Preference = true;
        using var retryRequest = await TNC.Trading.Platform.Api.IntegrationTests.Authentication.RealKeycloakAccessTokenFactory.CreateAuthenticatedRequestAsync(
            fixture.TokenEndpoint,
            HttpMethod.Post,
            "/api/platform/account-preferences/verification-retry",
            "local-operator",
            "platform.operator");
        retryRequest.Content = JsonContent.Create(new { accountId = "test-account" });
        using var retry = await client.SendAsync(retryRequest);
        var retryBody = await retry.Content.ReadAsStringAsync();
        Assert.True(retry.StatusCode == HttpStatusCode.OK, $"Expected OK but received {retry.StatusCode}: {retryBody}");
        var recoveredState = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("InSync", recoveredState.GetProperty("verificationStatus").GetString());
        Assert.Contains("GET /gateway/deal/preferences", fixture.Provider.Requests);
    }
}