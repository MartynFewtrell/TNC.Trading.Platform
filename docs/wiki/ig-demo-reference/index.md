# IG Web API .NET Sample Documentation

This reference-only section documents the legacy
[IG Web API .NET demo](https://github.com/MartynFewtrell/ig-webapi-dotnet-sample),
including its .NET Framework 4.5 PCL client and WPF sample. It does not describe
the current TNC Trading Platform implementation.

## Purpose

Provide a source-backed guide for engineers recreating the sample's behavior in
a new application.

## Key points (TL;DR)

* The app is a deprecated WPF desktop sample with a separate IG API client.
* REST snapshots and Lightstreamer updates jointly supply its read-only UI.
* The repository does not establish current compatibility with IG's APIs.

## Summary

This repository contains a Windows Presentation Foundation (WPF) desktop sample
and a reusable-looking .NET client library for the IG REST and Lightstreamer
streaming APIs. The desktop app authenticates to an IG demo or live environment,
shows account data, browses markets, and displays watchlists, open positions,
working orders, live prices, and one-minute candle updates.

The sample is explicitly marked deprecated in the repository README. Its
frameworks and libraries are legacy. Treat it as behavioral reference material,
not as a current production template.

## Doc set map

* [Application overview](./application-overview.md)
* [User workflows](./user-workflows.md)
* [Architecture](./architecture.md)
* [Components and services](./components-and-services.md)
* [Data model](./data-model.md)
* [API reference](./api-reference.md)
* [Authentication and authorization](./auth.md)
* [Integrations](./integrations.md)
* [Configuration](./configuration.md)
* [Local development](./local-development.md)
* [Deployment and infrastructure](./deployment.md)
* [Operations and observability](./operations.md)
* [Testing strategy](./testing.md)
* [Assumptions and gaps](./assumptions-and-gaps.md)
* [Appendix: code map](./appendix-code-map.md)

## Details

## Primary entry points

* `IGWebApiDotNetSample.sln` builds the WPF app and client library together.
* `SampleWPFTrader/ViewModel/ApplicationViewModel.cs` implements login,
  account subscriptions, trade updates, and logout.
* `IGWebApiClient/IGRestApiClient.cs` maps C# methods to IG REST endpoints.
* `IGWebApiClient/IGStreamingApi.cs` configures Lightstreamer connections,
  item subscriptions, and streaming data parsing.
* `SampleWPFTrader/MainWindow.xaml` defines the UI and its data grids.

## Evidence (repo references)

* `README.md`: identifies both projects and says the repository is deprecated.
* `IGWebApiDotNetSample.sln`: includes the WPF trader and client library projects.
* `SampleWPFTrader/SampleWPFTrader.csproj`: WPF executable and client project
  reference.

## Open questions / assumptions (if any)

* Assumption: The goal is to reproduce the sample's implemented read-oriented
  user experience, not every endpoint exposed by the client library.
  * Missing evidence: The repository contains no separate product requirements.
  * Risk/impact: Implementing trading or account-management commands would expand
    beyond what the WPF sample exposes.
