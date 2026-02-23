# Copilot Instructions

## Project Guidelines
- User is a .NET developer on the Microsoft stack: prefers C# with latest .NET, Minimal APIs, Blazor front ends, SQL Server, Aspire for local desktop/distributed development, Azure Container Apps deployments, scalable service-based architecture with messaging.
- When choosing a .NET version (if `global.json` does not define it), check Microsoft Learn to establish the latest .NET LTS version and target it by default. Ensure that best-practice guidance is grounded in Microsoft Learn for up-to-date details. Periodically validate instruction files and best-practice rules against Microsoft Learn.
- For .NET Aspire, use `https://aspire.dev/` as the primary reference site; include it in documentation and rules to ensure guidance can be re-researched as Aspire evolves. Additionally, validate Aspire-related instructions against aspire.dev as the primary source.
- For local authentication, use Keycloak in a container orchestrated by Aspire for local development; use Microsoft Entra ID in Azure. Ensure authentication is compatible with OIDC, OAuth 2.0, and SAML 2.0.

## Testing Guidelines
- Use the functional test naming convention: `<001>_<FR1>_point_of_test` where `001` is the work package number (from the subfolder) and `FR1/FR2/...` comes from the requirements document.