using System.Text.Json;
using TNC.Trading.Platform.Application.Features.MarketNavigation;

namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal static class IgMarketNavigationShapeParser
{
    internal static async Task<MarketNavigationShape> ParseAsync(
        HttpContent content,
        int maximumResponseBytes,
        CancellationToken cancellationToken)
    {
        using var document = await ReadDocumentAsync(content, maximumResponseBytes, cancellationToken).ConfigureAwait(false);
        return ParseShape(document.RootElement);
    }

    internal static async Task<JsonDocument> ReadDocumentAsync(
        HttpContent content,
        int maximumResponseBytes,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResponseBytes);
        if (content.Headers.ContentLength > maximumResponseBytes)
        {
            throw new JsonException("ResponseByteLimitExceeded");
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new byte[checked(maximumResponseBytes + 1)];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            length += read;
        }

        if (length > maximumResponseBytes)
        {
            throw new JsonException("ResponseByteLimitExceeded");
        }

        return JsonDocument.Parse(buffer.AsMemory(0, length), new JsonDocumentOptions { MaxDepth = 16 });
    }

    private static MarketNavigationShape ParseShape(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("InvalidNavigationShape");
        }

        var properties = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!properties.Add(property.Name))
            {
                throw new JsonException("DuplicateNavigationProperty");
            }
        }

        var nodesPresence = GetPresence(root, "nodes", out var nodes);
        var marketsPresence = GetPresence(root, "markets", out var markets);
        var nodeIds = new List<string>();
        if (nodesPresence == "Array")
        {
            foreach (var node in nodes.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.Object
                    || !node.TryGetProperty("id", out var id)
                    || id.ValueKind != JsonValueKind.String)
                {
                    throw new JsonException("InvalidNavigationNode");
                }

                var nodeId = id.GetString()!;
                if (string.IsNullOrWhiteSpace(nodeId) || nodeId.Length > 128 || nodeId.Any(char.IsControl))
                {
                    throw new JsonException("InvalidNavigationNode");
                }

                nodeIds.Add(nodeId);
            }
        }

        if (marketsPresence == "Array" && markets.EnumerateArray().Any(market => market.ValueKind != JsonValueKind.Object))
        {
            throw new JsonException("InvalidNavigationMarketShape");
        }

        return new(nodesPresence, marketsPresence, nodeIds.AsReadOnly(), marketsPresence == "Array" ? markets.GetArrayLength() : 0);
    }

    private static string GetPresence(JsonElement root, string name, out JsonElement collection)
    {
        if (!root.TryGetProperty(name, out collection))
        {
            return "Absent";
        }

        return collection.ValueKind switch
        {
            JsonValueKind.Null => "Null",
            JsonValueKind.Array => "Array",
            _ => throw new JsonException("InvalidNavigationCollection")
        };
    }
}