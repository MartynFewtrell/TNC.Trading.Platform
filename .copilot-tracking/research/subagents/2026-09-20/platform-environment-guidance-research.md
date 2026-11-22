---
title: Platform Environment Guidance Research
description: Delegated research on runtime environment derivation and configuration guidance for local Aspire and Azure deployments
author: GitHub Copilot
ms.date: 2026-09-20
ms.topic: concept
keywords:
  - asp.net core
  - .net aspire
  - configuration
  - azure
---

## Research Scope

* Derive an immutable platform environment at runtime using `IHostEnvironment`,
  `ASPNETCORE_ENVIRONMENT`, and `DOTNET_ENVIRONMENT`
* Establish configuration provider precedence and deployment-specific configuration practices
* Identify options validation and secure connection credential guidance
* Relate guidance to Desktop/local Aspire resources and planned Azure Development, Test,
  and Live deployment environments
* Identify Azure configuration services and deployment guidance where needed

## Key Discoveries

### Host Environment Is Startup-Fixed

The ASP.NET Core environment is an application-host concern. Microsoft states that the last
environment setting read determines the environment and that it cannot change while the
application is running. `IHostEnvironment.EnvironmentName` is the supported runtime source.
For `WebApplication`, `DOTNET_ENVIRONMENT` takes precedence over
`ASPNETCORE_ENVIRONMENT`; `Production` is the default when neither is set.

Use the host environment only to select host and deployment behavior, including
`appsettings.{Environment}.json` and Development-only secret loading. Do not derive a
trading or platform safety classification from a mutable configuration record or infer it
from the broker environment.

Source excerpt: "The app's environment can't be changed while the app is running."
[ASP.NET Core runtime environments](https://learn.microsoft.com/aspnet/core/fundamentals/environments?view=aspnetcore-10.0).

### Platform Environment Should Be Explicit and Immutable

The repository currently defines the domain platform environment as `Test` or `Live` in
`docs/systems-analysis.md`; `Test` blocks an `IG` Live broker selection. This is a separate
concept from ASP.NET Core's hosting environment, which is conventionally `Development`,
`Staging`, or `Production`.

Derive an immutable `PlatformEnvironment` at startup from a dedicated deployment setting,
for example `Platform:Environment`, then bind it to an allow-listed, validated options type.
Read the value once during composition, expose a narrow service/value object to application
code, and do not enable runtime configuration refresh for that setting. Fail startup when it
is absent or outside the deployment's permitted values.

This separates deployment topology from domain safety policy. A deployment stage called
`Development`, `Test`, or `Live` need not have the same vocabulary as the platform's
`Test`/`Live` behavior, but the deployment pipeline must map each stage deliberately.

### Configuration Precedence Requires Deliberate Ordering

.NET configuration providers are additive and the last provider supplying a key wins. With
the default host configuration, environment variables override `appsettings.json`,
`appsettings.{Environment}.json`, and Secret Manager; command-line arguments override those
sources again. Hierarchical environment variable keys use `__` rather than `:` for portability.

Use the default provider order for ordinary settings unless there is a documented need to
extend it. Register Azure App Configuration before any provider intended to override it,
and protect `Platform:Environment` from remote refresh or operator edits. Do not let an
AppHost-injected or command-line setting silently change the platform safety class outside
the deployment contract.

Source excerpt: "When keys are found by multiple providers, the last provider to read the
key overrides previous providers."
[Configuration providers](https://learn.microsoft.com/dotnet/core/extensions/configuration-providers).

### Validate Startup-Fixed Configuration at Startup

Bind related configuration through the options pattern. Microsoft documents
`ValidateDataAnnotations`, custom `IValidateOptions<TOptions>` for cross-field rules, and
`ValidateOnStart` to make invalid configuration fail during application startup rather than
on first access. Use an options type for the deployment-derived platform environment and a
custom validator when rules involve the host environment, broker selection, or deployment
stage.

Validate startup-fixed platform safety values with `ValidateOnStart`. Use
`IOptionsMonitor<T>` only for settings that are intentionally dynamic. A monitored,
refreshable platform environment would contradict the runtime immutability requirement.

Source excerpt: "The validation occurs at runtime, but you can configure it to occur at
startup by instead chaining a call to `ValidateOnStart`."
[Options pattern](https://learn.microsoft.com/dotnet/core/extensions/options).

### Credentials and Connection Strings Must Remain Secret-Safe

For local development, use Secret Manager for developer-specific secrets. It is excluded
from source control but is not encrypted and is not a production secret store. Environment
variables can override settings but are commonly plain text, so they are unsuitable as the
primary protection boundary for sensitive connection credentials.

For Azure Test and Live deployments, store secrets in separate Azure Key Vaults and access
them using managed identity. Microsoft explicitly recommends separate vaults for different
apps and development/production environments. Azure SQL should use managed identity where
available rather than a password connection string.

Source excerpts:

* "Production secrets should be accessed through a controlled means like Azure Key Vault."
  [Safe storage of app secrets in development](https://learn.microsoft.com/aspnet/core/security/app-secrets?view=aspnetcore-10.0)
* "Managed identities don't require storing a certificate in the app or in the Development
  environment."
  [Azure Key Vault configuration provider](https://learn.microsoft.com/aspnet/core/security/key-vault-configuration?view=aspnetcore-10.0)

### Azure App Configuration Fits Non-Secret, Deployment-Specific Settings

Azure App Configuration is appropriate for centrally managed non-secret settings and feature
flags. Microsoft recommends Microsoft Entra ID with managed identity over a connection
string. It supports an unlabeled baseline plus an environment-specific label, where the later
environment label overrides matching baseline keys. Secrets should remain in Key Vault, with
App Configuration holding Key Vault references when a centralized configuration catalog is
needed.

For a startup-fixed platform environment, prefer either a deployment setting or a pinned,
validated App Configuration value that is not configured for refresh. Dynamic refresh is
appropriate for explicitly dynamic operational settings, not safety classification.

Source excerpts:

* "A more secure and recommended approach is to use Microsoft Entra ID authentication."
* "Use a snapshot for your foundational configuration and dynamic configuration for emergency
  configuration overrides and feature flags."
  [Azure App Configuration best practices](https://learn.microsoft.com/azure/azure-app-configuration/howto-best-practices)

## Local Aspire Mapping

The current local AppHost composes `api` and the operator `web` UI with SQL Server
(`platformdb`), Keycloak, and Mailpit. SQL Server and Keycloak use persistent container
lifetimes by default. The AppHost passes connection details and endpoints through resource
references and passes local-only wiring through environment variables.

Aspire documents that the AppHost models resource dependencies and injects connection strings,
endpoints, and other configuration into consuming services. This supports a local Desktop or
operator-UI workflow without hard-coded connection values. The AppHost remains a development
orchestration boundary; it should pass the intended deployment classification explicitly,
rather than attempting to discover it from a local resource name or connection string.

Source excerpts:

* "WithReference(postgres) injects connection details into the API configuration."
* "Use separate configurations for development, testing, and production."
  [What is the AppHost?](https://aspire.dev/get-started/app-host/)

Relevant local implementation evidence:

* `src/TNC.Trading.Platform.AppHost/AppHostInfrastructureRegistration.cs` defines SQL Server,
  Mailpit, and Keycloak.
* `src/TNC.Trading.Platform.AppHost/AppHostProjectRegistration.cs` registers `api` and `web`
  and supplies resource references.
* `src/TNC.Trading.Platform.AppHost/AppHostEnvironmentWiring.cs` applies local endpoint,
  authentication, notification, and optional Azure Communication Services values.

## Azure Development, Test, and Live Mapping

The requested Azure stages should be treated as distinct deployment environments with explicit
bindings, not as an implicit rewrite of the domain platform environment:

| Azure deployment stage | ASP.NET Core host environment | Platform environment | Configuration and credentials |
|------------------------|-------------------------------|----------------------|-------------------------------|
| Development            | `Development` or a documented custom host name | Explicitly choose and validate `Test` unless a separate local-only value is approved | Local Secret Manager for developer execution; a development App Configuration store and Key Vault if deployed to Azure |
| Test                   | `Staging` or a documented custom host name | `Test` | Test App Configuration and Test Key Vault; managed identity; prevent IG Live use |
| Live                   | `Production` | `Live` | Live App Configuration and Live Key Vault; managed identity; independent access control and audit |

The exact host-environment names are a policy choice. The essential contract is that every
deployment template, AppHost deployment parameter, and service instance supplies the same
allowed `Platform:Environment` value for its stage, then startup validates the mapping.

Aspire deployment guidance states that `aspire publish` emits parameterized artifacts with
secret placeholders and that `aspire deploy` resolves parameters and applies target-specific
configuration. Keep platform-environment selection as a non-secret parameter and inject
credentials from the target's managed identity and vault rather than committing values.

Source excerpt: "Secrets remain as parameterized placeholders."
[Aspire deployment overview](https://aspire.dev/deployment/).

## Recommended Approaches

### Preferred: Dedicated Deployment Parameter

Set one non-secret `Platform__Environment` value per deployment stage through the Azure
deployment definition or host application settings. At startup, bind and validate it with
`ValidateOnStart`, construct an immutable platform-environment service, and prohibit refresh.
Use `IHostEnvironment` only to select the correct host-level configuration and providers.

This is the clearest approach because the deployment system owns deployment classification,
the application owns domain validation, and neither relies on mutable operator configuration.

### Alternative: App Configuration as the Source of Deployment Metadata

Load an environment-specific, non-refreshable `Platform:Environment` key from the stage's
App Configuration store or label, then validate it against the deployment stage. Keep one
store and Key Vault per deployment stage, use managed identity, and bootstrap only with the
non-secret App Configuration endpoint.

This is useful when release operators require centralized configuration as code. It adds a
startup dependency and requires a strict policy that the platform environment cannot refresh.

### Alternative: Single Deployment Manifest With Stage Parameters

Publish once from the AppHost and supply `Platform__Environment`, App Configuration endpoint,
and Key Vault name as stage-specific deployment parameters. Maintain separate Azure resource
instances and role assignments for Development, Test, and Live.

This keeps the AppHost topology portable and uses Aspire's parameterized deployment model.
It requires the team to choose and codify an Azure hosting integration, such as Azure Container
Apps or Azure App Service.

## Questions Requiring Local Confirmation

* Which Azure hosting target is approved for Development, Test, and Live: Azure Container
  Apps, Azure App Service, AKS, or another target?
* Is `Development` intended to be an Azure deployment stage, a local-only host environment,
  or a domain platform-environment value in addition to `Test` and `Live`?
* Which configuration ownership model is intended: deployment parameters only, Azure App
  Configuration plus Key Vault, or SQL-backed operator configuration for dynamic settings
  alongside deployment-fixed values?
* Which resource replaces or hosts the local Keycloak boundary in Azure, given the repository
  standard of Microsoft Entra ID for Azure authentication?

## Blockers

No research blocker remains. Implementation design is blocked only by the unresolved Azure
hosting target and the authoritative mapping between Azure Development/Test/Live stages and
the domain platform environment.
