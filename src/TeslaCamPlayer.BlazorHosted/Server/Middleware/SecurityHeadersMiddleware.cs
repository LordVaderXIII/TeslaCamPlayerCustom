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

            await _next(context);
        }
    }
}
