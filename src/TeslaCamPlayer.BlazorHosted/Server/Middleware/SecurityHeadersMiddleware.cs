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
                // CSP for Blazor WebAssembly + Leaflet + External CDNs
                // script-src: 'unsafe-eval'/'wasm-unsafe-eval' required for Blazor WASM
                // style-src: 'unsafe-inline' required for MudBlazor/Leaflet
                // img-src: tile.openstreetmap.org for map tiles
                var csp = "default-src 'self'; " +
                          "script-src 'self' 'unsafe-eval' 'wasm-unsafe-eval' https://unpkg.com https://cdn.jsdelivr.net; " +
                          "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://unpkg.com; " +
                          "img-src 'self' data: blob: https://*.tile.openstreetmap.org https://unpkg.com; " +
                          "font-src 'self' https://fonts.gstatic.com; " +
                          "connect-src 'self' https://*.tile.openstreetmap.org https://unpkg.com; " +
                          "media-src 'self' blob:; " +
                          "object-src 'none'; " +
                          "base-uri 'self'; " +
                          "form-action 'self'; " +
                          "frame-ancestors 'self';";

                context.Response.Headers["Content-Security-Policy"] = csp;
            }

            await _next(context);
        }
    }
}
