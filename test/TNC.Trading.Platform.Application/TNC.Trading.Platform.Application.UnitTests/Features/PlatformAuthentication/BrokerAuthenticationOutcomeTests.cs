using TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;

namespace TNC.Trading.Platform.Application.UnitTests.Features.PlatformAuthentication;

public sealed class BrokerAuthenticationOutcomeTests
{
    /// <summary>
    /// Traces to Clean Architecture migration Phase 4.
    /// Verifies a successful inward outcome contains only provider-neutral evidence and optional proof data.
    /// Expected: authentication is successful and no public property exposes token, header, HTTP, or provider DTO vocabulary.
    /// Why: the port contract must remain safe for use-case coordination without importing IG protocol mechanics.
    /// </summary>
    [Fact]
    public void Succeeded_ShouldExposeOnlyProviderNeutralData_WhenOutcomeCrossesBoundary()
    {
        var outcome = BrokerAuthenticationOutcome.Succeeded(
            new BrokerAuthenticationEvidence("ACC001", "stream-endpoint", null),
            new BrokerAuthenticationProof("Demo", "ACC001", 100m, 1));
        var propertyNames = outcome.GetType().GetProperties().Select(property => property.Name).ToArray();
        var requestProperty = Assert.Single(typeof(BrokerAuthenticationRequest).GetProperties());

        Assert.True(outcome.IsAuthenticated);
        Assert.Equal(nameof(BrokerAuthenticationRequest.Environment), requestProperty.Name);
        Assert.DoesNotContain(propertyNames, name => name.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("Header", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("Http", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Traces to Clean Architecture migration Phase 4.
    /// Verifies a failed inward outcome carries one typed, secret-safe failure and no partial evidence or proof.
    /// Expected: authentication is false and only the supplied failure category and summary remain.
    /// Why: use cases need deterministic failure handling without provider exceptions or partially trusted response data.
    /// </summary>
    [Fact]
    public void Failed_ShouldExcludePartialAuthenticationData_WhenGatewayRejectsRequest()
    {
        var outcome = BrokerAuthenticationOutcome.Failed(new BrokerAuthenticationFailure(
            BrokerAuthenticationFailureKind.RejectedCredentials,
            "Broker authentication was rejected."));

        Assert.False(outcome.IsAuthenticated);
        Assert.Null(outcome.Evidence);
        Assert.Null(outcome.Proof);
        Assert.Equal(BrokerAuthenticationFailureKind.RejectedCredentials, outcome.Failure!.Kind);
    }
}