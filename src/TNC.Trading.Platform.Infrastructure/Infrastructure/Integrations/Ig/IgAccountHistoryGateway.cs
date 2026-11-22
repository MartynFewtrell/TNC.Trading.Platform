using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using TNC.Trading.Platform.Application.Features.AccountHistory;

namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal sealed class IgAccountHistoryGateway(IgRestGatewayClient restClient) : IAccountHistoryGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<AccountHistoryGatewayOutcome<IReadOnlyList<AccountActivity>>> GetActivityByPeriodAsync(
        string expectedAccountId,
        long periodMilliseconds,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(expectedAccountId) || periodMilliseconds < 1)
        {
            return Failure<IReadOnlyList<AccountActivity>>(AccountHistoryFailureCategory.Rejected, "IG activity period request is invalid.");
        }

        var exchange = await restClient.SendDemoAccountAsync(
            HttpMethod.Get,
            $"history/activity/{periodMilliseconds.ToString(CultureInfo.InvariantCulture)}",
            "1",
            null,
            expectedAccountId,
            cancellationToken).ConfigureAwait(false);
        return Map(exchange, ParseActivities);
    }

    public async Task<AccountHistoryGatewayOutcome<IReadOnlyList<AccountActivity>>> GetActivityByDateRangeAsync(
        string expectedAccountId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(expectedAccountId) || toDate < fromDate)
        {
            return Failure<IReadOnlyList<AccountActivity>>(AccountHistoryFailureCategory.Rejected, "IG activity date range is invalid.");
        }

        var from = Uri.EscapeDataString(fromDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture));
        var to = Uri.EscapeDataString(toDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture));
        var exchange = await restClient.SendDemoAccountAsync(
            HttpMethod.Get,
            $"history/activity/{from}/{to}",
            "1",
            null,
            expectedAccountId,
            cancellationToken).ConfigureAwait(false);
        return Map(exchange, ParseActivities);
    }

    public async Task<AccountHistoryGatewayOutcome<IReadOnlyList<AccountTransaction>>> GetTransactionsByPeriodAsync(
        string expectedAccountId,
        TransactionHistoryType type,
        long periodMilliseconds,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(expectedAccountId)
            || !TryGetTransactionType(type, out var typeValue)
            || periodMilliseconds < 1)
        {
            return Failure<IReadOnlyList<AccountTransaction>>(AccountHistoryFailureCategory.Rejected, "IG transaction period request is invalid.");
        }

        var exchange = await restClient.SendDemoAccountAsync(
            HttpMethod.Get,
            $"history/transactions/{typeValue}/{periodMilliseconds.ToString(CultureInfo.InvariantCulture)}",
            "1",
            null,
            expectedAccountId,
            cancellationToken).ConfigureAwait(false);
        return Map(exchange, ParseTransactions);
    }

    public async Task<AccountHistoryGatewayOutcome<IReadOnlyList<AccountTransaction>>> GetTransactionsByDateRangeAsync(
        string expectedAccountId,
        TransactionHistoryType type,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(expectedAccountId)
            || !TryGetTransactionType(type, out var typeValue)
            || toDate < fromDate)
        {
            return Failure<IReadOnlyList<AccountTransaction>>(AccountHistoryFailureCategory.Rejected, "IG transaction date range is invalid.");
        }

        var from = Uri.EscapeDataString(fromDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture));
        var to = Uri.EscapeDataString(toDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture));
        var exchange = await restClient.SendDemoAccountAsync(
            HttpMethod.Get,
            $"history/transactions/{typeValue}/{from}/{to}",
            "1",
            null,
            expectedAccountId,
            cancellationToken).ConfigureAwait(false);
        return Map(exchange, ParseTransactions);
    }

    private static AccountHistoryGatewayOutcome<T> Map<T>(
        IgRestGatewayClient.ExchangeResult exchange,
        Func<string, T?> deserialize)
    {
        if (exchange is IgRestGatewayClient.ExchangeResult.Failed failed)
        {
            return Failure<T>(MapFailure(failed.Category), SafeReason(failed.Category));
        }

        var response = (IgRestGatewayClient.ExchangeResult.Completed)exchange;
        if ((int)response.StatusCode is < 200 or > 299)
        {
            return Failure<T>(MapFailure(response.StatusCode), SafeReason(response.StatusCode));
        }

        try
        {
            var value = deserialize(response.Body);
            return value is null
                ? Failure<T>(AccountHistoryFailureCategory.MalformedProviderData, "IG account history response was malformed.")
                : new AccountHistoryGatewayOutcome<T>.Succeeded(value, DateTimeOffset.UtcNow);
        }
        catch (JsonException)
        {
            return Failure<T>(AccountHistoryFailureCategory.MalformedProviderData, "IG account history response was malformed.");
        }
    }

    private static IReadOnlyList<AccountActivity>? ParseActivities(string body)
    {
        var response = JsonSerializer.Deserialize<IgActivityResponse>(body, JsonOptions);
        if (response?.Activities is null)
        {
            return null;
        }

        var activities = new List<AccountActivity>(response.Activities.Count);
        foreach (var activity in response.Activities)
        {
            if (string.IsNullOrWhiteSpace(activity.ActivityHistoryId))
            {
                return null;
            }

            activities.Add(new AccountActivity(
                activity.ActivityHistoryId,
                activity.Epic,
                activity.DealId,
                activity.Date,
                activity.Time,
                activity.Activity,
                activity.MarketName,
                activity.Period,
                activity.Result,
                activity.Channel,
                activity.Currency,
                activity.Size,
                activity.Level,
                activity.Stop,
                activity.StopType,
                activity.Limit,
                activity.ActionStatus));
        }

        return activities;
    }

    private static IReadOnlyList<AccountTransaction>? ParseTransactions(string body)
    {
        var response = JsonSerializer.Deserialize<IgTransactionResponse>(body, JsonOptions);
        if (response?.Transactions is null)
        {
            return null;
        }

        var transactions = new List<AccountTransaction>(response.Transactions.Count);
        foreach (var transaction in response.Transactions)
        {
            if (string.IsNullOrWhiteSpace(transaction.Reference)
                || string.IsNullOrWhiteSpace(transaction.TransactionType))
            {
                return null;
            }

            transactions.Add(new AccountTransaction(
                transaction.Reference,
                transaction.TransactionType,
                transaction.Date,
                transaction.InstrumentName,
                transaction.Period,
                transaction.ProfitAndLoss,
                transaction.OpenLevel,
                transaction.CloseLevel,
                transaction.Size,
                transaction.Currency,
                transaction.CashTransaction));
        }

        return transactions;
    }

    private static bool TryGetTransactionType(TransactionHistoryType type, out string value)
    {
        value = type switch
        {
            TransactionHistoryType.All => "ALL",
            TransactionHistoryType.AllDeal => "ALL_DEAL",
            TransactionHistoryType.Deposit => "DEPOSIT",
            TransactionHistoryType.Withdrawal => "WITHDRAWAL",
            _ => string.Empty
        };
        return value.Length > 0;
    }

    private static bool IsValidIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && !value.Contains('/');

    private static AccountHistoryFailureCategory MapFailure(IgRestGatewayClient.FailureCategory category) =>
        category switch
        {
            IgRestGatewayClient.FailureCategory.UnsupportedEnvironment => AccountHistoryFailureCategory.UnsupportedEnvironment,
            IgRestGatewayClient.FailureCategory.Unauthorized => AccountHistoryFailureCategory.Unauthorized,
            IgRestGatewayClient.FailureCategory.RateLimited => AccountHistoryFailureCategory.RateLimited,
            IgRestGatewayClient.FailureCategory.Unavailable => AccountHistoryFailureCategory.Unavailable,
            IgRestGatewayClient.FailureCategory.Timeout => AccountHistoryFailureCategory.Timeout,
            IgRestGatewayClient.FailureCategory.MalformedProviderData => AccountHistoryFailureCategory.MalformedProviderData,
            _ => AccountHistoryFailureCategory.Rejected
        };

    private static AccountHistoryFailureCategory MapFailure(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => AccountHistoryFailureCategory.Unauthorized,
            HttpStatusCode.TooManyRequests => AccountHistoryFailureCategory.RateLimited,
            HttpStatusCode.RequestTimeout => AccountHistoryFailureCategory.Timeout,
            >= HttpStatusCode.InternalServerError => AccountHistoryFailureCategory.Unavailable,
            _ => AccountHistoryFailureCategory.Rejected
        };

    private static string SafeReason(IgRestGatewayClient.FailureCategory category) =>
        category switch
        {
            IgRestGatewayClient.FailureCategory.UnsupportedEnvironment => "The applied IG Demo account is unavailable or does not match the requested account.",
            IgRestGatewayClient.FailureCategory.Unauthorized => "IG account history request was unauthorized.",
            IgRestGatewayClient.FailureCategory.RateLimited => "IG account history request was rate limited.",
            IgRestGatewayClient.FailureCategory.Unavailable => "IG account history service is unavailable.",
            IgRestGatewayClient.FailureCategory.Timeout => "IG account history request timed out.",
            IgRestGatewayClient.FailureCategory.MalformedProviderData => "IG account history response was malformed.",
            _ => "IG account history request was rejected."
        };

    private static string SafeReason(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized => "IG account history request was unauthorized.",
            HttpStatusCode.TooManyRequests => "IG account history request was rate limited.",
            HttpStatusCode.RequestTimeout => "IG account history request timed out.",
            >= HttpStatusCode.InternalServerError => "IG account history service is unavailable.",
            _ => "IG account history request was rejected."
        };

    private static AccountHistoryGatewayOutcome<T> Failure<T>(
        AccountHistoryFailureCategory category,
        string safeReason) =>
        new AccountHistoryGatewayOutcome<T>.Failed(category, safeReason);

    private sealed record IgActivityResponse(
        [property: JsonPropertyName("activities")] List<IgActivity>? Activities);

    private sealed record IgActivity(
        [property: JsonPropertyName("epic")] string? Epic,
        [property: JsonPropertyName("dealId")] string? DealId,
        [property: JsonPropertyName("activityHistoryId")] string? ActivityHistoryId,
        [property: JsonPropertyName("date")] string? Date,
        [property: JsonPropertyName("time")] string? Time,
        [property: JsonPropertyName("activity")] string? Activity,
        [property: JsonPropertyName("marketName")] string? MarketName,
        [property: JsonPropertyName("period")] string? Period,
        [property: JsonPropertyName("result")] string? Result,
        [property: JsonPropertyName("channel")] string? Channel,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("size")] string? Size,
        [property: JsonPropertyName("level")] string? Level,
        [property: JsonPropertyName("stop")] string? Stop,
        [property: JsonPropertyName("stopType")] string? StopType,
        [property: JsonPropertyName("limit")] string? Limit,
        [property: JsonPropertyName("actionStatus")] string? ActionStatus);

    private sealed record IgTransactionResponse(
        [property: JsonPropertyName("transactions")] List<IgTransaction>? Transactions);

    private sealed record IgTransaction(
        [property: JsonPropertyName("date")] string? Date,
        [property: JsonPropertyName("instrumentName")] string? InstrumentName,
        [property: JsonPropertyName("period")] string? Period,
        [property: JsonPropertyName("profitAndLoss")] string? ProfitAndLoss,
        [property: JsonPropertyName("transactionType")] string? TransactionType,
        [property: JsonPropertyName("reference")] string? Reference,
        [property: JsonPropertyName("openLevel")] string? OpenLevel,
        [property: JsonPropertyName("closeLevel")] string? CloseLevel,
        [property: JsonPropertyName("size")] string? Size,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("cashTransaction")] bool CashTransaction);
}
