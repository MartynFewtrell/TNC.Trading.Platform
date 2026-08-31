using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountPreferences;
using TNC.Trading.Platform.Api.Features.Platform;

namespace TNC.Trading.Platform.Api.UnitTests;

public sealed class AccountPreferencesEndpointMappingTests
{
    /// <summary>Trace: FR3, SR1, TR1. Verifies provider diagnostics never enter public Problem Details; operators receive stable platform-safe status and title.</summary>
    [Theory]
    [InlineData("UnsupportedEnvironment", 409, "unsupported-environment", "Account preferences are unsupported in the current environment.")]
    [InlineData("Unauthorized", 502, "provider-session-rejected", "IG account preferences session was rejected.")]
    [InlineData("RateLimited", 429, "rate-limited", "Account preferences allowance exhausted.")]
    [InlineData("MalformedProviderData", 502, "malformed-provider-data", "Account preferences provider data was invalid.")]
    [InlineData("Rejected", 409, "state-conflict", "Account preferences request conflicts with current state.")]
    [InlineData("Unavailable", 503, "provider-unavailable", "Account preferences provider is unavailable.")]
    [InlineData("Timeout", 504, "provider-timeout", "Account preferences provider timed out.")]
    [InlineData("Unsupported", 502, "unsupported-provider-response", "Account preferences provider response was unsupported.")]
    [InlineData("AccountMismatch", 409, "account-mismatch", "IG session account does not match the configured account.")]
    [InlineData("Transient", 503, "transient-provider-failure", "Account preferences provider failed temporarily.")]
    public void ToHttpResult_ShouldMapEveryFailureCategoryWithoutDiagnosticLeakage(string categoryName, int statusCode, string typeSuffix, string title)
    {
        var category = Enum.Parse<AccountPreferencesFailureCategory>(categoryName);
        var result = new AccountPreferencesGatewayOutcome.Failed(category, "provider-secret-diagnostic").ToHttpResult();
        var problem = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(statusCode, problem.StatusCode);
        var details = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>(result);
        Assert.Equal(title, details.ProblemDetails?.Title);
        Assert.Equal($"/problems/account-preferences/{typeSuffix}", details.ProblemDetails?.Type);
        Assert.Equal(category.ToString(), details.ProblemDetails?.Extensions["failureCategory"]);
        Assert.Equal(title, details.ProblemDetails?.Detail);
        Assert.DoesNotContain("provider-secret-diagnostic", details.ProblemDetails?.Detail ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("provider-secret-diagnostic", details.ProblemDetails?.Extensions.Values.Select(value => value?.ToString() ?? string.Empty) ?? [], StringComparer.Ordinal);
    }

    /// <summary>Trace: account-preferences load-performance mitigation Phase 2.3. Guards exhaustive mapping against accidental enum fall-through.</summary>
    [Fact]
    public void ToHttpResult_ShouldHaveOneNamedMappingPerFailureCategory()
    {
        var categories = Enum.GetValues<AccountPreferencesFailureCategory>();
        Assert.Equal(10, categories.Length);
        foreach (var category in categories)
            Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>(new AccountPreferencesGatewayOutcome.Failed(category, "diagnostic").ToHttpResult());
    }

    /// <summary>Trace: account-preferences load-performance mitigation Phase 2.3. Ensures unknown enum values fail closed instead of becoming a misleading provider outage.</summary>
    [Fact]
    public void ToHttpResult_ShouldRejectUnknownFailureCategory()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AccountPreferencesGatewayOutcome.Failed((AccountPreferencesFailureCategory)999, "diagnostic").ToHttpResult());
    }

    /// <summary>Trace: account-preferences load-performance mitigation Phase 2.3. The current outcome contract has no validated delay metadata, so failures must signal no retry delay.</summary>
    [Theory]
    [InlineData("RateLimited")]
    [InlineData("Unavailable")]
    [InlineData("Transient")]
    [InlineData("Rejected")]
    public async Task ToHttpResult_ShouldOmitRetryAfter_WhenNoValidatedDelayExists(string categoryName)
    {
        var category = Enum.Parse<AccountPreferencesFailureCategory>(categoryName);
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        await new AccountPreferencesGatewayOutcome.Failed(category, "diagnostic").ToHttpResult().ExecuteAsync(context);

        Assert.False(context.Response.Headers.ContainsKey("Retry-After"));
    }

    /// <summary>Trace: FR3, TR1. Verifies a successful provider result maps to the confirmed public account-preferences contract.</summary>
    [Fact]
    public void ToHttpResult_ShouldReturnConfirmedResponse_WhenProviderSucceeds()
    {
        var preferences = new AccountPreferences(true, "OK", DateTimeOffset.Parse("2026-08-30T10:00:00Z"));
        var result = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<AccountPreferencesResponse>>(
            new AccountPreferencesGatewayOutcome.Succeeded(preferences).ToHttpResult());
        Assert.Equal(preferences.TrailingStopsEnabled, result.Value!.TrailingStopsEnabled);
        Assert.Equal("InSync", result.Value.ApplicationStatus);
    }

    /// <summary>Trace: Phase 5.1. Verifies legacy Demo storage is presented externally as Test for account-preferences history.</summary>
    [Fact]
    public void ToResponse_ShouldPresentTest_WhenObservationUsesLegacyDemoBrokerEnvironment()
    {
        var observation = new TrailingStopsPreferenceObservation(
            Guid.NewGuid(), true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, "Observed", "AccountPreferences", "operator", null, "correlation");

        var response = observation.ToResponse();

        Assert.Equal("Test", response.BrokerEnvironment);
    }

    /// <summary>Trace: FR3, SR1. Verifies an authoritative mismatch maps to conflict Problem Details with stable extension fields.</summary>
    [Fact]
    public void ToHttpResult_ShouldReturnConflictProblem_WhenUpdateWasNotApplied()
    {
        var observed = new AccountPreferences(false, "OK", DateTimeOffset.Parse("2026-08-30T10:00:00Z"));
        var result = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>(
            new AccountPreferencesGatewayOutcome.NotApplied(true, observed).ToHttpResult());
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("Account preferences update was not applied.", result.ProblemDetails!.Title);
        Assert.Equal(true, result.ProblemDetails.Extensions["requestedTrailingStopsEnabled"]);
        Assert.Equal(false, result.ProblemDetails.Extensions["observedTrailingStopsEnabled"]);
    }

    /// <summary>Trace: FR3, SR1. Verifies nullable PUT input is rejected before application dispatch when the field is omitted.</summary>
    [Fact]
    public void Validate_ShouldReturnProblem_WhenTrailingStopsInputIsNull()
    {
        var errors = new UpdateAccountPreferencesValidator().Validate(new UpdateAccountPreferencesRequest(null));
        Assert.Contains("TrailingStopsEnabled is required.", errors);
    }

    /// <summary>Trace: FR3, SR1. Verifies history validation rejects invalid page sizes and malformed cursors without querying the store.</summary>
    [Theory]
    [InlineData(0, null)]
    [InlineData(101, null)]
    [InlineData(null, "")]
    [InlineData(null, "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public async Task HandleAsync_ShouldReturnValidationProblem_WhenHistoryPagingInputIsInvalid(int? pageSize, string? cursor)
    {
        var result = await GetTrailingStopsPreferenceObservationsEndpointHandler.HandleAsync(pageSize, cursor, null!, CancellationToken.None);
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
    }
}
