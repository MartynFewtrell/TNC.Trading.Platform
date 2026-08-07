using Microsoft.AspNetCore.Http;
using TNC.Trading.Platform.Api.Features.Platform;
using AppAccountDetails = TNC.Trading.Platform.Application.Features.AccountDetails;
using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Api.UnitTests;

public class AccountDetailsEndpointMappingTests
{
    /// <summary>Verifies an empty snapshot response stays a successful null retrieval, preserving read-only behavior.</summary>
    [Fact]
    public void ToResponse_ShouldReturnNullRetrieval_WhenNoSnapshotExists()
    {
        var response = new AppAccountDetails.GetAccountDetailsResponse(null, null, null).ToResponse();

        Assert.Null(response.Retrieval);
        Assert.Null(response.OlderCursor);
        Assert.Null(response.NewerCursor);
    }

    /// <summary>Verifies saved retrievals expose account data and both opaque navigation cursors.</summary>
    [Fact]
    public void ToResponse_ShouldReturnRetrievalAndCursors_WhenSnapshotIsSaved()
    {
        var snapshot = CreateSnapshot();

        var response = new AppAccountDetails.GetAccountDetailsResponse(snapshot, "older", "newer").ToResponse();

        Assert.Equal(snapshot.RetrievalId, response.Retrieval?.RetrievalId);
        Assert.Single(response.Retrieval!.Accounts);
        Assert.Equal("older", response.OlderCursor);
        Assert.Equal("newer", response.NewerCursor);
    }

    /// <summary>Verifies saved manual refreshes return HTTP 200 with the typed retrieval payload.</summary>
    [Fact]
    public void ToHttpResult_ShouldReturnOk_WhenRefreshIsSaved()
    {
        var result = new AppAccountDetails.RefreshAccountDetailsResponse(
            new AppAccountDetails.AccountDetailsRefreshOutcome.Saved(CreateSnapshot())).ToHttpResult();

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<AccountDetailsRetrievalResponse>>(result);
    }

    /// <summary>Verifies in-progress refreshes return HTTP 409 and preserve the newest known timestamp.</summary>
    [Fact]
    public void ToHttpResult_ShouldReturnConflict_WhenRefreshIsInProgress()
    {
        var retrievedAt = DateTimeOffset.UtcNow;
        var result = new AppAccountDetails.RefreshAccountDetailsResponse(
            new AppAccountDetails.AccountDetailsRefreshOutcome.RefreshInProgress(retrievedAt)).ToHttpResult();

        var conflict = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Conflict<AccountDetailsRefreshConflictResponse>>(result);
        Assert.Equal(retrievedAt, conflict.Value.LatestRetrievedAtUtc);
    }

    /// <summary>Verifies a coalesced refresh returns HTTP 409 without inventing a retrieval timestamp.</summary>
    [Fact]
    public void ToHttpResult_ShouldReturnConflictWithoutTimestamp_WhenRefreshIsDeferred()
    {
        var result = new AppAccountDetails.RefreshAccountDetailsResponse(
            new AppAccountDetails.AccountDetailsRefreshOutcome.Deferred()).ToHttpResult();

        var conflict = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Conflict<AccountDetailsRefreshConflictResponse>>(result);
        Assert.Null(conflict.Value.LatestRetrievedAtUtc);
    }

    /// <summary>Verifies each recognized provider failure maps to its specified HTTP status.</summary>
    [Theory]
    [InlineData(0, 429)]
    [InlineData(2, 502)]
    [InlineData(1, 503)]
    [InlineData(3, 504)]
    public void ToHttpResult_ShouldReturnSpecifiedStatus_WhenRefreshFails(int categoryValue, int expectedStatus)
    {
        var category = (AppAccountDetails.AccountDetailsFailureCategory)categoryValue;
        var result = new AppAccountDetails.RefreshAccountDetailsResponse(
            new AppAccountDetails.AccountDetailsRefreshOutcome.Failed(category, "failure")).ToHttpResult();

        Assert.Equal(expectedStatus, result switch
        {
            IStatusCodeHttpResult statusCode => statusCode.StatusCode,
            _ => null
        });
    }

    /// <summary>Verifies the application conflict failure category maps to the typed HTTP 409 response.</summary>
    [Fact]
    public void ToHttpResult_ShouldReturnConflict_WhenRefreshFailureIsConflict()
    {
        var result = new AppAccountDetails.RefreshAccountDetailsResponse(
            new AppAccountDetails.AccountDetailsRefreshOutcome.Failed(
                AppAccountDetails.AccountDetailsFailureCategory.Conflict, "conflict")).ToHttpResult();

        var conflict = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Conflict<AccountDetailsRefreshConflictResponse>>(result);
        Assert.Null(conflict.Value.LatestRetrievedAtUtc);
    }

    private static AppAccountDetails.AccountDetailsSnapshot CreateSnapshot()
        => new(Guid.NewGuid(), BrokerEnvironmentKind.Demo, DateTimeOffset.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow),
            AppAccountDetails.AccountDetailsTriggerSource.Manual,
            [new AppAccountDetails.AccountDetailsAccount("account-1", "Primary", "primary", "Active", "CFD", true,
                100m, 10m, 2m, 90m, "GBP", true, false)]);
}