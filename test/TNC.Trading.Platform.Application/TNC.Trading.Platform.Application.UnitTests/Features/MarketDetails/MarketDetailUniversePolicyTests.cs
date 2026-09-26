using TNC.Trading.Platform.Application.Features.MarketDetails;

namespace TNC.Trading.Platform.Application.UnitTests.Features.MarketDetails;

public sealed class MarketDetailUniversePolicyTests
{
    /// <summary>
    /// Trace: Market Details Work Item 1, step 3.
    /// Verifies: a shared EPIC becomes one transport target while retaining each selected category's listing provenance.
    /// Expected: the target is unique and has one membership for each source category.
    /// Why: cross-category duplicates must not consume duplicate provider requests or lose source lineage.
    /// </summary>
    [Fact]
    public void Freeze_ShouldDeduplicateEpic_AndRetainEveryCategoryMembership()
    {
        var collectionA = Guid.NewGuid();
        var collectionB = Guid.NewGuid();
        var sources = new[]
        {
            new MarketDetailListingSource("CRYPTO", collectionA, 3, true, ["CS.D.ADAUSD.CFD.IP"]),
            new MarketDetailListingSource("FX", collectionB, 8, true, ["CS.D.ADAUSD.CFD.IP"])
        };

        var result = new MarketDetailUniversePolicy().Freeze(
            sources,
            prerequisitesValidated: true,
            hasSelectedCurrentCategories: true);

        var target = Assert.Single(result.Targets);
        Assert.Equal("CS.D.ADAUSD.CFD.IP", target.Epic);
        Assert.Equal(
            ["CRYPTO", "FX"],
            target.Memberships.Select(membership => membership.CategoryCode).ToArray());
        Assert.True(result.IsReady);
    }

    /// <summary>
    /// Trace: Market Details Work Item 1, step 3.
    /// Verifies: missing selected-category source data blocks universe staging.
    /// Expected: no targets are staged and the reason identifies the absent source.
    /// Why: a last-good listing cannot stand in for a failed current listing prerequisite.
    /// </summary>
    [Fact]
    public void Freeze_ShouldBlock_WhenSelectedCategoryHasNoValidatedSource()
    {
        var result = new MarketDetailUniversePolicy().Freeze(
            [],
            prerequisitesValidated: true,
            hasSelectedCurrentCategories: true);

        Assert.False(result.IsReady);
        Assert.Equal(MarketDetailUniverseBlockReason.MissingSelectedCategorySource, result.BlockReason);
        Assert.Empty(result.Targets);
    }

    /// <summary>
    /// Trace: Market Details Work Item 1, step 3.
    /// Verifies: a genuinely empty selected universe is valid only when its prerequisites are validated.
    /// Expected: validated no-selection input creates a ready empty universe, while an unvalidated prerequisite blocks.
    /// Why: zero targets must not hide an earlier catalogue or listing failure.
    /// </summary>
    [Fact]
    public void Freeze_ShouldDistinguishValidatedEmptyUniverse_FromMissingPrerequisite()
    {
        var policy = new MarketDetailUniversePolicy();
        var validatedEmpty = policy.Freeze([], prerequisitesValidated: true, hasSelectedCurrentCategories: false);
        var invalid = policy.Freeze([], prerequisitesValidated: false, hasSelectedCurrentCategories: false);

        Assert.True(validatedEmpty.IsReady);
        Assert.Empty(validatedEmpty.Targets);
        Assert.Equal(MarketDetailUniverseBlockReason.MissingPrerequisite, invalid.BlockReason);
    }

    /// <summary>
    /// Trace: Market Details Work Item 1, step 3.
    /// Verifies: source snapshots must be complete before their EPICs can enter the frozen universe.
    /// Expected: an incomplete source blocks the entire universe rather than publishing a smaller denominator.
    /// Why: partial listings cannot create false aggregate completeness.
    /// </summary>
    [Fact]
    public void Freeze_ShouldBlockAllTargets_WhenAnyListingSourceIsIncomplete()
    {
        var source = new MarketDetailListingSource("CRYPTO", Guid.NewGuid(), 1, false, ["CS.D.ADAUSD.CFD.IP"]);

        var result = new MarketDetailUniversePolicy().Freeze(
            [source],
            prerequisitesValidated: true,
            hasSelectedCurrentCategories: true);

        Assert.Equal(MarketDetailUniverseBlockReason.IncompleteListingSource, result.BlockReason);
        Assert.Empty(result.Targets);
    }
}
