using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TNC.Trading.Platform.Application.Authentication;
using TNC.Trading.Platform.Web.Authentication;

namespace TNC.Trading.Platform.Web.UnitTests;

public class PlatformAccessTokenProviderTests
{
    /// <summary>
    /// Trace: NF2, SR4, IR2.
    /// Verifies: the access-token provider fails closed when the current operator session has no delegated access token.
    /// Expected: requesting a token throws an invalid-operation error and no auth-audit API request is attempted.
    /// Why: protected Web-to-API calls must not proceed when session token state is missing.
    /// </summary>
    [Fact]
    public async Task GetAccessTokenAsync_ShouldThrowInvalidOperationException_WhenSessionHasNoAccessToken()
    {
        var httpContext = CreateHttpContext(accessToken: null);
        var handler = new RecordingHttpMessageHandler();
        var auditClient = CreateAuditClient(handler, httpContext);
        var provider = new PlatformAccessTokenProvider(
            new HttpContextAccessor { HttpContext = httpContext },
            auditClient,
            NullLogger<PlatformAccessTokenProvider>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetAccessTokenAsync([PlatformAuthenticationDefaults.Scopes.Viewer], CancellationToken.None));

        Assert.Equal("The current operator session does not contain an access token.", exception.Message);
        Assert.Equal(0, handler.CallCount);
    }

    /// <summary>
    /// Trace: FR6, IR2, TR1.
    /// Verifies: the access-token provider returns the delegated access token when the current session already satisfies the requested scope set.
    /// Expected: the original access token is returned and no token-acquisition failure audit call is made.
    /// Why: the Web app should reuse a valid delegated token without triggering unnecessary remediation paths.
    /// </summary>
    [Fact]
    public async Task GetAccessTokenAsync_ShouldReturnAccessToken_WhenRequiredScopesAreAlreadyGranted()
    {
        var options = Options.Create(new PlatformAuthenticationOptions());
        var tokenFactory = new TestAuthenticationTokenFactory(options);
        var (_, properties) = tokenFactory.Create("local-operator", [PlatformAuthenticationDefaults.Scopes.Operator]);
        var accessToken = properties.GetTokenValue("access_token");
        var httpContext = CreateHttpContext(accessToken);
        var handler = new RecordingHttpMessageHandler();
        var auditClient = CreateAuditClient(handler, httpContext);
        var provider = new PlatformAccessTokenProvider(
            new HttpContextAccessor { HttpContext = httpContext },
            auditClient,
            NullLogger<PlatformAccessTokenProvider>.Instance);

        var token = await provider.GetAccessTokenAsync([PlatformAuthenticationDefaults.Scopes.Operator], CancellationToken.None);

        Assert.Equal(accessToken, token);
        Assert.Equal(0, handler.CallCount);
    }

    /// <summary>
    /// Trace: NF2, NF4, SR4, IR2.
    /// Verifies: the access-token provider records a token-acquisition failure and raises a scope challenge when the current session lacks a required delegated scope.
    /// Expected: the thrown exception lists the missing scope and the auth-audit client posts the token-acquisition failure event.
    /// Why: delegated-scope recovery must stay observable and fail closed before the Web app calls the protected API.
    /// </summary>
    [Fact]
    public async Task GetAccessTokenAsync_ShouldRecordAuditAndThrowScopeChallenge_WhenRequiredScopeIsMissing()
    {
        var options = Options.Create(new PlatformAuthenticationOptions());
        var tokenFactory = new TestAuthenticationTokenFactory(options);
        var (_, properties) = tokenFactory.Create("local-viewer", [PlatformAuthenticationDefaults.Scopes.Viewer]);
        var accessToken = properties.GetTokenValue("access_token");
        var httpContext = CreateHttpContext(accessToken, "/configuration");
        var handler = new RecordingHttpMessageHandler();
        var auditClient = CreateAuditClient(handler, httpContext);
        var logger = new RecordingLogger<PlatformAccessTokenProvider>();
        var provider = new PlatformAccessTokenProvider(
            new HttpContextAccessor { HttpContext = httpContext },
            auditClient,
            logger);

        var exception = await Assert.ThrowsAsync<PlatformScopeChallengeRequiredException>(() =>
            provider.GetAccessTokenAsync([PlatformAuthenticationDefaults.Scopes.Administrator], CancellationToken.None));

        Assert.Equal([PlatformAuthenticationDefaults.Scopes.Administrator], exception.MissingScopes);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal($"Bearer {accessToken}", handler.LastAuthorizationHeader);
        Assert.EndsWith("/api/platform/auth/audit", handler.LastRequestUri, StringComparison.Ordinal);
        Assert.Contains(PlatformAuthenticationDefaults.AuditEvents.TokenAcquisitionFailed, handler.LastContent, StringComparison.Ordinal);
        Assert.Contains("/configuration", handler.LastContent, StringComparison.Ordinal);
        Assert.Contains(PlatformAuthenticationDefaults.Scopes.Administrator, handler.LastContent, StringComparison.Ordinal);
        Assert.Contains(PlatformAuthenticationDefaults.Scopes.Administrator, logger.JoinedMessages, StringComparison.Ordinal);
        Assert.DoesNotContain(accessToken, logger.JoinedMessages, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: NF2, NF4, SR4, IR2.
    /// Verifies: the access-token provider fails closed when the delegated token is already expired even if the operator session still carries token material.
    /// Expected: requesting an operator scope throws a scope-challenge exception, records a token-acquisition failure event, and logs a warning without echoing the raw token.
    /// Why: session-expiry boundaries must remain deterministic and secret-safe beyond simple cookie-loss scenarios.
    /// </summary>
    [Fact]
    public async Task GetAccessTokenAsync_ShouldThrowScopeChallenge_WhenAccessTokenIsExpired()
    {
        var accessToken = CreateAccessToken(
            "local-operator",
            [PlatformAuthenticationDefaults.Roles.Operator],
            [PlatformAuthenticationDefaults.Scopes.Viewer],
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddMinutes(-10));
        var httpContext = CreateHttpContext(accessToken, "/configuration");
        var handler = new RecordingHttpMessageHandler();
        var logger = new RecordingLogger<PlatformAccessTokenProvider>();
        var provider = new PlatformAccessTokenProvider(
            new HttpContextAccessor { HttpContext = httpContext },
            CreateAuditClient(handler, httpContext),
            logger);

        var exception = await Assert.ThrowsAsync<PlatformScopeChallengeRequiredException>(() =>
            provider.GetAccessTokenAsync([PlatformAuthenticationDefaults.Scopes.Operator], CancellationToken.None));

        Assert.Equal([PlatformAuthenticationDefaults.Scopes.Operator], exception.MissingScopes);
        Assert.Equal(1, handler.CallCount);
        Assert.Contains(PlatformAuthenticationDefaults.Scopes.Operator, logger.JoinedMessages, StringComparison.Ordinal);
        Assert.DoesNotContain(accessToken, logger.JoinedMessages, StringComparison.Ordinal);
    }

    /// <summary>
    /// Trace: NF2, NF4, SR4, IR2.
    /// Verifies: the access-token provider fails closed when the delegated token is not yet valid.
    /// Expected: requesting an administrator scope throws a scope-challenge exception and records one token-acquisition failure event.
    /// Why: future-dated or otherwise invalid session tokens must not be treated as usable delegated access during privileged navigation.
    /// </summary>
    [Fact]
    public async Task GetAccessTokenAsync_ShouldThrowScopeChallenge_WhenAccessTokenIsNotYetValid()
    {
        var accessToken = CreateAccessToken(
            "local-admin",
            [PlatformAuthenticationDefaults.Roles.Administrator],
            [PlatformAuthenticationDefaults.Scopes.Viewer],
            DateTimeOffset.UtcNow.AddHours(1),
            DateTimeOffset.UtcNow.AddMinutes(5));
        var httpContext = CreateHttpContext(accessToken, "/administration/authentication");
        var handler = new RecordingHttpMessageHandler();
        var provider = new PlatformAccessTokenProvider(
            new HttpContextAccessor { HttpContext = httpContext },
            CreateAuditClient(handler, httpContext),
            NullLogger<PlatformAccessTokenProvider>.Instance);

        var exception = await Assert.ThrowsAsync<PlatformScopeChallengeRequiredException>(() =>
            provider.GetAccessTokenAsync([PlatformAuthenticationDefaults.Scopes.Administrator], CancellationToken.None));

        Assert.Equal([PlatformAuthenticationDefaults.Scopes.Administrator], exception.MissingScopes);
        Assert.Equal(1, handler.CallCount);
        Assert.Contains(PlatformAuthenticationDefaults.AuditEvents.TokenAcquisitionFailed, handler.LastContent, StringComparison.Ordinal);
        Assert.Contains("/administration/authentication", handler.LastContent, StringComparison.Ordinal);
    }

    private static PlatformAuthAuditClient CreateAuditClient(RecordingHttpMessageHandler handler, HttpContext httpContext) =>
        new(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://localhost")
            },
            new HttpContextAccessor { HttpContext = httpContext },
            NullLogger<PlatformAuthAuditClient>.Instance);

    private static DefaultHttpContext CreateHttpContext(string? accessToken, string path = "/")
    {
        var authenticationProperties = new AuthenticationProperties();
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            authenticationProperties.StoreTokens(
            [
                new AuthenticationToken { Name = "access_token", Value = accessToken }
            ]);
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: PlatformAuthenticationDefaults.Schemes.Cookie));
        var ticket = new AuthenticationTicket(principal, authenticationProperties, PlatformAuthenticationDefaults.Schemes.Cookie);
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<IAuthenticationService>(new TestAuthenticationService(AuthenticateResult.Success(ticket)))
                .BuildServiceProvider()
        };

        context.Request.Path = path;
        return context;
    }

    private static string CreateAccessToken(
        string userName,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> scopes,
        DateTimeOffset expiresUtc,
        DateTimeOffset? notBeforeUtc = null)
    {
        var options = new PlatformAuthenticationOptions();
        var claims = new List<Claim>
        {
            new(PlatformAuthenticationDefaults.Claims.Name, userName),
            new(PlatformAuthenticationDefaults.Claims.PreferredUserName, userName),
            new(ClaimTypes.NameIdentifier, userName),
            new(PlatformAuthenticationDefaults.Claims.Scope, string.Join(' ', scopes))
        };

        claims.AddRange(roles.Select(role => new Claim(PlatformAuthenticationDefaults.Claims.Role, role)));

        var token = new JwtSecurityToken(
            issuer: options.Test.Issuer,
            audience: options.Test.Audience,
            claims: claims,
            notBefore: (notBeforeUtc ?? DateTimeOffset.UtcNow.AddMinutes(-1)).UtcDateTime,
            expires: expiresUtc.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Test.SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly List<string> messages = [];

        public string JoinedMessages => string.Join(Environment.NewLine, messages);

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
