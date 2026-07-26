namespace TNC.Trading.Platform.AppHost;

internal static class AppHostInfrastructureRegistration
{
    private const string UsePersistentKeycloakStateConfigurationKey = "AppHost:UsePersistentKeycloakState";

    internal static AppHostInfrastructure Create(IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var usePersistentKeycloakState = !string.Equals(
            builder.Configuration[UsePersistentKeycloakStateConfigurationKey],
            bool.FalseString,
            StringComparison.OrdinalIgnoreCase);

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
                url.Url = "/admin/master/console/";
                url.DisplayText = "Keycloak Admin Console";
            });

        return new AppHostInfrastructure(platformDatabase, mailpit, keycloak);
    }
}
