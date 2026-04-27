using Xunit;

namespace TNC.Trading.Platform.Web.E2ETests.Authentication;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AuthenticationE2ETestCollection
{
    public const string Name = "Authentication E2E";
}
