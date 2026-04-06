using System.Text.Json;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public class OperationalDataRedactorTests
{
    /// <summary>
    /// Trace: FR7, SR2, TR3.
    /// Verifies: operational payload serialization redacts nested sensitive values before they are recorded.
    /// Expected: sensitive fields are replaced with redaction markers throughout the serialized JSON output.
    /// Why: persisted operational payloads must not become a path for leaking secrets during diagnostics or audit review.
    /// </summary>
    [Fact]
    public void Serialize_ShouldRedactSensitiveValues_WhenPayloadContainsNestedSecretFields()
    {
        var payload = new
        {
            ApiKey = "top-secret",
            Nested = new
            {
                AccessToken = "access-token",
                NonSecret = "visible"
            },
            Items = new object[]
            {
                new { Password = "password-1", NonSecret = (string?)null },
                new { Password = (string?)null, NonSecret = "still-visible" }
            }
        };

        var serialized = (string)InfrastructureReflection.InvokeStatic(
            "TNC.Trading.Platform.Infrastructure.Platform.OperationalDataRedactor",
            "Serialize",
            payload)!;

        using var json = JsonDocument.Parse(serialized);
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal("[redacted]", json.RootElement.GetProperty("apiKey").GetString());
        Assert.Equal("[redacted]", json.RootElement.GetProperty("nested").GetProperty("accessToken").GetString());
        Assert.Equal("[redacted]", json.RootElement.GetProperty("nested").GetProperty("nonSecret").GetString());
        Assert.Equal("[redacted]", items[0].GetProperty("password").GetString());
        Assert.Equal("[redacted]", items[1].GetProperty("nonSecret").GetString());
    }

    /// <summary>
    /// Trace: FR7, SR2, TR3.
    /// Verifies: plaintext summaries are scrubbed when they contain password or bearer-token assignments.
    /// Expected: the returned text omits raw secret fragments and includes redaction markers instead.
    /// Why: log and notification text must remain operationally useful without exposing sensitive authentication material.
    /// </summary>
    [Fact]
    public void RedactText_ShouldRedactSensitiveAssignments_WhenPlainTextContainsSecrets()
    {
        var redacted = (string?)InfrastructureReflection.InvokeStatic(
            "TNC.Trading.Platform.Infrastructure.Platform.OperationalDataRedactor",
            "RedactText",
            "Authentication failed with password=super-secret and Authorization: Bearer abc123");

        Assert.NotNull(redacted);
        Assert.DoesNotContain("super-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", redacted, StringComparison.Ordinal);
        Assert.Contains("[redacted]", redacted, StringComparison.Ordinal);
    }
}
