namespace TNC.Trading.Platform.AppHost;

internal static class AppHostCompositionConstants
{
    internal const string ApiAudience = "tnc-trading-platform-api";
    internal const string KeycloakAdminUserName = "keycloak-admin";
    internal const string KeycloakAuthority = "http://localhost:8080/realms/tnc-trading-platform";
    internal const string KeycloakRealmName = "tnc-trading-platform";
    internal const string TestIssuer = "https://test-auth.local";
    internal const string TestSigningKey = "0123456789abcdef0123456789abcdef";
}