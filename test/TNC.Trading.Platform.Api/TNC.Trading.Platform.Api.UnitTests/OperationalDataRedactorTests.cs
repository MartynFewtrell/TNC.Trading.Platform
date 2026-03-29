using System.Text.Json;

namespace TNC.Trading.Platform.Api.UnitTests;

public class OperationalDataRedactorTests
{
    [Fact]
    public void Serialize_RedactsSensitiveNestedProperties()
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

        var serialized = (string)ApiReflection.InvokeStatic(
            "TNC.Trading.Platform.Api.Infrastructure.Platform.OperationalDataRedactor",
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

    [Fact]
    public void RedactText_RedactsSensitiveAssignmentsInPlainText()
    {
        var redacted = (string?)ApiReflection.InvokeStatic(
            "TNC.Trading.Platform.Api.Infrastructure.Platform.OperationalDataRedactor",
            "RedactText",
            "Authentication failed with password=super-secret and Authorization: Bearer abc123");

        Assert.NotNull(redacted);
        Assert.DoesNotContain("super-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", redacted, StringComparison.Ordinal);
        Assert.Contains("[redacted]", redacted, StringComparison.Ordinal);
    }
}
