## 2024-05-23 - [Rate Limiting and Auth Logic]
**Vulnerability:** The `Login` endpoint used manual rate limiting that was vulnerable to bypass if the IP was not correctly detected (e.g. via proxy), and the `Update` endpoint had no rate limiting at all, allowing brute force attempts to change admin credentials.
**Learning:** Using `HttpContext.Connection.RemoteIpAddress` without proper Forwarded Headers configuration can lead to security bypasses or DoS (if all users share the proxy IP).
**Prevention:** Use ASP.NET Core's built-in `RateLimiting` middleware which handles policies more consistently. Ensure `ForwardedHeadersOptions` are configured correctly in containerized environments (which `Program.cs` already had).

## 2025-01-29 - [Auth Setup Vulnerability]
**Vulnerability:** The `/api/auth/update` endpoint allows unauthenticated users to enable authentication and set the admin password if authentication is currently disabled (`IsEnabled = false`).
**Learning:** The application uses a "Trust on First Use" model where the server starts unprotected. Unlike typical setup wizards, this state persists until someone manually triggers the update, leaving the server vulnerable to takeover on the local network (or public if exposed) indefinitely.
**Prevention:** Future improvements should restrict this endpoint to localhost or require a one-time setup token generated at startup.

## 2025-01-30 - [Resource Exhaustion in Export Service]
**Vulnerability:** The video export process created temporary files that were only deleted if the process completed successfully. Exceptions during processing left these files on disk indefinitely, leading to potential disk space exhaustion (DoS).
**Learning:** `finally` blocks are crucial for resource cleanup, especially when dealing with external processes (like ffmpeg) or file I/O where exceptions are likely.
**Prevention:** Always wrap temporary file creation and usage in `try...finally` blocks to ensure cleanup happens regardless of success or failure.

## 2026-01-21 - [Auth Hijack via Re-Enable]
**Vulnerability:** Even if a password was previously set, disabling authentication allowed anyone to re-enable it and overwrite the password without providing the old one.
**Learning:** "Disabled" authentication state should not imply "Reset" state. Sensitive operations (like changing passwords) must always require the current credential if one exists, regardless of the global auth switch.
**Prevention:** Enforce `CurrentPassword` verification for sensitive updates whenever a password hash exists in the database. Ensure recovery mechanisms (like `RESET_AUTH`) explicitly clear credentials if they are intended to bypass this check.

## 2024-05-30 - [Admin Lockout via Empty Password]
**Vulnerability:** The `Update` endpoint allowed enabling authentication (`IsEnabled = true`) without ensuring a password was set. If the system was in an initial state (null `PasswordHash`) and the user enabled auth without providing a password, they would be locked out as the login process would fail on the null hash.
**Learning:** Validating state transitions is critical. Just because a request is valid in isolation (e.g. valid boolean) doesn't mean the resulting system state is valid.
**Prevention:** Enforce invariants during state changes. Added a check to ensure that if `IsEnabled` becomes true, a password must either already exist or be provided in the request.
