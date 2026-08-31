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
        var cookies = new CookieContainer();
        await Authentication.RealAuthenticationSessionFactory.AuthenticateBrowserSessionAsync(
            fixture.WebBaseUri,
            cookies,
            "local-operator",
            "/account-preferences",
            "platform.operator");

        using var client = Authentication.FunctionalBrowserClientFactory.Create(fixture.WebBaseUri, allowAutoRedirect: false, cookies);
        using var response = await client.GetAsync("/api/platform/account-preferences");

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
        var cookies = new CookieContainer();
        await Authentication.RealAuthenticationSessionFactory.AuthenticateBrowserSessionAsync(
            fixture.WebBaseUri,
            cookies,
            "local-operator",
            "/account-preferences",
            "platform.operator");

        fixture.Provider.Available = false;
        using var client = Authentication.FunctionalBrowserClientFactory.Create(fixture.WebBaseUri, allowAutoRedirect: false, cookies);

        using var unavailableRead = await client.GetAsync("/api/platform/account-preferences");
        Assert.Equal(HttpStatusCode.OK, unavailableRead.StatusCode);
        var initialState = await unavailableRead.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(initialState.TryGetProperty("desiredTrailingStopsEnabled", out _));

        using var update = await client.PutAsJsonAsync("/api/platform/account-preferences", new { trailingStopsEnabled = true });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        fixture.Provider.Available = true;
        fixture.Provider.Preference = true;
        using var retry = await client.PostAsJsonAsync("/api/platform/account-preferences/verification-retry", new { accountId = (string?)null });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var recoveredState = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("InSync", recoveredState.GetProperty("verificationStatus").GetString());
        Assert.Contains("GET /gateway/deal/preferences", fixture.Provider.Requests);
    }
}