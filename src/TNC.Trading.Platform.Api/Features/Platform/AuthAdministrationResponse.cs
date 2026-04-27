namespace TNC.Trading.Platform.Api.Features.Platform;

internal sealed record AuthAdministrationResponse(
    string Provider,
    string RoleClaimType,
    string? ApiAudience);
