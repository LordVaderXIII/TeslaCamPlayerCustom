## 2025-12-11 - Partial Path Traversal in ApiController
**Vulnerability:** `IsUnderRootPath` used `StartsWith` on the root path string without ensuring a trailing directory separator. This allows bypassing the check if the requested path is a sibling directory with a name that shares the same prefix (e.g., `/data` vs `/database`).
**Learning:** `StartsWith` is insufficient for path containment checks. Path validation must operate on canonicalized paths and ensure directory boundaries are respected (e.g., by ensuring a trailing slash).
**Prevention:** Always ensure the root path ends with `Path.DirectorySeparatorChar` before using `StartsWith`, or use proper path manipulation libraries that handle containment checks.
