# IG Web API .NET Sample — Documentation Suite

## Purpose
Provide a reverse-engineered, evidence-backed documentation set for this repository, sufficient to reimplement an equivalent application (WPF UI + REST/streaming client) without referencing the original code.

## Reference scope and provenance

This suite was moved from the IG sample workspace into TNC.Trading.Platform
on 2026-11-08. It describes the deprecated IG sample, not the platform's
implemented architecture or supported operational procedures. The remaining
pages preserve the original research findings.

Source file paths and line numbers in this suite refer to the original sample
repository root at revision `d19c4b0da435a169f2883627e92570b827ff71a1`.
They do not require the IG-Group folder to be connected locally. Consult the
[pinned upstream source tree](https://github.com/IG-Group/ig-webapi-dotnet-sample/tree/d19c4b0da435a169f2883627e92570b827ff71a1)
for source inspection and the
[sample deprecation notice](https://github.com/IG-Group/ig-webapi-dotnet-sample/blob/d19c4b0da435a169f2883627e92570b827ff71a1/README.md#L1)
for support status.

For platform decisions, start with the
[instrument discovery research and recommendations](../ig-sample-instrument-discovery-research-2026-11-08.md).
The [platform implementation wiki](../wiki/README.md) remains the source of
truth for implemented behavior. Disconnecting the sample workspace does not
remove this retained research.

## Key points (TL;DR)
- This repo is a deprecated .NET Framework WPF sample trading app that talks to IG’s REST API and Lightstreamer streaming API.
- Two projects: `SampleWPFTrader` (WPF UI) and `IGWebApiClient` (REST + streaming client + DTOs).
- Configuration is via `SampleWPFTrader/App.config` (`IgWebApiConnection` section: `environment`, `username`, `password`, `apikey`).
- REST calls are made to `https://{env}api.ig.com/gateway/deal/...` with headers `X-IG-API-KEY`, `CST`, `X-SECURITY-TOKEN`, and `VERSION`.
- Streaming uses Lightstreamer with item namespaces like `ACCOUNT:<id>`, `TRADE:<id>`, `L1:<epic>`, and `CHART:<epic>:<scale>`.

## Details
### Doc set map
- [Application overview](./application-overview.md)
- [User workflows](./user-workflows.md)
- [Architecture](./architecture.md)
- [Components & services](./components-and-services.md)
- [Data model](./data-model.md)
- [APIs](./api-reference.md)
- [Authentication & authorization](./auth.md)
- [Integrations](./integrations.md)
- [Configuration](./configuration.md)
- [Local development](./local-development.md)
- [Deployment & infrastructure](./deployment.md)
- [Operations & observability](./operations.md)
- [Testing strategy](./testing.md)
- [Assumptions & gaps](./assumptions-and-gaps.md)
- [Appendix: code map](./appendix-code-map.md)

## Evidence (repo references)
- `README.md` (lines 7–35): states the repo is a WPF sample using IG REST + Streaming APIs; identifies the two projects.
- `IGWebApiDotNetSample.sln` (lines 6–9): solution projects `SampleWPFTrader` and `IGWebApiClient`.
- `SampleWPFTrader/App.config` (lines 44–50): shows required config keys.

## Open questions / assumptions (if any)
- Assumption: The IG REST endpoints and Lightstreamer field semantics match IG’s public documentation for the corresponding API versions.
  - Missing evidence: The repo does not include external API specs or mocks.
  - Risk/impact: A clean-room reimplementation may differ in edge cases (error handling, optional fields, permissions).