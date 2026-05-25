using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace TNC.Trading.Platform.AppHost;

/// <summary>
/// Focused composition tests for the assembled AppHost resource graph.
/// </summary>
/// <remarks>
/// Requirement traceability: FR1, FR2, FR3, FR4, NF2, NF5, SR1, SR3, IR1, IR2, TR1, TR2, OR1.
/// These tests verify that the full AppHost composition assembled from the extracted support units still
/// contains the expected resources, waits, URLs, and provider-parity behavior after the refactor. This matters
/// because the work package promises a stable local topology while moving composition logic into smaller units.
/// </remarks>
public sealed class AppHostCompositionTests
{
    /// <summary>
    /// Verifies that the assembled AppHost composition preserves the expected resource set, waits, and URLs.
    /// </summary>
    /// <remarks>
    /// Expected outcome: the resource graph contains sql, platformdb, mailpit, keycloak, api, and web;
    /// the API waits for the database, Mailpit, and Keycloak; the Web waits for the API and Keycloak; and the
    /// documented Scalar UI, Operator UI, Mailpit UI, and Keycloak Admin Console links remain discoverable.
    /// Why this matters: this is the focused lower-level smoke that catches topology drift before expensive distributed runs.
    /// </remarks>
    [Fact]
    public void Compose_ShouldPreserveExpectedTopology_WhenAppHostSupportUnitsAreAssembled()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var settings = new AppHostSettings(
            ApiAuthenticationProvider: null,
            EnableInteractiveTestSignIn: false,
            AcsEndpoint: null,
            AcsSenderAddress: null,
            AcsConnectionString: null);

        var infrastructure = AppHostInfrastructureRegistration.Create(builder);
        var apiProject = AppHostProjectRegistration.AddApiProject(builder, infrastructure);
        var webProject = AppHostProjectRegistration.AddWebProject(builder, apiProject);

        AppHostEnvironmentWiring.ConfigureApiProject(apiProject, infrastructure, settings);
        AppHostEnvironmentWiring.ConfigureWebProject(webProject);
        AppHostEnvironmentWiring.ConfigureAuthenticationProvider(apiProject, webProject, infrastructure.Keycloak, settings);

        var resourceNames = builder.Resources.Select(resource => resource.Name).ToArray();
        var apiWaitResourceNames = apiProject.Resource.Annotations.OfType<WaitAnnotation>().Select(annotation => annotation.Resource.Name).ToArray();
        var webWaitResourceNames = webProject.Resource.Annotations.OfType<WaitAnnotation>().Select(annotation => annotation.Resource.Name).ToArray();

        Assert.Contains("sql", resourceNames);
        Assert.Contains("platformdb", resourceNames);
        Assert.Contains("mailpit", resourceNames);
        Assert.Contains("keycloak", resourceNames);
        Assert.Contains("api", resourceNames);
        Assert.Contains("web", resourceNames);

        Assert.Contains("platformdb", apiWaitResourceNames);
        Assert.Contains("mailpit", apiWaitResourceNames);
        Assert.Contains("keycloak", apiWaitResourceNames);

        Assert.Contains("api", webWaitResourceNames);
        Assert.Contains("keycloak", webWaitResourceNames);

        Assert.Contains(
            infrastructure.Mailpit.Resource.Annotations,
            annotation => annotation is ResourceUrlsCallbackAnnotation);
        Assert.Contains(
            infrastructure.Keycloak.Resource.Annotations,
            annotation => annotation is ResourceUrlsCallbackAnnotation);
        Assert.Contains(
            apiProject.Resource.Annotations,
            annotation => annotation is ResourceUrlsCallbackAnnotation);
        Assert.Contains(
            webProject.Resource.Annotations,
            annotation => annotation is ResourceUrlsCallbackAnnotation);

        var mailpitEndpointNames = infrastructure.Mailpit.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .Select(annotation => annotation.Name)
            .ToArray();
        var keycloakEndpointNames = infrastructure.Keycloak.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .Select(annotation => annotation.Name)
            .ToArray();
        var apiEndpointNames = apiProject.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .Select(annotation => annotation.Name)
            .ToArray();
        var webEndpointNames = webProject.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .Select(annotation => annotation.Name)
            .ToArray();

        Assert.Contains("http", mailpitEndpointNames);
        Assert.Contains("smtp", mailpitEndpointNames);
        Assert.Contains("http", keycloakEndpointNames);
        Assert.Contains("https", apiEndpointNames);
        Assert.Contains("https", webEndpointNames);
    }

    /// <summary>
    /// Verifies that only the API provider branch can switch to the synthetic test provider while the Web runtime remains Keycloak-backed.
    /// </summary>
    /// <remarks>
    /// Expected outcome: the API provider changes to Test when explicitly requested, while the Web provider remains Keycloak.
    /// Why this matters: the mitigation plan requires provider-parity proof that the delivered runtime stays Keycloak-backed except for the isolated API negative-path override.
    /// </remarks>
    [Fact]
    public void Compose_ShouldKeepWebRuntimeKeycloakBacked_WhenApiProviderOverrideUsesTestAuthentication()
    {
        var testProviderSettings = new AppHostSettings(
            ApiAuthenticationProvider: "Test",
            EnableInteractiveTestSignIn: false,
            AcsEndpoint: null,
            AcsSenderAddress: null,
            AcsConnectionString: null);

        var apiEnvironmentValues = AppHostEnvironmentWiring.GetApiAuthenticationEnvironmentValues(testProviderSettings);
        var webEnvironmentValues = AppHostEnvironmentWiring.GetWebAuthenticationEnvironmentValues(testProviderSettings);

        Assert.Equal("Test", apiEnvironmentValues["Authentication__Provider"]);
        Assert.Equal("Keycloak", webEnvironmentValues["Authentication__Provider"]);
        Assert.Equal(AppHostCompositionConstants.KeycloakAuthority, webEnvironmentValues["Authentication__Keycloak__Authority"]);
    }
}
