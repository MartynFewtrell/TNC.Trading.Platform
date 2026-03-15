---
name: run-platform-local
description: Start this repository with Aspire and open the local API (or Scalar UI) URL in the default browser.
---

# Run platform locally with Aspire

## When this skill is useful

Use this skill when you need to start the local distributed app for this repository and immediately open the running API (or its Scalar UI in Development) in a browser.

## Repository context

- Workspace root: repository root (the folder containing this repository)
- AppHost path: `src/TNC.Trading.Platform.AppHost`
- Start command: `aspire run`
- API URL: the API root (`/`) exposed by Aspire; in Development, the Scalar UI is available at `/scalar/v1`. Obtain the base URL from the Aspire dashboard or `aspire run` console output.

## Steps

1. Change to `src/TNC.Trading.Platform.AppHost` from the repository root.
2. Start the app using `aspire run`.
3. Wait until Aspire reports that the application is running and the API endpoint is reachable (via the Aspire dashboard or console output).
4. Open the default browser to the API base URL reported by Aspire (for example the root `/`), or, in Development, navigate to `/scalar/v1` on that base URL for the Scalar UI.
5. Keep Aspire running unless the user asks to stop it.

## Commands

```powershell
Set-Location src/TNC.Trading.Platform.AppHost
aspire run
# After Aspire starts, note the API base URL from the Aspire dashboard or console output,
# then open the API (or Scalar UI in Development) in the default browser, for example:
# $apiUrl = "<api-base-url-from-aspire>"
# Start-Process "$apiUrl"          # API root
# Start-Process "$apiUrl/scalar/v1" # Scalar UI (Development only)
```

## Output expectations

Return a concise status with:
- run status (`started`, `failed`, or `already running`)
- commands executed
- whether the browser was opened
- only blocking errors, if any
