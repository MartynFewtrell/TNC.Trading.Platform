using System.Text.Json;

namespace TNC.Trading.Platform.Infrastructure.UnitTests.Integrations.Ig;

public sealed class MarketDetailsBulkContractFixtureTests
{
    /// <summary>
    /// Trace: Market Details Work Item 1, step 1.
    /// Verifies: the requester-supplied filter=ALL response contains instrument, dealing rules, and snapshot for the observed currency market.
    /// Expected: all required sections and representative nullable, zero, unit-bearing, and ordered fields remain in the fixture.
    /// Why: the bulk parser must preserve actual provider values rather than silently null-filling or flattening the data.
    /// </summary>
    [Fact]
    public void UserSuppliedResponse_ShouldContainAllRequiredSections_AndRepresentativeOptionalValues()
    {
        using var stream = typeof(MarketDetailsBulkContractFixtureTests).Assembly.GetManifestResourceStream(
            "TNC.Trading.Platform.Infrastructure.UnitTests.Fixtures.market-details-v2-filter-all-adausd.json");
        Assert.NotNull(stream);
        using var document = JsonDocument.Parse(stream);

        var markets = document.RootElement.GetProperty("marketDetails");
        var market = Assert.Single(markets.EnumerateArray());
        var instrument = market.GetProperty("instrument");
        var dealingRules = market.GetProperty("dealingRules");
        var snapshot = market.GetProperty("snapshot");

        Assert.Equal("CS.D.ADAUSD.CFD.IP", instrument.GetProperty("epic").GetString());
        Assert.Equal("CURRENCIES", instrument.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, instrument.GetProperty("marginDepositBands")[3].GetProperty("max").ValueKind);
        Assert.Equal("POINTS", dealingRules.GetProperty("minStepDistance").GetProperty("unit").GetString());
        Assert.Equal(0, snapshot.GetProperty("delayTime").GetDecimal());
        Assert.Equal(JsonValueKind.Null, snapshot.GetProperty("binaryOdds").ValueKind);
        Assert.Equal(
            "Quoted 24/7",
            instrument.GetProperty("specialInfo")[5].GetString());
    }

    /// <summary>
    /// Trace: Market Details Work Item 1, step 1.
    /// Verifies: a controlled Demo multi-EPIC filter=ALL response returns one complete marketDetails row for each requested EPIC.
    /// Expected: response cardinality and EPIC identities exactly match the two EPICs in the supplied URL, with all three required sections.
    /// Why: the collector must not silently drop a requested market or claim batch completeness from a partial result.
    /// </summary>
    [Fact]
    public void DemoMultiEpicResponse_ShouldReturnOneCompleteRowPerRequestedEpic()
    {
        using var stream = typeof(MarketDetailsBulkContractFixtureTests).Assembly.GetManifestResourceStream(
            "TNC.Trading.Platform.Infrastructure.UnitTests.Fixtures.market-details-v2-filter-all-eth-ltc.json");
        Assert.NotNull(stream);
        using var document = JsonDocument.Parse(stream);

        var markets = document.RootElement.GetProperty("marketDetails").EnumerateArray().ToArray();

        Assert.Equal(2, markets.Length);
        Assert.Equal(
            "CS.D.ETHUSD.CFD.IP",
            markets[0].GetProperty("instrument").GetProperty("epic").GetString());
        Assert.Equal(
            "CS.D.LTCUSD.CFD.IP",
            markets[1].GetProperty("instrument").GetProperty("epic").GetString());
        Assert.All(markets, market =>
        {
            Assert.Equal(JsonValueKind.Object, market.GetProperty("instrument").ValueKind);
            Assert.Equal(JsonValueKind.Object, market.GetProperty("dealingRules").ValueKind);
            Assert.Equal(JsonValueKind.Object, market.GetProperty("snapshot").ValueKind);
        });
    }
}
