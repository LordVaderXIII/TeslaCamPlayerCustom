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
                // Blazor WASM requires unsafe-eval (for mono interpreter) and unsafe-inline (for boot/styles).
                // Allowing external CDNs for Leaflet, Three.js, and Google Fonts as used in index.html.
                context.Response.Headers["Content-Security-Policy"] =
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://unpkg.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
                    "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://unpkg.com; " +
                    "img-src 'self' data: https://*.tile.openstreetmap.org https://unpkg.com; " +
                    "connect-src 'self' data:; " +
                    "font-src 'self' data: https://fonts.gstatic.com; " +
                    "media-src 'self' blob:; " +
                    "base-uri 'self'; " +
                    "form-action 'self'; " +
                    "frame-ancestors 'self';";
            }

            if (!context.Response.Headers.ContainsKey("X-XSS-Protection"))
            {
                context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
            }

            await _next(context);
        }
    }
}
