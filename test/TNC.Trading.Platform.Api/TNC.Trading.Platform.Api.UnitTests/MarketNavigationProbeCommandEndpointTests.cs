using TNC.Trading.Platform.Api.Features.Platform;

namespace TNC.Trading.Platform.Api.UnitTests;

public sealed class MarketNavigationProbeCommandEndpointTests
{
    /// <summary>
    /// Trace: IG navigation migration Phase 1, controlled local investigation.
    /// Verifies: the internal command endpoint accepts only the current AppHost capability key.
    /// Expected: a matching key is accepted while absent, mismatched, or differently sized keys are rejected.
    /// Why: the local-only diagnostic route must not be invocable through the ordinary unauthenticated API surface.
    /// </summary>
    [Theory]
    [InlineData("ephemeral-key", "ephemeral-key", true)]
    [InlineData(null, "ephemeral-key", false)]
    [InlineData("wrong-key", "ephemeral-key", false)]
    [InlineData("short", "ephemeral-key", false)]
    public void IsAuthorized_ShouldRequireMatchingCapabilityKey_WhenCommandIsInvoked(
        string? providedKey,
        string? expectedKey,
        bool expected)
    {
        Assert.Equal(expected, MarketNavigationProbeCommandEndpointMapping.IsAuthorized(providedKey, expectedKey));
    }
}
