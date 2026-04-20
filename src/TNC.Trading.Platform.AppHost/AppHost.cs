using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);
const string keycloakRealmName = "tnc-trading-platform";
const string keycloakAuthority = "http://localhost:8080/realms/tnc-trading-platform";

var enableInfrastructureContainers = string.Equals(
    builder.Configuration["AppHost:EnableInfrastructureContainers"],
    bool.TrueString,
    StringComparison.OrdinalIgnoreCase);
var acsEndpoint = builder.Configuration["NotificationTransports:AzureCommunicationServices:Endpoint"];
var acsSenderAddress = builder.Configuration["NotificationTransports:AzureCommunicationServices:SenderAddress"];
var acsConnectionString = builder.Configuration["NotificationTransports:AzureCommunicationServices:ConnectionString"];

IResourceBuilder<IResourceWithConnectionString>? platformDatabase = null;
IResourceBuilder<IResourceWithEndpoints>? mailpit = null;
IResourceBuilder<IResourceWithEndpoints>? keycloak = null;

if (enableInfrastructureContainers)
{
    var sqlPassword = builder.AddParameter("sql-password", secret: true);
    var sql = builder.AddSqlServer("sql", sqlPassword)
        .WithDataVolume();

    platformDatabase = sql.AddDatabase("platformdb");

    mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "v1.27")
        .WithHttpEndpoint(targetPort: 8025, name: "http")
        .WithEndpoint(targetPort: 1025, name: "smtp")
        .WithUrlForEndpoint("http", _ => new()
        {
            Url = "/",
            DisplayText = "Mailpit UI"
        });

    keycloak = builder.AddKeycloak("keycloak", port: 8080)
        .WithRealmImport("./Realms");
}

var api = builder.AddProject<Projects.TNC_Trading_Platform_Api>("api")
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("https", _ => new()
    {
        Url = "/scalar/v1",
        DisplayText = "Scalar UI"
    });

if (platformDatabase is not null)
{
    api.WithReference(platformDatabase)
        .WaitFor(platformDatabase);
}

if (mailpit is not null)
{
    var mailpitSmtpEndpoint = mailpit.Resource.GetEndpoint("smtp");

    api.WaitFor(mailpit)
        .WithEnvironment("NotificationTransports__Smtp__Host", mailpitSmtpEndpoint.Property(EndpointProperty.IPV4Host))
        .WithEnvironment("NotificationTransports__Smtp__Port", mailpitSmtpEndpoint.Property(EndpointProperty.Port))
        .WithEnvironment("NotificationTransports__Smtp__SenderAddress", "platform@local.test")
        .WithEnvironment("NotificationTransports__Smtp__EnableSsl", bool.FalseString);
}

if (!string.IsNullOrWhiteSpace(acsEndpoint))
{
    api.WithEnvironment("NotificationTransports__AzureCommunicationServices__Endpoint", acsEndpoint);
}

if (!string.IsNullOrWhiteSpace(acsSenderAddress))
{
    api.WithEnvironment("NotificationTransports__AzureCommunicationServices__SenderAddress", acsSenderAddress);
}

if (!string.IsNullOrWhiteSpace(acsConnectionString))
{
    api.WithEnvironment("NotificationTransports__AzureCommunicationServices__ConnectionString", acsConnectionString);
}

var web = builder.AddProject<Projects.TNC_Trading_Platform_Web>("web")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("https", _ => new()
    {
        Url = "/status",
        DisplayText = "Operator UI"
    });

var webProject = web
    .WithEnvironment("Authentication__CallbackPath", "/signin-oidc")
    .WithEnvironment("Authentication__SignedOutRedirectPath", "/")
    .WithEnvironment("Authentication__ApiAudience", "tnc-trading-platform-api")
    .WithEnvironment("Authentication__RequiredScopes__0", "platform.viewer")
    .WithEnvironment("Authentication__Keycloak__Realm", keycloakRealmName)
    .WithEnvironment("Authentication__Keycloak__ClientId", "tnc-trading-platform-web")
    .WithEnvironment("Authentication__Keycloak__ApiClientId", "tnc-trading-platform-api")
    .WithEnvironment("Authentication__Keycloak__SeededUserPassword", "LocalAuth!123")
    .WithEnvironment("Authentication__Authorization__DisplayNameClaimType", "name")
    .WithEnvironment("Authentication__Authorization__DisplayNameFallbackClaimType", "preferred_username");

var apiProject = api
    .WithEnvironment("Authentication__ApiAudience", "tnc-trading-platform-api")
    .WithEnvironment("Authentication__Keycloak__Realm", keycloakRealmName)
    .WithEnvironment("Authentication__Keycloak__ApiClientId", "tnc-trading-platform-api")
    .WithEnvironment("Authentication__Authorization__DisplayNameClaimType", "name")
    .WithEnvironment("Authentication__Authorization__DisplayNameFallbackClaimType", "preferred_username");

if (keycloak is not null)
{
    apiProject = apiProject
        .WaitFor(keycloak)
        .WithEnvironment("Authentication__Provider", "Keycloak")
        .WithEnvironment("Authentication__Keycloak__Authority", keycloakAuthority)
        .WithEnvironment("Authentication__Authorization__RoleClaimType", "role");

    webProject = webProject
        .WaitFor(keycloak)
        .WithEnvironment("Authentication__Provider", "Keycloak")
        .WithEnvironment("Authentication__Keycloak__Authority", keycloakAuthority)
        .WithEnvironment("Authentication__Authorization__RoleClaimType", "role");
}
else
{
    apiProject = apiProject
        .WithEnvironment("Authentication__Provider", "Test")
        .WithEnvironment("Authentication__Test__Issuer", "https://test-auth.local")
        .WithEnvironment("Authentication__Test__Audience", "tnc-trading-platform-api")
        .WithEnvironment("Authentication__Test__SigningKey", "0123456789abcdef0123456789abcdef")
        .WithEnvironment("Authentication__Authorization__RoleClaimType", "role");

    webProject = webProject
        .WithEnvironment("Authentication__Provider", "Test")
        .WithEnvironment("Authentication__Test__Issuer", "https://test-auth.local")
        .WithEnvironment("Authentication__Test__Audience", "tnc-trading-platform-api")
        .WithEnvironment("Authentication__Test__SigningKey", "0123456789abcdef0123456789abcdef")
        .WithEnvironment("Authentication__Authorization__RoleClaimType", "role");
}

builder.Build().Run();
