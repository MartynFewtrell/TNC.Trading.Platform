namespace TNC.Trading.Platform.Web.Authentication;

internal sealed record PlatformOperatorContext(
    string DisplayName,
    string UserName,
    bool IsAuthenticated,
    bool IsViewer,
    bool IsOperator,
    bool IsAdministrator,
    IReadOnlyList<string> Roles)
{
    public bool HasAnyPlatformRole => IsViewer || IsOperator || IsAdministrator;
}
