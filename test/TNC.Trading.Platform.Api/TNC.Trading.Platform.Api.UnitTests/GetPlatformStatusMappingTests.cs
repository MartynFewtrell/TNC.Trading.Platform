using TNC.Trading.Platform.Api.Features.GetPlatformStatus;

namespace TNC.Trading.Platform.Api.UnitTests;

public class GetPlatformStatusMappingTests
{
    /// <summary>
    /// Traces to FR9, NF2, OR1.
    /// Verifies: an IgLoginStatusResponse that carries a non-null IgProofDataResponse round-trips
    /// through the response record with all proof-data fields preserved.
    /// Expected: each field of LatestProofData matches the values used to construct the response.
    /// Why: the API contract must faithfully surface proof data so the UI can display account and position details.
    /// </summary>
    [Fact]
    public void IgLoginStatusResponse_WhenProofDataPresent_ShouldCarryAllFields()
    {
        var retrievedAt = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var proofData = new IgProofDataResponse(
            "Demo Account",
            "ABC123",
            10250.75m,
            3,
            retrievedAt);
        var loginStatus = CreateIgLoginStatusResponse(proofData);

        Assert.NotNull(loginStatus.LatestProofData);
        Assert.Equal("Demo Account", loginStatus.LatestProofData.PreferredAccountName);
        Assert.Equal("ABC123", loginStatus.LatestProofData.PreferredAccountId);
        Assert.Equal(10250.75m, loginStatus.LatestProofData.Balance);
        Assert.Equal(3, loginStatus.LatestProofData.OpenPositionCount);
        Assert.Equal(retrievedAt, loginStatus.LatestProofData.RetrievedAtUtc);
    }

    /// <summary>
    /// Traces to FR9, NF2, OR1.
    /// Verifies: an IgLoginStatusResponse constructed without proof data carries null for LatestProofData.
    /// Expected: LatestProofData is null.
    /// Why: proof data is optional; the API must tolerate the not-yet-retrieved state without errors.
    /// </summary>
    [Fact]
    public void IgLoginStatusResponse_WhenProofDataNull_ShouldMapNullLatestProofData()
    {
        var loginStatus = CreateIgLoginStatusResponse(latestProofData: null);

        Assert.Null(loginStatus.LatestProofData);
    }

    private static IgLoginStatusResponse CreateIgLoginStatusResponse(IgProofDataResponse? latestProofData)
    {
        return new IgLoginStatusResponse(
            "Active",
            new TradingScheduleStateResponse(true, "Active"),
            new RetryStateResponse("None", 0, null, false, false),
            null,
            null,
            null,
            null,
            null,
            latestProofData);
    }
}
