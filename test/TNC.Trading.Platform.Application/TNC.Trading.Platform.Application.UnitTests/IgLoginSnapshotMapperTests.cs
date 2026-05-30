using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Infrastructure.Ig;

namespace TNC.Trading.Platform.Application.UnitTests;

public class IgLoginSnapshotMapperTests
{
    /// <summary>
    /// Trace: FR3, FR4, SR2, SR3, TR3, TR5.
    /// Verifies: the explicit IG login snapshot mapper captures the approved non-secret payload while excluding protected authentication material.
    /// Expected: the mapped snapshot keeps non-sensitive fields and drops token-bearing headers and token values from the persisted payload shape.
    /// Why: the login snapshot contract must stay stable and secret-safe before persistence and downstream status projection consume it.
    /// </summary>
    [Fact]
    public void MapLatestSnapshot_ShouldCaptureAllowedFields_WhenResponseContainsProtectedValues()
    {
        var response = new IgAuthenticateResponse(
            "ABC123",
            "https://stream.example.test",
            new DateTimeOffset(2026, 5, 28, 9, 30, 0, TimeSpan.Zero),
            "client-session-token",
            "account-security-token",
            new Dictionary<string, string?>
            {
                ["CST"] = "cst-token",
                ["X-SECURITY-TOKEN"] = "security-token",
                ["Version"] = "3",
                ["Cache-Control"] = "no-cache"
            });

        var snapshot = IgLoginSnapshotMapper.MapLatestSnapshot(
            BrokerEnvironmentKind.Demo,
            response,
            new DateTimeOffset(2026, 5, 28, 9, 30, 5, TimeSpan.Zero));

        Assert.Equal(BrokerEnvironmentKind.Demo, snapshot.BrokerEnvironment);
        Assert.Equal(IgLoginSnapshotKind.Latest, snapshot.SnapshotKind);
        Assert.Equal("ABC123", snapshot.CurrentAccountId);
        Assert.Equal("https://stream.example.test", snapshot.LightstreamerEndpoint);
        Assert.Equal("3", snapshot.ResponseHeaders["Version"]);
        Assert.Equal("no-cache", snapshot.ResponseHeaders["Cache-Control"]);
        Assert.DoesNotContain(snapshot.ResponseHeaders.Keys, key => string.Equals(key, "CST", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(snapshot.ResponseHeaders.Keys, key => string.Equals(key, "X-SECURITY-TOKEN", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("client-session-token", snapshot.RawNonSecretPayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("account-security-token", snapshot.RawNonSecretPayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("cst-token", snapshot.RawNonSecretPayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("security-token", snapshot.RawNonSecretPayloadJson, StringComparison.Ordinal);
    }
}
