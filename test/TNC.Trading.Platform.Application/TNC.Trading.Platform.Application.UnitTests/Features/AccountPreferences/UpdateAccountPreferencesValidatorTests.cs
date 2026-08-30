using TNC.Trading.Platform.Application.Features.AccountPreferences;

namespace TNC.Trading.Platform.Application.UnitTests.Features.AccountPreferences;

public sealed class UpdateAccountPreferencesValidatorTests
{
    /// <summary>
    /// Trace: Trailing Stops Phase 2.1 validation requirement. Verifies omitted Boolean input is rejected so false is never confused with absence.
    /// </summary>
    [Fact]
    public void Validate_ShouldRejectMissingTrailingStopsValue_WhenRequestOmitsInput()
    {
        var result = new UpdateAccountPreferencesValidator().Validate(new UpdateAccountPreferencesRequest(null));

        Assert.Single(result);
    }

    /// <summary>
    /// Trace: Trailing Stops Phase 2.1 validation requirement. Verifies both Boolean values are accepted as intentional updates.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_ShouldAcceptExplicitBoolean_WhenInputIsPresent(bool enabled)
    {
        var result = new UpdateAccountPreferencesValidator().Validate(new UpdateAccountPreferencesRequest(enabled));

        Assert.Empty(result);
    }
}