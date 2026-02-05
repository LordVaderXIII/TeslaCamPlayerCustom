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
using Newtonsoft.Json.Linq;
using TeslaCamPlayer.BlazorHosted.Server.Models;
using TeslaCamPlayer.BlazorHosted.Server.Providers.Interfaces;
using TeslaCamPlayer.BlazorHosted.Server.Services;
using Xunit;

namespace TeslaCamPlayer.BlazorHosted.Server.Tests.Services
{
    public class JulesApiServiceSecurityTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<JulesApiService>> _mockLogger;
        private readonly Mock<ISettingsProvider> _mockSettingsProvider;
        private readonly Mock<IWebHostEnvironment> _mockEnvironment;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly string _testDataPath;
        private readonly string _contentRootPath;

        public JulesApiServiceSecurityTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<JulesApiService>>();
            _mockSettingsProvider = new Mock<ISettingsProvider>();
            _mockEnvironment = new Mock<IWebHostEnvironment>();

            _testDataPath = Path.Combine(Path.GetTempPath(), "TeslaCamTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_testDataPath);

            _contentRootPath = Path.Combine(_testDataPath, "AppRoot");
            Directory.CreateDirectory(_contentRootPath);

            var settings = new Settings { ClipsRootPath = _testDataPath };
            _mockSettingsProvider.Setup(s => s.Settings).Returns(settings);

            _mockEnvironment.Setup(e => e.ContentRootPath).Returns(_contentRootPath);

            _mockConfiguration.Setup(c => c["JULES_API_KEY"]).Returns("fake-key");
            _mockConfiguration.Setup(c => c["JULES_SOURCE"]).Returns("sources/github/test/test");

            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        }

        [Fact]
        public async Task ReportFrontendErrorAsync_ShouldNotIncludeSnippet_FromArbitraryFile()
        {
            // Arrange
            // Create a file outside the ContentRootPath (simulating arbitrary file read)
            var secretFilePath = Path.Combine(_testDataPath, "secret.cs");
            await File.WriteAllTextAsync(secretFilePath, "SECRET_CONTENT_DO_NOT_LEAK");

            // Construct a stack trace pointing to this file
            var stackTrace = $"at SomeMethod() in {secretFilePath}:line 1";

            // Mock HTTP response
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"name\": \"sessions/123\"}")
                })
                .Verifiable();

            var service = new JulesApiService(_httpClient, _mockConfiguration.Object, _mockLogger.Object, _mockSettingsProvider.Object, _mockEnvironment.Object);

            // Act
            await service.ReportFrontendErrorAsync("Error message", stackTrace, "Context");

            // Assert
            // Verify the request body sent to Jules API
            _mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req => CheckRequestBody(req, "SECRET_CONTENT_DO_NOT_LEAK", false)),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task ReportErrorAsync_ShouldNotIncludeSnippet_FromOutsideContentRoot()
        {
            // Arrange
            // Create a file outside the ContentRootPath
            var secretFilePath = Path.Combine(_testDataPath, "outside.cs");
            await File.WriteAllTextAsync(secretFilePath, "OUTSIDE_CONTENT");

            // Construct a stack trace
            var stackTrace = $"at SomeMethod() in {secretFilePath}:line 1";

            _mockHttpMessageHandler
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                   "SendAsync",
                   ItExpr.IsAny<HttpRequestMessage>(),
                   ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(new HttpResponseMessage
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent("{\"name\": \"sessions/123\"}")
               });

            var service = new JulesApiService(_httpClient, _mockConfiguration.Object, _mockLogger.Object, _mockSettingsProvider.Object, _mockEnvironment.Object);

            // Act
            await service.ReportErrorAsync(new Exception("Backend Error") { }, "Context", stackTrace);

            // Assert
            _mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req => CheckRequestBody(req, "OUTSIDE_CONTENT", false)),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        private bool CheckRequestBody(HttpRequestMessage request, string contentToCheck, bool shouldContain)
        {
            if (request.Content == null) return false;
            var json = request.Content.ReadAsStringAsync().Result;
            var body = JsonConvert.DeserializeObject<JObject>(json);
            var prompt = body?["prompt"]?.ToString();

            if (prompt == null) return false;

            bool contains = prompt.Contains(contentToCheck);
            return shouldContain ? contains : !contains;
        }
    }
}
