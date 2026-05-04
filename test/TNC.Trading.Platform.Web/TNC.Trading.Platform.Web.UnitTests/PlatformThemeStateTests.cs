using TNC.Trading.Platform.Web.Components.Layout;

namespace TNC.Trading.Platform.Web.UnitTests;

public class PlatformThemeStateTests
{
    /// <summary>
    /// Trace: FR6, FR7, NF10, NF11, TR2.
    /// Verifies: the theme parser falls back to dark mode when no browser preference has been stored yet.
    /// Expected: an empty or unknown stored value resolves to `Dark`.
    /// Why: the refreshed operator UI must default to dark mode until a browser-specific preference is saved.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unexpected")]
    public void ParseThemeMode_NoStoredPreference_ReturnsDarkTheme(string? storedValue)
    {
        var result = PlatformThemeState.ParseThemeMode(storedValue);

        Assert.Equal(PlatformThemeMode.Dark, result);
    }

    /// <summary>
    /// Trace: FR6, NF10, TR2.
    /// Verifies: the theme parser recognizes the stored light-mode browser preference.
    /// Expected: a `light` value resolves to `Light`.
    /// Why: the shared shell must restore the operator's explicit light-theme selection accurately.
    /// </summary>
    [Fact]
    public void ParseThemeMode_LightPreference_ReturnsLightTheme()
    {
        var result = PlatformThemeState.ParseThemeMode("light");

        Assert.Equal(PlatformThemeMode.Light, result);
    }

    /// <summary>
    /// Trace: FR6, NF10, TR2.
    /// Verifies: the selected theme mode maps to the expected Radzen Software theme family name.
    /// Expected: dark mode returns `software-dark` and light mode returns `software`.
    /// Why: the UI foundation depends on a stable Radzen theme mapping across the shared shell and refreshed pages.
    /// </summary>
    [Fact]
    public void GetRadzenThemeName_SelectedTheme_ReturnsExpectedSoftwareTheme()
    {
        Assert.Equal("software-dark", PlatformThemeState.GetRadzenThemeName(PlatformThemeMode.Dark));
        Assert.Equal("software", PlatformThemeState.GetRadzenThemeName(PlatformThemeMode.Light));
    }
}
