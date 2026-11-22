using System.Text;
using System.Text.Json;
using TNC.Trading.Platform.Infrastructure.Integrations.Ig;

namespace TNC.Trading.Platform.Infrastructure.UnitTests.Integrations.Ig;

public sealed class IgMarketNavigationShapeParserTests
{
    /// <summary>Migration phase 1: missing, null and explicit empty collections remain distinguishable, preventing invented empty coverage.</summary>
    [Theory]
    [InlineData("{}", "Absent", "Absent")]
    [InlineData("{\"nodes\":null,\"markets\":null}", "Null", "Null")]
    [InlineData("{\"nodes\":[],\"markets\":[]}", "Array", "Array")]
    public async Task ParseAsync_ShouldPreservePresence_WhenCollectionsDiffer(string body, string nodesPresence, string marketsPresence)
    {
        using var content = Content(body);
        var shape = await IgMarketNavigationShapeParser.ParseAsync(content, 1024, CancellationToken.None);
        Assert.Equal(nodesPresence, shape.NodesPresence);
        Assert.Equal(marketsPresence, shape.MarketsPresence);
        Assert.Empty(shape.NodeIds);
        Assert.Equal(0, shape.MarketCount);
    }

    /// <summary>Migration phase 1: mixed responses record both collections without the sample's mutually exclusive branch.</summary>
    [Fact]
    public async Task ParseAsync_ShouldObserveBothCollections_WhenResponseIsMixed()
    {
        using var content = Content("{\"nodes\":[{\"id\":\"opaque/root\"}],\"markets\":[{\"epic\":\"fixture.epic\"}]}");
        var shape = await IgMarketNavigationShapeParser.ParseAsync(content, 1024, CancellationToken.None);
        Assert.Equal("opaque/root", Assert.Single(shape.NodeIds));
        Assert.Equal(1, shape.MarketCount);
    }

    /// <summary>Migration phase 1: ambiguous or malformed shapes fail without returning provider data as diagnostics.</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("{\"nodes\":{}}")]
    [InlineData("{\"nodes\":[],\"nodes\":null}")]
    [InlineData("{\"nodes\":[{\"id\":\"\"}]}")]
    [InlineData("{\"markets\":[null]}")]
    public async Task ParseAsync_ShouldRejectShape_WhenPayloadIsMalformed(string body)
    {
        using var content = Content(body);
        await Assert.ThrowsAsync<JsonException>(() => IgMarketNavigationShapeParser.ParseAsync(content, 1024, CancellationToken.None));
    }

    /// <summary>Migration phase 1: byte limits apply even without a declared content length, bounding untrusted parsing.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ParseAsync_ShouldRejectBody_WhenResponseExceedsByteLimit(bool declaredLength)
    {
        using var content = Content("{\"nodes\":[],\"markets\":[]}");
        content.Headers.ContentLength = declaredLength ? 25 : null;
        await Assert.ThrowsAsync<JsonException>(() => IgMarketNavigationShapeParser.ParseAsync(content, 10, CancellationToken.None));
    }

    private static StringContent Content(string body) => new(body, Encoding.UTF8, "application/json");
}