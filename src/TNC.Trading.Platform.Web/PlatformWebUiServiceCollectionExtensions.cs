using Radzen;
using TNC.Trading.Platform.Web.Components.Layout;

namespace TNC.Trading.Platform.Web;

internal static class PlatformWebUiServiceCollectionExtensions
{
    public static WebApplicationBuilder AddPlatformWebUi(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddRadzenComponents();
        builder.Services.AddScoped<PlatformThemeState>();
        builder.Services.AddScoped<PlatformShellContextProvider>();

        return builder;
    }
}
