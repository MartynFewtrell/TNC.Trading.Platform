using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using TNC.Trading.Platform.Application.Authentication;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDetails;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class MarketCategoryInstrumentEndpoints
{
    private const string CursorPurpose = "TNC.Trading.Platform.MarketCategoryInstrumentCursor.v1";

    public static void Map(RouteGroupBuilder platform)
    {
        platform.MapGet("/market-categories", GetMarketCategoriesAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Viewer)
            .WithName("GetMarketCategoriesWithInterest")
            .WithSummary("Get saved market categories and instrument collection status.")
            .WithDescription("Returns the applied environment's saved categories, operator interest, and saved collection status without calling IG.")
            .Produces<MarketCategoriesResponse>()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        platform.MapPost("/market-categories/refresh", RefreshMarketCategoriesAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Operator)
            .WithName("RefreshMarketCategories")
            .WithSummary("Refresh saved market categories during the active schedule.")
            .WithDescription("Refreshes the saved category catalogue only while the applied environment's trading schedule is active.")
            .Produces<MarketCategoriesResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
        platform.MapGet("/market-categories/{categoryCode}/instruments", GetInstrumentPageAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Viewer)
            .WithName("GetMarketCategoryInstruments")
            .WithSummary("Browse a saved market-category instrument snapshot.")
            .WithDescription("Reads a versioned SQL snapshot using an opaque forward-only cursor; it never calls IG.")
            .Produces<MarketCategoryInstrumentPageResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        platform.MapGet("/market-categories/{categoryCode}/instruments/{epic}/market-details", GetMarketDetailAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Viewer)
            .WithName("GetMarketDetail")
            .WithSummary("Get saved details and coverage for a listed instrument.")
            .WithDescription("Returns the current applied environment's saved market detail, provenance, and independent category coverage from SQL without calling IG.")
            .Produces<MarketDetailResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        platform.MapGet("/instrument-collection/status", GetCollectionStatusAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Viewer)
            .WithName("GetInstrumentCollectionStatus")
            .WithSummary("Get the applied environment's instrument collection status.")
            .WithDescription("Returns persisted collector progress, budget, safe failure state, and the next schedule wake-up.")
            .Produces<MarketCategoryInstrumentStatusResponse>()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        platform.MapPut("/market-categories/{categoryCode}/interest", UpdateInterestAsync)
            .RequireAuthorization(PlatformAuthenticationDefaults.Policies.Operator)
            .WithName("UpdateMarketCategoryInterest")
            .WithSummary("Update an operator's shared interest in a current market category.")
            .WithDescription("Uses the supplied environment-wide interest revision for optimistic concurrency and does not call IG.")
            .Produces<UpdateMarketCategoryInterestHttpResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> GetMarketCategoriesAsync(
        GetMarketCategoriesWithInterestHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await handler.HandleAsync(new GetMarketCategoriesWithInterestRequest(), cancellationToken)
                .ConfigureAwait(false);
            return result.IsAvailable
                ? TypedResults.Ok(result.ToResponse())
                : TypedResults.Problem(
                    "The applied market-data environment is unavailable.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (InvalidOperationException)
        {
            return TypedResults.Problem(
                "Saved market-category status is unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> RefreshMarketCategoriesAsync(
        RefreshMarketCategoriesManuallyHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new RefreshMarketCategoriesManuallyRequest(), cancellationToken).ConfigureAwait(false);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetInstrumentPageAsync(
        string categoryCode,
        int? pageSize,
        string? cursor,
        IDataProtectionProvider dataProtectionProvider,
        GetMarketCategoryInstrumentPageHandler handler,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var size = pageSize ?? 50;
        if (size is < 1 or > 100)
        {
            errors[nameof(pageSize)] = ["Page size must be between 1 and 100."];
        }

        if (string.IsNullOrWhiteSpace(categoryCode)
            || categoryCode.Length > 128
            || categoryCode.Any(char.IsControl))
        {
            errors[nameof(categoryCode)] = ["Category code is invalid."];
        }

        var protector = dataProtectionProvider.CreateProtector(CursorPurpose);
        InstrumentPageCursor? decodedCursor = null;
        if (cursor is not null)
        {
            try
            {
                decodedCursor = JsonSerializer.Deserialize<InstrumentPageCursor>(protector.Unprotect(cursor));
                if (decodedCursor is null
                    || decodedCursor.SnapshotVersion < 1
                    || string.IsNullOrWhiteSpace(decodedCursor.CategoryCode)
                    || decodedCursor.CategoryCode.Length > 128
                    || string.IsNullOrWhiteSpace(decodedCursor.BrokerEnvironment)
                    || string.IsNullOrWhiteSpace(decodedCursor.AfterEpic)
                    || decodedCursor.AfterEpic.Length > 64
                    || decodedCursor.AfterEpic.Any(char.IsControl)
                    || !Enum.TryParse<BrokerEnvironmentKind>(decodedCursor.BrokerEnvironment, true, out _))
                {
                    errors[nameof(cursor)] = ["Cursor is invalid."];
                }
            }
            catch (CryptographicException)
            {
                errors[nameof(cursor)] = ["Cursor is invalid."];
            }
            catch (JsonException)
            {
                errors[nameof(cursor)] = ["Cursor is invalid."];
            }
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var result = await handler.HandleAsync(
            new(
                categoryCode,
                size,
                decodedCursor?.SnapshotVersion,
                decodedCursor?.CategoryCode,
                decodedCursor?.AfterEpic,
                decodedCursor?.BrokerEnvironment),
            cancellationToken).ConfigureAwait(false);
        return result.Status switch
        {
            MarketCategoryInstrumentPageReadStatus.CategoryNotFound => TypedResults.Problem(
                "The requested market category does not exist in the saved catalogue.",
                statusCode: StatusCodes.Status404NotFound),
            MarketCategoryInstrumentPageReadStatus.StaleCursor => TypedResults.Problem(
                "The instrument snapshot changed; restart browsing from the first page.",
                statusCode: StatusCodes.Status409Conflict),
            MarketCategoryInstrumentPageReadStatus.AppliedEnvironmentUnavailable => TypedResults.Problem(
                "The applied market-data environment is unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable),
            MarketCategoryInstrumentPageReadStatus.NeverCollected => TypedResults.Ok(
                new MarketCategoryInstrumentPageResponse(
                    "NeverCollected",
                    categoryCode,
                    null,
                    null,
                    [],
                    null)),
            MarketCategoryInstrumentPageReadStatus.Page when result.Page is { } page && result.BrokerEnvironment is { } environment =>
                TypedResults.Ok(page.ToResponse(categoryCode, environment, protector)),
            _ => throw new InvalidOperationException("Unknown market-category instrument page result.")
        };
    }

    private static async Task<IResult> GetCollectionStatusAsync(
        GetMarketCategoryInstrumentStatusHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await handler.HandleAsync(new GetMarketCategoryInstrumentStatusRequest(), cancellationToken)
                .ConfigureAwait(false);
            return TypedResults.Ok(result.ToResponse());
        }
        catch (InvalidOperationException)
        {
            return TypedResults.Problem(
                "Instrument collection status is unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> GetMarketDetailAsync(
        string categoryCode,
        string epic,
        long? listingVersion,
        GetMarketDetailHandler handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(categoryCode)
            || categoryCode.Length > 128
            || categoryCode.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(epic)
            || epic.Length > 64
            || epic.Any(char.IsControl)
            || listingVersion is < 1)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(categoryCode)] = string.IsNullOrWhiteSpace(categoryCode)
                    || categoryCode.Length > 128
                    || categoryCode.Any(char.IsControl)
                    ? ["Category code is invalid."]
                    : [],
                [nameof(epic)] = string.IsNullOrWhiteSpace(epic)
                    || epic.Length > 64
                    || epic.Any(char.IsControl)
                    ? ["EPIC is invalid."]
                    : [],
                [nameof(listingVersion)] = listingVersion is < 1
                    ? ["Listing version must be positive."]
                    : []
            }.Where(item => item.Value.Length > 0).ToDictionary(item => item.Key, item => item.Value));
        }

        try
        {
            var result = await handler.HandleAsync(
                new(categoryCode, epic, listingVersion),
                cancellationToken).ConfigureAwait(false);
            return result.ToHttpResult();
        }
        catch (InvalidOperationException)
        {
            return TypedResults.Problem(
                "Saved market details are unavailable for the applied environment.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<IResult> UpdateInterestAsync(
        string categoryCode,
        UpdateMarketCategoryInterestHttpRequest request,
        UpdateMarketCategoryInterestHandler handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(categoryCode)
            || categoryCode.Length > 128
            || categoryCode.Any(char.IsControl)
            || request.ExpectedRevision < 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(categoryCode)] = string.IsNullOrWhiteSpace(categoryCode)
                    || categoryCode.Length > 128
                    || categoryCode.Any(char.IsControl)
                    ? ["Category code is invalid."]
                    : [],
                [nameof(request.ExpectedRevision)] = request.ExpectedRevision < 0
                    ? ["Expected revision must be non-negative."]
                    : []
            }.Where(item => item.Value.Length > 0).ToDictionary(item => item.Key, item => item.Value));
        }

        var result = await handler.HandleAsync(
            new(categoryCode, request.Interested, request.ExpectedRevision),
            cancellationToken).ConfigureAwait(false);
        return result.Status switch
        {
            UpdateMarketCategoryInterestStatus.Saved =>
                TypedResults.Ok(new UpdateMarketCategoryInterestHttpResponse(result.Revision!.Value)),
            UpdateMarketCategoryInterestStatus.CategoryNotFound => TypedResults.Problem(
                "The requested market category does not exist in the saved catalogue.",
                statusCode: StatusCodes.Status404NotFound),
            UpdateMarketCategoryInterestStatus.RevisionConflict => TypedResults.Problem(
                "The category interest set changed; reload it before saving.",
                statusCode: StatusCodes.Status409Conflict),
            UpdateMarketCategoryInterestStatus.AppliedEnvironmentUnavailable => TypedResults.Problem(
                "The applied market-data environment is unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => throw new InvalidOperationException("Unknown market-category interest result.")
        };
    }
}
