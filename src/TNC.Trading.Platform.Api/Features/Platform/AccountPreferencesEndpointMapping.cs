using Microsoft.AspNetCore.Http.HttpResults;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountPreferences;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class AccountPreferencesEndpointMapping
{
    internal static AccountPreferencesResponse ToResponse(this AccountPreferencesCurrentState state) =>
        new(state.AccountId, state.DesiredTrailingStopsEnabled, state.DesiredRevision, state.DesiredChangedAtUtc,
            state.ObservedTrailingStopsEnabled, state.ObservedAccountId, state.ObservedAtUtc,
            state.VerificationStatus.ToString(), state.LastVerifiedAtUtc, state.NextRetryAtUtc,
            state.FailureSummary, state.DesiredTrailingStopsEnabled, state.VerificationStatus.ToString());

    internal static AccountPreferencesObservationResponse ToResponse(this TrailingStopsPreferenceObservation observation) =>
        new(observation.Id, observation.TrailingStopsEnabled, observation.ObservedAtUtc, observation.RecordedAtUtc, observation.PlatformEnvironment.ToString(), BrokerEnvironmentPresentation.ForAccountPreferences(observation.BrokerEnvironment), observation.ObservationKind, observation.Source, observation.Actor, observation.CorrelationId);

    internal static IResult ToUnconfiguredResponse() => TypedResults.Ok(new AccountPreferencesResponse(null, null, null, null, null, null, null, "Unconfigured", null, null, null, null, "Unconfigured"));

    internal static IResult ToHttpResult(this AccountPreferencesGatewayOutcome outcome)
        => outcome switch
        {
            AccountPreferencesGatewayOutcome.Succeeded succeeded => TypedResults.Ok(new AccountPreferencesResponse(null, succeeded.Preferences.TrailingStopsEnabled, null, null, succeeded.Preferences.TrailingStopsEnabled, null, succeeded.Preferences.ObservedAtUtc, "InSync", succeeded.Preferences.ObservedAtUtc, null, null, succeeded.Preferences.TrailingStopsEnabled, "InSync")),
            AccountPreferencesGatewayOutcome.Failed failed => failed.ToProblem(),
            AccountPreferencesGatewayOutcome.Indeterminate indeterminate => indeterminate.ToProblem(),
            AccountPreferencesGatewayOutcome.NotApplied notApplied => notApplied.ToProblem(),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown account preferences outcome.")
        };

    private static IResult ToProblem(this AccountPreferencesGatewayOutcome.Failed failure)
        => Problem(failure.Category);

    private static IResult ToProblem(this AccountPreferencesGatewayOutcome.Indeterminate failure)
        => Problem(failure.Category);

    private static IResult Problem(AccountPreferencesFailureCategory category) => TypedResults.Problem(statusCode: StatusCode(category), type: $"/problems/account-preferences/{TypeSuffix(category)}", title: Title(category), detail: Detail(category), extensions: new Dictionary<string, object?> { ["failureCategory"] = category.ToString() });

    private static IResult ToProblem(this AccountPreferencesGatewayOutcome.NotApplied notApplied)
        => TypedResults.Problem(statusCode: 409, title: "Account preferences update was not applied.", detail: $"Requested trailing-stops state was {notApplied.RequestedTrailingStopsEnabled}, but the provider observed {notApplied.ObservedPreferences.TrailingStopsEnabled}.", extensions: new Dictionary<string, object?> { ["requestedTrailingStopsEnabled"] = notApplied.RequestedTrailingStopsEnabled, ["observedTrailingStopsEnabled"] = notApplied.ObservedPreferences.TrailingStopsEnabled });

    private static int StatusCode(AccountPreferencesFailureCategory category) => category switch
    {
        AccountPreferencesFailureCategory.UnsupportedEnvironment => 409,
        AccountPreferencesFailureCategory.Unauthorized => 502,
        AccountPreferencesFailureCategory.RateLimited => 429,
        AccountPreferencesFailureCategory.MalformedProviderData => 502,
        AccountPreferencesFailureCategory.Rejected => 409,
        AccountPreferencesFailureCategory.Unavailable => 503,
        AccountPreferencesFailureCategory.Timeout => 504,
        AccountPreferencesFailureCategory.Unsupported => 502,
        AccountPreferencesFailureCategory.AccountMismatch => 409,
        AccountPreferencesFailureCategory.Transient => 503,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown account preferences failure category.")
    };

    private static string Title(AccountPreferencesFailureCategory category) => category switch
    {
        AccountPreferencesFailureCategory.UnsupportedEnvironment => "Account preferences are unsupported in the current environment.",
        AccountPreferencesFailureCategory.Unauthorized => "IG account preferences session was rejected.",
        AccountPreferencesFailureCategory.RateLimited => "Account preferences allowance exhausted.",
        AccountPreferencesFailureCategory.MalformedProviderData => "Account preferences provider data was invalid.",
        AccountPreferencesFailureCategory.Rejected => "Account preferences request conflicts with current state.",
        AccountPreferencesFailureCategory.Unavailable => "Account preferences provider is unavailable.",
        AccountPreferencesFailureCategory.Timeout => "Account preferences provider timed out.",
        AccountPreferencesFailureCategory.Unsupported => "Account preferences provider response was unsupported.",
        AccountPreferencesFailureCategory.AccountMismatch => "IG session account does not match the configured account.",
        AccountPreferencesFailureCategory.Transient => "Account preferences provider failed temporarily.",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown account preferences failure category.")
    };

    private static string TypeSuffix(AccountPreferencesFailureCategory category) => category switch
    {
        AccountPreferencesFailureCategory.UnsupportedEnvironment => "unsupported-environment",
        AccountPreferencesFailureCategory.Unauthorized => "provider-session-rejected",
        AccountPreferencesFailureCategory.RateLimited => "rate-limited",
        AccountPreferencesFailureCategory.MalformedProviderData => "malformed-provider-data",
        AccountPreferencesFailureCategory.Rejected => "state-conflict",
        AccountPreferencesFailureCategory.Unavailable => "provider-unavailable",
        AccountPreferencesFailureCategory.Timeout => "provider-timeout",
        AccountPreferencesFailureCategory.Unsupported => "unsupported-provider-response",
        AccountPreferencesFailureCategory.AccountMismatch => "account-mismatch",
        AccountPreferencesFailureCategory.Transient => "transient-provider-failure",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown account preferences failure category.")
    };

    private static string Detail(AccountPreferencesFailureCategory category) => Title(category);
}

internal sealed record AccountPreferencesResponse(
    string? AccountId,
    bool? DesiredTrailingStopsEnabled,
    long? DesiredRevision,
    DateTimeOffset? DesiredChangedAtUtc,
    bool? ObservedTrailingStopsEnabled,
    string? ObservedAccountId,
    DateTimeOffset? ObservedAtUtc,
    string VerificationStatus,
    DateTimeOffset? LastVerifiedAtUtc,
    DateTimeOffset? NextRetryAtUtc,
    string? FailureSummary,
    bool? TrailingStopsEnabled,
    string ApplicationStatus);
internal sealed record AccountPreferencesObservationResponse(Guid Id, bool TrailingStopsEnabled, DateTimeOffset ObservedAtUtc, DateTimeOffset RecordedAtUtc, string PlatformEnvironment, string BrokerEnvironment, string ObservationKind, string Source, string? Actor, string CorrelationId);
internal sealed record AccountPreferencesHistoryResponse(IReadOnlyList<AccountPreferencesObservationResponse> Observations, string? NextCursor);
internal sealed record UpdateAccountPreferencesHttpRequest(bool? TrailingStopsEnabled);
internal sealed record RetryAccountPreferencesHttpRequest(string? AccountId);
internal sealed record RemediateAccountPreferencesHttpRequest(string? AccountId, long? DesiredRevision, bool? TrailingStopsEnabled);
