namespace TNC.Trading.Platform.AppHost;

internal static class AppHostInfrastructureRegistration
{
    private const string UsePersistentKeycloakStateConfigurationKey = "AppHost:UsePersistentKeycloakState";
    private const string UsePersistentSqlStateConfigurationKey = "AppHost:UsePersistentSqlState";

    internal static AppHostInfrastructure Create(IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var usePersistentKeycloakState = !string.Equals(
            builder.Configuration[UsePersistentKeycloakStateConfigurationKey],
            bool.FalseString,
            StringComparison.OrdinalIgnoreCase);
        var usePersistentSqlState = !string.Equals(
            builder.Configuration[UsePersistentSqlStateConfigurationKey],
            bool.FalseString,
            StringComparison.OrdinalIgnoreCase);

        var sql = builder.AddSqlServer("sql");
        if (usePersistentSqlState)
        {
            sql.WithDataVolume();
            sql.WithLifetime(ContainerLifetime.Persistent);
        }
        else
        {
            sql.WithLifetime(ContainerLifetime.Session);
        }
        var platformDatabase = sql.AddDatabase("platformdb");
        var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "v1.27")
            .WithLifetime(ContainerLifetime.Persistent)
            .WithHttpEndpoint(targetPort: 8025, name: "http")
            .WithEndpoint(targetPort: 1025, name: "smtp")
            .WithUrlForEndpoint("http", url =>
            {
                url.Url = "/";
                url.DisplayText = "Mailpit UI";
            })
            .WithAnnotation(new ResourceUrlsCallbackAnnotation(context =>
                context.Urls.RemoveAll(url => url.Endpoint?.EndpointName == "smtp")));
        var keycloakAdminUser = builder.AddParameter("keycloak-admin-user", AppHostCompositionConstants.KeycloakAdminUserName);
        var keycloak = builder.AddKeycloak(
            "keycloak",
            port: 8080,
            adminUsername: keycloakAdminUser)
            .WithEndpointProxySupport(proxyEnabled: false)
            .WithLifetime(usePersistentKeycloakState ? ContainerLifetime.Persistent : ContainerLifetime.Session)
            .WithRealmImport("./Realms")
            .WithUrlForEndpoint("http", url =>
            {
                url.Url = "https://localhost:8080/admin/master/console/";
                url.DisplayText = "Keycloak Admin Console";
            });

        return new AppHostInfrastructure(platformDatabase, mailpit, keycloak);
    }
}
