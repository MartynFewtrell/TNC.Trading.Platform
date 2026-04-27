namespace TNC.Trading.Platform.Application.Authentication;

/// <summary>
/// Resolves shared authentication provider configuration for the platform Web and API hosts.
/// </summary>
public static class PlatformAuthenticationConfigurationResolver
{
    /// <summary>
    /// Validates that the configured authentication provider is supported.
    /// </summary>
    /// <param name="provider">The configured provider identifier.</param>
    public static void ValidateProviderSupported(string provider)
    {
        if (string.Equals(provider, PlatformAuthenticationDefaults.Providers.Keycloak, StringComparison.Ordinal)
            || string.Equals(provider, PlatformAuthenticationDefaults.Providers.Entra, StringComparison.Ordinal)
            || string.Equals(provider, PlatformAuthenticationDefaults.Providers.Test, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException($"The authentication provider '{provider}' is not supported.");
    }

    /// <summary>
    /// Resolves the issuer authority for the configured authentication provider.
    /// </summary>
    /// <param name="authenticationOptions">The platform authentication options.</param>
    /// <returns>The resolved authority.</returns>
    public static string? ResolveAuthority(PlatformAuthenticationOptions authenticationOptions)
    {
        ArgumentNullException.ThrowIfNull(authenticationOptions);

        return authenticationOptions.Provider switch
        {
            PlatformAuthenticationDefaults.Providers.Entra => ResolveEntraAuthority(authenticationOptions.Entra),
            PlatformAuthenticationDefaults.Providers.Keycloak => ResolveKeycloakAuthority(authenticationOptions.Keycloak),
            PlatformAuthenticationDefaults.Providers.Test => authenticationOptions.Test.Issuer,
            _ => throw new InvalidOperationException($"The authentication provider '{authenticationOptions.Provider}' is not supported.")
        };
    }

    /// <summary>
    /// Resolves the audience for the configured authentication provider.
    /// </summary>
    /// <param name="authenticationOptions">The platform authentication options.</param>
    /// <returns>The resolved audience.</returns>
    public static string ResolveAudience(PlatformAuthenticationOptions authenticationOptions)
    {
        ArgumentNullException.ThrowIfNull(authenticationOptions);

        return !string.IsNullOrWhiteSpace(authenticationOptions.ApiAudience)
            ? authenticationOptions.ApiAudience
            : authenticationOptions.Provider switch
            {
                PlatformAuthenticationDefaults.Providers.Entra => !string.IsNullOrWhiteSpace(authenticationOptions.Entra.ApiClientId)
                    ? authenticationOptions.Entra.ApiClientId
                    : throw new InvalidOperationException("The configuration key 'Authentication:ApiAudience' or 'Authentication:Entra:ApiClientId' is required when using the Entra provider."),
                PlatformAuthenticationDefaults.Providers.Keycloak => !string.IsNullOrWhiteSpace(authenticationOptions.Keycloak.ApiClientId)
                    ? authenticationOptions.Keycloak.ApiClientId
                    : throw new InvalidOperationException("The configuration key 'Authentication:ApiAudience' or 'Authentication:Keycloak:ApiClientId' is required when using the Keycloak provider."),
                PlatformAuthenticationDefaults.Providers.Test => !string.IsNullOrWhiteSpace(authenticationOptions.Test.Audience)
                    ? authenticationOptions.Test.Audience
                    : throw new InvalidOperationException("The configuration key 'Authentication:ApiAudience' or 'Authentication:Test:Audience' is required when using the Test provider."),
                _ => throw new InvalidOperationException($"The authentication provider '{authenticationOptions.Provider}' is not supported.")
            };
    }

    /// <summary>
    /// Resolves the Web client identifier for OpenID Connect providers.
    /// </summary>
    /// <param name="authenticationOptions">The platform authentication options.</param>
    /// <returns>The resolved client identifier.</returns>
    public static string ResolveClientId(PlatformAuthenticationOptions authenticationOptions)
    {
        ArgumentNullException.ThrowIfNull(authenticationOptions);

        return authenticationOptions.Provider switch
        {
            PlatformAuthenticationDefaults.Providers.Entra => !string.IsNullOrWhiteSpace(authenticationOptions.Entra.ClientId)
                ? authenticationOptions.Entra.ClientId
                : throw new InvalidOperationException("The configuration key 'Authentication:Entra:ClientId' is required when using the Entra provider."),
            PlatformAuthenticationDefaults.Providers.Keycloak => !string.IsNullOrWhiteSpace(authenticationOptions.Keycloak.ClientId)
                ? authenticationOptions.Keycloak.ClientId
                : throw new InvalidOperationException("The configuration key 'Authentication:Keycloak:ClientId' is required when using the Keycloak provider."),
            _ => throw new InvalidOperationException($"The authentication provider '{authenticationOptions.Provider}' is not supported.")
        };
    }

    /// <summary>
    /// Resolves the optional Web client secret for OpenID Connect providers.
    /// </summary>
    /// <param name="authenticationOptions">The platform authentication options.</param>
    /// <returns>The resolved client secret, if any.</returns>
    public static string? ResolveClientSecret(PlatformAuthenticationOptions authenticationOptions)
    {
        ArgumentNullException.ThrowIfNull(authenticationOptions);

        return authenticationOptions.Provider switch
        {
            PlatformAuthenticationDefaults.Providers.Entra => authenticationOptions.Entra.ClientSecret,
            PlatformAuthenticationDefaults.Providers.Keycloak => authenticationOptions.Keycloak.ClientSecret,
            _ => throw new InvalidOperationException($"The authentication provider '{authenticationOptions.Provider}' is not supported.")
        };
    }

    /// <summary>
    /// Validates the minimum OpenID Connect configuration required by the Web host.
    /// </summary>
    /// <param name="authenticationOptions">The platform authentication options.</param>
    public static void ValidateOpenIdConnectConfiguration(PlatformAuthenticationOptions authenticationOptions)
    {
        ArgumentNullException.ThrowIfNull(authenticationOptions);

        _ = ResolveAuthority(authenticationOptions);
        _ = ResolveClientId(authenticationOptions);
    }

    private static string ResolveEntraAuthority(PlatformAuthenticationOptions.EntraOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Instance) || string.IsNullOrWhiteSpace(options.TenantId))
        {
            throw new InvalidOperationException("The configuration keys 'Authentication:Entra:Instance' and 'Authentication:Entra:TenantId' are required when using the Entra provider.");
        }

        return $"{options.Instance.TrimEnd('/')}/{options.TenantId}/v2.0";
    }

    private static string ResolveKeycloakAuthority(PlatformAuthenticationOptions.KeycloakOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return !string.IsNullOrWhiteSpace(options.Authority)
            ? options.Authority
            : throw new InvalidOperationException("The configuration key 'Authentication:Keycloak:Authority' is required when using the Keycloak provider.");
    }
}
