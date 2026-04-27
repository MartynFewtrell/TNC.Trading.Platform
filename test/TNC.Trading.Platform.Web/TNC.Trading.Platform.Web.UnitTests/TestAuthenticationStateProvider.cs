using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace TNC.Trading.Platform.Web.UnitTests;

internal sealed class TestAuthenticationStateProvider(ClaimsPrincipal principal) : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new AuthenticationState(principal));
}
