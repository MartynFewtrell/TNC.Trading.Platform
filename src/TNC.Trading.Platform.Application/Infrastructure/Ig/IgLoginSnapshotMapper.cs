using System.Text.Json;
using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Infrastructure.Ig;

internal static class IgLoginSnapshotMapper
{
    public static IgLoginSnapshot MapLatestSnapshot(
        BrokerEnvironmentKind brokerEnvironment,
        IgAuthenticateResponse response,
        DateTimeOffset capturedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(response);

        var allowedHeaders = response.Headers
            .Where(pair => !IsSensitiveHeader(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value!,
                StringComparer.OrdinalIgnoreCase);

        var payload = new
        {
            response.CurrentAccountId,
            response.LightstreamerEndpoint,
            response.ExpiresAtUtc,
            Headers = allowedHeaders
        };

        return new IgLoginSnapshot(
            Guid.NewGuid(),
            brokerEnvironment,
            capturedAtUtc,
            DateOnly.FromDateTime(capturedAtUtc.UtcDateTime),
            IgLoginSnapshotKind.Latest,
            response.CurrentAccountId,
            response.LightstreamerEndpoint,
            response.ExpiresAtUtc,
            allowedHeaders,
            JsonSerializer.Serialize(payload));
    }

    private static bool IsSensitiveHeader(string headerName)
    {
        return headerName.Contains("token", StringComparison.OrdinalIgnoreCase)
            || headerName.Contains("authorization", StringComparison.OrdinalIgnoreCase)
            || headerName.Contains("cst", StringComparison.OrdinalIgnoreCase);
    }
}
