using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TNC.Trading.Platform.Application.Authentication;
using TNC.Trading.Platform.Web.Authentication;

namespace TNC.Trading.Platform.Web.UnitTests;

public class PlatformTestAuthenticationSignInHandlerTests
{
    /// <summary>
    /// Trace: NF2, TR3.
    /// Verifies: the extracted synthetic sign-in handler is enabled only when the explicit Web test harness flag is set for the Test provider.
    /// Expected: the handler reports that it is enabled when the Test provider and the interactive-sign-in flag are both active.
    /// Why: the extracted synthetic sign-in flow must remain opt-in test infrastructure rather than a default product-host capability.
    /// </summary>
    [Fact]
    public void IsEnabled_ShouldReturnTrue_WhenTestProviderAndInteractiveHarnessAreEnabled()
    {
        var options = Options.Create(new PlatformAuthenticationOptions
        {
            Provider = PlatformAuthenticationDefaults.Providers.Test,
            Test = new PlatformAuthenticationOptions.TestOptions
            {
                EnableInteractiveSignIn = true
            }
        });

        var handler = new PlatformTestAuthenticationSignInHandler(
            options,
            new TestAuthenticationTokenFactory(options),
            NullLogger<PlatformTestAuthenticationSignInHandler>.Instance);

        Assert.True(handler.IsEnabled);
    }

    /// <summary>
    /// Trace: NF2, TR3.
    /// Verifies: the extracted synthetic sign-in handler stays disabled when the explicit Web test harness flag is not enabled.
    /// Expected: the handler reports that it is disabled even when the Test provider is selected.
    /// Why: selecting the Test provider alone must not reintroduce the synthetic interactive sign-in surface into the general product host path.
    /// </summary>
    [Fact]
    public void IsEnabled_ShouldReturnFalse_WhenInteractiveHarnessIsDisabled()
    {
        var options = Options.Create(new PlatformAuthenticationOptions
        {
            Provider = PlatformAuthenticationDefaults.Providers.Test,
            Test = new PlatformAuthenticationOptions.TestOptions
            {
                EnableInteractiveSignIn = false
            }
        });

        var handler = new PlatformTestAuthenticationSignInHandler(
            options,
            new TestAuthenticationTokenFactory(options),
            NullLogger<PlatformTestAuthenticationSignInHandler>.Instance);

        Assert.False(handler.IsEnabled);
    }
}
