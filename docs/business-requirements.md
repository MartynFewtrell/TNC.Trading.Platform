# Business Requirements

This document captures the business context and high-level requirements for the Algorithmic Trading Platform project. It describes what should be built and why, and provides the foundation for systems analysis and work packages under `./docs/00x-work/`.

## 1. Summary

- **Project**: Algorithmic Trading Platform
- **Document**: `./docs/business-requirements.md`
- **Owner**: TNC Trading
- **Date**: 2026-03-05
- **Status**: draft
- **Outputs**:
  - `./docs/systems-analysis.md`
  - Work packages under `./docs/00x-work/` with `requirements.md`, `technical-specification.md`, and numbered plan files under `plans/`

### 1.1 Links

| Document | Path |
| --- | --- |
| Business requirements | `./docs/business-requirements.md` |
| Systems analysis | `./docs/systems-analysis.md` |

## 2. Context

### 2.1 Background

This project exists to create a profit-generating internal platform for TNC Trading by enabling algorithmic trading capabilities.

### 2.2 Current state

There is no existing end-to-end platform suitable for delivering this as a profit-generating internal platform; this is a greenfield initiative.

### 2.3 Desired outcomes

The platform provides measurable business value by achieving positive net profitability each month over a 6-month evaluation window.

## 3. Goals and Success Measures

### 3.1 Goals

- Create a profit-generating internal platform that enables algorithmic trading.
- Demonstrate measurable business value from the offering.

### 3.2 Success measures

| ID | Measure | Target | How it will be assessed | Notes |
| --- | --- | --- | --- | --- |
| SM1 | Net profitability | Positive net profitability each month | Monthly outcome reporting shows net profitability is positive for each month within the 6-month evaluation window. | Evaluation window: 6 months. Net profitability includes realised and unrealised P&L and trading costs (including spread, commissions, and financing where applicable). Source of truth is `IG` account activity/statements; the platform’s activity ledger must reconcile against `IG` (reconciliation tolerance up to £0.01 per trade). |

## 4. Scope

### 4.1 In scope

- Capability to select and operate against the intended `IG` environment (demo or live) with safeguards.
- Capability to authenticate to `IG` and operate continuously within provider constraints.
- Capability to browse available instruments, select one or more instruments to track, and manage the tracked instrument set.
- Capability to obtain timely market data for tracked instruments suitable for automated strategies.
- Capability to define and operate automated trading strategies.
- Capability to place and manage orders required by automated strategies.
- Capability to configure and enforce business-level risk controls.
- Capability to operate and intervene safely (pause, resume, stop) during normal operation and abnormal conditions.
- Capability to provide monitoring, reporting, and an audit trail suitable for personal/internal review.

### 4.2 Out of scope

- The initial release is not intended for external customers.
- Manual/discretionary trading workflows or a manual trading user experience.
- Execute trades in the `IG` live environment (initial release is demo-only for execution).

## 5. Stakeholders and Users

### 5.1 Stakeholders

| Name/Group | Role | Responsibilities | Decision rights | Notes |
| --- | --- | --- | --- | --- |
| TNC Trading | Business owner / sponsor | Approve scope and success criteria; provide direction and prioritization; provide subject-matter input and review; own go/no-go decisions. | Approves project scope and changes to this document; approves go/no-go for releases; approves business-level trading/risk policy decisions; approves prioritization of work packages. |  |

### 5.2 Users

| User group | Description | Goals | Notes |
| --- | --- | --- | --- |
| Project owner (you) | Single individual user who defines, operates, and monitors automated trading activity for this personal project. | Create and run strategies; monitor outcomes; manage operational tasks required to keep the platform running. | Personal/internal use only. |

## 6. Business Requirements

Use `BR1`, `BR2`, ... for business requirements. These should be written at the capability/outcome level and avoid prescribing a technical implementation.

| ID | Requirement (capability/outcome) | Rationale | Priority | Acceptance criteria | Notes/Constraints |
| --- | --- | --- | --- | --- | --- |
| BR1 | Provide safe separation between `IG` demo and live environments to reduce accidental live trading. | The project assumes strategies are proven in demo before reliance; environment confusion is a material operational risk. | Must | The platform has an explicit environment selection; the selected environment is always observable during operation and in logs/reports; switching environments requires an explicit action and results in clear separation of configuration and recorded activity. | `IG` provides distinct base URLs for demo and live environments. |
| BR2 | Enable the platform to authenticate to `IG` and maintain session continuity for sustained operation. | Strategies and monitoring depend on authenticated access to market data and execution services. | Must | The platform can establish an authenticated session; session state is observable; the platform detects expired/invalid sessions and restores a working session without corrupting trading state; authentication events are recorded without exposing secrets. | `IG` authentication may involve different token types for REST and streaming usage. |
| BR3 | Operate within `IG` API usage quotas and rate limits while maintaining safe behavior. | Excessive API usage can cause throttling, degraded operation, or policy breaches. | Must | The platform limits request volume to configured caps; rate limit responses are handled safely (for example by pausing non-critical activity and preventing unsafe trading actions); rate limit events are recorded and visible in operational reporting. | `IG` quotas apply across trading, non-trading, and historical pricing requests. |
| BR4 | Enable users to discover and track tradable instruments for automated strategies. | Strategies need an explicit set of instruments to monitor and trade. | Must | The system can present a list of available instruments; the user can select one or more instruments to track; tracked instruments persist and are visible with current status. | Initial release uses `IG` and supports `FX` and `Indices`. |
| BR5 | Provide resilient real-time market data access for tracked instruments suitable for intraday strategy operation. | Intraday strategies require timely prices and an explicit signal when prices are stale or unavailable. | Must | For tracked instruments, the platform provides current pricing with an observable freshness/availability status; if pricing becomes stale or unavailable beyond a configurable tolerance, automated trading is prevented and the condition is recorded; the platform can restore pricing and resume once safe. | Initial release prefers `IG` streaming where appropriate and respects provider subscription limits. |
| BR6 | Enable users to create, manage, and run algorithmic trading strategies. | Core capability needed to automate trading decisions and actions. | Must | Users can define a strategy, associate it with one or more tracked instruments, activate it, and confirm it is running with observable status; the system can open and close trades automatically according to the strategy’s rules. |  |
| BR7 | Provide business-level risk controls and governance for automated trading. | Reduce likelihood and impact of unacceptable trading behavior. | Must | Risk controls can be configured; trades that breach configured controls are prevented and recorded as rejected. | Controls are configurable by the project owner. |
| BR8 | Provide operational run controls to safely pause, resume, and stop automated trading. | The project owner needs a reliable way to intervene during abnormal conditions or planned maintenance. | Must | The project owner can pause and resume strategy execution; the platform can enter a safe stopped state on critical failures; while paused/stopped, automated trade placement is prevented; all state changes are recorded with timestamps. | Includes manual pause and automated safety stops (for example due to connectivity loss). |
| BR9 | Enable placement and management of trade orders for supported instruments and venues. | The platform must be able to convert trading decisions into executed trades. | Must | For a supported instrument, an order can be submitted, amended, cancelled, and its final state can be confirmed, including opening and closing trades as directed by an active strategy. | Initial release uses `IG` as the single venue/broker and supports `FX` and `Indices`. |
| BR10 | Provide end-to-end trade intent traceability and reconciliation across submission, confirmation, and final outcome. | Automated trading requires the ability to prove what was intended, what was executed, and the resulting state. | Must | For each automated trade decision, the platform records the intent and correlates it to provider acknowledgements/confirmations and the eventual resulting state (for example position opened/closed or order rejected); duplicate or repeated submissions do not cause unintended duplicate trades. | Support personal review and fault recovery after transient failures. |
| BR11 | Provide operational monitoring and reporting for strategy and trading activity. | Users need visibility into performance, issues, and outcomes. | Should | Users can view activity history and a summary of key outcomes for a selected time period. | Reporting is focused on the needs of the project owner for personal/internal use. |
| BR12 | Protect `IG` credentials and platform secrets used for automated trading. | Compromise of trading credentials can lead to financial loss and operational disruption. | Must | Secrets are stored and accessed securely; secrets are not exposed in logs or reports; access to secrets is limited to the minimum required for operation; loss or suspected compromise of secrets can be addressed by rotating credentials without data loss. | Applies to API keys, tokens, and any other sensitive configuration. |
| BR13 | Retain and recover operational and trading records for personal/internal audit and review. | Automated trading requires an audit trail that can survive failures and support investigation. | Should | Trading and operational records are retained for a defined period; records can be exported; records can be recovered after a failure without losing the ability to review prior trading activity. | Retention period and backup approach to be confirmed. |

## 7. Constraints and Policies

### 7.1 Constraints

- This is a part-time personal project.
- The initial release must use `IG` as the venue/broker.

### 7.2 Policies and compliance (if any)

- Must comply with `IG` account terms and acceptable use requirements.
- Maintain an audit trail of trading activity sufficient for personal review.

## 8. Assumptions, Risks, and Dependencies

### 8.1 Assumptions

- An `IG` demo account exists and is available for use.
- Trading strategies will be developed and proven before being relied upon.

### 8.2 Risks

- **R1: Strategy performance risk**: Trading strategies may not perform as expected, resulting in financial loss.
  - **Mitigation**: Develop and prove strategies using the existing `IG` demo account before relying on them, and review outcomes regularly using the project audit trail.
- **R2: Operational risk**: Outages, disconnects, or other operational issues may prevent trading or monitoring.
  - **Mitigation**: Ensure the platform provides basic monitoring and reporting for activity and connectivity, and prefer IG streaming where appropriate to reduce polling.
- **R3: Execution risk**: Orders may not be placed or managed as intended (for example, wrong size or wrong instrument).
  - **Mitigation**: Validate order intent before submission, confirm order results after submission, and maintain an audit trail to support review and correction.

### 8.3 Dependencies

- `IG` APIs (REST and Streaming) remain accessible for demo usage.
- An `IG` API key is available and maintained.
- Stable internet connectivity is available for streaming and order placement.
- `IG` market data access for `FX` and `Indices` is enabled on the account.

## 9. Work Package Outline (optional)

If you already know likely work packages, outline them here. Keep this section business-oriented; technical design belongs in each work package’s `technical-specification.md`.

| Candidate work package | Summary | Related BR IDs | Notes |
| --- | --- | --- | --- |
| To be confirmed | To be confirmed | To be confirmed |  |

## 10. Open Questions

### 10.1 Resolved decisions

- Operating model: strictly intraday (flatten positions end-of-day; no overnight positions).
- Market data freshness tolerance: configurable with a default tolerance of 5 seconds.
- Audit and operational record retention: 90 days.
- Profitability reporting: `IG` account activity/statements are authoritative; the platform ledger reconciles to `IG` with tolerance up to £0.01 per trade.

### 10.2 Remaining open questions

- What safeguards are required before enabling live trading (for example explicit opt-in, separate configuration, or additional approvals)?

## 11. Glossary (optional)

| Term | Definition |
| --- | --- |
| Algorithmic trading | Automated trading driven by a defined strategy. |
