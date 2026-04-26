using Microsoft.AspNetCore.Components;

namespace TNC.Trading.Platform.Web.UnitTests;

internal sealed class TestNavigationManager : NavigationManager
{
    public TestNavigationManager()
    {
        Initialize("https://localhost/", "https://localhost/");
    }

    public string? LastNavigationUri { get; private set; }

    public bool LastForceLoad { get; private set; }

    public void SetCurrentUri(string relativeOrAbsoluteUri)
    {
        Uri = ToAbsoluteUri(relativeOrAbsoluteUri).ToString();
        LastNavigationUri = null;
        LastForceLoad = false;
    }

    protected override void NavigateToCore(string uri, bool forceLoad)
    {
        LastForceLoad = forceLoad;
        LastNavigationUri = ToAbsoluteUri(uri).ToString();
        Uri = LastNavigationUri;
    }
}
