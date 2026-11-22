using System.Net;
using System.Text;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDetails;
using TNC.Trading.Platform.Infrastructure.Integrations.Ig;

namespace TNC.Trading.Platform.Infrastructure.UnitTests.Integrations.Ig;

public sealed class IgProviderErrorCodeParserTests
{
    /// <summary>
    /// Trace: IG market-data recovery plan Phase 1. Verifies only explicitly allow-listed error codes survive parsing and every other safe body value is discarded.
    /// Expected: each fixture produces its bounded parse outcome, while the unknown provider string is never returned.
    /// Why: raw or attacker-controlled provider text must not enter persisted diagnostics, API responses, logs, or the UI.
    /// </summary>
    [Theory]
    [InlineData("{\"errorCode\":\"error.public-api.exceeded-account-allowance\"}", "Parsed", "error.public-api.exceeded-account-allowance")]
    [InlineData("{\"errorCode\":\"error.public-api.exceeded-account-historical-data-allowance\"}", "Parsed", "error.public-api.exceeded-account-historical-data-allowance")]
    [InlineData("{\"errorCode\":\"error.security.api-key-disabled\"}", "Parsed", "error.security.api-key-disabled")]
    [InlineData("{\"errorCode\":\"error.security.api-key-restricted\"}", "Parsed", "error.security.api-key-restricted")]
    [InlineData("{\"errorCode\":\"error.public-api.failure.product-code-not-allowed\"}", "Parsed", "error.public-api.failure.product-code-not-allowed")]
    [InlineData("{\"errorCode\":\"error.public-api.failure.stockbroking-not-supported\"}", "Parsed", "error.public-api.failure.stockbroking-not-supported")]
    [InlineData("", "Missing", null)]
    [InlineData("{}", "Missing", null)]
    [InlineData("{\"errorCode\":\"operator-secret-value\"}", "Unknown", null)]
    [InlineData("{\"errorCode\"", "Malformed", null)]
    [InlineData("provider secret response", "NonJson", null)]
    [InlineData("{\"errorCode\":42}", "Malformed", null)]
    public void Parse_ShouldReturnOnlyBoundedAllowListedCode_WhenProviderBodyVaries(
        string body,
        string expectedOutcome,
        string? expectedCode)
    {
        var result = IgProviderErrorCodeParser.Parse(Encoding.UTF8.GetBytes(body));

        Assert.Equal(Enum.Parse<MarketCategoryProviderErrorCodeParseOutcome>(expectedOutcome), result.Outcome);
        Assert.Equal(expectedCode, result.SafeCode);
        if (expectedCode is null)
        {
            Assert.DoesNotContain("secret", result.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Trace: IG market-data recovery plan Phase 4, Step 4.1.
    /// Verifies: per-minute denial classes receive their matching scope, while daily historical denial uses a longer block and configuration denial receives none.
    /// Expected: account/key cooldowns are one minute, historical cooldown is twenty-four hours, and key configuration codes are not treated as allowance failures.
    /// Why: cooldowns must prevent repeat calls without pretending that historical limits reset after a minute or that key configuration can be fixed by waiting.
    /// </summary>
    [Fact]
    public void GetCooldownPolicy_ShouldUseSafeScopeAndDuration_WhenProviderCodeIsClassified()
    {
        Assert.Equal("Account", MarketDetailProviderErrorCodes.GetCooldownScope("error.public-api.exceeded-account-allowance"));
        Assert.Equal("App", MarketDetailProviderErrorCodes.GetCooldownScope("error.public-api.exceeded-api-key-allowance"));
        Assert.Equal(TimeSpan.FromMinutes(1), MarketDetailProviderErrorCodes.GetCooldownDuration("error.public-api.exceeded-application-allowance"));
        Assert.Equal(TimeSpan.FromHours(24), MarketDetailProviderErrorCodes.GetCooldownDuration("error.public-api.exceeded-account-historical-data-allowance"));
        Assert.Equal("ApiKeyDisabled", MarketDetailProviderErrorCodes.GetSafeDenialReason("error.security.api-key-disabled"));
        Assert.True(MarketDetailProviderErrorCodes.IsConfigurationDenial("error.public-api.failure.product-code-not-allowed"));
        Assert.False(MarketDetailProviderErrorCodes.IsAllowanceCode("error.security.api-key-disabled"));
        Assert.Null(MarketDetailProviderErrorCodes.GetCooldownDuration("error.security.api-key-disabled"));
    }

    /// <summary>
    /// Trace: IG market-data recovery plan Phase 1. Verifies an I/O failure while reading a provider error body has a safe explicit outcome.
    /// Expected: the parser returns Unavailable and no code instead of exposing the stream exception or body.
    /// Why: a diagnostic failure must not replace the original provider rejection or leak transport details.
    /// </summary>
    [Fact]
    public async Task ParseAsync_ShouldReturnUnavailableWithoutContent_WhenProviderErrorStreamFails()
    {
        using var content = new UnavailableContent();

        var result = await IgProviderErrorCodeParser.ParseAsync(content, CancellationToken.None);

        Assert.Equal(MarketCategoryProviderErrorCodeParseOutcome.Unavailable, result.Outcome);
        Assert.Null(result.SafeCode);
    }

    /// <summary>
    /// Trace: IG market-data recovery plan Phase 1. Verifies oversized and unsafe provider values fail closed without retaining their content.
    /// Expected: an over-limit body and an overlong code both return no safe code with their explicit parse outcomes.
    /// Why: bounded parsing must prevent oversized-body processing and must not preserve arbitrary strings that could contain secrets.
    /// </summary>
    [Fact]
    public async Task ParseAsync_ShouldRejectOversizedBodyAndUnsafeCodeWithoutRetainingText_WhenInputExceedsBounds()
    {
        using var oversizedContent = new StringContent(new string('x', IgProviderErrorCodeParser.MaximumErrorResponseBytes + 1));
        var oversized = await IgProviderErrorCodeParser.ParseAsync(oversizedContent, CancellationToken.None);
        var unsafeCode = IgProviderErrorCodeParser.Parse(
            Encoding.UTF8.GetBytes($"{{\"errorCode\":\"{new string('x', 129)}\"}}"));

        Assert.Equal(MarketCategoryProviderErrorCodeParseOutcome.Oversized, oversized.Outcome);
        Assert.Null(oversized.SafeCode);
        Assert.Equal(MarketCategoryProviderErrorCodeParseOutcome.Unsafe, unsafeCode.Outcome);
        Assert.Null(unsafeCode.SafeCode);
        Assert.DoesNotContain(new string('x', 129), unsafeCode.ToString(), StringComparison.Ordinal);
    }

    private sealed class UnavailableContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.FromException(new IOException("fixture failure"));

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return true;
        }
    }
}
