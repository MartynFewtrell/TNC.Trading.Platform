using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
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

    /// <summary>
    /// Trace: regression.
    /// Verifies: theme initialization can recover after JavaScript interop is unavailable on the first attempt.
    /// Expected: the first initialization attempt leaves the state uninitialized, and the next successful attempt restores and applies the stored browser theme.
    /// Why: interactive server rendering can delay browser storage access, so the shared shell must retry until the operator's saved theme is restored.
    /// </summary>
    [Fact]
    public async Task EnsureInitializedAsync_InteropUnavailableInitially_RetriesSuccessfully()
    {
        var jsRuntime = new SequencedJsRuntime(
            (_, _) => throw new InvalidOperationException("JavaScript interop is not ready."),
            (_, _) => "light",
            (_, arguments) =>
            {
                var storageValue = Assert.Single(arguments);
                Assert.Equal("light", Assert.IsType<string>(storageValue));
                return null;
            });
        var state = new PlatformThemeState(jsRuntime, NullLogger<PlatformThemeState>.Instance);
        var changeCount = 0;

        state.Changed += () => changeCount++;

        await state.EnsureInitializedAsync();

        Assert.False(state.IsInitialized);
        Assert.Equal(PlatformThemeMode.Dark, state.CurrentMode);
        Assert.Equal(0, changeCount);

        await state.EnsureInitializedAsync();

        Assert.True(state.IsInitialized);
        Assert.Equal(PlatformThemeMode.Light, state.CurrentMode);
        Assert.Equal(1, changeCount);
        Assert.Collection(
            jsRuntime.Invocations,
            invocation => Assert.Equal("platformTheme.getTheme", invocation),
            invocation => Assert.Equal("platformTheme.getTheme", invocation),
            invocation => Assert.Equal("platformTheme.applyTheme", invocation));
    }

    private sealed class SequencedJsRuntime(params Func<string, IReadOnlyList<object?>, object?>[] responses) : IJSRuntime
    {
        private readonly Queue<Func<string, IReadOnlyList<object?>, object?>> responses = new(responses);

        public List<string> Invocations { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Invocations.Add(identifier);

            if (responses.Count == 0)
            {
                throw new InvalidOperationException("No JavaScript response was configured for the invocation.");
            }

            var result = responses.Dequeue()(identifier, args ?? []);

            if (result is null)
            {
                return ValueTask.FromResult<TValue>(default!);
            }

            return ValueTask.FromResult((TValue)result);
        }
    }
}
