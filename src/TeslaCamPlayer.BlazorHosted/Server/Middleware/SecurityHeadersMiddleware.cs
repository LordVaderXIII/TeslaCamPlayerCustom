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
                // CSP Policies:
                // script-src: Allow self, unsafe-eval (Blazor WASM), unsafe-inline (Blazor boot/libs), and external CDNs used (unpkg, cdnjs, jsdelivr)
                // style-src: Allow self, unsafe-inline (MudBlazor/Libs), fonts.googleapis.com, unpkg.com
                // img-src: Allow self, data:, blob:, and OpenStreetMap tiles
                // font-src: Allow self, data:, and fonts.gstatic.com (Google Fonts)
                // connect-src: Allow self, Jules API, and OpenStreetMap
                // media-src: Allow self and blob: (Video playback)
                var csp = "default-src 'self'; " +
                          "script-src 'self' 'unsafe-eval' 'wasm-unsafe-eval' 'unsafe-inline' https://unpkg.com https://cdnjs.cloudflare.com https://cdn.jsdelivr.net; " +
                          "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://unpkg.com; " +
                          "img-src 'self' data: blob: https://*.tile.openstreetmap.org; " +
                          "font-src 'self' data: https://fonts.gstatic.com; " +
                          "connect-src 'self' https://jules.googleapis.com https://*.tile.openstreetmap.org; " +
                          "media-src 'self' blob:; " +
                          "object-src 'none'; " +
                          "frame-ancestors 'self';";

                context.Response.Headers["Content-Security-Policy"] = csp;
            }

            await _next(context);
        }
    }
}
