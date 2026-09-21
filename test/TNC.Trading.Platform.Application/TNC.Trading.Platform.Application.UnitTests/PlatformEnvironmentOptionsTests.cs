using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.UnitTests;

public sealed class PlatformEnvironmentOptionsTests
{
    /// <summary>
    /// Trace: environment-model Phase 1. Verifies that the deployment-owned Desktop value is accepted.
    /// Expected: validation returns Desktop so local AppHost composition has a stable safety classification.
    /// Why: Desktop must be distinct from the host framework environment and available before broker work starts.
    /// </summary>
    [Fact]
    public void GetValidatedEnvironment_ShouldReturnDesktop_WhenDesktopIsConfigured()
    {
        var options = new PlatformEnvironmentOptions { Environment = "Desktop" };

        Assert.Equal(PlatformEnvironmentKind.Desktop, options.GetValidatedEnvironment());
    }

    /// <summary>
    /// Trace: environment-model Phase 1. Verifies that missing or unknown deployment values fail closed.
    /// Expected: both values throw before an application can construct a runtime context.
    /// Why: an absent or typoed platform classification must never fall back to a mutable SQL value.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("Unknown")]
    public void GetValidatedEnvironment_ShouldThrow_WhenEnvironmentIsMissingOrUnknown(string? environment)
    {
        var options = new PlatformEnvironmentOptions { Environment = environment };

        Assert.Throws<InvalidOperationException>(() => options.GetValidatedEnvironment());
    }
}