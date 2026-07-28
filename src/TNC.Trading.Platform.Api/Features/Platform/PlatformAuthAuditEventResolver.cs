using System.Security.Claims;
using TNC.Trading.Platform.Application.Authentication;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class PlatformAuthAuditEventResolver
{
    internal static bool TryResolve(
        RecordAuthAuditEventRequest request,
        ClaimsPrincipal user,
        out string userName)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(user);

        userName = ResolveUserName(user);

        return request.EventType switch
        {
            PlatformAuthenticationDefaults.AuditEvents.SignInCompleted => true,
            PlatformAuthenticationDefaults.AuditEvents.SignOutCompleted => true,
            PlatformAuthenticationDefaults.AuditEvents.AccessDenied => true,
            PlatformAuthenticationDefaults.AuditEvents.TokenAcquisitionFailed => true,
            _ => false
        };
    }

    internal static string ResolveUserName(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return user.FindFirstValue(PlatformAuthenticationDefaults.Claims.PreferredUserName)
            ?? user.FindFirstValue(PlatformAuthenticationDefaults.Claims.Name)
            ?? user.Identity?.Name
            ?? "unknown-operator";
    }
}
