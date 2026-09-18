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

    /// <summary>Trace: account-preferences IG-first save contract. A confirmed save must read back from IG before returning the durable projection, including the server-owned account and actor.</summary>
    [Fact]
    public async Task Save_ShouldReturnConfirmedProjection_WhenProviderConfirmsTheRequestedValue()
    {
        await fixture.ResetAccountPreferencesAsync();
        fixture.Provider.Reset();
        using var client = new HttpClient { BaseAddress = fixture.ApiBaseUri };
        using var current = await SendAsync(client, HttpMethod.Get, "/api/platform/account-preferences");
        var currentState = await current.Content.ReadFromJsonAsync<JsonElement>();
        currentState.TryGetProperty("desiredRevision", out var currentRevision);
        using var response = await SendAsync(client, HttpMethod.Put, "/api/platform/account-preferences", new { trailingStopsEnabled = true, expectedRevision = currentRevision.ValueKind == JsonValueKind.Number ? currentRevision.GetInt64() : (long?)null }, Guid.NewGuid().ToString("N"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(fixture.Provider.AccountId, state.GetProperty("accountId").GetString());
        Assert.True(state.GetProperty("desiredTrailingStopsEnabled").GetBoolean());
        Assert.True(state.GetProperty("observedTrailingStopsEnabled").GetBoolean());
        Assert.Equal("InSync", state.GetProperty("verificationStatus").GetString());
        Assert.Contains("PUT /gateway/deal/accounts/preferences", fixture.Provider.Requests);
    }

    /// <summary>Trace: account-preferences equality, drift correction, and body-free Check status. Repeated equal saves are idempotent, while Check status converges external drift.</summary>
    [Fact]
    public async Task CheckStatus_ShouldConvergeDriftAndPreserveEquality_WhenProviderStateChanges()
    {
        await fixture.ResetAccountPreferencesAsync();
        fixture.Provider.Reset();
        using var client = new HttpClient { BaseAddress = fixture.ApiBaseUri };
        var key = Guid.NewGuid().ToString("N");

        using var current = await SendAsync(client, HttpMethod.Get, "/api/platform/account-preferences");
        var currentState = await current.Content.ReadFromJsonAsync<JsonElement>();
        currentState.TryGetProperty("desiredRevision", out var currentRevision);
        using var saved = await SendAsync(client, HttpMethod.Put, "/api/platform/account-preferences", new { trailingStopsEnabled = true, expectedRevision = currentRevision.ValueKind == JsonValueKind.Number ? currentRevision.GetInt64() : (long?)null }, key);
        var savedState = await saved.Content.ReadFromJsonAsync<JsonElement>();
        var revision = savedState.GetProperty("desiredRevision").GetInt64();
        using var replay = await SendAsync(client, HttpMethod.Put, "/api/platform/account-preferences", new { trailingStopsEnabled = true, expectedRevision = revision }, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        fixture.Provider.Preference = false;
        using var check = await SendAsync(client, HttpMethod.Post, "/api/platform/account-preferences/check-status");
        Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        var checkedState = await check.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(checkedState.GetProperty("desiredTrailingStopsEnabled").GetBoolean());
        Assert.True(checkedState.GetProperty("observedTrailingStopsEnabled").GetBoolean());
        Assert.Equal(fixture.Provider.AccountId, checkedState.GetProperty("observedAccountId").GetString());
        using var history = await SendAsync(client, HttpMethod.Get, "/api/platform/account-preferences/observations?pageSize=20");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        var historyBody = await history.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(historyBody.GetProperty("observations").GetArrayLength() >= 2);
    }

    /// <summary>Trace: account-preferences failure, account ownership, and recovery. Provider failure is surfaced without accepting browser-owned account or actor data, then Check status recovers.</summary>
    [Fact]
    public async Task SaveAndCheckStatus_ShouldFailSafelyAndRecover_WhenProviderIsUnavailable()
    {
        await fixture.ResetAccountPreferencesAsync();
        fixture.Provider.Reset();
        using var client = new HttpClient { BaseAddress = fixture.ApiBaseUri };

        using var current = await SendAsync(client, HttpMethod.Get, "/api/platform/account-preferences");
        var currentState = await current.Content.ReadFromJsonAsync<JsonElement>();
        currentState.TryGetProperty("desiredRevision", out var currentRevision);
        using var confirmed = await SendAsync(client, HttpMethod.Put, "/api/platform/account-preferences", new { trailingStopsEnabled = true, expectedRevision = currentRevision.ValueKind == JsonValueKind.Number ? currentRevision.GetInt64() : (long?)null }, Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var confirmedState = await confirmed.Content.ReadFromJsonAsync<JsonElement>();
        var confirmedRevision = confirmedState.GetProperty("desiredRevision").GetInt64();

        fixture.Provider.Available = false;
        using var unavailableCheck = await SendAsync(client, HttpMethod.Post, "/api/platform/account-preferences/check-status");
        var unavailableCheckBody = await unavailableCheck.Content.ReadAsStringAsync();
        Assert.True(unavailableCheck.StatusCode == HttpStatusCode.OK, $"Expected OK but received {unavailableCheck.StatusCode}: {unavailableCheckBody}");
        var unavailableCheckState = JsonSerializer.Deserialize<JsonElement>(unavailableCheckBody);
        Assert.Equal("VerificationFailed", unavailableCheckState.GetProperty("applicationStatus").GetString());
        Assert.Contains("unavailable", unavailableCheckState.GetProperty("failureSummary").GetString(), StringComparison.OrdinalIgnoreCase);

        using var failed = await SendAsync(client, HttpMethod.Put, "/api/platform/account-preferences", new { trailingStopsEnabled = true, expectedRevision = confirmedRevision, accountId = "attacker", actor = "attacker" }, Guid.NewGuid().ToString("N"));
        var failedBody = await failed.Content.ReadAsStringAsync();
        Assert.True(failed.StatusCode == HttpStatusCode.ServiceUnavailable, $"Expected ServiceUnavailable but received {failed.StatusCode}: {failedBody}");
        Assert.Contains("\"failureCategory\":\"Unavailable\"", failedBody, StringComparison.Ordinal);
        Assert.DoesNotContain("attacker", failedBody, StringComparison.Ordinal);
        fixture.Provider.Available = true;
        fixture.Provider.Preference = true;
        using var recovered = await SendAsync(client, HttpMethod.Post, "/api/platform/account-preferences/check-status");
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        var state = await recovered.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(fixture.Provider.AccountId, state.GetProperty("accountId").GetString());
        Assert.Equal(fixture.Provider.AccountId, state.GetProperty("observedAccountId").GetString());
        Assert.True(state.GetProperty("observedTrailingStopsEnabled").GetBoolean());
    }

    private async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, object? body = null, string? idempotencyKey = null)
    {
        using var request = await TNC.Trading.Platform.Api.IntegrationTests.Authentication.RealKeycloakAccessTokenFactory.CreateAuthenticatedRequestAsync(fixture.TokenEndpoint, method, path, "local-operator", "platform.operator");
        if (idempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}