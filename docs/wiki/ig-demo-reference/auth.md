# Authentication and Authorization

## Purpose

Describe the sample's login sequence, API headers, and authorization boundaries.

## Key points (TL;DR)

* The app authenticates to IG with a username/identifier, password, and API key.
* The client fetches the IG encryption key and attempts RSA encryption of the
  password plus timestamp before V2 session creation.
* CST and X-SECURITY-TOKEN are captured from response headers and reused on REST
  calls.
* The current account ID, tokens, and streaming endpoint are reused for
  Lightstreamer.
* No application-level roles or permissions are implemented.

## Details

### Session creation

`ApplicationViewModel.Login` reads the configuration section, constructs the V2
authentication DTO, and calls `SecureAuthenticate`. This method creates a
`ConversationContext`, fetches the encryption key with the API key, then
encrypts `<rawPassword>|<timeStamp>` using the returned public key. If a
response key exists, it marks the password encrypted. Session creation uses
`POST /gateway/deal/session` with `VERSION: 2`.

Response headers named `CST` and `X-SECURITY-TOKEN` are copied into the shared
context. Subsequent REST calls add those headers and the `X-IG-API-KEY` and
`VERSION` headers.

### Streaming credentials

The WPF app passes the current account ID, CST, X-SECURITY-TOKEN, API key, and
Lightstreamer endpoint to `Connect`. The checked-in client sets the Lightstreamer
adapter to `DEFAULT`, the `User` to the account ID, and the `Password` to
`CST-<value>|XST-<value>`. The method does not use its API-key argument when
building the connection info.

### Authorization and account switching

The API key and session tokens are the only authorization material visible in
this implementation. There is no local role or claim policy. The client library
does expose an account-switch method, but the WPF screens use the current
account ID returned during login and do not implement account switching.

The authentication DTO includes rerouting-related properties and comments, but
the UI does not implement the documented rerouting or silent-login sequence.
Do not treat that omitted flow as proof it is unnecessary.

### Security caveats for a new application

The sample's `App.config` contains placeholder username and password values and
a configured API-key value. Do not copy the value into new code or commit real
credentials. The RSA helper swallows key-import exceptions into a local string,
and the REST service logs full request URLs and status, not response bodies.
The repository is marked deprecated, so verify authentication rules with current
IG documentation before implementation.

## Evidence (repo references)

* `SampleWPFTrader/ViewModel/ApplicationViewModel.cs:229-308`: credential
  loading and login-to-stream sequence.
* `IGWebApiClient/IGRestApiClient.cs:34-102`: context and password encryption.
* `IGWebApiClient/IGRestService.cs:39-76`: CST/XST capture and request headers.
* `IGWebApiClient/IGStreamingApi.cs:544-557`: Lightstreamer credentials.
* `IGWebApiClient/Model/dto/endpoint/auth/session/v2/AuthenticationResponse.cs`:
  account, endpoint, and rerouting properties.
* `SampleWPFTrader/App.config:44-50`: configuration keys and example values.

## Open questions / assumptions (if any)

* Assumption: IG's current authentication protocol still supports the sample's
  encryption and session header flow.
  * Missing evidence: No current vendor API specification is checked in.
  * Risk/impact: Login may fail or require additional steps, including regional
    rerouting, MFA, or newer session behavior.
