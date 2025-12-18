## 2025-12-11 - Partial Path Traversal in ApiController
**Vulnerability:** `IsUnderRootPath` used `StartsWith` on the root path string without ensuring a trailing directory separator. This allows bypassing the check if the requested path is a sibling directory with a name that shares the same prefix (e.g., `/data` vs `/database`).
**Learning:** `StartsWith` is insufficient for path containment checks. Path validation must operate on canonicalized paths and ensure directory boundaries are respected (e.g., by ensuring a trailing slash).
**Prevention:** Always ensure the root path ends with `Path.DirectorySeparatorChar` before using `StartsWith`, or use proper path manipulation libraries that handle containment checks.

## 2025-12-11 - Broken Access Control with Toggleable Authentication
**Vulnerability:** The application supports toggleable authentication (via `Users` table), but the API controllers (`ApiController`, `ExportController`) lacked authorization checks. This meant that even when authentication was enabled, the API endpoints were publicly accessible to anyone who knew the URLs, bypassing the login requirement.
**Learning:** Toggleable authentication requires dynamic authorization policies. Standard `[Authorize]` attributes are static and might be omitted if the developer relies on the "login required" UI state rather than backend enforcement.
**Prevention:** Implement a custom authorization filter or policy that checks the dynamic authentication setting and enforces access control on all sensitive endpoints. Ensure that "Auth Enabled" means "Backend Enforced", not just "Frontend Hidden".

## 2025-12-14 - Missing Rate Limiting on Authentication Endpoint
**Vulnerability:** The `AuthController.Login` endpoint lacked rate limiting, allowing unlimited password guessing attempts (brute-force attacks) against the admin account.
**Learning:** Single-user authentication systems often overlook rate limiting because of their simplicity ("it's just me"), but they are equally susceptible to automated attacks.
**Prevention:** Implement rate limiting (e.g., using `IMemoryCache` or a middleware) on all authentication endpoints, tracking failed attempts by IP address.

## 2025-12-15 - DoS Risk via Unbounded Background Processes
**Vulnerability:** The `ExportService` launched a background FFmpeg process for every export request immediately via `Task.Run`, without any concurrency limit. This allowed a Denial of Service (DoS) attack where a user (or attacker) could trigger multiple export jobs, exhausting server CPU and memory resources.
**Learning:** "Fire-and-forget" background tasks (`Task.Run`) are dangerous for resource-intensive operations. They bypass the natural backpressure of the request-response cycle.
**Prevention:** Always implement a queue or concurrency limiter (like `SemaphoreSlim` or `Channel<T>`) for background jobs that consume significant system resources.

## 2025-12-16 - Arbitrary File Read in JulesApiService
**Vulnerability:** The `JulesApiService.ExtractSnippet` method parsed file paths from user-provided stack traces and read file content without validation. This allowed an attacker to read any file on the server (Arbitrary File Read) by supplying a crafted stack trace pointing to the target file.
**Learning:** Never trust file paths extracted from user input, even if they look like system-generated strings (like stack traces). Stack traces can be forged or manipulated.
**Prevention:** Validate file paths against an allowlist of extensions or directories. In this case, we restricted access to known source code extensions (`.cs`, `.razor`, etc.) as the feature is intended for code context only.

## 2025-12-16 - Missing Rate Limiting on Error Reporting Endpoint
**Vulnerability:** The public `ErrorController.ReportError` endpoint lacked rate limiting, allowing a malicious actor to flood the backend with error reports, exhausting the daily Jules API quota (DoS) and filling disk logs.
**Learning:** Publicly exposed utility endpoints (like error reporting or feedback) are often overlooked for rate limiting but are trivial vectors for resource exhaustion DoS.
**Prevention:** Apply rate limiting (e.g., per IP) to all public endpoints that trigger resource-intensive operations or external API calls.

## 2025-12-18 - Rate Limiting Bypass and HSTS Failure in Docker
**Vulnerability:** The application relied on `HttpContext.Connection.RemoteIpAddress` for rate limiting and `UseHsts`/`UseHttpsRedirection`, but lacked `ForwardedHeadersMiddleware`. In a Docker container behind a reverse proxy, this caused the app to see the Docker Gateway IP for all requests, leading to a DoS vulnerability (one failed login blocks everyone) and preventing HSTS activation.
**Learning:** Containerized applications almost always run behind a proxy. Default ASP.NET Core templates do not enable `ForwardedHeadersMiddleware`, causing standard security features to malfunction.
**Prevention:** Always configure `ForwardedHeadersMiddleware` in container-ready applications, explicitly clearing `KnownNetworks` if the proxy IP is dynamic.
