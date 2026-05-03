namespace TNC.Trading.Platform.Web.Authentication;

/// <summary>
/// Represents the current signed-in operator identity and platform role availability for the Web UI.
/// </summary>
public sealed record PlatformOperatorContext(
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
