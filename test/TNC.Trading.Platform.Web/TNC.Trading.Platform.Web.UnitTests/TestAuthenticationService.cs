using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace TNC.Trading.Platform.Web.UnitTests;

internal sealed class TestAuthenticationService(AuthenticateResult authenticateResult) : IAuthenticationService
{
    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
        Task.FromResult(authenticateResult);

    public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
        throw new NotSupportedException();

    public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
        throw new NotSupportedException();

    public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) =>
        throw new NotSupportedException();

    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
        throw new NotSupportedException();
}
