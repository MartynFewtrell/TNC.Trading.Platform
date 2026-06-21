using System.Text.Json;
using TNC.Trading.Platform.Application.Infrastructure.Ig;

namespace TNC.Trading.Platform.Application.UnitTests;

public class IgAuthenticationResponseSanitizerTests
{
    /// <summary>
    /// Traces to FR7, SR2, TR3.
    /// Verifies: the authentication response sanitizer redacts token-bearing headers while preserving token-presence signals.
    /// Expected: sensitive header values are removed, presence flags remain true, and non-sensitive headers stay visible.
    /// Why: downstream diagnostics must be able to reason about auth responses without leaking IG authentication material.
    /// </summary>
    [Fact]
    public void Sanitize_ShouldRedactSensitiveHeaders_WhenAuthenticationResponseContainsTokens()
    {
        var response = new IgAuthenticateResponse(
            "ABC123",
            "https://stream.example.test",
            new DateTimeOffset(2026, 3, 29, 12, 0, 0, TimeSpan.Zero),
            "client-session-token",
            "account-security-token",
            new Dictionary<string, string?>
            {
                ["CST"] = "cst-token",
                ["X-SECURITY-TOKEN"] = "security-token",
                ["Version"] = "3"
            });

        var sanitized = IgAuthenticationResponseSanitizer.Sanitize(response);

        Assert.True(sanitized.HasClientSessionToken);
        Assert.True(sanitized.HasAccountSecurityToken);

        var headersJson = JsonSerializer.Serialize(sanitized.Headers);
        Assert.Contains("[redacted]", headersJson, StringComparison.Ordinal);
        Assert.Contains("\"Version\":\"3\"", headersJson, StringComparison.Ordinal);
        Assert.DoesNotContain("client-session-token", headersJson, StringComparison.Ordinal);
        Assert.DoesNotContain("account-security-token", headersJson, StringComparison.Ordinal);
    }

    /// <summary>
    /// Traces to SR2, SR3, NF3.
    /// Verifies: the sanitizer redacts the CST header value.
    /// Expected: the CST header entry is replaced with "[redacted]".
    /// Why: the CST token is an IG session credential and must never appear in logs or operational records.
    /// </summary>
    [Fact]
    public void Sanitize_WhenResponseHasCstHeader_ShouldRedactCstValue()
    {
        var response = BuildResponse(new Dictionary<string, string?> { ["CST"] = "my-cst-value" });

        var sanitized = IgAuthenticationResponseSanitizer.Sanitize(response);

        Assert.Equal("[redacted]", sanitized.Headers["CST"]);
    }

    /// <summary>
    /// Traces to SR2, SR3, NF3.
    /// Verifies: the sanitizer redacts the X-SECURITY-TOKEN header value.
    /// Expected: the X-SECURITY-TOKEN entry is replaced with "[redacted]".
    /// Why: the security token is an IG session credential and must not be persisted or surfaced.
    /// </summary>
    [Fact]
    public void Sanitize_WhenResponseHasXSecurityTokenHeader_ShouldRedactSecurityTokenValue()
    {
        var response = BuildResponse(new Dictionary<string, string?> { ["X-SECURITY-TOKEN"] = "my-security-token" });

        var sanitized = IgAuthenticationResponseSanitizer.Sanitize(response);

        Assert.Equal("[redacted]", sanitized.Headers["X-SECURITY-TOKEN"]);
    }

    /// <summary>
    /// Traces to SR2, SR3, NF3.
    /// Verifies: the sanitizer redacts any Authorization header value.
    /// Expected: the Authorization entry is replaced with "[redacted]".
    /// Why: authorization bearer values are protected credentials that must not leak into diagnostics.
    /// </summary>
    [Fact]
    public void Sanitize_WhenResponseHasAuthorizationHeader_ShouldRedactAuthorizationValue()
    {
        var response = BuildResponse(new Dictionary<string, string?> { ["Authorization"] = "Bearer abc123" });

        var sanitized = IgAuthenticationResponseSanitizer.Sanitize(response);

        Assert.Equal("[redacted]", sanitized.Headers["Authorization"]);
    }

    /// <summary>
    /// Traces to SR2, SR3, NF3.
    /// Verifies: the sanitizer redacts the X-IG-API-KEY header value.
    /// Expected: the X-IG-API-KEY entry is replaced with "[redacted]".
    /// Why: the IG API key is a secret credential that must not appear in logs, operational records, or UI output.
    /// </summary>
    [Fact]
    public void Sanitize_WhenResponseHasXIgApiKeyHeader_ShouldRedactApiKeyValue()
    {
        var response = BuildResponse(new Dictionary<string, string?> { ["X-IG-API-KEY"] = "my-api-key" });

        var sanitized = IgAuthenticationResponseSanitizer.Sanitize(response);

        Assert.Equal("[redacted]", sanitized.Headers["X-IG-API-KEY"]);
    }

    /// <summary>
    /// Traces to SR2, NF3.
    /// Verifies: non-sensitive headers such as Version pass through the sanitizer unchanged.
    /// Expected: the Version header value is preserved as-is.
    /// Why: diagnostic headers that carry no credentials should remain visible to aid operations.
    /// </summary>
    [Fact]
    public void Sanitize_WhenResponseHasNonSensitiveVersionHeader_ShouldPassThroughValue()
    {
        var response = BuildResponse(new Dictionary<string, string?> { ["Version"] = "3" });

        var sanitized = IgAuthenticationResponseSanitizer.Sanitize(response);

        Assert.Equal("3", sanitized.Headers["Version"]);
    }

    /// <summary>
    /// Traces to SR3, NF3.
    /// Verifies: when a non-empty ClientSessionToken is present the sanitized response indicates token presence.
    /// Expected: HasClientSessionToken is true.
    /// Why: the platform must be able to verify that a session token was established without exposing its value.
    /// </summary>
    [Fact]
    public void Sanitize_WhenResponseHasClientSessionToken_ShouldIndicateTokenPresence()
    {
        var response = BuildResponse(clientSessionToken: "valid-cst");

        var sanitized = IgAuthenticationResponseSanitizer.Sanitize(response);

        Assert.True(sanitized.HasClientSessionToken);
    }

    /// <summary>
    /// Traces to SR3, NF3.
    /// Verifies: when the ClientSessionToken is absent or empty the sanitized response indicates no token.
    /// Expected: HasClientSessionToken is false.
    /// Why: accurately reporting token absence prevents the platform from treating a missing session as active.
    /// </summary>
    [Fact]
    public void Sanitize_WhenResponseHasNoClientSessionToken_ShouldIndicateTokenAbsence()
    {
        var response = BuildResponse(clientSessionToken: null);

        var sanitized = IgAuthenticationResponseSanitizer.Sanitize(response);

        Assert.False(sanitized.HasClientSessionToken);
    }

    // Helpers

    private static IgAuthenticateResponse BuildResponse(
        Dictionary<string, string?>? headers = null,
        string? clientSessionToken = "token",
        string? accountSecurityToken = "token")
    {
        return new IgAuthenticateResponse(
            "ACC001",
            "https://stream.example.test",
            null,
            clientSessionToken,
            accountSecurityToken,
            headers ?? new Dictionary<string, string?>());
    }
}

