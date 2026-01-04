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
            var middleware = new SecurityHeadersMiddleware(next: (innerHttpContext) => Task.CompletedTask);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            var headers = context.Response.Headers;
            Assert.True(headers.ContainsKey("X-Frame-Options"));
            Assert.Equal("SAMEORIGIN", headers["X-Frame-Options"]);
            Assert.True(headers.ContainsKey("X-Content-Type-Options"));
            Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
            Assert.True(headers.ContainsKey("Referrer-Policy"));
            Assert.Equal("strict-origin-when-cross-origin", headers["Referrer-Policy"]);
            Assert.True(headers.ContainsKey("Permissions-Policy"));

            // Check for Content-Security-Policy (which we are about to add)
            Assert.True(headers.ContainsKey("Content-Security-Policy"), "Content-Security-Policy header is missing");
            var csp = headers["Content-Security-Policy"].ToString();
            Assert.Contains("default-src 'self'", csp);
            Assert.Contains("script-src 'self'", csp);
        }
    }
}
