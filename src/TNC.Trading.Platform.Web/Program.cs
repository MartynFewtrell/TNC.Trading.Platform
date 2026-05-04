using TNC.Trading.Platform.Web;
using TNC.Trading.Platform.Web.Authentication;
using TNC.Trading.Platform.Web.Components;
using TNC.Trading.Platform.Application.Authentication;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddPlatformWebAuthentication();
builder.AddPlatformWebUi();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpClient<PlatformApiClient>(client =>
{
    client.BaseAddress = new Uri("https+http://api");
});
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
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
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
