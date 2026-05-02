namespace TNC.Trading.Platform.Web;

internal sealed record AuthAdministrationViewModel(
    string Provider,
    string RoleClaimType,
    string? ApiAudience);
