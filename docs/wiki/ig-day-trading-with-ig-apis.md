# Day trading with IG APIs

This document gives a high-level, implementation-oriented overview of how a day trading application typically uses IG’s REST and Streaming APIs for market data and trade execution. It is written for software developers building a custom trading interface and automation.

## Scope and assumptions

- This describes the technical workflow for using IG APIs, not trading advice.
- IG API trading commonly involves leveraged derivative products such as CFDs and spread bets. These products have higher risk characteristics than unleveraged investing.
- Examples and limits can change; validate against the IG and IG Labs documentation before implementation.

## What you build

A typical day trading application integrating with IG has these major components:

- An authentication module to log in and manage tokens.
- A market discovery module to find tradable instruments.
- A pricing module for real-time streaming prices and REST snapshots.
- An execution module to open, amend, and close trades.
- A risk and controls module to enforce safeguards.
- An operations module for logging, monitoring, and rate limit handling.

## Getting access

1. Create an IG live account.
2. Create a demo account linked to the live account to test safely.
3. Generate an API key in the IG web platform under My Account, Settings, API Keys.

See IG Labs Getting started for onboarding steps.

## Environments and base URLs

IG Labs documents separate base URLs for demo and production environments:

- Demo environment base URL: https://demo-api.ig.com/gateway/deal
- Live environment base URL: https://api.ig.com/gateway/deal

Your application should make it easy to switch between these.

## Authentication and session management

IG’s REST Trading API guide describes two login patterns.

### Session token login

- Your app calls a session endpoint to authenticate.
- IG returns tokens in headers, commonly CST and X-SECURITY-TOKEN.
- Your app sends these tokens on subsequent REST requests.

### OAuth login

- Your app authenticates and receives OAuth access and refresh tokens.
- REST calls use an Authorization Bearer header for the access token.
- You also pass IG-ACCOUNT-ID to specify which account is in scope.

### Streaming authentication note

IG Labs streaming guidance notes that streaming does not accept OAuth tokens directly. If your REST integration uses OAuth, you may need to fetch session tokens for streaming using the session endpoint with a fetchSessionTokens option.

Your app must handle token expiry and re-authentication. IG Labs notes planned maintenance can invalidate tokens, so you should implement retries and a re-login path.

## Market discovery and instrument identifiers

Before you can trade or subscribe to prices you need to identify instruments.

Key ideas:

- IG instruments in the web APIs typically represent derivatives such as CFDs or spread bets.
- Instruments are identified with codes such as EPICs.
- IG Labs describes a market navigation hierarchy for browsing instruments similarly to the Finder view in the dealing platform.

A common flow is:

1. Search or browse for a market.
2. Store the instrument identifier (for example an EPIC) in your watchlist.
3. Use the identifier for pricing subscriptions and order placement.

## Market data in a day trading app

Day trading systems usually need both live prices and history.

### Real-time pricing

You can obtain current pricing in two ways:

- Streaming API for a continuous feed of updates.
- REST API for a single snapshot at the time of the request.

Streaming is typically preferred for active intraday decision-making because it avoids aggressive polling.

### Historical pricing

Historical prices are typically retrieved through REST endpoints. IG Labs documents that historical price requests are subject to datapoint limits.

## Streaming API with Lightstreamer

IG’s streaming API uses Lightstreamer.

A typical streaming sequence is:

1. Authenticate via REST.
2. Obtain the Lightstreamer server address from the session response.
3. Connect to the Lightstreamer server using the active account identifier and the required tokens.
4. Create subscriptions:
   - Choose items (such as markets you want to stream).
   - Choose fields (such as bid and offer).
5. Consume updates and trigger strategy logic.

IG Labs notes you should not hard-code the Lightstreamer server address and that there is a default cap on concurrent subscriptions per connection.

## Trade execution lifecycle

From an API perspective, day trading is usually a repeated loop.

### Start of session

- Log in.
- Fetch account and margin information.
- Establish streaming subscriptions for your watchlist.

### Entry

- Strategy determines entry conditions.
- Place an order or open a position via the REST API.
- Capture identifiers returned by the platform so you can reconcile the trade later.

### Risk management while in the trade

- Monitor price movements and account state.
- Maintain protective exits (for example stop and limit style logic, depending on the order types you use).
- Detect disconnects and reconnect streaming if needed.

### Exit

- Close the position via REST based on strategy rules.
- Verify closure by querying positions and account activity.

### End of day

- Ensure positions are closed if your approach is strictly intraday.
- Export activity and performance data.
- Disconnect streaming cleanly.

## Rate limits and quotas

IG Labs documents default quotas for REST and streaming usage. As of the limits page, defaults include:

- Per-app non-trading requests per minute: 60
- Per-account trading requests per minute: 100
- Per-account non-trading requests per minute: 30
- Historical price datapoints per week: 10,000
- Streaming concurrent subscriptions: 40

Your application should be built assuming these caps can be reached during active intraday trading.

Your application should:

- Use streaming where appropriate to reduce polling.
- Rate-limit locally and implement backoff for HTTP 429 and similar errors.
- Avoid creating multiple concurrent streaming connections as a workaround.

## Operational considerations

### Error handling

- Treat any order placement as a two-step process: submit, then confirm.
- Implement idempotency strategies where possible (for example by de-duplicating on your side using deal references).
- Record full request correlation identifiers when provided.

### Time and timestamps

IG Labs notes date and time are typically ISO 8601 and UTC unless stated otherwise. Persist timestamps exactly as received and convert only for display.

### Observability

At minimum, log:

- Authentication events (without secrets).
- Orders submitted and confirmations received.
- Streaming connection state changes.
- Rate limit responses.

## Plain-English definitions

- API: A way for software systems to talk to each other.
- REST API: A request and response style API where you ask for a resource and get a single response.
- Streaming API: A live connection where updates keep arriving after the initial request.
- API key: A unique identifier for your application used by IG to recognize your app.
- Authentication: Proving who you are.
- Authorisation: Proving what you are allowed to do.
- CST and X-SECURITY-TOKEN: Session tokens that identify your logged-in session and account context.
- OAuth access token: A short-lived token used to authenticate REST calls.
- Refresh token: A token used to obtain a new access token when the old one expires.
- IG-ACCOUNT-ID: The identifier for which account a request should apply to.
- EPIC: An identifier for an IG instrument used in API calls.
- Instrument: The thing you are trading (for example a forex pair or index).
- Bid and offer: The prices you can sell at and buy at.
- Spread: The difference between bid and offer; often an implicit cost.
- Position: An open trade.
- Order: An instruction to open or close a trade.
- Working order: An order that waits until price conditions are met.
- Stop: A pre-set exit intended to limit losses.
- Limit: A pre-set exit intended to take profit.
- Long and short: Buying to benefit from rising prices, or selling to benefit from falling prices.
- Leverage: Controlling a larger market exposure with a smaller deposit.
- Margin: The deposit required to open or maintain a leveraged position.
- CFD: A derivative that tracks price movement without owning the underlying asset.
- Spread betting: A derivative product where profit and loss depends on how far price moves.
- OTC: Trading directly with a provider rather than via a public exchange order book.
- Lightstreamer: The streaming technology IG uses for real-time updates.
- FIX API: An institutional trading protocol offered separately from the public web APIs.

## References

- [IG Labs Getting started](https://labs.ig.com/getting-started.html)
- [IG Trading with APIs](https://www.ig.com/uk/trading-platforms/trading-apis)
- [IG How to use our APIs](https://www.ig.com/uk/trading-platforms/trading-apis/how-to-use-ig-api)
- [IG Labs REST trading API guide](https://labs.ig.com/rest-trading-api-guide.html)
- [IG Labs Streaming API guide](https://labs.ig.com/streaming-api-guide.html)
- [IG Labs FAQ limits](https://labs.ig.com/faq.html#limits)
