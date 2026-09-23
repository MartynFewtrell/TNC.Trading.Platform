using TNC.Trading.Platform.Web;
using TNC.Trading.Platform.Web.Authentication;
using TNC.Trading.Platform.Web.Components;
using TNC.Trading.Platform.Application.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TNC.Trading.Platform.Application.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<PlatformEnvironmentOptions>()
    .Bind(builder.Configuration.GetSection("Platform"))
    .Validate(options =>
    {
        try
        {
            options.GetValidatedEnvironment();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }, "Platform:Environment must be one of Desktop, Development, Test, or Live.")
    .ValidateOnStart();
builder.Services.AddSingleton<IPlatformEnvironmentContext>(serviceProvider =>
    new PlatformEnvironmentContext(serviceProvider.GetRequiredService<IOptions<PlatformEnvironmentOptions>>().Value.GetValidatedEnvironment()));

builder.AddServiceDefaults();
builder.AddPlatformDataProtection();
builder.AddPlatformWebAuthentication();
builder.AddPlatformWebUi();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddPlatformApiClient();
builder.Services.AddHttpClient<PlatformAuthAuditClient>(client =>
{
    client.BaseAddress = new Uri("https+http://api");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/")
    {
        var hasPlatformPromptedMarker = context.Request.Query.TryGetValue("platformPrompted", out var platformPromptedValues)
            && platformPromptedValues.Any(static value => string.Equals(value, "1", StringComparison.Ordinal));

        if (!hasPlatformPromptedMarker)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                await context.SignOutAsync(PlatformAuthenticationDefaults.Schemes.Cookie);
            }

            context.Response.Redirect("/authentication/sign-in?returnUrl=%2F&prompt=login");
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.Redirect("/authentication/sign-in?returnUrl=%2F&prompt=login");
            return;
        }

        var accessToken = await context.GetTokenAsync("access_token");
        if (!PlatformTokenScopeEvaluator.HasUsableSessionToken(accessToken))
        {
            await context.SignOutAsync(PlatformAuthenticationDefaults.Schemes.Cookie);
            context.Response.Redirect("/authentication/sign-in?returnUrl=%2F&prompt=login");
            return;
        }
    }

    await next();
});

app.MapStaticAssets();
app.MapPlatformAuthenticationEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();

await app.RunAsync();
