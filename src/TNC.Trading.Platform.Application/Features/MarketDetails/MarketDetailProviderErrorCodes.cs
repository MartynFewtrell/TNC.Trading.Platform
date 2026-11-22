namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal static class MarketDetailProviderErrorCodes
{
    private static readonly HashSet<string> AllowanceCodes = new(StringComparer.Ordinal)
    {
        "error.public-api.exceeded-account-allowance",
        "error.public-api.exceeded-account-trading-allowance",
        "error.public-api.exceeded-application-allowance",
        "error.public-api.exceeded-api-key-allowance",
        "error.public-api.exceeded-account-historical-data-allowance"
    };

    private static readonly HashSet<string> ConfigurationDenialCodes = new(StringComparer.Ordinal)
    {
        "error.security.api-key-disabled",
        "error.security.api-key-invalid",
        "error.security.api-key-restricted",
        "error.security.api-key-revoked",
        "error.public-api.failure.product-code-not-allowed",
        "error.public-api.failure.stockbroking-not-supported"
    };

    private static readonly HashSet<string> AllowedCodes = new(StringComparer.Ordinal)
    {
        "error.public-api.exceeded-account-allowance",
        "error.public-api.exceeded-account-trading-allowance",
        "error.public-api.exceeded-application-allowance",
        "error.public-api.exceeded-api-key-allowance",
        "error.public-api.exceeded-account-historical-data-allowance",
        "error.security.api-key-disabled",
        "error.security.api-key-invalid",
        "error.security.api-key-restricted",
        "error.security.api-key-revoked",
        "error.public-api.failure.product-code-not-allowed",
        "error.public-api.failure.stockbroking-not-supported",
        "error.public-api.epic-not-found"
    };

    public static bool IsAllowed(string? code) => code is not null && AllowedCodes.Contains(code);

    public static bool IsAllowanceCode(string? code) => code is not null && AllowanceCodes.Contains(code);

    public static bool IsConfigurationDenial(string? code) =>
        code is not null && ConfigurationDenialCodes.Contains(code);

    public static string? GetCooldownScope(string? code) => code switch
    {
        "error.public-api.exceeded-account-allowance"
            or "error.public-api.exceeded-account-trading-allowance"
            or "error.public-api.exceeded-account-historical-data-allowance" => "Account",
        "error.public-api.exceeded-application-allowance"
            or "error.public-api.exceeded-api-key-allowance" => "App",
        _ => null
    };

    public static TimeSpan? GetCooldownDuration(string? code) => code switch
    {
        "error.public-api.exceeded-account-allowance"
            or "error.public-api.exceeded-application-allowance"
            or "error.public-api.exceeded-api-key-allowance" => TimeSpan.FromMinutes(1),
        "error.public-api.exceeded-account-trading-allowance"
            or "error.public-api.exceeded-account-historical-data-allowance" => TimeSpan.FromHours(24),
        _ => null
    };

    public static string? GetSafeDenialReason(string? code) => code switch
    {
        "error.public-api.exceeded-account-allowance" => "AccountAllowanceExceeded",
        "error.public-api.exceeded-account-trading-allowance" => "AccountTradingAllowanceExceeded",
        "error.public-api.exceeded-application-allowance" => "ApplicationAllowanceExceeded",
        "error.public-api.exceeded-api-key-allowance" => "ApiKeyAllowanceExceeded",
        "error.public-api.exceeded-account-historical-data-allowance" => "HistoricalDataAllowanceExceeded",
        "error.security.api-key-disabled" => "ApiKeyDisabled",
        "error.security.api-key-invalid" => "ApiKeyInvalid",
        "error.security.api-key-restricted" => "ApiKeyRestricted",
        "error.security.api-key-revoked" => "ApiKeyRevoked",
        "error.public-api.failure.product-code-not-allowed" => "ProductCodeNotAllowed",
        "error.public-api.failure.stockbroking-not-supported" => "ProductNotSupported",
        "error.public-api.epic-not-found" => "ProductNotFound",
        _ => null
    };
}
