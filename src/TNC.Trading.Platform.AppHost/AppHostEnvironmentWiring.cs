using Aspire.Hosting.ApplicationModel;

namespace TNC.Trading.Platform.AppHost;

internal static class AppHostEnvironmentWiring
{
    internal static IReadOnlyDictionary<string, string> GetApiEnvironmentValues()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Authentication__ApiAudience"] = AppHostCompositionConstants.ApiAudience,
            ["Authentication__Keycloak__Realm"] = AppHostCompositionConstants.KeycloakRealmName,
            ["Authentication__Keycloak__ApiClientId"] = AppHostCompositionConstants.ApiAudience,
            ["Authentication__Authorization__DisplayNameClaimType"] = "name",
            ["Authentication__Authorization__DisplayNameFallbackClaimType"] = "preferred_username"
        };
    }

    internal static IReadOnlyDictionary<string, string> GetWebEnvironmentValues()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Authentication__CallbackPath"] = "/signin-oidc",
            ["Authentication__SignedOutRedirectPath"] = "/",
            ["Authentication__ApiAudience"] = AppHostCompositionConstants.ApiAudience,
            ["Authentication__RequiredScopes__0"] = "platform.viewer",
            ["Authentication__Keycloak__Realm"] = AppHostCompositionConstants.KeycloakRealmName,
            ["Authentication__Keycloak__ClientId"] = "tnc-trading-platform-web",
            ["Authentication__Keycloak__ApiClientId"] = AppHostCompositionConstants.ApiAudience,
            ["Authentication__Keycloak__SeededUserPassword"] = "LocalAuth!123",
            ["Authentication__Authorization__DisplayNameClaimType"] = "name",
            ["Authentication__Authorization__DisplayNameFallbackClaimType"] = "preferred_username"
        };
    }

    internal static IReadOnlyDictionary<string, string> GetApiAuthenticationEnvironmentValues(AppHostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var apiAuthenticationProvider = string.Equals(settings.ApiAuthenticationProvider, "Test", StringComparison.Ordinal)
            ? "Test"
            : "Keycloak";

        var environmentValues = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Authentication__Provider"] = apiAuthenticationProvider,
            ["Authentication__Authorization__RoleClaimType"] = "role"
        };

        if (string.Equals(apiAuthenticationProvider, "Keycloak", StringComparison.Ordinal))
        {
            environmentValues["Authentication__Keycloak__Authority"] = AppHostCompositionConstants.KeycloakAuthority;
        }
        else
        {
            environmentValues["Authentication__Test__Issuer"] = AppHostCompositionConstants.TestIssuer;
            environmentValues["Authentication__Test__SigningKey"] = AppHostCompositionConstants.TestSigningKey;
        }

        return environmentValues;
    }

    internal static IReadOnlyDictionary<string, string> GetWebAuthenticationEnvironmentValues(AppHostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var environmentValues = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Authentication__Provider"] = "Keycloak",
            ["Authentication__Keycloak__Authority"] = AppHostCompositionConstants.KeycloakAuthority,
            ["Authentication__Authorization__RoleClaimType"] = "role"
        };

        if (settings.EnableInteractiveTestSignIn)
        {
            environmentValues["Authentication__Test__EnableInteractiveSignIn"] = bool.TrueString;
        }

        return environmentValues;
    }

    internal static IReadOnlyDictionary<string, string> GetAcsEnvironmentValues(AppHostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var environmentValues = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(settings.AcsEndpoint))
        {
            environmentValues["NotificationTransports__AzureCommunicationServices__Endpoint"] = settings.AcsEndpoint;
        }

        if (!string.IsNullOrWhiteSpace(settings.AcsSenderAddress))
        {
            environmentValues["NotificationTransports__AzureCommunicationServices__SenderAddress"] = settings.AcsSenderAddress;
        }

        if (!string.IsNullOrWhiteSpace(settings.AcsConnectionString))
        {
            environmentValues["NotificationTransports__AzureCommunicationServices__ConnectionString"] = settings.AcsConnectionString;
        }

        return environmentValues;
    }

    internal static void ConfigureApiProject(
        IResourceBuilder<ProjectResource> apiProject,
        AppHostInfrastructure infrastructure,
        AppHostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(apiProject);
        ArgumentNullException.ThrowIfNull(settings);

        var configuredApi = apiProject;

        foreach (var environmentValue in GetApiEnvironmentValues())
        {
            configuredApi = configuredApi
                .WithEnvironment(environmentValue.Key, environmentValue.Value);
        }

        if (infrastructure.Mailpit is not null)
        {
            var mailpitSmtpEndpoint = infrastructure.Mailpit.Resource.GetEndpoint("smtp");
            configuredApi = configuredApi
                .WithEnvironment("NotificationTransports__Smtp__Host", mailpitSmtpEndpoint.Property(EndpointProperty.IPV4Host))
                .WithEnvironment("NotificationTransports__Smtp__Port", mailpitSmtpEndpoint.Property(EndpointProperty.Port))
                .WithEnvironment("NotificationTransports__Smtp__SenderAddress", "platform@local.test")
                .WithEnvironment("NotificationTransports__Smtp__EnableSsl", bool.FalseString);
        }

        foreach (var environmentValue in GetAcsEnvironmentValues(settings))
        {
            configuredApi = configuredApi
                .WithEnvironment(environmentValue.Key, environmentValue.Value);
        }
    }

    internal static void ConfigureWebProject(IResourceBuilder<ProjectResource> webProject)
    {
        ArgumentNullException.ThrowIfNull(webProject);

        var configuredWeb = webProject;

        foreach (var environmentValue in GetWebEnvironmentValues())
        {
            configuredWeb = configuredWeb
                .WithEnvironment(environmentValue.Key, environmentValue.Value);
        }
    }

    internal static void ConfigureAuthenticationProvider(
        IResourceBuilder<ProjectResource> apiProject,
        IResourceBuilder<ProjectResource> webProject,
        IResourceBuilder<IResourceWithEndpoints> keycloak,
        AppHostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(apiProject);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(webProject);

        var configuredApi = apiProject;
        var apiAuthenticationEnvironmentValues = GetApiAuthenticationEnvironmentValues(settings);

        foreach (var environmentValue in apiAuthenticationEnvironmentValues)
        {
            configuredApi = configuredApi
                .WithEnvironment(environmentValue.Key, environmentValue.Value);
        }

        if (string.Equals(apiAuthenticationEnvironmentValues["Authentication__Provider"], "Keycloak", StringComparison.Ordinal))
        {
            configuredApi = configuredApi.WaitFor(keycloak);
        }

        var configuredWeb = webProject.WaitFor(keycloak);

        foreach (var environmentValue in GetWebAuthenticationEnvironmentValues(settings))
        {
            configuredWeb = configuredWeb
                .WithEnvironment(environmentValue.Key, environmentValue.Value);
        }
    }
}
