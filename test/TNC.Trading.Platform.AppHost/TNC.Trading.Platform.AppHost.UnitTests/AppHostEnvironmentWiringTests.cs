using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace TNC.Trading.Platform.AppHost;

/// <summary>
/// Tests for <see cref="AppHostEnvironmentWiring"/> environment value construction.
/// </summary>
/// <remarks>
/// Requirement traceability: FR1, FR2, FR3, FR4, NF1, SR1, SR3, TR1, TR2.
/// These tests verify that the AppHost environment wiring produces the expected configuration keys for
/// the API and Web projects, preserves the default Keycloak-backed runtime, and keeps the synthetic API
/// authentication branch deliberately scoped. This matters because configuration drift in the AppHost can
/// silently break local authentication, protected-route behavior, or notification transport wiring.
/// </remarks>
public sealed class AppHostEnvironmentWiringTests
{
    /// <summary>
    /// Verifies that the API base environment values include the required authentication and display-name keys.
    /// </summary>
    /// <remarks>
    /// Expected outcome: the returned environment values include the API audience, Keycloak realm and client,
    /// and both display-name claim keys.
    /// Why this matters: the API must receive a stable authentication baseline before provider-specific wiring is applied.
    /// </remarks>
    [Fact]
    public void GetApiEnvironmentValues_ShouldReturnRequiredAuthenticationKeys_WhenApiProjectIsConfigured()
    {
        var environmentValues = AppHostEnvironmentWiring.GetApiEnvironmentValues();

        Assert.Equal(AppHostCompositionConstants.ApiAudience, environmentValues["Authentication__ApiAudience"]);
        Assert.Equal(AppHostCompositionConstants.KeycloakRealmName, environmentValues["Authentication__Keycloak__Realm"]);
        Assert.Equal(AppHostCompositionConstants.ApiAudience, environmentValues["Authentication__Keycloak__ApiClientId"]);
        Assert.Equal("name", environmentValues["Authentication__Authorization__DisplayNameClaimType"]);
        Assert.Equal("preferred_username", environmentValues["Authentication__Authorization__DisplayNameFallbackClaimType"]);
    }

    /// <summary>
    /// Verifies that the Web base environment values include the required callback, scope, and Keycloak keys.
    /// </summary>
    /// <remarks>
    /// Expected outcome: the returned environment values include the callback path, signed-out redirect path,
    /// required viewer scope, and the fixed Web Keycloak client settings.
    /// Why this matters: the Web project must stay Keycloak-backed in the delivered local topology.
    /// </remarks>
    [Fact]
    public void GetWebEnvironmentValues_ShouldReturnRequiredAuthenticationKeys_WhenWebProjectIsConfigured()
    {
        var environmentValues = AppHostEnvironmentWiring.GetWebEnvironmentValues();

        Assert.Equal("/signin-oidc", environmentValues["Authentication__CallbackPath"]);
        Assert.Equal("/", environmentValues["Authentication__SignedOutRedirectPath"]);
        Assert.Equal(AppHostCompositionConstants.ApiAudience, environmentValues["Authentication__ApiAudience"]);
        Assert.Equal("platform.viewer", environmentValues["Authentication__RequiredScopes__0"]);
        Assert.Equal(AppHostCompositionConstants.KeycloakRealmName, environmentValues["Authentication__Keycloak__Realm"]);
        Assert.Equal("tnc-trading-platform-web", environmentValues["Authentication__Keycloak__ClientId"]);
        Assert.Equal(AppHostCompositionConstants.ApiAudience, environmentValues["Authentication__Keycloak__ApiClientId"]);
        Assert.Equal("LocalAuth!123", environmentValues["Authentication__Keycloak__SeededUserPassword"]);
        Assert.Equal("name", environmentValues["Authentication__Authorization__DisplayNameClaimType"]);
        Assert.Equal("preferred_username", environmentValues["Authentication__Authorization__DisplayNameFallbackClaimType"]);
    }

    /// <summary>
    /// Verifies that the API authentication environment defaults to the real Keycloak-backed provider.
    /// </summary>
    /// <remarks>
    /// Expected outcome: the provider is Keycloak, the role claim type is present, and the Keycloak authority is configured.
    /// Why this matters: the delivered local runtime must remain Keycloak-backed unless the API test-provider override is explicitly selected.
    /// </remarks>
    [Fact]
    public void GetApiAuthenticationEnvironmentValues_ShouldReturnKeycloakConfiguration_WhenApiProviderIsNotExplicitlyTest()
    {
        var settings = new AppHostSettings(
            ApiAuthenticationProvider: null,
            EnableInteractiveTestSignIn: false,
            AcsEndpoint: null,
            AcsSenderAddress: null,
            AcsConnectionString: null);

        var environmentValues = AppHostEnvironmentWiring.GetApiAuthenticationEnvironmentValues(settings);

        Assert.Equal("Keycloak", environmentValues["Authentication__Provider"]);
        Assert.Equal("role", environmentValues["Authentication__Authorization__RoleClaimType"]);
        Assert.Equal(AppHostCompositionConstants.KeycloakAuthority, environmentValues["Authentication__Keycloak__Authority"]);
        Assert.DoesNotContain("Authentication__Test__Issuer", environmentValues.Keys);
        Assert.DoesNotContain("Authentication__Test__SigningKey", environmentValues.Keys);
    }

    /// <summary>
    /// Verifies that the API authentication environment switches to the synthetic test provider only when explicitly requested.
    /// </summary>
    /// <remarks>
    /// Expected outcome: the provider is Test, the role claim type is present, and the synthetic issuer/signing key are configured.
    /// Why this matters: the isolated API negative-path branch must stay explicit instead of accidentally changing the default runtime.
    /// </remarks>
    [Fact]
    public void GetApiAuthenticationEnvironmentValues_ShouldReturnSyntheticTestConfiguration_WhenApiProviderIsExplicitlyTest()
    {
        var settings = new AppHostSettings(
            ApiAuthenticationProvider: "Test",
            EnableInteractiveTestSignIn: false,
            AcsEndpoint: null,
            AcsSenderAddress: null,
            AcsConnectionString: null);

        var environmentValues = AppHostEnvironmentWiring.GetApiAuthenticationEnvironmentValues(settings);

        Assert.Equal("Test", environmentValues["Authentication__Provider"]);
        Assert.Equal("role", environmentValues["Authentication__Authorization__RoleClaimType"]);
        Assert.Equal(AppHostCompositionConstants.TestIssuer, environmentValues["Authentication__Test__Issuer"]);
        Assert.Equal(AppHostCompositionConstants.TestSigningKey, environmentValues["Authentication__Test__SigningKey"]);
        Assert.DoesNotContain("Authentication__Keycloak__Authority", environmentValues.Keys);
    }

    /// <summary>
    /// Verifies that the Web authentication environment remains Keycloak-backed even when the API uses the synthetic provider branch.
    /// </summary>
    /// <remarks>
    /// Expected outcome: the Web provider remains Keycloak and the Keycloak authority is still configured.
    /// Why this matters: the work package explicitly preserves the real Web runtime while keeping only the isolated API negative path synthetic.
    /// </remarks>
    [Fact]
    public void GetWebAuthenticationEnvironmentValues_ShouldRemainKeycloakBacked_WhenApiProviderUsesSyntheticTestBranch()
    {
        var settings = new AppHostSettings(
            ApiAuthenticationProvider: "Test",
            EnableInteractiveTestSignIn: false,
            AcsEndpoint: null,
            AcsSenderAddress: null,
            AcsConnectionString: null);

        var environmentValues = AppHostEnvironmentWiring.GetWebAuthenticationEnvironmentValues(settings);

        Assert.Equal("Keycloak", environmentValues["Authentication__Provider"]);
        Assert.Equal(AppHostCompositionConstants.KeycloakAuthority, environmentValues["Authentication__Keycloak__Authority"]);
        Assert.Equal("role", environmentValues["Authentication__Authorization__RoleClaimType"]);
        Assert.DoesNotContain("Authentication__Test__Issuer", environmentValues.Keys);
        Assert.DoesNotContain("Authentication__Test__SigningKey", environmentValues.Keys);
    }

    /// <summary>
    /// Verifies that the Web authentication environment includes the interactive test sign-in flag only when enabled.
    /// </summary>
    /// <remarks>
    /// Expected outcome: the interactive test sign-in key is present only when the AppHost settings enable it.
    /// Why this matters: interactive sign-in should remain opt-in so the default local experience stays aligned with the delivered runtime.
    /// </remarks>
    [Fact]
    public void GetWebAuthenticationEnvironmentValues_ShouldIncludeInteractiveSignInFlag_WhenInteractiveSignInIsEnabled()
    {
        var settings = new AppHostSettings(
            ApiAuthenticationProvider: null,
            EnableInteractiveTestSignIn: true,
            AcsEndpoint: null,
            AcsSenderAddress: null,
            AcsConnectionString: null);

        var environmentValues = AppHostEnvironmentWiring.GetWebAuthenticationEnvironmentValues(settings);

        Assert.Equal(bool.TrueString, environmentValues["Authentication__Test__EnableInteractiveSignIn"]);
    }

    /// <summary>
    /// Verifies that ACS environment values are emitted only for non-empty configured values.
    /// </summary>
    /// <remarks>
    /// Expected outcome: only the configured ACS endpoint and sender address are returned, while blank values are omitted.
    /// Why this matters: optional ACS configuration must not inject empty environment values into the API runtime.
    /// </remarks>
    [Fact]
    public void GetAcsEnvironmentValues_ShouldReturnOnlyConfiguredValues_WhenAcsSettingsArePartiallyPresent()
    {
        var settings = new AppHostSettings(
            ApiAuthenticationProvider: null,
            EnableInteractiveTestSignIn: false,
            AcsEndpoint: "https://example.communication.azure.com",
            AcsSenderAddress: "noreply@example.com",
            AcsConnectionString: " ");

        var environmentValues = AppHostEnvironmentWiring.GetAcsEnvironmentValues(settings);

        Assert.Equal("https://example.communication.azure.com", environmentValues["NotificationTransports__AzureCommunicationServices__Endpoint"]);
        Assert.Equal("noreply@example.com", environmentValues["NotificationTransports__AzureCommunicationServices__SenderAddress"]);
        Assert.DoesNotContain("NotificationTransports__AzureCommunicationServices__ConnectionString", environmentValues.Keys);
    }

    /// <summary>
    /// Verifies that <see cref="AppHostEnvironmentWiring.ConfigureApiProject(Aspire.Hosting.ApplicationModel.IResourceBuilder{ProjectResource}, AppHostInfrastructure, AppHostSettings)"/>
    /// applies the required API authentication, Mailpit SMTP, and ACS transport values.
    /// </summary>
    /// <remarks>
    /// Expected outcome: the configured API project receives the baseline auth keys, Mailpit host and port wiring,
    /// and only the non-empty ACS settings.
    /// Why this matters: focused AppHost tests should catch API environment-contract drift before a distributed runtime startup is required.
    /// </remarks>
    [Fact]
    public async Task ConfigureApiProject_ShouldApplyRequiredEnvironmentValues_WhenInfrastructureAndAcsSettingsArePresent()
    {
        var builder = CreateBuilder();
        var infrastructure = AppHostInfrastructureRegistration.Create(builder);
        var apiProject = AppHostProjectRegistration.AddApiProject(builder, infrastructure);
        var settings = new AppHostSettings(
            ApiAuthenticationProvider: null,
            EnableInteractiveTestSignIn: false,
            AcsEndpoint: "https://example.communication.azure.com",
            AcsSenderAddress: "noreply@example.com",
            AcsConnectionString: "endpoint=https://example.communication.azure.com/;accesskey=FAKEKEYVALUE");

        AppHostEnvironmentWiring.ConfigureApiProject(apiProject, infrastructure, settings);

#pragma warning disable CS0618
        var environmentValues = await apiProject.Resource.GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Publish);
#pragma warning restore CS0618

        Assert.Equal(AppHostCompositionConstants.ApiAudience, environmentValues["Authentication__ApiAudience"]);
        Assert.Equal(AppHostCompositionConstants.KeycloakRealmName, environmentValues["Authentication__Keycloak__Realm"]);
        Assert.Equal(AppHostCompositionConstants.ApiAudience, environmentValues["Authentication__Keycloak__ApiClientId"]);
        Assert.Equal("name", environmentValues["Authentication__Authorization__DisplayNameClaimType"]);
        Assert.Equal("preferred_username", environmentValues["Authentication__Authorization__DisplayNameFallbackClaimType"]);
        Assert.Equal("platform@local.test", environmentValues["NotificationTransports__Smtp__SenderAddress"]);
        Assert.Equal(bool.FalseString, environmentValues["NotificationTransports__Smtp__EnableSsl"]);
        Assert.Equal("https://example.communication.azure.com", environmentValues["NotificationTransports__AzureCommunicationServices__Endpoint"]);
        Assert.Equal("noreply@example.com", environmentValues["NotificationTransports__AzureCommunicationServices__SenderAddress"]);
        Assert.Equal("endpoint=https://example.communication.azure.com/;accesskey=FAKEKEYVALUE", environmentValues["NotificationTransports__AzureCommunicationServices__ConnectionString"]);
        Assert.Contains("NotificationTransports__Smtp__Host", environmentValues.Keys);
        Assert.Contains("NotificationTransports__Smtp__Port", environmentValues.Keys);
    }

    /// <summary>
    /// Verifies that <see cref="AppHostEnvironmentWiring.ConfigureWebProject(Aspire.Hosting.ApplicationModel.IResourceBuilder{ProjectResource})"/>
    /// applies the required Web authentication configuration.
    /// </summary>
    /// <remarks>
    /// Expected outcome: the configured Web project receives the callback path, signed-out redirect, required scope,
    /// and the fixed Keycloak client settings.
    /// Why this matters: lower-level AppHost validation should catch Web environment drift before browser-driven suites are needed.
    /// </remarks>
    [Fact]
    public async Task ConfigureWebProject_ShouldApplyRequiredEnvironmentValues_WhenWebProjectIsConfigured()
    {
        var builder = CreateBuilder();
        var infrastructure = AppHostInfrastructureRegistration.Create(builder);
        var apiProject = AppHostProjectRegistration.AddApiProject(builder, infrastructure);
        var webProject = AppHostProjectRegistration.AddWebProject(builder, apiProject);

        AppHostEnvironmentWiring.ConfigureWebProject(webProject);

#pragma warning disable CS0618
        var environmentValues = await webProject.Resource.GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Publish);
#pragma warning restore CS0618

        Assert.Equal("/signin-oidc", environmentValues["Authentication__CallbackPath"]);
        Assert.Equal("/", environmentValues["Authentication__SignedOutRedirectPath"]);
        Assert.Equal(AppHostCompositionConstants.ApiAudience, environmentValues["Authentication__ApiAudience"]);
        Assert.Equal("platform.viewer", environmentValues["Authentication__RequiredScopes__0"]);
        Assert.Equal(AppHostCompositionConstants.KeycloakRealmName, environmentValues["Authentication__Keycloak__Realm"]);
        Assert.Equal("tnc-trading-platform-web", environmentValues["Authentication__Keycloak__ClientId"]);
        Assert.Equal(AppHostCompositionConstants.ApiAudience, environmentValues["Authentication__Keycloak__ApiClientId"]);
        Assert.Equal("LocalAuth!123", environmentValues["Authentication__Keycloak__SeededUserPassword"]);
    }

    /// <summary>
    /// Verifies that <see cref="AppHostEnvironmentWiring.ConfigureAuthenticationProvider(Aspire.Hosting.ApplicationModel.IResourceBuilder{ProjectResource}, Aspire.Hosting.ApplicationModel.IResourceBuilder{ProjectResource}, Aspire.Hosting.ApplicationModel.IResourceBuilder{IResourceWithEndpoints}, AppHostSettings)"/>
    /// keeps the default runtime Keycloak-backed while scoping the synthetic override to the API project.
    /// </summary>
    /// <remarks>
    /// Expected outcome: the default API and Web projects both wait for Keycloak, while the synthetic API override removes only the API Keycloak wait and keeps the Web project on Keycloak.
    /// Why this matters: provider-parity coverage must prove the delivered runtime stays Keycloak-backed except for the isolated API negative-path override.
    /// </remarks>
    [Fact]
    public async Task ConfigureAuthenticationProvider_ShouldKeepWebRuntimeKeycloakBacked_WhenApiProviderUsesSyntheticOverride()
    {
        var builder = CreateBuilder();
        var infrastructure = AppHostInfrastructureRegistration.Create(builder);
        var defaultApiProject = AppHostProjectRegistration.AddApiProject(builder, infrastructure);
        var defaultWebProject = AppHostProjectRegistration.AddWebProject(builder, defaultApiProject);

        AppHostEnvironmentWiring.ConfigureAuthenticationProvider(
            defaultApiProject,
            defaultWebProject,
            infrastructure.Keycloak,
            new AppHostSettings(
                ApiAuthenticationProvider: null,
                EnableInteractiveTestSignIn: false,
                AcsEndpoint: null,
                AcsSenderAddress: null,
                AcsConnectionString: null));

        var syntheticApiProject = builder.AddProject<Projects.TNC_Trading_Platform_Api>("api-test-override");
        var syntheticWebProject = builder.AddProject<Projects.TNC_Trading_Platform_Web>("web-test-override");

        AppHostEnvironmentWiring.ConfigureAuthenticationProvider(
            syntheticApiProject,
            syntheticWebProject,
            infrastructure.Keycloak,
            new AppHostSettings(
                ApiAuthenticationProvider: "Test",
                EnableInteractiveTestSignIn: true,
                AcsEndpoint: null,
                AcsSenderAddress: null,
                AcsConnectionString: null));

        var defaultApiWaitResourceNames = defaultApiProject.Resource.Annotations
            .OfType<WaitAnnotation>()
            .Select(annotation => annotation.Resource.Name)
            .ToArray();
        var defaultWebWaitResourceNames = defaultWebProject.Resource.Annotations
            .OfType<WaitAnnotation>()
            .Select(annotation => annotation.Resource.Name)
            .ToArray();
        var syntheticApiWaitResourceNames = syntheticApiProject.Resource.Annotations
            .OfType<WaitAnnotation>()
            .Select(annotation => annotation.Resource.Name)
            .ToArray();
        var syntheticWebWaitResourceNames = syntheticWebProject.Resource.Annotations
            .OfType<WaitAnnotation>()
            .Select(annotation => annotation.Resource.Name)
            .ToArray();

#pragma warning disable CS0618
        var syntheticApiEnvironmentValues = await syntheticApiProject.Resource.GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Publish);
        var syntheticWebEnvironmentValues = await syntheticWebProject.Resource.GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Publish);
#pragma warning restore CS0618

        Assert.Contains("keycloak", defaultApiWaitResourceNames);
        Assert.Contains("keycloak", defaultWebWaitResourceNames);
        Assert.DoesNotContain("keycloak", syntheticApiWaitResourceNames);
        Assert.Contains("keycloak", syntheticWebWaitResourceNames);
        Assert.Equal("Test", syntheticApiEnvironmentValues["Authentication__Provider"]);
        Assert.Equal(AppHostCompositionConstants.TestIssuer, syntheticApiEnvironmentValues["Authentication__Test__Issuer"]);
        Assert.Equal(AppHostCompositionConstants.TestSigningKey, syntheticApiEnvironmentValues["Authentication__Test__SigningKey"]);
        Assert.Equal("Keycloak", syntheticWebEnvironmentValues["Authentication__Provider"]);
        Assert.Equal(AppHostCompositionConstants.KeycloakAuthority, syntheticWebEnvironmentValues["Authentication__Keycloak__Authority"]);
        Assert.Equal(bool.TrueString, syntheticWebEnvironmentValues["Authentication__Test__EnableInteractiveSignIn"]);
    }

    /// <summary>
    /// Verifies that the wiring helpers fail fast when the settings input is null.
    /// </summary>
    /// <remarks>
    /// Expected outcome: ArgumentNullException is thrown for the provider and ACS helper methods.
    /// Why this matters: the AppHost should surface invalid wiring inputs immediately instead of silently producing incomplete configuration.
    /// </remarks>
    [Fact]
    public void GetAuthenticationAndAcsEnvironmentValues_ShouldThrowArgumentNullException_WhenSettingsAreNull()
    {
        Assert.Throws<ArgumentNullException>(() => AppHostEnvironmentWiring.GetApiAuthenticationEnvironmentValues(null!));
        Assert.Throws<ArgumentNullException>(() => AppHostEnvironmentWiring.GetWebAuthenticationEnvironmentValues(null!));
        Assert.Throws<ArgumentNullException>(() => AppHostEnvironmentWiring.GetAcsEnvironmentValues(null!));
    }

    private static IDistributedApplicationBuilder CreateBuilder()
    {
        return DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            DisableDashboard = true,
            AllowUnsecuredTransport = true
        });
    }
}
