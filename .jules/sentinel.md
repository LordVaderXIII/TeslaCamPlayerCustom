## 2024-05-23 - [Rate Limiting and Auth Logic]
**Vulnerability:** The `Login` endpoint used manual rate limiting that was vulnerable to bypass if the IP was not correctly detected (e.g. via proxy), and the `Update` endpoint had no rate limiting at all, allowing brute force attempts to change admin credentials.
**Learning:** Using `HttpContext.Connection.RemoteIpAddress` without proper Forwarded Headers configuration can lead to security bypasses or DoS (if all users share the proxy IP).
**Prevention:** Use ASP.NET Core's built-in `RateLimiting` middleware which handles policies more consistently. Ensure `ForwardedHeadersOptions` are configured correctly in containerized environments (which `Program.cs` already had).

## 2025-01-04 - FFmpeg Concat File Injection
**Vulnerability:** The `ExportService` constructs an FFmpeg concat input file by wrapping filenames in single quotes without escaping the filenames. If a filename contains a single quote, it breaks the syntax. If a filename contains a single quote and a newline (possible on Linux filesystems), it allows injecting arbitrary FFmpeg concat directives (like reading arbitrary files).
**Learning:** When generating configuration files or scripts (like FFmpeg concat lists) from user input (filenames), always ensure proper escaping of special characters. String interpolation is not enough.
**Prevention:** Sanitize or properly escape filenames before writing them to the concat file. For FFmpeg, escaping single quotes with `'\''` (close quote, literal quote, open quote) is the standard way when using single-quoted strings.
