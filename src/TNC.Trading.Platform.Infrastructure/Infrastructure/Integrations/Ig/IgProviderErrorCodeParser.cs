using System.Text.Json;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDetails;

namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal static class IgProviderErrorCodeParser
{
    internal const int MaximumErrorResponseBytes = 16 * 1024;
    private static readonly JsonDocumentOptions DocumentOptions = new() { MaxDepth = 32 };

    internal static async Task<IgProviderErrorCodeParseResult> ParseAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is long length && length > MaximumErrorResponseBytes)
        {
            return Result(MarketCategoryProviderErrorCodeParseOutcome.Oversized);
        }

        try
        {
            await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var readBuffer = new byte[8192];
            while (true)
            {
                var remaining = MaximumErrorResponseBytes + 1 - (int)buffer.Length;
                if (remaining <= 0)
                {
                    return Result(MarketCategoryProviderErrorCodeParseOutcome.Oversized);
                }

                var read = await stream.ReadAsync(
                        readBuffer.AsMemory(0, Math.Min(readBuffer.Length, remaining)),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    return Parse(buffer.ToArray());
                }

                if (buffer.Length + read > MaximumErrorResponseBytes)
                {
                    return Result(MarketCategoryProviderErrorCodeParseOutcome.Oversized);
                }

                buffer.Write(readBuffer, 0, read);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException or TaskCanceledException)
        {
            return Result(MarketCategoryProviderErrorCodeParseOutcome.Unavailable);
        }
    }

    internal static IgProviderErrorCodeParseResult Parse(byte[] body)
    {
        if (body.Length == 0)
        {
            return Result(MarketCategoryProviderErrorCodeParseOutcome.Missing);
        }

        try
        {
            using var document = JsonDocument.Parse(body, DocumentOptions);
            var codeElement = document.RootElement.ValueKind switch
            {
                JsonValueKind.String => document.RootElement,
                JsonValueKind.Object when document.RootElement.TryGetProperty("errorCode", out var errorCode) => errorCode,
                _ => default
            };

            if (codeElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                return Result(MarketCategoryProviderErrorCodeParseOutcome.Missing);
            }

            if (codeElement.ValueKind != JsonValueKind.String)
            {
                return Result(MarketCategoryProviderErrorCodeParseOutcome.Malformed);
            }

            var code = codeElement.GetString();
            if (code is null)
            {
                return Result(MarketCategoryProviderErrorCodeParseOutcome.Missing);
            }

            if (code.Length > 128 || code.Any(char.IsControl))
            {
                return Result(MarketCategoryProviderErrorCodeParseOutcome.Unsafe);
            }

            return MarketDetailProviderErrorCodes.IsAllowed(code)
                ? new(code, MarketCategoryProviderErrorCodeParseOutcome.Parsed)
                : Result(MarketCategoryProviderErrorCodeParseOutcome.Unknown);
        }
        catch (JsonException)
        {
            return Result(IsJsonLike(body)
                ? MarketCategoryProviderErrorCodeParseOutcome.Malformed
                : MarketCategoryProviderErrorCodeParseOutcome.NonJson);
        }
    }

    private static bool IsJsonLike(ReadOnlySpan<byte> body)
    {
        var index = 0;
        if (body.Length >= 3 && body[0] == 0xEF && body[1] == 0xBB && body[2] == 0xBF)
        {
            index = 3;
        }

        while (index < body.Length && body[index] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            index++;
        }

        return index < body.Length && body[index] is (byte)'{' or (byte)'[' or (byte)'"' or (byte)'-'
            or (byte)'t' or (byte)'f' or (byte)'n' or >= (byte)'0' and <= (byte)'9';
    }

    private static IgProviderErrorCodeParseResult Result(MarketCategoryProviderErrorCodeParseOutcome outcome) =>
        new(null, outcome);
}
