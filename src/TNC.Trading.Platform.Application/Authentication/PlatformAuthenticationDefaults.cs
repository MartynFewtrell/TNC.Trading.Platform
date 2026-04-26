namespace TNC.Trading.Platform.Application.Authentication;

/// <summary>
/// Defines shared authentication and authorization constants used across the platform Web and API hosts.
/// </summary>
public static class PlatformAuthenticationDefaults
{
    /// <summary>
    /// Gets the configuration section name for platform authentication settings.
    /// </summary>
    public const string ConfigurationSectionName = "Authentication";

    /// <summary>
    /// Gets the configuration section name for platform authorization settings.
    /// </summary>
    public const string AuthorizationSectionName = "Authorization";

    /// <summary>
    /// Defines supported authentication providers.
    /// </summary>
    public static class Providers
    {
        /// <summary>
        /// Gets the local Keycloak provider identifier.
        /// </summary>
        public const string Keycloak = "Keycloak";

        /// <summary>
        /// Gets the Azure Microsoft Entra ID provider identifier.
        /// </summary>
        public const string Entra = "Entra";

        /// <summary>
        /// Gets the automated-test provider identifier.
        /// </summary>
        public const string Test = "Test";
    }

    /// <summary>
    /// Defines authentication scheme names.
    /// </summary>
    public static class Schemes
    {
        /// <summary>
        /// Gets the cookie authentication scheme used by the Web app.
        /// </summary>
        public const string Cookie = "PlatformCookie";

        /// <summary>
        /// Gets the OpenID Connect scheme used by the Web app.
        /// </summary>
        public const string OpenIdConnect = "PlatformOidc";

        /// <summary>
        /// Gets the JWT bearer scheme used by the API.
        /// </summary>
        public const string Bearer = "PlatformBearer";
    }

    /// <summary>
    /// Defines shared platform role names.
    /// </summary>
    public static class Roles
    {
        /// <summary>
        /// Gets the administrator role name.
        /// </summary>
        public const string Administrator = "Administrator";

        /// <summary>
        /// Gets the operator role name.
        /// </summary>
        public const string Operator = "Operator";

        /// <summary>
        /// Gets the viewer role name.
        /// </summary>
        public const string Viewer = "Viewer";
    }

    /// <summary>
    /// Defines shared authorization policy names.
    /// </summary>
    public static class Policies
    {
        /// <summary>
        /// Gets the policy name for any authenticated operator role.
        /// </summary>
        public const string Viewer = "PlatformViewer";

        /// <summary>
        /// Gets the policy name for operator-or-administrator access.
        /// </summary>
        public const string Operator = "PlatformOperator";

        /// <summary>
        /// Gets the policy name for administrator-only access.
        /// </summary>
        public const string Administrator = "PlatformAdministrator";
    }

    /// <summary>
    /// Defines delegated API scope names.
    /// </summary>
    public static class Scopes
    {
        /// <summary>
        /// Gets the baseline viewer scope.
        /// </summary>
        public const string Viewer = "platform.viewer";

        /// <summary>
        /// Gets the operator scope.
        /// </summary>
        public const string Operator = "platform.operator";

        /// <summary>
        /// Gets the administrator scope.
        /// </summary>
        public const string Administrator = "platform.admin";
    }

    /// <summary>
    /// Defines shared claim names used by the platform.
    /// </summary>
    public static class Claims
    {
        /// <summary>
        /// Gets the name claim type.
        /// </summary>
        public const string Name = "name";

        /// <summary>
        /// Gets the preferred username claim type.
        /// </summary>
        public const string PreferredUserName = "preferred_username";

        /// <summary>
        /// Gets the scope claim type used by OAuth providers.
        /// </summary>
        public const string Scope = "scope";

        /// <summary>
        /// Gets the alternative scope claim type used by Microsoft Entra ID.
        /// </summary>
        public const string Scp = "scp";

        /// <summary>
        /// Gets the default Keycloak role claim type.
        /// </summary>
        public const string Role = "role";

        /// <summary>
        /// Gets the default Microsoft Entra ID role claim type.
        /// </summary>
        public const string Roles = "roles";
    }

    /// <summary>
    /// Defines persisted authentication audit event types.
    /// </summary>
    public static class AuditEvents
    {
        /// <summary>
        /// Gets the sign-in audit event type.
        /// </summary>
        public const string SignInCompleted = "OperatorSignInCompleted";

        /// <summary>
        /// Gets the sign-out audit event type.
        /// </summary>
        public const string SignOutCompleted = "OperatorSignOutCompleted";

        /// <summary>
        /// Gets the access-denied audit event type.
        /// </summary>
        public const string AccessDenied = "OperatorAccessDenied";

        /// <summary>
        /// Gets the delegated-token acquisition failure audit event type.
        /// </summary>
        public const string TokenAcquisitionFailed = "OperatorTokenAcquisitionFailed";
    }
}
