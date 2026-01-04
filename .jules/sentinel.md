## 2025-12-11 - Missing Content-Security-Policy
**Vulnerability:** The application was missing the `Content-Security-Policy` header, which protects against XSS and data injection attacks.
**Learning:** Even with other security headers present (like X-Frame-Options), CSP is critical for modern web apps, especially when handling third-party scripts.
**Prevention:** Always verify headers using a tool or by inspecting `SecurityHeadersMiddleware`. For Blazor WASM, ensure `unsafe-eval` is allowed in `script-src` and handle CDNs explicitly.
