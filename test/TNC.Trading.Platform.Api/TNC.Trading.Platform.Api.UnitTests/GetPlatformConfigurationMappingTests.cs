using System.Reflection;
using TNC.Trading.Platform.Api.Features.GetPlatformConfiguration;
using TNC.Trading.Platform.Application.Configuration;
using AppResponse = TNC.Trading.Platform.Application.Features.GetPlatformConfiguration.GetPlatformConfigurationResponse;

namespace TNC.Trading.Platform.Api.UnitTests;

public class GetPlatformConfigurationMappingTests
{
    /// <summary>
    /// Traces to the Phase 3 configuration projection requirements.
    /// Verifies that a complete, usable credential projection preserves every safe presence and usability boolean.
    /// Expected: authentication is ready and re-entry is not required.
    /// Why: operators need an accurate safe status without exposing the credential values themselves.
    /// </summary>
    [Fact]
    public void ToResponse_WhenCredentialsAreUsable_ShouldMapEverySafeCredentialBoolean()
    {
        var response = CreateApplicationResponse(new CredentialPresence(true, true, true, true, true, true));

        var mapped = response.ToResponse();

        Assert.True(mapped.Credentials.HasApiKey);
        Assert.True(mapped.Credentials.HasIdentifier);
        Assert.True(mapped.Credentials.HasPassword);
        Assert.True(mapped.Credentials.IsApiKeyUsable);
        Assert.True(mapped.Credentials.IsIdentifierUsable);
        Assert.True(mapped.Credentials.IsPasswordUsable);
        Assert.True(mapped.Credentials.IsAuthenticationReady);
        Assert.False(mapped.Credentials.RequiresCredentialReentry);
    }

    /// <summary>
    /// Traces to the Phase 3 configuration projection requirements.
    /// Verifies that complete credentials with one unusable component map to the re-entry-required state.
    /// Expected: presence remains true, the unusable component remains false, authentication is not ready, and re-entry is required.
    /// Why: the UI must distinguish retained but invalid credentials from missing credentials without receiving secrets or exceptions.
    /// </summary>
    [Fact]
    public void ToResponse_WhenCredentialsRequireReentry_ShouldMapUsabilityAndReentryState()
    {
        var response = CreateApplicationResponse(new CredentialPresence(true, true, true, false, true, true));

        var mapped = response.ToResponse();

        Assert.True(mapped.Credentials.HasApiKey);
        Assert.True(mapped.Credentials.HasIdentifier);
        Assert.True(mapped.Credentials.HasPassword);
        Assert.False(mapped.Credentials.IsApiKeyUsable);
        Assert.True(mapped.Credentials.IsIdentifierUsable);
        Assert.True(mapped.Credentials.IsPasswordUsable);
        Assert.False(mapped.Credentials.IsAuthenticationReady);
        Assert.True(mapped.Credentials.RequiresCredentialReentry);
    }

    /// <summary>
    /// Traces to the Phase 3 configuration projection requirements.
    /// Verifies that every response model property is safe and that no secret, ciphertext, exception, or key field is introduced.
    /// Expected: the complete response graph contains no forbidden property names or exception-typed properties.
    /// Why: configuration reads cross an API boundary and must never disclose protected credential material or implementation failures.
    /// </summary>
    [Fact]
    public void ToResponse_ShouldContainOnlySafeResponseFields()
    {
        var propertyNames = typeof(GetPlatformConfigurationResponse)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .SelectMany(GetPropertyNames)
            .ToArray();

        Assert.DoesNotContain(propertyNames, name => name.Contains("secret", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("cipher", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("exception", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Equals("key", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> GetPropertyNames(PropertyInfo property)
    {
        yield return property.Name;
        foreach (var nestedProperty in property.PropertyType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            yield return nestedProperty.Name;
        }
    }

    private static AppResponse CreateApplicationResponse(CredentialPresence credentials)
        => new(CreateSnapshot(credentials));

    private static PlatformConfigurationSnapshot CreateSnapshot(CredentialPresence credentials)
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
            false);
}