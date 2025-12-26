## 2024-05-23 - Cookie Security Hardening
**Vulnerability:** The authentication cookie lacked `HttpOnly`, `SameSite`, and `Secure` attributes.
**Learning:** Default `CookieAuthenticationDefaults` in ASP.NET Core do not enforce strict security settings like `SameSite=Strict` or `HttpOnly` in all scenarios, leaving the application vulnerable to XSS (cookie theft) and CSRF.
**Prevention:** Always explicitly configure `Cookie.HttpOnly = true`, `Cookie.SameSite = SameSiteMode.Strict`, and `Cookie.SecurePolicy = CookieSecurePolicy.Always` (or `SameAsRequest`) when setting up cookie authentication.
