# Testing strategy

## Purpose
Describe the testing approach present in the repository and how to validate behavior.

## Key points (TL;DR)
- No automated test projects were detected in this repo.
- Validation is primarily manual via running the WPF app against IG demo/live environments.

## Details
## Automated tests
- No `test/` or `tests/` projects were found and no xUnit/NUnit/MSTest projects are present in the solution files examined.

## Manual verification (suggested)
1. Configure credentials in `SampleWPFTrader/App.config`.
2. Run `SampleWPFTrader`.
3. Verify Home tab login triggers and populates Accounts.
4. Verify streaming account updates change values over time.
5. Navigate to Positions/Orders/Watchlists/Browse tabs and confirm REST load + streaming updates.

## Regression checklist (integration-focused)
- REST header behavior: verify `VERSION` header is set for each call.
- Session token behavior: verify CST/XST captured from login responses.
- Streaming connection: verify Lightstreamer connection uses CST/XST password format.

## Evidence (repo references)
- `IGWebApiDotNetSample.sln` (lines 6–9): contains only `SampleWPFTrader` and `IGWebApiClient`.
- `README.md` (lines 13–27): describes manual build/run steps.
- `SampleWPFTrader/ViewModel/ApplicationViewModel.cs` (lines 229–326): login is triggered from UI selection; no test harness exists.

## Open questions / assumptions (if any)
- Assumption: Manual testing against IG demo is sufficient to validate this sample.
  - Missing evidence: No expected-output baselines or mocked API responses.
  - Risk/impact: Reimplementation correctness may be hard to prove without adding contract tests and recorded fixtures.