using TNC.Trading.Platform.Web;
using TNC.Trading.Platform.Web.Authentication;
using TNC.Trading.Platform.Web.Components;

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

app.Use(async (httpContext, next) =>
{
    if (HttpMethods.IsGet(httpContext.Request.Method)
        && string.Equals(httpContext.Request.Path, "/", StringComparison.Ordinal)
        && httpContext.User.Identity?.IsAuthenticated != true)
    {
        httpContext.Response.Redirect("/authentication/sign-in?returnUrl=%2F&prompt=login");
        return;
    }

    await next();
});

app.MapStaticAssets();
app.MapPlatformAuthenticationEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();

await app.RunAsync();
