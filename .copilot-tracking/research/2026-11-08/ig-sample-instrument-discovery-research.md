---
description: 'Research into IG sample instrument discovery and recommendations for the platform collector.'
ms.date: 2026-11-08
---

# IG sample instrument discovery research

The IG sample demonstrates hierarchical market discovery, not a complete
instrument download service. We should test that discovery contract against
our applied IG Demo account before replacing the category collector. The
sample is explicitly deprecated, and neither its continued endpoint access
nor equivalence to the category catalogue has been established.

The [IG sample reference suite](ig-sample-reference/index.md) is retained in
this repository. Sample source links below refer to upstream revision
`d19c4b0da435a169f2883627e92570b827ff71a1`, not a connected local workspace.

## Recommendation

Keep the current fail-closed collector and last-good snapshots. Add a bounded,
nonpublishing comparison of the category endpoint and the sample's navigation
endpoint, subject to explicit operator authorization. If navigation is still
supported and covers the required instruments, use it as an alternative
discovery adapter feeding the existing market-detail stage. If not, escalate
the category pagination discrepancy to IG with existing sanitized diagnostics.

Do not spend further iterations changing page sizes without evidence that the
provider's advertised sequence can produce a complete listing. Do not treat
the old sample as proof that navigation is supported today.

## What the IG sample does

| Step | Behavior | Source |
| --- | --- | --- |
| Authenticate | Reuses API key, CST and account security token in a conversation context; sends an explicit API version header and captures replacement response tokens. | [IG transport](https://github.com/IG-Group/ig-webapi-dotnet-sample/blob/d19c4b0da435a169f2883627e92570b827ff71a1/IGWebApiClient/IGRestService.cs#L38) |
| Discover roots | Calls `GET /gateway/deal/marketnavigation`, version 1; populates root nodes after login. | [Client root method](https://github.com/IG-Group/ig-webapi-dotnet-sample/blob/d19c4b0da435a169f2883627e92570b827ff71a1/IGWebApiClient/IGRestApiClient.cs#L135), [root view-model method](https://github.com/IG-Group/ig-webapi-dotnet-sample/blob/d19c4b0da435a169f2883627e92570b827ff71a1/SampleWPFTrader/ViewModel/BrowseViewModel.cs#L278) |
| Browse a node | Calls `GET /gateway/deal/marketnavigation/{nodeId}`, version 1, for the selected provider node ID. | [Client node method](https://github.com/IG-Group/ig-webapi-dotnet-sample/blob/d19c4b0da435a169f2883627e92570b827ff71a1/IGWebApiClient/IGRestApiClient.cs#L146), [browse handler](https://github.com/IG-Group/ig-webapi-dotnet-sample/blob/d19c4b0da435a169f2883627e92570b827ff71a1/SampleWPFTrader/ViewModel/BrowseViewModel.cs#L181) |
| Interpret results | Models child `nodes` and leaf `markets`; there are no page numbers, page sizes or total-count metadata in the response DTO. | [Browse response](https://github.com/IG-Group/ig-webapi-dotnet-sample/blob/d19c4b0da435a169f2883627e92570b827ff71a1/IGWebApiClient/Model/dto/endpoint/browse/BrowseMarketsResponse.cs#L7) |
| Identify markets | Maps `epic` and display name into the UI; does not require unique names. | [Market mapping](https://github.com/IG-Group/ig-webapi-dotnet-sample/blob/d19c4b0da435a169f2883627e92570b827ff71a1/SampleWPFTrader/ViewModel/BrowseViewModel.cs#L259), [market DTO](https://github.com/IG-Group/ig-webapi-dotnet-sample/blob/d19c4b0da435a169f2883627e92570b827ff71a1/IGWebApiClient/Model/dto/endpoint/browse/HierarchyMarket.cs#L7) |
| Subscribe to prices | Deduplicates EPICs and subscribes only markets with streaming prices enabled; unsubscribes when changing the browse selection. | [Subscription selection](https://github.com/IG-Group/ig-webapi-dotnet-sample/blob/d19c4b0da435a169f2883627e92570b827ff71a1/SampleWPFTrader/ViewModel/BrowseViewModel.cs#L237) |
| Expose detail APIs | Provides single-EPIC detail methods and a version-1 multi-EPIC method whose comment specifies a maximum of 50 EPICs. | [Detail client methods](https://github.com/IG-Group/ig-webapi-dotnet-sample/blob/d19c4b0da435a169f2883627e92570b827ff71a1/IGWebApiClient/IGRestApiClient.cs#L343) |

The browse UI advances one selected node at a time. It replaces the visible
node list rather than retaining a queue of unexplored branches. It does not
recursively walk all roots, persist a catalogue, track completion, resume a
failed crawl, or publish a transactional snapshot. Its browse handler processes
`markets` only in the `else` branch when `nodes` is null; an empty non-null
node list would therefore bypass markets. A new collector must not copy that
branching assumption without a validated contract.

The browse flow uses streaming prices, not REST detail hydration. The separate
detail methods are reusable protocol evidence, not evidence that this UI
downloads all instrument details. Streaming availability is a price-access
condition, not an appropriate filter for catalogue membership.

## Current blocker and differences

Our current gateway calls the documented
`GET /categories/{categoryId}/instruments`, version 1, and expects a complete,
zero-based paginated response. It checks total pages, total rows, page sizes,
row counts, field validity and both EPIC and name uniqueness. Its production
limits are 100 pages and 15,000 instruments per category.

Sources: [gateway collection logic](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs#L137),
[request resource](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs#L1121),
[row validation](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketCategoryInstrumentsGateway.cs#L1435).

The implemented runtime now has failure-stage logging, safe provider-denial
classification, initial/restarted page evidence, shared allowance controls,
and a bounded nonpublishing category diagnostic. These are already implemented;
the older October failure research should not be read as the current feature
inventory. The documented commodities incident returned 65 rows for a request
of 150, then 38 for the size-65 restart. This does not establish a stable
provider page cap. Current default requests use 25 rows, five-second minimum
spacing and a 60-second category pause, but those traffic controls do not
establish completeness.

Sources: [current collection and incident evidence](../../../docs/wiki/runtime-behavior.md#L201),
[operator diagnostic controls](../../../docs/wiki/operator-guide.md#L536),
[historical failure analysis](../../../docs/market-category-collection-failures-research-2026-10-03.md).

The working hypothesis is that navigation may expose the required EPICs
without depending on the problematic category pagination contract. The
cheapest disconfirming check is an authorized, bounded root request followed
by a child request in the same applied environment/account context. A denied
or unsupported endpoint, an unavailable required root, or unusable response
shape would disprove its suitability as an immediate replacement. A successful
pair of requests would establish availability only, not catalogue completeness.

Navigation cannot fix an account/product denial merely by using another URL,
nor can it fix a downstream SQL publication error. Inspect the saved failure
stage, HTTP status and allow-listed provider code for each category before
assuming commodities and cryptocurrency have the same cause.

## Changes to consider

### First validate the provider contract

1. Keep existing saved failures and snapshots. Review the latest persisted
   diagnostic outcome for each affected category; do not infer today's failure
   from the October screenshot or a dashboard started after the attempt.
2. With explicit operational authorization, reuse the existing admission,
   environment resolution, protected credentials, request budget and pacing
   controls for a nonpublishing navigation probe. Cap actual HTTP requests,
   including authentication and retries. Do not use credentials from the sample
   configuration or run the legacy WPF application as a workaround.
3. Establish whether version-1 navigation is available for this Demo account,
   what roots it returns, and how child/leaf responses behave. Keep node IDs
   opaque: a navigation node ID is not our category code. Define an explicit
   environment/account-scoped mapping for commodities, crypto and other
   selected categories rather than guessing IDs from names.
4. If permitted by the request budget, traverse one small mapped subtree and
   compare its distinct EPIC set with a known successful category snapshot or
   an IG-confirmed reference set. Record missing/extra membership internally;
   expose sanitized aggregate counts only. Confirm coverage expectations with
   IG where neither endpoint supplies authoritative totals.

### If navigation is supported, implement complete discovery

Use an Infrastructure-owned navigation adapter rather than importing the
legacy client. Keep scheduling, interest selection and completion policy in
Application, and reuse existing persistence and detail orchestration where
their contracts remain valid.

* Traverse every mapped root using a queue or stack; process returned markets
  and enqueue returned children independently, with validated empty responses.
* Track visited node IDs, bound node count, depth, response size, instrument
  count, elapsed time and actual requests, and stop safely on cycles or limits.
  A repeated shared node may be skipped after successful traversal, but it must
  still retain all relevant category membership.
* Deduplicate discovery by EPIC while retaining many-to-many category/node
  provenance. Duplicate names are legitimate display data unless IG provides
  a uniqueness guarantee. Conflicting metadata for the same EPIC needs an
  explicit reconciliation rule, not an arbitrary last-write-wins policy.
* Stage an entire run before publication. A failed required node, exhausted
  budget, cancelled run or unresolved mapping must not replace last-good data.
  If resumable traversal is introduced, pin context and source mapping revision
  and define freshness/revalidation rules before combining observations.
* Define traversal coverage separately from provider catalogue completeness.
  Exhausting a queue proves that all returned branches were visited, not that
  IG returned every instrument available to the account. Do not label it a
  complete category without an agreed provider coverage contract.
* Change collection metadata and storage validation to represent discovery
  strategy, roots, visited-node coverage and source version. The current writer
  stores page counts and provider totals; do not fabricate these to fit
  navigation into a paginated result.

Source: [current snapshot persistence](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketCategoryInstrumentSnapshotStore.cs#L140).

The platform already batches explicit EPICs for detail retrieval through
`markets?epics=...&filter=ALL`. Feed validated discovered EPICs into that stage,
preserving per-target outcomes and request controls. Do not replace batched
hydration with one detail call per EPIC, and do not use unfiltered `/markets`
or keyword search as an assumed exhaustive catalogue endpoint.

Source: [existing detail batching](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Integrations/Ig/IgMarketDetailsGateway.cs#L1420).

### If categories remain the supported discovery API

Use the existing category diagnostic to establish whether advertised pages
yield the advertised distinct EPIC count. If the discrepancy persists, ask IG
to clarify whether pagination describes rows before account/product filtering,
whether short intermediate pages are valid, and how completeness can be
verified. These are questions, not established explanations for the incident.

Change completeness rules only against that confirmed contract and a sanitized
regression fixture. Review name uniqueness across gateway, writer and database
constraints so different EPICs with the same name can be represented. Review
expiry parsing separately: the sample models expiry as text, while our gateway
accepts a limited set of formats. Preserve provider labels within storage
bounds and derive typed dates separately when the confirmed contract requires
other values. Neither issue is proven to cause the observed short pages.

The writer already checks EPIC uniqueness without checking name uniqueness,
so the gateway's additional name restriction is a specific candidate for a
small regression-tested change. Also align the writer's supported frequency
with the operator settings: it still rejects `EffectiveUpdatesPerDay > 4`,
although settings allow higher values with a warning. This is an independent
publication defect, not a diagnosed explanation for category pagination or
the historical cryptocurrency failure.

Sources: [writer validation](../../../src/TNC.Trading.Platform.Infrastructure/Infrastructure/Persistence/EntityFramework/EfMarketCategoryInstrumentSnapshotStore.cs#L296),
[supported operator frequency](../../../docs/wiki/operator-guide.md#L477).

## Verification criteria

* Discovery fixtures cover branching, leaf responses, empty collections,
  duplicate names, repeated EPICs, shared nodes, cycles, malformed responses,
  account denial, budget exhaustion and a failed late node.
* Category fixtures preserve the observed short-page/restart discrepancy as a
  rejected production collection until a different contract is confirmed.
* Persistence tests verify no partial replacement, context isolation,
  category membership, lease fencing and strategy-specific provenance.
* Detail tests verify bounded EPIC batches and that non-streaming instruments
  remain discoverable. Detail coverage is reported separately from discovery.
* A subsequently authorized Demo comparison establishes the required coverage
  and successful publication before navigation becomes the default. This
  research did not run either discovery endpoint against a broker account.

## Support status and confidence

The sample [README deprecation notice](https://github.com/IG-Group/ig-webapi-dotnet-sample/blob/d19c4b0da435a169f2883627e92570b827ff71a1/README.md#L1)
dates deprecation to August 2024. The transport also blocks on task results,
creates clients per request and suppresses deserialization errors. Those
patterns are unsuitable for a production complete-snapshot collector.

The [current IG API index](https://labs.ig.com/rest-trading-api-reference.html)
lists categories, category instruments, markets and search, but not navigation.
The [IG FAQ](https://labs.ig.com/faq.html) still describes the navigation tree
and states default non-trading limits of 60 requests per application per minute
and 30 per account per minute. The limits are shared constraints, not a new
allowance for each collector. Navigation reference fetches returned HTTP 500
at `https://labs.ig.com/reference/marketnavigation.html` and HTTP 404 at
`https://labs.ig.com/marketnavigation.html`; child-reference URLs tried were
also unavailable. Documentation failures do not prove API removal.

The [category instruments reference](https://labs.ig.com/reference/categories-category-id-instruments.html)
still documents version 1, zero-based page numbers, default size 150 and a
maximum of 1,000. The [market details reference](https://labs.ig.com/reference/markets-epic.html)
lists newer versions than those used by the sample; do not select versions
solely because the legacy client uses them.

Confidence is high in the sample's implemented discovery pattern and the
platform contract differences. Confidence is limited in navigation's current
availability, coverage equivalence and the latest provider failure cause.
The next decision should be driven by the bounded comparison and current
saved diagnostics, not by adopting a deprecated dependency.