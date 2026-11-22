# Authentication & authorization

## Purpose
Describe how the app authenticates to IG and how session/authorization information is propagated to REST and streaming calls.

## Key points (TL;DR)
- Credentials and API key are loaded from `SampleWPFTrader/App.config`.
- Login uses `IgRestApiClient.SecureAuthenticate`, which:
  1. fetches an encryption key from `/session/encryptionKey`
  2. encrypts `{password}|{timestamp}` using RSA PKCS#1
  3. calls `/session` (VERSION=2)
- Session tokens (`CST` and `X-SECURITY-TOKEN`) are captured from REST response headers and reused for subsequent REST calls.
- Streaming authentication uses Lightstreamer credentials derived from those session tokens.

## Details
## Configuration inputs
The app expects these values in `IgWebApiConnection`:
- `environment`: `demo` or `live` (also used to build the REST base URL)
- `username`
- `password`
- `apikey`

## REST session establishment
### Conversation context
- `ConversationContext` stores `apiKey`, `cst`, and `xSecurityToken`.
- On first login, the WPF app creates a context with `apiKey` set and tokens unset.

### Password encryption flow
- `SecureAuthenticate` calls `fetchEncryptionKey`.
- If the key is returned, it RSA-encrypts the string `{rawPassword}|{timeStamp}`.
- It sets `encryptedPassword` flag based on whether encryption succeeded.

### Token capture
- After REST responses, `IgRestService.ParseHeaders` updates `ConversationContext.cst` from `CST` header and `ConversationContext.xSecurityToken` from `X-SECURITY-TOKEN` header.

## Streaming session establishment
The WPF app uses `AuthenticationResponse.lightstreamerEndpoint` plus tokens from `ConversationContext` to connect:
- Lightstreamer `User`: the app passes `currentAccountId`.
- Lightstreamer `Password`: `CST-{cst}|XST-{xSecurityToken}`.

## Authorization model
- There is no local authorization model; access is governed by IG’s session tokens.
- UI gating is coarse: most tabs check `LoggedIn` before making calls.

## Evidence (repo references)
- `SampleWPFTrader/App.config` (lines 44–50): credential inputs.
- `SampleWPFTrader/ViewModel/ApplicationViewModel.cs` (lines 233–250, 277–302): reads config, calls `SecureAuthenticate`, then uses tokens and `lightstreamerEndpoint` to connect.
- `IGWebApiClient/IGRestApiClient.cs` (lines 55–70, 72–93, 116–119): `SecureAuthenticate` + `fetchEncryptionKey` + password encryption.
- `IGWebApiClient/Security/Rsa.cs` (lines 8–52): RSA PKCS#1 encryption via PCLCrypto.
- `IGWebApiClient/IGRestService.cs` (lines 39–55, 57–75): header parsing and header injection for subsequent requests.
- `IGWebApiClient/IGStreamingApi.cs` (lines 544–553): Lightstreamer connection password format.

## Open questions / assumptions (if any)
- Assumption: The encryption key returned by `/session/encryptionKey` is always compatible with RSA PKCS#1 and the `{password}|{timestamp}` payload format.
  - Missing evidence: No unit tests or sample payloads are included.
  - Risk/impact: Authentication may fail if IG changes the encryption scheme or payload format.

- Assumption: Storing credentials in `App.config` is acceptable for a sample, but a real implementation should use secure secret storage.
  - Missing evidence: No secret management integration exists in-repo.
  - Risk/impact: Credentials can leak via source control or local machine compromise.