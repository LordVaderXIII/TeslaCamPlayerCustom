using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TeslaCamPlayer.BlazorHosted.Server.Controllers;
using TeslaCamPlayer.BlazorHosted.Server.Models;
using TeslaCamPlayer.BlazorHosted.Server.Providers.Interfaces;
using TeslaCamPlayer.BlazorHosted.Server.Services.Interfaces;
using TeslaCamPlayer.BlazorHosted.Shared.Models;
using Xunit;

namespace TeslaCamPlayer.BlazorHosted.Server.Tests.Controllers
{
    public class ApiControllerTests
    {
        private readonly Mock<ISettingsProvider> _mockSettingsProvider;
        private readonly Mock<IClipsService> _mockClipsService;
        private readonly ApiController _controller;

        public ApiControllerTests()
        {
            _mockSettingsProvider = new Mock<ISettingsProvider>();
            _mockSettingsProvider.Setup(x => x.Settings).Returns(new Settings { ClipsRootPath = "/TeslaCam" });

            _mockClipsService = new Mock<IClipsService>();

            _controller = new ApiController(_mockSettingsProvider.Object, _mockClipsService.Object);
        }

        [Fact]
        public async Task GetClips_ShouldReturnBadRequest_WhenSyncModeIsFull()
        {
            var result = await _controller.GetClips(SyncMode.Full);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetClips_ShouldReturnBadRequest_WhenSyncModeIsIncremental()
        {
            var result = await _controller.GetClips(SyncMode.Incremental);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetClips_ShouldReturnClips_WhenSyncModeIsNone()
        {
            var expectedClips = new[] { new Clip(ClipType.Recent, new ClipVideoSegment[0]) };
            _mockClipsService.Setup(x => x.GetClipsAsync(SyncMode.None))
                .ReturnsAsync(expectedClips);

            var result = await _controller.GetClips(SyncMode.None);

            // Since we return Clip[] directly (converted to ActionResult), Result is null and Value is set.
            Assert.Null(result.Result);
            Assert.Equal(expectedClips, result.Value);
        }

        [Fact]
        public async Task Sync_ShouldCallService_WhenSyncModeIsFull()
        {
            var result = await _controller.Sync(SyncMode.Full);

            Assert.IsType<OkResult>(result);
            _mockClipsService.Verify(x => x.GetClipsAsync(SyncMode.Full), Times.Once);
        }
    }
}
