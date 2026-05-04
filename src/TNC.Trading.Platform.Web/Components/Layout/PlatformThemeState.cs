using Microsoft.JSInterop;

namespace TNC.Trading.Platform.Web.Components.Layout;

internal sealed class PlatformThemeState(IJSRuntime jsRuntime, ILogger<PlatformThemeState> logger)
{
    private const string BrowserApiName = "platformTheme";
    private PlatformThemeMode currentMode = PlatformThemeMode.Dark;
    private bool isInitialized;

    public event Action? Changed;

    public PlatformThemeMode CurrentMode => currentMode;

    public bool IsDarkMode => currentMode == PlatformThemeMode.Dark;

    public string RadzenThemeName => GetRadzenThemeName(currentMode);

    public async Task EnsureInitializedAsync()
    {
        if (isInitialized)
        {
            return;
        }

        try
        {
            currentMode = ParseThemeMode(await jsRuntime.InvokeAsync<string?>(
                $"{BrowserApiName}.getTheme"));
            await jsRuntime.InvokeVoidAsync($"{BrowserApiName}.applyTheme", ToStorageValue(currentMode));
            isInitialized = true;
            Changed?.Invoke();
        }
        catch (Exception exception) when (exception is InvalidOperationException or JSDisconnectedException)
        {
            logger.LogDebug(exception, "Theme initialization will resume once JavaScript interop is available.");
        }
    }

    public Task SetDarkModeAsync() => SetModeAsync(PlatformThemeMode.Dark);

    public Task SetLightModeAsync() => SetModeAsync(PlatformThemeMode.Light);

    public Task ToggleAsync() =>
        SetModeAsync(IsDarkMode ? PlatformThemeMode.Light : PlatformThemeMode.Dark);

    public async Task SetModeAsync(PlatformThemeMode mode)
    {
        currentMode = mode;
        isInitialized = true;

        try
        {
            var storageValue = ToStorageValue(mode);
            await jsRuntime.InvokeVoidAsync($"{BrowserApiName}.setTheme", storageValue);
            await jsRuntime.InvokeVoidAsync($"{BrowserApiName}.applyTheme", storageValue);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JSDisconnectedException)
        {
            logger.LogWarning(exception, "The selected theme could not be persisted to browser storage.");
        }

        Changed?.Invoke();
    }

    internal static PlatformThemeMode ParseThemeMode(string? value) =>
        string.Equals(value, "light", StringComparison.OrdinalIgnoreCase)
            ? PlatformThemeMode.Light
            : PlatformThemeMode.Dark;

    internal static string GetRadzenThemeName(PlatformThemeMode mode) =>
        mode == PlatformThemeMode.Light
            ? "software"
            : "software-dark";

    private static string ToStorageValue(PlatformThemeMode mode) =>
        mode == PlatformThemeMode.Light ? "light" : "dark";
}
