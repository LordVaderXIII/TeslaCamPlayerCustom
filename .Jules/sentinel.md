## 2025-12-11 - Partial Path Traversal in ApiController
**Vulnerability:** `IsUnderRootPath` used `StartsWith` on the root path string without ensuring a trailing directory separator. This allows bypassing the check if the requested path is a sibling directory with a name that shares the same prefix (e.g., `/data` vs `/database`).
**Learning:** `StartsWith` is insufficient for path containment checks. Path validation must operate on canonicalized paths and ensure directory boundaries are respected (e.g., by ensuring a trailing slash).
**Prevention:** Always ensure the root path ends with `Path.DirectorySeparatorChar` before using `StartsWith`, or use proper path manipulation libraries that handle containment checks.

## 2025-12-11 - Broken Access Control with Toggleable Authentication
**Vulnerability:** The application supports toggleable authentication (via `Users` table), but the API controllers (`ApiController`, `ExportController`) lacked authorization checks. This meant that even when authentication was enabled, the API endpoints were publicly accessible to anyone who knew the URLs, bypassing the login requirement.
**Learning:** Toggleable authentication requires dynamic authorization policies. Standard `[Authorize]` attributes are static and might be omitted if the developer relies on the "login required" UI state rather than backend enforcement.
**Prevention:** Implement a custom authorization filter or policy that checks the dynamic authentication setting and enforces access control on all sensitive endpoints. Ensure that "Auth Enabled" means "Backend Enforced", not just "Frontend Hidden".
