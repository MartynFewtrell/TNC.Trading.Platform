using System.Text.Json;

namespace TNC.Trading.Platform.Application.UnitTests;

public class IgAuthenticationResponseSanitizerTests
{
    /// <summary>
    /// Trace: FR7, SR2, TR3.
    /// Verifies: the authentication response sanitizer redacts token-bearing headers while preserving token-presence signals.
    /// Expected: sensitive header values are removed, presence flags remain true, and non-sensitive headers stay visible.
    /// Why: downstream diagnostics must be able to reason about auth responses without leaking IG authentication material.
    /// </summary>
    [Fact]
    public void Sanitize_ShouldRedactSensitiveHeaders_WhenAuthenticationResponseContainsTokens()
    {
        var response = ApplicationReflection.Create(
            "TNC.Trading.Platform.Application.Infrastructure.Ig.IgAuthenticateResponse",
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

        var sanitized = ApplicationReflection.InvokeStatic(
            "TNC.Trading.Platform.Application.Infrastructure.Ig.IgAuthenticationResponseSanitizer",
            "Sanitize",
            response);

        Assert.True(ApplicationReflection.GetProperty<bool>(sanitized!, "HasClientSessionToken"));
        Assert.True(ApplicationReflection.GetProperty<bool>(sanitized!, "HasAccountSecurityToken"));

        var headersJson = JsonSerializer.Serialize(ApplicationReflection.GetProperty<object>(sanitized!, "Headers"));
        Assert.Contains("[redacted]", headersJson, StringComparison.Ordinal);
        Assert.Contains("\"Version\":\"3\"", headersJson, StringComparison.Ordinal);
        Assert.DoesNotContain("client-session-token", headersJson, StringComparison.Ordinal);
        Assert.DoesNotContain("account-security-token", headersJson, StringComparison.Ordinal);
    }
}
