using Microsoft.AspNetCore.Http;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountPreferences;
using TNC.Trading.Platform.Api.Features.Platform;

namespace TNC.Trading.Platform.Api.UnitTests;

public sealed class AccountPreferencesEndpointMappingTests
{
    /// <summary>Trace: FR3, SR1, TR1. Verifies provider diagnostics never enter public Problem Details; operators receive stable platform-safe status and title.</summary>
    [Theory]
    [InlineData(2, 429, "Account preferences allowance exhausted.")]
    [InlineData(3, 502, "Account preferences provider data was invalid.")]
    [InlineData(5, 503, "Account preferences provider is unavailable.")]
    [InlineData(6, 504, "Account preferences provider timed out.")]
    public void ToHttpResult_ShouldMapFailureWithoutDiagnosticLeakage_WhenProviderFails(int categoryValue, int statusCode, string title)
    {
        var category = (AccountPreferencesFailureCategory)categoryValue;
        var result = new AccountPreferencesGatewayOutcome.Failed(category, "provider-secret-diagnostic").ToHttpResult();
        var problem = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(statusCode, problem.StatusCode);
        var details = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>(result);
        Assert.Equal(title, details.ProblemDetails?.Title);
        Assert.DoesNotContain("provider-secret-diagnostic", details.ProblemDetails?.Detail ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>Trace: FR3, TR1. Verifies a successful provider result maps to the confirmed public account-preferences contract.</summary>
    [Fact]
    public void ToHttpResult_ShouldReturnConfirmedResponse_WhenProviderSucceeds()
    {
        var preferences = new AccountPreferences(true, "OK", DateTimeOffset.Parse("2026-08-30T10:00:00Z"));
        var result = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<AccountPreferencesResponse>>(
            new AccountPreferencesGatewayOutcome.Succeeded(preferences).ToHttpResult());
        Assert.Equal(preferences.TrailingStopsEnabled, result.Value!.TrailingStopsEnabled);
        Assert.Equal("OK", result.Value.ApplicationStatus);
    }

    /// <summary>Trace: Phase 5.1. Verifies legacy Demo storage is presented externally as Test for account-preferences history.</summary>
    [Fact]
    public void ToResponse_ShouldPresentTest_WhenObservationUsesLegacyDemoBrokerEnvironment()
    {
        var observation = new TrailingStopsPreferenceObservation(
            Guid.NewGuid(), true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, "Observed", "AccountPreferences", "operator", "correlation");

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
