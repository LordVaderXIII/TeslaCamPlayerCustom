using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace TeslaCamPlayer.BlazorHosted.Server.Middleware
{
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Add security headers to the response
            if (!context.Response.Headers.ContainsKey("X-Frame-Options"))
            {
                context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
            }

            if (!context.Response.Headers.ContainsKey("X-Content-Type-Options"))
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            }

            if (!context.Response.Headers.ContainsKey("Referrer-Policy"))
            {
                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            }

            if (!context.Response.Headers.ContainsKey("Permissions-Policy"))
            {
                context.Response.Headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
            }

            if (!context.Response.Headers.ContainsKey("Content-Security-Policy"))
            {
                // Blazor WASM requires 'unsafe-eval' (or 'wasm-unsafe-eval')
                // Styles often need 'unsafe-inline' in Blazor apps unless strict mode is used (which is hard to retrofit)
                // Images: data: and blob: are needed. Map tiles from openstreetmap.org.
                // Fonts: Google Fonts.
                // Scripts: unpkg.com, cdn.jsdelivr.net, cdnjs.cloudflare.com.
                // Styles: Google Fonts, unpkg.com (Leaflet).
                // Connect: self, openstreetmap.

                var csp = "default-src 'self'; " +
                          "script-src 'self' 'unsafe-eval' 'unsafe-inline' 'wasm-unsafe-eval' https://unpkg.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
                          "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://unpkg.com; " +
                          "img-src 'self' data: blob: https://*.tile.openstreetmap.org; " +
                          "font-src 'self' data: https://fonts.gstatic.com; " +
                          "connect-src 'self' https://*.tile.openstreetmap.org; " +
                          "media-src 'self' blob:; " +
                          "object-src 'none'; " +
                          "base-uri 'self'; " +
                          "form-action 'self'; " +
                          "frame-ancestors 'self'; " +
                          "upgrade-insecure-requests;";

                context.Response.Headers["Content-Security-Policy"] = csp;
            }

            await _next(context);
        }
    }
}
