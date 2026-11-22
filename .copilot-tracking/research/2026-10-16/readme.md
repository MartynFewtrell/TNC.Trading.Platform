<!-- markdownlint-disable-file -->
---
title: Email to IG Web API Support
description: Draft request for clarification of category-instrument pagination and EPIC overlap.
date: 2026-10-16
---

## Email draft

To: webapisupport@ig.com

Subject: Demo API: category instrument pagination, short pages and duplicate EPICs

Hello IG Web API Support,

We are integrating the category-instruments resource with our application and
would appreciate clarification of its pagination and completeness semantics.
Our Demo environment collection attempts on 16 October 2026 produced short
pages for COMMODITIES and a repeated EPIC across pages for CRYPTOCURRENCY.

We authenticate using POST `/session` version 2, then use the returned CST
and X-SECURITY-TOKEN with our API key for GET
`/categories/{categoryId}/instruments` version 1.

### Commodities observations

Our application recorded the following responses:

| Request | Returned page metadata | Instrument count |
| --- | --- | --- |
| `GET https://demo-api.ig.com/gateway/deal/categories/COMMODITIES/instruments?pageNumber=0&pageSize=25` | Page number 0, page size 25, total results 290 | 12 |
| `GET https://demo-api.ig.com/gateway/deal/categories/COMMODITIES/instruments?pageNumber=0&pageSize=12` | Page number 0, page size 12, total results 290 | 5 |

The second request was our application's attempt to restart pagination using
the number of instruments observed in the first response. It did not produce
a full page at the smaller size.

These counts come from our deserialized instrument arrays and saved diagnostic
metadata. We have not yet independently captured and compared the complete raw
JSON arrays for these requests.

### Cryptocurrency observations

On separate collection attempts, our application detected that the first
instrument on zero-based page 1 had the same EPIC as an instrument already
returned on page 0. Our current validator rejects this overlap before saving
a new complete category listing.

We have not yet established whether the overlap reflects changing results
between requests, expected resource behaviour, or another cause.

### Questions

1. Is `pageSize` an upper limit, with short intermediate pages permitted even
   when `totalResults` indicates additional results?
2. What exactly does `totalResults` count: instruments available to the
   authenticated account, distinct EPICs, or a larger candidate set before
   account, product or environment filtering?
3. If filtering applies, does it occur before or after page boundaries are
   calculated? Can that explain fewer returned instruments when metadata
   continues to report the requested page size?
4. What does `totalPages` represent, and what is the supported termination rule
   for obtaining all available instruments? Should clients visit every
   advertised page even when an intermediate page is short or empty?
5. Can an EPIC legitimately occur on more than one page of a category result?
   Are ordering and membership stable across requests, or is there a supported
   way to obtain a consistent snapshot?
6. Are there known Demo-specific differences or issues affecting COMMODITIES
   or CRYPTOCURRENCY category pagination?
7. Is this category resource the supported way to enumerate category
   membership? If another resource is recommended, could you confirm its
   endpoint, version, paging rules and coverage limitations?

We consulted the category-instruments reference:
https://labs.ig.com/reference/categories-category-id-instruments.html

It documents zero-based paging and permitted page sizes, but we could not find
definitions for the observed total fields or explicit guidance on short pages
and cross-page overlap.

Our aim is to discover available instruments without incorrectly declaring a
partial result complete or removing previously discovered instruments. We are
not assuming that each page must be full if that is not part of the contract.

Please let us know what evidence would help you investigate. We can arrange a
bounded Demo reproduction and provide sanitized request parameters, UTC
timestamps, response metadata, instrument counts, overlap details and
X-REQUEST-ID values where available. We will exclude API keys, passwords and
session tokens.

Thank you for your help.

Kind regards,
Martyn

## Before sending

* Confirm the observations and account/environment details are still accurate.
* If available, add exact UTC timestamps and X-REQUEST-ID values from a
  controlled reproduction. Do not infer timestamps or request IDs from the UI.
* Only attach complete sanitized JSON or overlap examples after capturing and
  verifying them; this draft does not claim those attachments already exist.
* Do not attach credentials, API keys, CST, X-SECURITY-TOKEN, authorization
  headers or unrestricted logs.
