using Aspire.Hosting.ApplicationModel;
using TNC.Trading.Platform.AppHost;

const string keycloakRealmName = "tnc-trading-platform";
const string keycloakAuthority = "http://localhost:8080/realms/tnc-trading-platform";
const string keycloakAdminUserName = "keycloak-admin";
const string testIssuer = "https://test-auth.local";
const string apiAudience = "tnc-trading-platform-api";
const string testSigningKey = "0123456789abcdef0123456789abcdef";

var builder = DistributedApplication.CreateBuilder(args);

var settings = AppHostSettings.FromConfiguration(builder.Configuration);
var infrastructure = ConfigureInfrastructureResources(builder, settings.UseSyntheticRuntimeForTests);

var api = builder.AddProject<Projects.TNC_Trading_Platform_Api>("api")
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("https", _ => new()
    {
        Url = "/scalar/v1",
        DisplayText = "Scalar UI"
    });

var apiProject = ConfigureApiProject(api, infrastructure, settings);

var web = builder.AddProject<Projects.TNC_Trading_Platform_Web>("web")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("https", _ => new()
    {
        Url = "/",
        DisplayText = "Operator UI"
    });

var webProject = ConfigureWebProject(web);

ConfigureAuthenticationEnvironment(apiProject, webProject, infrastructure.Keycloak, settings.EnableInteractiveTestSignIn);

builder.Build().Run();

static AppHostInfrastructure ConfigureInfrastructureResources(IDistributedApplicationBuilder builder, bool useSyntheticRuntimeForTests)
{
    ArgumentNullException.ThrowIfNull(builder);

    if (useSyntheticRuntimeForTests)
    {
        return new AppHostInfrastructure(null, null, null);
    }

    var sql = builder.AddSqlServer("sql")
        .WithDataVolume()
        .WithLifetime(ContainerLifetime.Persistent);
    var platformDatabase = sql.AddDatabase("platformdb");
    var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "v1.27")
        .WithLifetime(ContainerLifetime.Persistent)
        .WithHttpEndpoint(targetPort: 8025, name: "http")
        .WithEndpoint(targetPort: 1025, name: "smtp")
        .WithUrlForEndpoint("http", _ => new()
        {
            Url = "/",
            DisplayText = "Mailpit UI"
        });
    var keycloakAdminUser = builder.AddParameter("keycloak-admin-user", keycloakAdminUserName);
    var keycloak = builder.AddKeycloak(
            "keycloak",
            port: 8080,
            adminUsername: keycloakAdminUser)
        .WithEndpointProxySupport(proxyEnabled: false)
        .WithLifetime(ContainerLifetime.Persistent)
        .WithRealmImport("./Realms")
        .WithUrlForEndpoint("http", url =>
        {
            url.Url = "/admin/master/console/";
            url.DisplayText = "Keycloak Admin Console";
        });

    return new AppHostInfrastructure(platformDatabase, mailpit, keycloak);
}

static IResourceBuilder<ProjectResource> ConfigureApiProject(
    IResourceBuilder<ProjectResource> apiProject,
    AppHostInfrastructure infrastructure,
    AppHostSettings settings)
{
    ArgumentNullException.ThrowIfNull(apiProject);

    var configuredApi = apiProject
        .WithEnvironment("Authentication__ApiAudience", apiAudience)
        .WithEnvironment("Authentication__Keycloak__Realm", keycloakRealmName)
        .WithEnvironment("Authentication__Keycloak__ApiClientId", apiAudience)
        .WithEnvironment("Authentication__Authorization__DisplayNameClaimType", "name")
        .WithEnvironment("Authentication__Authorization__DisplayNameFallbackClaimType", "preferred_username");

    if (infrastructure.PlatformDatabase is not null)
    {
        configuredApi = configuredApi
            .WithReference(infrastructure.PlatformDatabase)
            .WaitFor(infrastructure.PlatformDatabase);
    }

    if (infrastructure.Mailpit is not null)
    {
        var mailpitSmtpEndpoint = infrastructure.Mailpit.Resource.GetEndpoint("smtp");
        configuredApi = configuredApi
            .WaitFor(infrastructure.Mailpit)
            .WithEnvironment("NotificationTransports__Smtp__Host", mailpitSmtpEndpoint.Property(EndpointProperty.IPV4Host))
            .WithEnvironment("NotificationTransports__Smtp__Port", mailpitSmtpEndpoint.Property(EndpointProperty.Port))
            .WithEnvironment("NotificationTransports__Smtp__SenderAddress", "platform@local.test")
            .WithEnvironment("NotificationTransports__Smtp__EnableSsl", bool.FalseString);
    }

    return ApplyAcsConfiguration(configuredApi, settings);
}

static IResourceBuilder<ProjectResource> ConfigureWebProject(IResourceBuilder<ProjectResource> webProject)
{
    ArgumentNullException.ThrowIfNull(webProject);

    return webProject
    .WithEnvironment("Authentication__CallbackPath", "/signin-oidc")
    .WithEnvironment("Authentication__SignedOutRedirectPath", "/")
        .WithEnvironment("Authentication__ApiAudience", apiAudience)
    .WithEnvironment("Authentication__RequiredScopes__0", "platform.viewer")
    .WithEnvironment("Authentication__Keycloak__Realm", keycloakRealmName)
    .WithEnvironment("Authentication__Keycloak__ClientId", "tnc-trading-platform-web")
        .WithEnvironment("Authentication__Keycloak__ApiClientId", apiAudience)
    .WithEnvironment("Authentication__Keycloak__SeededUserPassword", "LocalAuth!123")
    .WithEnvironment("Authentication__Authorization__DisplayNameClaimType", "name")
    .WithEnvironment("Authentication__Authorization__DisplayNameFallbackClaimType", "preferred_username");
}

static void ConfigureAuthenticationEnvironment(
    IResourceBuilder<ProjectResource> apiProject,
    IResourceBuilder<ProjectResource> webProject,
    IResourceBuilder<IResourceWithEndpoints>? keycloak,
    bool enableInteractiveTestSignIn)
{
    ArgumentNullException.ThrowIfNull(apiProject);
    ArgumentNullException.ThrowIfNull(webProject);

    if (keycloak is not null)
    {
        _ = apiProject
            .WaitFor(keycloak)
            .WithEnvironment("Authentication__Provider", "Keycloak")
            .WithEnvironment("Authentication__Keycloak__Authority", keycloakAuthority)
            .WithEnvironment("Authentication__Authorization__RoleClaimType", "role");
        _ = webProject
            .WaitFor(keycloak)
            .WithEnvironment("Authentication__Provider", "Keycloak")
            .WithEnvironment("Authentication__Keycloak__Authority", keycloakAuthority)
            .WithEnvironment("Authentication__Authorization__RoleClaimType", "role");
        return;
    }

    _ = apiProject
        .WithEnvironment("Authentication__Provider", "Test")
        .WithEnvironment("Authentication__Test__Issuer", testIssuer)
        .WithEnvironment("Authentication__Test__Audience", apiAudience)
        .WithEnvironment("Authentication__Test__SigningKey", testSigningKey)
        .WithEnvironment("Persistence__UseInMemoryDatabase", bool.TrueString)
        .WithEnvironment("Authentication__Authorization__RoleClaimType", "role");

    var configuredWeb = webProject
        .WithEnvironment("Authentication__Provider", "Test")
        .WithEnvironment("Authentication__Test__Issuer", testIssuer)
        .WithEnvironment("Authentication__Test__Audience", apiAudience)
        .WithEnvironment("Authentication__Test__SigningKey", testSigningKey)
        .WithEnvironment("Authentication__Authorization__RoleClaimType", "role");

    if (enableInteractiveTestSignIn)
    {
        _ = configuredWeb.WithEnvironment("Authentication__Test__EnableInteractiveSignIn", bool.TrueString);
    }
}

static IResourceBuilder<ProjectResource> ApplyAcsConfiguration(
    IResourceBuilder<ProjectResource> apiProject,
    AppHostSettings settings)
{
    ArgumentNullException.ThrowIfNull(apiProject);
    ArgumentNullException.ThrowIfNull(settings);

    var configuredApi = apiProject;

    if (!string.IsNullOrWhiteSpace(settings.AcsEndpoint))
    {
        configuredApi = configuredApi.WithEnvironment("NotificationTransports__AzureCommunicationServices__Endpoint", settings.AcsEndpoint);
    }

    if (!string.IsNullOrWhiteSpace(settings.AcsSenderAddress))
    {
        configuredApi = configuredApi.WithEnvironment("NotificationTransports__AzureCommunicationServices__SenderAddress", settings.AcsSenderAddress);
    }

    if (!string.IsNullOrWhiteSpace(settings.AcsConnectionString))
    {
        configuredApi = configuredApi.WithEnvironment("NotificationTransports__AzureCommunicationServices__ConnectionString", settings.AcsConnectionString);
    }

    return configuredApi;
}
