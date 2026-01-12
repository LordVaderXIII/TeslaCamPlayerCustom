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
        public async Task InvokeAsync_ShouldAddSecurityHeaders()
        {
            // Arrange
            var context = new DefaultHttpContext();
            var next = new Mock<RequestDelegate>();
            next.Setup(n => n(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);
            var middleware = new SecurityHeadersMiddleware(next.Object);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal("SAMEORIGIN", context.Response.Headers["X-Frame-Options"]);
            Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);
            Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"]);
            Assert.Contains("accelerometer=()", context.Response.Headers["Permissions-Policy"].ToString());
            Assert.Contains("default-src 'self'", context.Response.Headers["Content-Security-Policy"].ToString());
        }
    }
}
