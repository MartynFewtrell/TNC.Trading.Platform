using Microsoft.Extensions.Configuration;

namespace TNC.Trading.Platform.AppHost;

/// <summary>
/// Tests for <see cref="AppHostSettings"/> configuration parsing and defaults.
/// </summary>
/// <remarks>
/// Requirement traceability: FR1 (AppHost composition), FR2 (API authentication), FR3 (Web authentication),
/// FR4 (notification configuration), NF1 (configurability), SR1 (configuration parsing).
/// These tests verify that AppHost settings are correctly parsed from configuration, default values are applied,
/// and the settings accurately reflect operator intent for authentication providers, interactive sign-in, and
/// notification transport configuration. This matters because incorrect settings parsing could silently route
/// local development to unintended authentication providers or fail to wire notification transports.
/// </remarks>
public sealed class AppHostSettingsTests
{
    /// <summary>
    /// Verifies that <see cref="AppHostSettings.FromConfiguration"/> applies correct default values when
    /// no configuration keys are present.
    /// </summary>
    /// <remarks>
    /// Expected outcome: ApiAuthenticationProvider is null (defaults to Keycloak), EnableInteractiveTestSignIn
    /// is false, and all ACS configuration values are null.
    /// This behavior matters because the default runtime should be Keycloak-backed without explicit configuration.
    /// </remarks>
    [Fact]
    public void FromConfiguration_ShouldReturnDefaultValues_WhenNoConfigurationKeysArePresent()
    {
        var configuration = new ConfigurationBuilder().Build();

        var settings = AppHostSettings.FromConfiguration(configuration);

        Assert.Null(settings.ApiAuthenticationProvider);
        Assert.False(settings.EnableInteractiveTestSignIn);
        Assert.Null(settings.AcsEndpoint);
        Assert.Null(settings.AcsSenderAddress);
        Assert.Null(settings.AcsConnectionString);
        Assert.Null(settings.AccountPreferencesBaseUrl);
    }

    [Fact]
    public void FromConfiguration_ShouldParseAccountPreferencesBaseUrl_WhenConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ig:AccountPreferencesBaseUrl"] = "http://127.0.0.1:8081/gateway/deal/"
            })
            .Build();

        var settings = AppHostSettings.FromConfiguration(configuration);

        Assert.Equal("http://127.0.0.1:8081/gateway/deal/", settings.AccountPreferencesBaseUrl);
    }

    /// <summary>
    /// Verifies that <see cref="AppHostSettings.FromConfiguration"/> correctly parses the explicit "Test"
    /// API authentication provider setting.
    /// </summary>
    /// <remarks>
    /// Expected outcome: ApiAuthenticationProvider is "Test" when Authentication:ApiProvider is set to "Test".
    /// This behavior matters because it enables the isolated synthetic API negative path for integration tests
    /// that should not require real Keycloak infrastructure.
    /// </remarks>
    [Fact]
    public void FromConfiguration_ShouldParseTestApiProvider_WhenAuthenticationApiProviderIsTest()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:ApiProvider"] = "Test"
            })
            .Build();

        var settings = AppHostSettings.FromConfiguration(configuration);

        Assert.Equal("Test", settings.ApiAuthenticationProvider);
    }

    /// <summary>
    /// Verifies that <see cref="AppHostSettings.FromConfiguration"/> correctly parses the explicit "Keycloak"
    /// API authentication provider setting.
    /// </summary>
    /// <remarks>
    /// Expected outcome: ApiAuthenticationProvider is "Keycloak" when Authentication:ApiProvider is set to "Keycloak".
    /// This behavior matters because it confirms that the default Keycloak-backed runtime can also be explicitly selected.
    /// </remarks>
    [Fact]
    public void FromConfiguration_ShouldParseKeycloakApiProvider_WhenAuthenticationApiProviderIsKeycloak()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:ApiProvider"] = "Keycloak"
            })
            .Build();

        var settings = AppHostSettings.FromConfiguration(configuration);

        Assert.Equal("Keycloak", settings.ApiAuthenticationProvider);
    }

    /// <summary>
    /// Verifies that <see cref="AppHostSettings.FromConfiguration"/> correctly parses the interactive sign-in
    /// setting when it is set to "true".
    /// </summary>
    /// <remarks>
    /// Expected outcome: EnableInteractiveTestSignIn is true when Authentication:Test:EnableInteractiveSignIn is "true".
    /// This behavior matters because it enables Web-based interactive test sign-in flows for local testing and debugging.
    /// </remarks>
    [Fact]
    public void FromConfiguration_ShouldEnableInteractiveTestSignIn_WhenConfigurationValueIsTrue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Test:EnableInteractiveSignIn"] = "true"
            })
            .Build();

        var settings = AppHostSettings.FromConfiguration(configuration);

        Assert.True(settings.EnableInteractiveTestSignIn);
    }

    /// <summary>
    /// Verifies that <see cref="AppHostSettings.FromConfiguration"/> correctly parses the interactive sign-in
    /// setting when it is set to "True" (case-insensitive).
    /// </summary>
    /// <remarks>
    /// Expected outcome: EnableInteractiveTestSignIn is true when Authentication:Test:EnableInteractiveSignIn is "True".
    /// This behavior matters because configuration values should be case-insensitive for boolean settings.
    /// </remarks>
    [Fact]
    public void FromConfiguration_ShouldEnableInteractiveTestSignIn_WhenConfigurationValueIsTrueCaseInsensitive()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Test:EnableInteractiveSignIn"] = "True"
            })
            .Build();

        var settings = AppHostSettings.FromConfiguration(configuration);

        Assert.True(settings.EnableInteractiveTestSignIn);
    }

    /// <summary>
    /// Verifies that <see cref="AppHostSettings.FromConfiguration"/> does not enable interactive sign-in
    /// when the configuration value is "false".
    /// </summary>
    /// <remarks>
    /// Expected outcome: EnableInteractiveTestSignIn is false when Authentication:Test:EnableInteractiveSignIn is "false".
    /// This behavior matters because false explicitly disables the feature, unlike the default null/missing value.
    /// </remarks>
    [Fact]
    public void FromConfiguration_ShouldNotEnableInteractiveTestSignIn_WhenConfigurationValueIsFalse()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Test:EnableInteractiveSignIn"] = "false"
            })
            .Build();

        var settings = AppHostSettings.FromConfiguration(configuration);

        Assert.False(settings.EnableInteractiveTestSignIn);
    }

    /// <summary>
    /// Verifies that <see cref="AppHostSettings.FromConfiguration"/> correctly parses Azure Communication Services
    /// endpoint, sender address, and connection string when all are present.
    /// </summary>
    /// <remarks>
    /// Expected outcome: AcsEndpoint, AcsSenderAddress, and AcsConnectionString match the configured values.
    /// This behavior matters because the API project requires all three ACS values to enable email notifications.
    /// </remarks>
    [Fact]
    public void FromConfiguration_ShouldParseAcsConfiguration_WhenAllAcsSettingsArePresent()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NotificationTransports:AzureCommunicationServices:Endpoint"] = "https://example.communication.azure.com",
                ["NotificationTransports:AzureCommunicationServices:SenderAddress"] = "noreply@example.com",
                ["NotificationTransports:AzureCommunicationServices:ConnectionString"] = "endpoint=https://example.communication.azure.com/;accesskey=FAKEKEYVALUE"
            })
            .Build();

        var settings = AppHostSettings.FromConfiguration(configuration);

        Assert.Equal("https://example.communication.azure.com", settings.AcsEndpoint);
        Assert.Equal("noreply@example.com", settings.AcsSenderAddress);
        Assert.Equal("endpoint=https://example.communication.azure.com/;accesskey=FAKEKEYVALUE", settings.AcsConnectionString);
    }

    /// <summary>
    /// Verifies that <see cref="AppHostSettings.FromConfiguration"/> throws ArgumentNullException when
    /// the configuration argument is null.
    /// </summary>
    /// <remarks>
    /// Expected outcome: ArgumentNullException is thrown.
    /// This behavior matters because FromConfiguration should fail fast when given invalid input.
    /// </remarks>
    [Fact]
    public void FromConfiguration_ShouldThrowArgumentNullException_WhenConfigurationIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => AppHostSettings.FromConfiguration(null!));
    }
}
