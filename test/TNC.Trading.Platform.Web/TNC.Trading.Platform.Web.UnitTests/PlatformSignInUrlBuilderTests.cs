using TNC.Trading.Platform.Web.Authentication;

namespace TNC.Trading.Platform.Web.UnitTests;

public class PlatformSignInUrlBuilderTests
{
    /// <summary>
    /// Trace: FR1, NF2, TR1.
    /// Verifies: the shared sign-in URL builder forces an interactive login prompt for default UI entry flows.
    /// Expected: creating a sign-in URL with only a return URL appends the encoded return path and `prompt=login`.
    /// Why: shared sign-in entry points must consistently prevent silent reuse of an existing identity-provider session.
    /// </summary>
    [Fact]
    public void Create_ShouldAppendPromptLogin_WhenCalledWithDefaultArguments()
    {
        var url = PlatformSignInUrlBuilder.Create("/");

        Assert.Equal("/authentication/sign-in?returnUrl=%2F&prompt=login", url);
    }

    /// <summary>
    /// Trace: FR5, IR2, TR3.
    /// Verifies: the shared sign-in URL builder preserves optional scope and user values for sign-in recovery flows.
    /// Expected: creating a sign-in URL with scope and user appends each encoded query parameter to the sign-in destination.
    /// Why: delegated-scope recovery must keep its extra context while still using the shared sign-in URL policy.
    /// </summary>
    [Fact]
    public void Create_ShouldAppendScopeAndUser_WhenOptionalValuesAreProvided()
    {
        var url = PlatformSignInUrlBuilder.Create(
            "/configuration",
            prompt: null,
            scope: "platform.operator",
            user: "local-operator");

        Assert.Equal(
            "/authentication/sign-in?returnUrl=%2Fconfiguration&scope=platform.operator&user=local-operator",
            url);
    }

    /// <summary>
    /// Trace: NF2, TR1.
    /// Verifies: the shared sign-in URL builder rejects missing return URLs.
    /// Expected: creating a sign-in URL with a blank return URL throws an <see cref="ArgumentException"/>.
    /// Why: sign-in redirects must always target a defined local destination rather than generating an ambiguous challenge URL.
    /// </summary>
    [Fact]
    public void Create_ShouldThrowArgumentException_WhenReturnUrlIsBlank()
    {
        Assert.Throws<ArgumentException>(() => PlatformSignInUrlBuilder.Create(" "));
    }
}
