using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using TeslaCamPlayer.BlazorHosted.Server.Middleware;
using Xunit;

namespace TeslaCamPlayer.BlazorHosted.Server.Tests.Middleware
{
    public class SecurityHeadersMiddlewareTests
    {
        [Fact]
        public async Task InvokeAsync_AddsSecurityHeaders()
        {
            // Arrange
            var context = new DefaultHttpContext();
            var next = new RequestDelegate(innerContext => Task.CompletedTask);
            var middleware = new SecurityHeadersMiddleware(next);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.True(context.Response.Headers.ContainsKey("X-Frame-Options"));
            Assert.Equal("SAMEORIGIN", context.Response.Headers["X-Frame-Options"]);

            Assert.True(context.Response.Headers.ContainsKey("X-Content-Type-Options"));
            Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);

            Assert.True(context.Response.Headers.ContainsKey("Referrer-Policy"));
            Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"]);

            Assert.True(context.Response.Headers.ContainsKey("Permissions-Policy"));

            // Verify CSP is present
            Assert.True(context.Response.Headers.ContainsKey("Content-Security-Policy"), "CSP should be present");
            var csp = context.Response.Headers["Content-Security-Policy"].ToString();
            Assert.Contains("default-src 'self'", csp);
            Assert.Contains("script-src", csp);
            Assert.Contains("https://unpkg.com", csp);
            Assert.Contains("https://cdn.jsdelivr.net", csp);
            Assert.Contains("https://cdnjs.cloudflare.com", csp);
            Assert.Contains("style-src", csp);
            Assert.Contains("img-src", csp);
            Assert.Contains("media-src 'self' blob:", csp);
        }
    }
}
