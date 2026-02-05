using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
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
        private readonly Mock<IWebHostEnvironment> _mockEnvironment;
        private readonly string _testDataPath;

        public JulesApiServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<JulesApiService>>();
            _mockSettingsProvider = new Mock<ISettingsProvider>();
            _mockEnvironment = new Mock<IWebHostEnvironment>();

            _testDataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDataPath);

            // Fix setup for Settings
            var settings = new Settings { ClipsRootPath = _testDataPath };
            _mockSettingsProvider.Setup(s => s.Settings).Returns(settings);

            _mockEnvironment.Setup(e => e.ContentRootPath).Returns(_testDataPath);
        }

        [Fact]
        public async Task ReportErrorAsync_ShouldRespectDailyLimit()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["JULES_API_KEY"]).Returns("fake-key");
            _mockConfiguration.Setup(c => c["JULES_SOURCE"]).Returns("sources/github/test/test");

            var service = new JulesApiService(new HttpClient(), _mockConfiguration.Object, _mockLogger.Object, _mockSettingsProvider.Object, _mockEnvironment.Object);

            // Act
            // Call report 6 times (limit is 5)
            // Note: Since we mock HttpClient inside the service (internal implementation issue),
            // the actual HTTP call will fail or throw if not handled.
            // However, the rate limit check happens BEFORE the HTTP call.
            // But wait, the service creates its own HttpClient. I cannot mock it easily without refactoring.
            // But I can check if it updates the file.
            // If it tries to make a request, it will likely fail with "Connection refused" or similar in test env if URL is real.
            // I should handle that exception or accept it.
            // The service catches exceptions during reporting.

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
            var service = new JulesApiService(new HttpClient(), _mockConfiguration.Object, _mockLogger.Object, _mockSettingsProvider.Object, _mockEnvironment.Object);

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
    }
}
