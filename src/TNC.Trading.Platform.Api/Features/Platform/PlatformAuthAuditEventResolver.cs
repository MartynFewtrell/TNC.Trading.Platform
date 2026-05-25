using System.Security.Claims;
using TNC.Trading.Platform.Application.Authentication;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class PlatformAuthAuditEventResolver
{
    internal static bool TryResolve(
        RecordAuthAuditEventRequest request,
        ClaimsPrincipal user,
        out PlatformAuthAuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(user);

        var userName = ResolveUserName(user);

        switch (request.EventType)
        {
            case var eventType when string.Equals(eventType, PlatformAuthenticationDefaults.AuditEvents.SignInCompleted, StringComparison.Ordinal):
                record = new PlatformAuthAuditRecord($"Operator {userName} completed sign-in.", "Information", userName);
                return true;
            case var eventType when string.Equals(eventType, PlatformAuthenticationDefaults.AuditEvents.SignOutCompleted, StringComparison.Ordinal):
                record = new PlatformAuthAuditRecord($"Operator {userName} completed sign-out.", "Information", userName);
                return true;
            case var eventType when string.Equals(eventType, PlatformAuthenticationDefaults.AuditEvents.AccessDenied, StringComparison.Ordinal):
                record = new PlatformAuthAuditRecord($"Operator {userName} was denied access to {request.Path ?? "a protected platform surface"}.", "Warning", userName);
                return true;
            case var eventType when string.Equals(eventType, PlatformAuthenticationDefaults.AuditEvents.TokenAcquisitionFailed, StringComparison.Ordinal):
                record = new PlatformAuthAuditRecord($"Operator {userName} could not acquire delegated access for {request.Scope ?? "the requested scope set"}.", "Warning", userName);
                return true;
            default:
                record = default!;
                return false;
        }
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
