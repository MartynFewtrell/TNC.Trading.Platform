using System.Text.Json;

namespace TNC.Trading.Platform.Api.UnitTests;

public class IgAuthenticationResponseSanitizerTests
{
    [Fact]
    public void Sanitize_RedactsSensitiveHeadersAndExposesOnlyPresenceFlags()
    {
        var response = ApiReflection.Create(
            "TNC.Trading.Platform.Api.Infrastructure.Ig.IgAuthenticateResponse",
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

        var sanitized = ApiReflection.InvokeStatic(
            "TNC.Trading.Platform.Api.Infrastructure.Ig.IgAuthenticationResponseSanitizer",
            "Sanitize",
            response);

        Assert.True(ApiReflection.GetProperty<bool>(sanitized!, "HasClientSessionToken"));
        Assert.True(ApiReflection.GetProperty<bool>(sanitized!, "HasAccountSecurityToken"));

        var headersJson = JsonSerializer.Serialize(ApiReflection.GetProperty<object>(sanitized!, "Headers"));
        Assert.Contains("[redacted]", headersJson, StringComparison.Ordinal);
        Assert.Contains("\"Version\":\"3\"", headersJson, StringComparison.Ordinal);
        Assert.DoesNotContain("client-session-token", headersJson, StringComparison.Ordinal);
        Assert.DoesNotContain("account-security-token", headersJson, StringComparison.Ordinal);
    }
}
