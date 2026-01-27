using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using TeslaCamPlayer.BlazorHosted.Server.Models;
using TeslaCamPlayer.BlazorHosted.Server.Providers.Interfaces;
using TeslaCamPlayer.BlazorHosted.Server.Services;
using Xunit;

namespace TeslaCamPlayer.BlazorHosted.Server.Tests.Services
{
    public class JulesApiServiceTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<JulesApiService>> _mockLogger;
        private readonly Mock<ISettingsProvider> _mockSettingsProvider;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<IWebHostEnvironment> _mockWebHostEnvironment;
        private readonly string _testDataPath;

        public JulesApiServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<JulesApiService>>();
            _mockSettingsProvider = new Mock<ISettingsProvider>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockWebHostEnvironment = new Mock<IWebHostEnvironment>();

            _testDataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDataPath);

            var settings = new Settings { ClipsRootPath = _testDataPath };
            _mockSettingsProvider.Setup(s => s.Settings).Returns(settings);

            _mockWebHostEnvironment.Setup(e => e.ContentRootPath).Returns(_testDataPath);

            // Default mock for HttpClientFactory
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{}")
                });
            var client = new HttpClient(mockHandler.Object);
            _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        }

        [Fact]
        public async Task ReportErrorAsync_ShouldRespectDailyLimit()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["JULES_API_KEY"]).Returns("fake-key");
            _mockConfiguration.Setup(c => c["JULES_SOURCE"]).Returns("sources/github/test/test");

            var service = new JulesApiService(
                _mockConfiguration.Object,
                _mockLogger.Object,
                _mockSettingsProvider.Object,
                _mockHttpClientFactory.Object,
                _mockWebHostEnvironment.Object);

            // Act
            for (int i = 0; i < 6; i++)
            {
                await service.ReportErrorAsync(new Exception("Test"), "Context");
            }

            // Assert
            var limitFile = Path.Combine(_testDataPath, "jules_sessions_limit.json");
            Assert.True(File.Exists(limitFile));
            var content = await File.ReadAllTextAsync(limitFile);
            dynamic data = JsonConvert.DeserializeObject(content);

            Assert.Equal(5, (int)data.Count);
        }

        [Fact]
        public async Task ReportErrorAsync_ShouldNotReport_IfNoApiKey()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["JULES_API_KEY"]).Returns((string)null);
            var service = new JulesApiService(
                _mockConfiguration.Object,
                _mockLogger.Object,
                _mockSettingsProvider.Object,
                _mockHttpClientFactory.Object,
                _mockWebHostEnvironment.Object);

            // Act
            await service.ReportErrorAsync(new Exception("Test"), "Context");

            // Assert
            // Log warning should have been called
             _mockLogger.Verify(l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("JULES_API_KEY is not set")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Fact]
        public async Task ReportErrorAsync_ShouldNotIncludeSnippet_WhenFileIsOutsideContentRoot()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["JULES_API_KEY"]).Returns("fake-key");
            _mockConfiguration.Setup(c => c["JULES_SOURCE"]).Returns("sources/github/test/test");

            // Create a file OUTSIDE the testDataPath (which is the ContentRoot)
            var outsidePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(outsidePath);
            var secretFile = Path.Combine(outsidePath, "Secret.cs");
            await File.WriteAllTextAsync(secretFile, "SECRET CODE");

            // Mock Handler to capture request
            string capturedBody = null;
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>(async (r, c) =>
                {
                    capturedBody = await r.Content.ReadAsStringAsync();
                })
                .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("{}") });

            var client = new HttpClient(mockHandler.Object);
            _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

            var service = new JulesApiService(
                _mockConfiguration.Object,
                _mockLogger.Object,
                _mockSettingsProvider.Object,
                _mockHttpClientFactory.Object,
                _mockWebHostEnvironment.Object);

            // Act
            var stackTrace = $"   at TestMethod() in {secretFile}:line 1";
            await service.ReportErrorAsync(new Exception("Test"), "Context", stackTrace);

            // Assert
            Assert.NotNull(capturedBody);
            Assert.DoesNotContain("SECRET CODE", capturedBody);
            Assert.DoesNotContain("Code Snippet:", capturedBody); // Should not even try to add snippet section if empty

            // Cleanup
            Directory.Delete(outsidePath, true);
        }
    }
}
