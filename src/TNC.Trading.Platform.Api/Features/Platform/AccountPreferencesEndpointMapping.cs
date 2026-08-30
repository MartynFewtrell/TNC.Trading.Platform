using Microsoft.AspNetCore.Http.HttpResults;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountPreferences;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class AccountPreferencesEndpointMapping
{
    internal static AccountPreferencesResponse ToResponse(this AccountPreferences preferences) =>
        new(preferences.TrailingStopsEnabled, preferences.ApplicationStatus, preferences.ObservedAtUtc);

    internal static AccountPreferencesObservationResponse ToResponse(this TrailingStopsPreferenceObservation observation) =>
        new(observation.Id, observation.TrailingStopsEnabled, observation.ObservedAtUtc, observation.RecordedAtUtc, observation.PlatformEnvironment.ToString(), BrokerEnvironmentPresentation.ForAccountPreferences(observation.BrokerEnvironment), observation.ObservationKind, observation.Source, observation.Actor, observation.CorrelationId);

    internal static IResult ToHttpResult(this AccountPreferencesGatewayOutcome outcome)
        => outcome switch
        {
            AccountPreferencesGatewayOutcome.Succeeded succeeded => TypedResults.Ok(succeeded.Preferences.ToResponse()),
            AccountPreferencesGatewayOutcome.Failed failed => failed.ToProblem(),
            AccountPreferencesGatewayOutcome.Indeterminate indeterminate => indeterminate.ToProblem(),
            AccountPreferencesGatewayOutcome.NotApplied notApplied => notApplied.ToProblem(),
            _ => TypedResults.Problem(statusCode: 503, title: "Account preferences unavailable.")
        };

    private static IResult ToProblem(this AccountPreferencesGatewayOutcome.Failed failure)
        => TypedResults.Problem(statusCode: StatusCode(failure.Category), title: Title(failure.Category));

    private static IResult ToProblem(this AccountPreferencesGatewayOutcome.Indeterminate failure)
        => TypedResults.Problem(statusCode: StatusCode(failure.Category), title: Title(failure.Category));

    private static IResult ToProblem(this AccountPreferencesGatewayOutcome.NotApplied notApplied)
        => TypedResults.Problem(statusCode: 409, title: "Account preferences update was not applied.", detail: $"Requested trailing-stops state was {notApplied.RequestedTrailingStopsEnabled}, but the provider observed {notApplied.ObservedPreferences.TrailingStopsEnabled}.", extensions: new Dictionary<string, object?> { ["requestedTrailingStopsEnabled"] = notApplied.RequestedTrailingStopsEnabled, ["observedTrailingStopsEnabled"] = notApplied.ObservedPreferences.TrailingStopsEnabled });

    private static int StatusCode(AccountPreferencesFailureCategory category) => category switch
    {
        AccountPreferencesFailureCategory.RateLimited => 429,
        AccountPreferencesFailureCategory.MalformedProviderData => 502,
        AccountPreferencesFailureCategory.Timeout => 504,
        _ => 503
    };

    private static string Title(AccountPreferencesFailureCategory category) => category switch
    {
        AccountPreferencesFailureCategory.RateLimited => "Account preferences allowance exhausted.",
        AccountPreferencesFailureCategory.MalformedProviderData => "Account preferences provider data was invalid.",
        AccountPreferencesFailureCategory.Timeout => "Account preferences provider timed out.",
        _ => "Account preferences provider is unavailable."
    };
}

internal sealed record AccountPreferencesResponse(bool TrailingStopsEnabled, string ApplicationStatus, DateTimeOffset ObservedAtUtc);
internal sealed record AccountPreferencesObservationResponse(Guid Id, bool TrailingStopsEnabled, DateTimeOffset ObservedAtUtc, DateTimeOffset RecordedAtUtc, string PlatformEnvironment, string BrokerEnvironment, string ObservationKind, string Source, string? Actor, string CorrelationId);
internal sealed record AccountPreferencesHistoryResponse(IReadOnlyList<AccountPreferencesObservationResponse> Observations, string? NextCursor);
internal sealed record UpdateAccountPreferencesHttpRequest(bool? TrailingStopsEnabled);
