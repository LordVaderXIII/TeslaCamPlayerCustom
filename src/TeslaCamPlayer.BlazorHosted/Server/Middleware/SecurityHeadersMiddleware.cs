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

            // CSP
            if (!context.Response.Headers.ContainsKey("Content-Security-Policy"))
            {
                // Blazor WASM requires unsafe-eval and unsafe-inline for scripts.
                // We also need external CDNs for Leaflet, Three.js, etc.
                var csp = "default-src 'self'; " +
                          "script-src 'self' 'unsafe-eval' 'unsafe-inline' https://unpkg.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
                          "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://unpkg.com; " +
                          "img-src 'self' data: https://tile.openstreetmap.org https://unpkg.com; " +
                          "font-src 'self' https://fonts.gstatic.com; " +
                          "connect-src 'self'; " +
                          "media-src 'self' blob:; " +
                          "frame-ancestors 'none'; " +
                          "base-uri 'self'; " +
                          "form-action 'self';";

                context.Response.Headers["Content-Security-Policy"] = csp;
            }

            await _next(context);
        }
    }
}
