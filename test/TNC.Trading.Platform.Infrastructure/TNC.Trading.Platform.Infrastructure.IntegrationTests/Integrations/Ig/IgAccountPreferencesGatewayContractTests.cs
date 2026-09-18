using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;
using TNC.Trading.Platform.Infrastructure.Integrations.Ig;
using TNC.Trading.Platform.TestShared.AccountPreferences;

namespace TNC.Trading.Platform.Infrastructure.IntegrationTests.Integrations.Ig;

public sealed class IgAccountPreferencesGatewayContractTests
{
    /// <summary>
    /// Verifies the real gateway speaks the production IG session and account-preferences contract to the controlled provider.
    /// Expected: GET observes the provider value, PUT acknowledges SUCCESS, and remediation reads the updated value back.
    /// Why: this boundary catches route, payload, session-account, and session-header drift before functional tests exercise the workflow.
    /// </summary>
    [Fact]
    public async Task Gateway_ShouldObserveAndRemediateThroughProductionContract_WhenProviderIsControlled()
    {
        await using var provider = ControllableIgProvider.Start();
        var gateway = new IgAccountPreferencesGateway(
            new HttpClient { BaseAddress = new Uri(provider.BaseUri, "gateway/deal/") },
            new FakeProtectedCredentialService());

        var observed = await gateway.ObserveAsync(new(provider.AccountId), CancellationToken.None);
        var remediated = await gateway.RemediateAsync(new(provider.AccountId, true), CancellationToken.None);

        Assert.False(observed.TrailingStopsEnabled);
        Assert.True(remediated.WritePerformed);
        Assert.True(remediated.TrailingStopsEnabled);
        Assert.Equal(
            [
                "POST /gateway/deal/session",
                "GET /gateway/deal/accounts/preferences",
                "POST /gateway/deal/session",
                "GET /gateway/deal/accounts/preferences",
                "PUT /gateway/deal/accounts/preferences",
                "GET /gateway/deal/accounts/preferences"
            ],
            provider.Requests);
    }

    private sealed class FakeProtectedCredentialService : IProtectedCredentialService
    {
        public Task<CredentialPresence> GetPresenceAsync(BrokerEnvironmentKind environment, CancellationToken cancellationToken) =>
            Task.FromResult(new CredentialPresence(true, true, true));

        public Task<IgCredentials> GetCredentialsAsync(BrokerEnvironmentKind environment, CancellationToken cancellationToken) =>
            Task.FromResult(new IgCredentials("api-key", "identifier", "password"));

        public Task UpdateAsync(BrokerEnvironmentKind environment, string? apiKey, string? identifier, string? password, string changedBy, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}