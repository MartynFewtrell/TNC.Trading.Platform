namespace TNC.Trading.Platform.Web.Authentication;

internal sealed class PlatformScopeChallengeRequiredException(IReadOnlyCollection<string> missingScopes)
    : InvalidOperationException($"The current operator session is missing the required delegated scopes: {string.Join(", ", missingScopes)}.")
{
    public IReadOnlyCollection<string> MissingScopes { get; } = missingScopes;
}
