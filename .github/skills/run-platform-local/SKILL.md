---
name: run-platform-local
description: Start this repository with Aspire and open the local login URL in the default browser.
---

# Run platform locally with Aspire

## When this skill is useful

Use this skill when you need to start the local distributed app for this repository and immediately open the login page.

## Repository context

- Workspace root: repository root (the folder containing this repository)
- AppHost path: `src/TNC.Trading.Platform.AppHost`
- Start command: `aspire run`
- Login URL: application login page (for example `/login`) on the frontend exposed by Aspire; obtain the full URL from the Aspire dashboard or `aspire run` console output.

## Steps

1. Change to `src/TNC.Trading.Platform.AppHost` from the repository root.
2. Start the app using `aspire run`.
3. Wait until Aspire reports that the application is running and the frontend endpoint is reachable (via the Aspire dashboard or console output).
4. Open the default browser to the frontend URL reported by Aspire and navigate to the `/login` page.
5. Keep Aspire running unless the user asks to stop it.

## Commands

```powershell
Set-Location src/TNC.Trading.Platform.AppHost
aspire run
# After Aspire starts, note the frontend URL from the Aspire dashboard or console output,
# then open the login page in the default browser, for example:
# $frontendUrl = "<frontend-url-from-aspire>"
# Start-Process "$frontendUrl/login"
```

## Output expectations

Return a concise status with:
- run status (`started`, `failed`, or `already running`)
- commands executed
- whether the browser was opened
- only blocking errors, if any
