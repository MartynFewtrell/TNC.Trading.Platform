namespace TNC.Trading.Platform.Application.Authentication;

/// <summary>
/// Represents the environment-driven authentication and authorization settings shared by the platform Web and API hosts.
/// </summary>
public sealed class PlatformAuthenticationOptions
{
    /// <summary>
    /// Gets or sets the active authentication provider.
    /// </summary>
    public string Provider { get; set; } = PlatformAuthenticationDefaults.Providers.Keycloak;

    /// <summary>
    /// Gets or sets the OpenID Connect callback path.
    /// </summary>
    public string CallbackPath { get; set; } = "/signin-oidc";

    /// <summary>
    /// Gets or sets the post-sign-out redirect path.
    /// </summary>
    public string SignedOutRedirectPath { get; set; } = "/";

    /// <summary>
    /// Gets or sets the protected API audience.
    /// </summary>
    public string? ApiAudience { get; set; }

    /// <summary>
    /// Gets or sets the baseline delegated scopes requested at sign-in.
    /// </summary>
    public IReadOnlyList<string> RequiredScopes { get; set; } =
    [
        PlatformAuthenticationDefaults.Scopes.Viewer
    ];

    /// <summary>
    /// Gets or sets the Keycloak configuration.
    /// </summary>
    public KeycloakOptions Keycloak { get; set; } = new();

    /// <summary>
    /// Gets or sets the Microsoft Entra ID configuration.
    /// </summary>
    public EntraOptions Entra { get; set; } = new();

    /// <summary>
    /// Gets or sets the automated-test authentication configuration.
    /// </summary>
    public TestOptions Test { get; set; } = new();

    /// <summary>
    /// Gets or sets the authorization claim-mapping configuration.
    /// </summary>
    public AuthorizationOptions Authorization { get; set; } = new();

    /// <summary>
    /// Represents Keycloak-specific configuration.
    /// </summary>
    public sealed class KeycloakOptions
    {
        /// <summary>
        /// Gets or sets the local Keycloak authority.
        /// </summary>
        public string? Authority { get; set; }

        /// <summary>
        /// Gets or sets the Keycloak realm name.
        /// </summary>
        public string Realm { get; set; } = "tnc-trading-platform";

        /// <summary>
        /// Gets or sets the Web client identifier.
        /// </summary>
        public string ClientId { get; set; } = "tnc-trading-platform-web";

        /// <summary>
        /// Gets or sets the Web client secret.
        /// </summary>
        public string? ClientSecret { get; set; }

        /// <summary>
        /// Gets or sets the protected API client identifier.
        /// </summary>
        public string ApiClientId { get; set; } = "tnc-trading-platform-api";

        /// <summary>
        /// Gets or sets the local realm import path.
        /// </summary>
        public string? RealmImportPath { get; set; }

        /// <summary>
        /// Gets or sets the shared seeded-user password for local development.
        /// </summary>
        public string? SeededUserPassword { get; set; }
    }

    /// <summary>
    /// Represents Microsoft Entra ID configuration.
    /// </summary>
    public sealed class EntraOptions
    {
        /// <summary>
        /// Gets or sets the Microsoft Entra ID instance URL.
        /// </summary>
        public string Instance { get; set; } = "https://login.microsoftonline.com/";

        /// <summary>
        /// Gets or sets the Microsoft Entra tenant identifier.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Gets or sets the Microsoft Entra tenant domain.
        /// </summary>
        public string? Domain { get; set; }

        /// <summary>
        /// Gets or sets the Web application client identifier.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Gets or sets the Web application client secret.
        /// </summary>
        public string? ClientSecret { get; set; }

        /// <summary>
        /// Gets or sets the protected API application identifier.
        /// </summary>
        public string? ApiClientId { get; set; }
    }

    /// <summary>
    /// Represents automated-test authentication configuration.
    /// </summary>
    public sealed class TestOptions
    {
        /// <summary>
        /// Gets or sets the JWT issuer used by automated tests.
        /// </summary>
        public string Issuer { get; set; } = "https://test-auth.local";

        /// <summary>
        /// Gets or sets the JWT audience used by automated tests.
        /// </summary>
        public string Audience { get; set; } = "tnc-trading-platform-api";

        /// <summary>
        /// Gets or sets the symmetric signing key used by automated tests.
        /// </summary>
        public string SigningKey { get; set; } = "0123456789abcdef0123456789abcdef";
    }

    /// <summary>
    /// Represents authorization claim-mapping settings.
    /// </summary>
    public sealed class AuthorizationOptions
    {
        /// <summary>
        /// Gets or sets the claim type used for role mapping.
        /// </summary>
        public string RoleClaimType { get; set; } = PlatformAuthenticationDefaults.Claims.Role;

        /// <summary>
        /// Gets or sets the primary display-name claim type.
        /// </summary>
        public string DisplayNameClaimType { get; set; } = PlatformAuthenticationDefaults.Claims.Name;

        /// <summary>
        /// Gets or sets the fallback display-name claim type.
        /// </summary>
        public string DisplayNameFallbackClaimType { get; set; } = PlatformAuthenticationDefaults.Claims.PreferredUserName;
    }
}
