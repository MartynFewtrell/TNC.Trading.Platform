using System.Reflection;
using TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;
using TNC.Trading.Platform.Application.Configuration;
using AppResponse = TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration.UpdatePlatformConfigurationResponse;

namespace TNC.Trading.Platform.Api.UnitTests;

public class UpdatePlatformConfigurationMappingTests
{
    /// <summary>
    /// Traces to the Phase 3 configuration projection requirements.
    /// Verifies that an update result with usable credentials preserves every safe credential boolean and restart state.
    /// Expected: all presence and usability flags are true, authentication is ready, re-entry is false, and restart is preserved.
    /// Why: update responses drive the operator confirmation view and must describe the committed safe state precisely.
    /// </summary>
    [Fact]
    public void ToResponse_WhenUpdatedCredentialsAreUsable_ShouldMapEverySafeCredentialBooleanAndRestartState()
    {
        var response = CreateApplicationResponse(new CredentialPresence(true, true, true, true, true, true), true);

        var mapped = response.ToResponse();

        Assert.True(mapped.Credentials.HasApiKey);
        Assert.True(mapped.Credentials.HasIdentifier);
        Assert.True(mapped.Credentials.HasPassword);
        Assert.True(mapped.Credentials.IsApiKeyUsable);
        Assert.True(mapped.Credentials.IsIdentifierUsable);
        Assert.True(mapped.Credentials.IsPasswordUsable);
        Assert.True(mapped.Credentials.IsAuthenticationReady);
        Assert.False(mapped.Credentials.RequiresCredentialReentry);
        Assert.True(mapped.RestartRequired);
    }

    /// <summary>
    /// Traces to the Phase 3 configuration projection requirements.
    /// Verifies that an update result requiring credential re-entry maps the invalid usability state without leaking values.
    /// Expected: all credentials are present, one usability flag is false, authentication is not ready, and re-entry is required.
    /// Why: operators must be told to re-enter retained-but-unusable credentials after configuration updates.
    /// </summary>
    [Fact]
    public void ToResponse_WhenUpdatedCredentialsRequireReentry_ShouldMapReentryState()
    {
        var response = CreateApplicationResponse(new CredentialPresence(true, true, true, true, false, true), false);

        var mapped = response.ToResponse();

        Assert.True(mapped.Credentials.HasApiKey);
        Assert.True(mapped.Credentials.HasIdentifier);
        Assert.True(mapped.Credentials.HasPassword);
        Assert.True(mapped.Credentials.IsApiKeyUsable);
        Assert.False(mapped.Credentials.IsIdentifierUsable);
        Assert.True(mapped.Credentials.IsPasswordUsable);
        Assert.False(mapped.Credentials.IsAuthenticationReady);
        Assert.True(mapped.Credentials.RequiresCredentialReentry);
        Assert.False(mapped.RestartRequired);
    }

    /// <summary>
    /// Traces to the Phase 3 configuration projection requirements.
    /// Verifies that the update response graph has no secret, ciphertext, exception, or key fields.
    /// Expected: no forbidden property names or exception-typed fields are present in the public API response models.
    /// Why: update responses must remain safe even when the application result contains protected configuration internally.
    /// </summary>
    [Fact]
    public void ToResponse_ShouldContainOnlySafeResponseFields()
    {
        var propertyNames = typeof(UpdatePlatformConfigurationResponse)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .SelectMany(property => new[] { property.Name }.Concat(property.PropertyType.GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(nested => nested.Name)))
            .ToArray();

        Assert.DoesNotContain(propertyNames, name => name.Contains("secret", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("cipher", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("exception", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Equals("key", StringComparison.OrdinalIgnoreCase));
    }

    private static AppResponse CreateApplicationResponse(CredentialPresence credentials, bool restartRequired)
        => new(new UpdatePlatformConfigurationResult(CreateSnapshot(credentials, restartRequired), restartRequired));

    private static PlatformConfigurationSnapshot CreateSnapshot(CredentialPresence credentials, bool restartRequired)
        => new(
            PlatformEnvironmentKind.Test,
            BrokerEnvironmentKind.Demo,
            new TradingScheduleConfiguration(new TimeOnly(8, 0), new TimeOnly(16, 30), [DayOfWeek.Monday], WeekendBehavior.ExcludeWeekends, [], "UTC"),
            new RetryPolicyConfiguration(1, 2, 2, 8, 5),
            new NotificationSettingsConfiguration("Email", "operator@example.test"),
            credentials,
            true,
            true,
            new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero),
            restartRequired);
}