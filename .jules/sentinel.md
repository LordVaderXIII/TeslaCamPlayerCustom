## 2024-05-23 - [Rate Limiting and Auth Logic]
**Vulnerability:** The `Login` endpoint used manual rate limiting that was vulnerable to bypass if the IP was not correctly detected (e.g. via proxy), and the `Update` endpoint had no rate limiting at all, allowing brute force attempts to change admin credentials.
**Learning:** Using `HttpContext.Connection.RemoteIpAddress` without proper Forwarded Headers configuration can lead to security bypasses or DoS (if all users share the proxy IP).
**Prevention:** Use ASP.NET Core's built-in `RateLimiting` middleware which handles policies more consistently. Ensure `ForwardedHeadersOptions` are configured correctly in containerized environments (which `Program.cs` already had).

## 2025-01-29 - [Auth Setup Vulnerability]
**Vulnerability:** The `/api/auth/update` endpoint allows unauthenticated users to enable authentication and set the admin password if authentication is currently disabled (`IsEnabled = false`).
**Learning:** The application uses a "Trust on First Use" model where the server starts unprotected. Unlike typical setup wizards, this state persists until someone manually triggers the update, leaving the server vulnerable to takeover on the local network (or public if exposed) indefinitely.
**Prevention:** Future improvements should restrict this endpoint to localhost or require a one-time setup token generated at startup.
