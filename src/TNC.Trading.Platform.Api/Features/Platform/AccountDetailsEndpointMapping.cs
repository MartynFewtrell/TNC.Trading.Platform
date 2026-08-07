using AppAccountDetails = TNC.Trading.Platform.Application.Features.AccountDetails;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class AccountDetailsEndpointMapping
{
    public static AccountDetailsResponse ToResponse(this AppAccountDetails.GetAccountDetailsResponse response)
        => new(ToOptionalRetrievalResponse(response.Retrieval), response.OlderCursor, response.NewerCursor);

    public static IResult ToHttpResult(this AppAccountDetails.RefreshAccountDetailsResponse response)
        => response.Outcome switch
        {
            AppAccountDetails.AccountDetailsRefreshOutcome.Saved saved => TypedResults.Ok(ToRetrievalResponse(saved.Snapshot)),
            AppAccountDetails.AccountDetailsRefreshOutcome.RefreshInProgress conflict => TypedResults.Conflict(
                new AccountDetailsRefreshConflictResponse(conflict.LatestRetrievedAtUtc)),
            AppAccountDetails.AccountDetailsRefreshOutcome.Deferred => TypedResults.Conflict(
                new AccountDetailsRefreshConflictResponse(null)),
            AppAccountDetails.AccountDetailsRefreshOutcome.Failed failed => failed.ToHttpResult(),
            _ => throw new InvalidOperationException("Unknown account details refresh outcome.")
        };

    private static IResult ToHttpResult(this AppAccountDetails.AccountDetailsRefreshOutcome.Failed failure)
        => failure.Category switch
        {
            AppAccountDetails.AccountDetailsFailureCategory.AllowanceLimited => TypedResults.StatusCode(StatusCodes.Status429TooManyRequests),
            AppAccountDetails.AccountDetailsFailureCategory.MalformedProviderData => TypedResults.Problem(
                failure.Summary, statusCode: StatusCodes.Status502BadGateway),
            AppAccountDetails.AccountDetailsFailureCategory.Timeout => TypedResults.Problem(
                failure.Summary, statusCode: StatusCodes.Status504GatewayTimeout),
            AppAccountDetails.AccountDetailsFailureCategory.Unavailable or
            AppAccountDetails.AccountDetailsFailureCategory.UnsupportedEnvironment => TypedResults.Problem(
                failure.Summary, statusCode: StatusCodes.Status503ServiceUnavailable),
            AppAccountDetails.AccountDetailsFailureCategory.Conflict => TypedResults.Conflict(
                new AccountDetailsRefreshConflictResponse(null)),
            _ => throw new InvalidOperationException("Unknown account details failure category.")
        };

    private static AccountDetailsRetrievalResponse? ToOptionalRetrievalResponse(AppAccountDetails.AccountDetailsSnapshot? snapshot)
        => snapshot is null ? null : ToRetrievalResponse(snapshot);

    private static AccountDetailsRetrievalResponse ToRetrievalResponse(AppAccountDetails.AccountDetailsSnapshot snapshot)
        => new(snapshot.RetrievalId, snapshot.RetrievedAtUtc, snapshot.TradingDay, snapshot.TriggerSource.ToString(),
            snapshot.Accounts.Select(account => new AccountDetailsAccountResponse(
                account.AccountId,
                account.AccountName,
                account.AccountAlias,
                account.Status,
                account.AccountType,
                account.IsPreferred,
                account.Balance,
                account.Deposit,
                account.ProfitLoss,
                account.Available,
                account.Currency,
                account.CanTransferFrom,
                account.CanTransferTo)).ToList());
}

internal sealed record AccountDetailsResponse(
    AccountDetailsRetrievalResponse? Retrieval,
    string? OlderCursor,
    string? NewerCursor);

internal sealed record AccountDetailsRetrievalResponse(
    Guid RetrievalId,
    DateTimeOffset RetrievedAtUtc,
    DateOnly TradingDay,
    string TriggerSource,
    IReadOnlyList<AccountDetailsAccountResponse> Accounts);

internal sealed record AccountDetailsAccountResponse(
    string AccountId,
    string? AccountName,
    string? AccountAlias,
    string Status,
    string AccountType,
    bool IsPreferred,
    decimal Balance,
    decimal Deposit,
    decimal ProfitLoss,
    decimal Available,
    string Currency,
    bool CanTransferFrom,
    bool CanTransferTo);

internal sealed record AccountDetailsRefreshConflictResponse(DateTimeOffset? LatestRetrievedAtUtc);