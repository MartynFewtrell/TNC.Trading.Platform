# Configuration

## Purpose

List application settings, their consumers, and safe handling requirements.

## Key points (TL;DR)

* `SampleWPFTrader/App.config` defines the `IgWebApiConnection` section.
* Required keys read at login are `environment`, `username`, `password`, and
  `apikey`.
* Supported environment values in the source are `demo` and `live`.
* Do not commit real credentials or copy the configured API key value.
* Other config entries include legacy binding redirects and an unused EF section.

## Details

### IG connection section

```xml
<IgWebApiConnection>
  <add key="environment" value="demo" />
  <add key="username" value="YOUR_IG_IDENTIFIER" />
  <add key="password" value="SUPPLY_SECURELY" />
  <add key="apikey" value="YOUR_IG_API_KEY" />
</IgWebApiConnection>
```

The sample's configuration reader uses `ConfigurationManager.GetSection` and
casts it to `NameValueCollection`. `IgRestService` maps `live` to
`https://api.ig.com`; any other value is formatted as a prefixed host, so
validate the environment explicitly in a new implementation.

### Runtime and dependency settings

The WPF config targets .NET Framework 4.6.1 and contains redirects for older
HTTP and validation assemblies. It declares an Entity Framework section and
LocalDB factory, but no matching EF dependency or application usage was found.
The client has a separate `App.config` with a Validation binding redirect.

### Secret handling

The checked-in configuration includes sample credentials and a non-placeholder
API-key value. This guide intentionally does not repeat that value. Replace
sample values locally, avoid checking credentials into source control, and use
the target application's secret-management mechanism.

## Evidence (repo references)

* `SampleWPFTrader/App.config:4-6,44-50`: section declaration and connection keys.
* `SampleWPFTrader/ViewModel/ApplicationViewModel.cs:229-250`: reads and
  validates the four login settings.
* `SampleWPFTrader/ViewModel/ViewModelBase.cs:20-29`: reads environment to
  instantiate the clients.
* `IGWebApiClient/IGRestService.cs:28-37`: host selection.
* `SampleWPFTrader/SampleWPFTrader.csproj:12-13`: target framework.

## Open questions / assumptions (if any)

* Assumption: `live` and `demo` are the only intended settings.
  * Missing evidence: No schema or validation for the environment setting.
  * Risk/impact: Other values silently construct an invalid host.
