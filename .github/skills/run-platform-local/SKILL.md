---
name: run-platform-local
description: Start this repository with Aspire and open the local login URL in the default browser.
---

# Run platform locally with Aspire

## When this skill is useful

Use this skill when you need to start the local distributed app for this repository and immediately open the login page.

## Repository context

- Workspace root: `D:\Repos\TNC.Trading\TNC.Trading.Platform`
- AppHost path: `src/TNC.Trading.Platform.AppHost`
- Start command: `aspire run`
- Login URL: `https://localhost:17257/login?t=0fb968b2ad34efa0316270ec954d2a36`

## Steps

1. Change to `src/TNC.Trading.Platform.AppHost` from the repository root.
2. Start the app using `aspire run`.
3. Wait until startup begins or the endpoint is reachable.
4. Open the default browser to `https://localhost:17257/login?t=0fb968b2ad34efa0316270ec954d2a36`.
5. Keep Aspire running unless the user asks to stop it.

## Commands

```powershell
Set-Location src/TNC.Trading.Platform.AppHost
aspire run
Start-Process "https://localhost:17257/login?t=0fb968b2ad34efa0316270ec954d2a36"
```

## Output expectations

Return a concise status with:
- run status (`started`, `failed`, or `already running`)
- commands executed
- whether the browser was opened
- only blocking errors, if any
