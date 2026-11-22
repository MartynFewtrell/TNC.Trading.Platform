using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using TNC.Trading.Platform.Application.Features.MarketNavigation;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class MarketNavigationProbeCommandEndpointMapping
{
    private const string CommandKeyConfigurationKey = "MarketNavigationProbe:CommandKey";
    private const string CommandKeyHeaderName = "X-Market-Navigation-Probe-Key";
    private const string Route = "/_internal/market-navigation/probe";

    internal static void Map(WebApplication app)
    {
        if (!app.Environment.IsDevelopment()
            || string.IsNullOrWhiteSpace(app.Configuration[CommandKeyConfigurationKey]))
        {
            return;
        }

        app.MapPost(Route, HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        MarketNavigationProbeCommandRequest request,
        [FromHeader(Name = CommandKeyHeaderName)] string? commandKey,
        IConfiguration configuration,
        RunMarketNavigationProbeHandler handler,
        CancellationToken cancellationToken)
    {
        var expectedKey = configuration[CommandKeyConfigurationKey];
        if (!IsAuthorized(commandKey, expectedKey))
        {
            return TypedResults.Unauthorized();
        }

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.AuthorizationReference)
            || request.AuthorizationReference.Length > 128
            || request.AuthorizationReference.Any(char.IsControl))
        {
            errors[nameof(request.AuthorizationReference)] = ["A valid authorization reference is required."];
        }

        if (request.NodeId is not null
            && (string.IsNullOrWhiteSpace(request.NodeId)
                || request.NodeId.Length > 128
                || request.NodeId.Any(char.IsControl)))
        {
            errors[nameof(request.NodeId)] = ["A valid node ID is required."];
        }

        if (errors.Count != 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var result = await handler.HandleAsync(
            new RunMarketNavigationProbeRequest(request.AuthorizationReference, request.NodeId),
            cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(result);
    }

    internal static bool IsAuthorized(string? providedKey, string? expectedKey)
    {
        if (string.IsNullOrEmpty(providedKey) || string.IsNullOrEmpty(expectedKey))
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(providedKey);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
        return providedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
