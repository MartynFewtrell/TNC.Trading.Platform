using Aspire.Hosting.ApplicationModel;

const string keycloakRealmName = "tnc-trading-platform";
const string keycloakAuthority = "http://localhost:8080/realms/tnc-trading-platform";
const string testIssuer = "https://test-auth.local";
const string apiAudience = "tnc-trading-platform-api";
const string testSigningKey = "0123456789abcdef0123456789abcdef";

var builder = DistributedApplication.CreateBuilder(args);

var useSyntheticRuntimeForTests = string.Equals(
    builder.Configuration["AppHost:UseSyntheticRuntime"],
    bool.TrueString,
    StringComparison.OrdinalIgnoreCase);
var enableInteractiveTestSignIn = string.Equals(
    builder.Configuration["Authentication:Test:EnableInteractiveSignIn"],
    bool.TrueString,
    StringComparison.OrdinalIgnoreCase);
var acsEndpoint = builder.Configuration["NotificationTransports:AzureCommunicationServices:Endpoint"];
var acsSenderAddress = builder.Configuration["NotificationTransports:AzureCommunicationServices:SenderAddress"];
var acsConnectionString = builder.Configuration["NotificationTransports:AzureCommunicationServices:ConnectionString"];

var infrastructure = ConfigureInfrastructureResources(builder, useSyntheticRuntimeForTests);

var api = builder.AddProject<Projects.TNC_Trading_Platform_Api>("api")
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("https", _ => new()
    {
        Url = "/scalar/v1",
        DisplayText = "Scalar UI"
    });

var apiProject = ConfigureApiProject(api, infrastructure, acsEndpoint, acsSenderAddress, acsConnectionString);

var web = builder.AddProject<Projects.TNC_Trading_Platform_Web>("web")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("https", _ => new()
    {
        Url = "/status",
        DisplayText = "Operator UI"
    });

var webProject = ConfigureWebProject(web);

ConfigureAuthenticationEnvironment(apiProject, webProject, infrastructure.Keycloak, enableInteractiveTestSignIn);

builder.Build().Run();

static (IResourceBuilder<IResourceWithConnectionString>? PlatformDatabase, IResourceBuilder<IResourceWithEndpoints>? Mailpit, IResourceBuilder<IResourceWithEndpoints>? Keycloak)
    ConfigureInfrastructureResources(IDistributedApplicationBuilder builder, bool useSyntheticRuntimeForTests)
{
    ArgumentNullException.ThrowIfNull(builder);

    if (useSyntheticRuntimeForTests)
    {
        return (null, null, null);
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
    var keycloak = builder.AddKeycloak(
            "keycloak",
            port: 8080)
        .WithLifetime(ContainerLifetime.Persistent)
        .WithRealmImport("./Realms");

    return (platformDatabase, mailpit, keycloak);
}

static IResourceBuilder<ProjectResource> ConfigureApiProject(
    IResourceBuilder<ProjectResource> apiProject,
    (IResourceBuilder<IResourceWithConnectionString>? PlatformDatabase, IResourceBuilder<IResourceWithEndpoints>? Mailpit, IResourceBuilder<IResourceWithEndpoints>? Keycloak) infrastructure,
    string? acsEndpoint,
    string? acsSenderAddress,
    string? acsConnectionString)
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

    if (!string.IsNullOrWhiteSpace(acsEndpoint))
    {
        configuredApi = configuredApi.WithEnvironment("NotificationTransports__AzureCommunicationServices__Endpoint", acsEndpoint);
    }

    if (!string.IsNullOrWhiteSpace(acsSenderAddress))
    {
        configuredApi = configuredApi.WithEnvironment("NotificationTransports__AzureCommunicationServices__SenderAddress", acsSenderAddress);
    }

    if (!string.IsNullOrWhiteSpace(acsConnectionString))
    {
        configuredApi = configuredApi.WithEnvironment("NotificationTransports__AzureCommunicationServices__ConnectionString", acsConnectionString);
    }

    return configuredApi;
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
