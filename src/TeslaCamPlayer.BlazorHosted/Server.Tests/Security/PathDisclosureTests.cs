using Newtonsoft.Json;
using TeslaCamPlayer.BlazorHosted.Shared.Models;
using Xunit;

namespace TeslaCamPlayer.BlazorHosted.Server.Tests.Security
{
    public class PathDisclosureTests
    {
        [Fact]
        public void VideoFile_Serialization_ShouldNotExposeFilePath_Newtonsoft()
        {
            // Arrange
            var videoFile = new VideoFile
            {
                FilePath = "/home/user/secret/path/to/video.mp4",
                Url = "/Api/Video/video.mp4",
                StartDate = System.DateTime.Now,
                Duration = System.TimeSpan.FromSeconds(10)
            };

            // Act
            var json = JsonConvert.SerializeObject(videoFile);

            // Assert
            Assert.DoesNotContain("FilePath", json);
            Assert.DoesNotContain("/home/user/secret/path/to/video.mp4", json);
        }

        [Fact]
        public void VideoFile_Serialization_ShouldNotExposeFilePath_SystemTextJson()
        {
            // Arrange
            var videoFile = new VideoFile
            {
                FilePath = "/home/user/secret/path/to/video.mp4",
                Url = "/Api/Video/video.mp4",
                StartDate = System.DateTime.Now,
                Duration = System.TimeSpan.FromSeconds(10)
            };

            // Act
            var json = System.Text.Json.JsonSerializer.Serialize(videoFile);

            // Assert
            Assert.DoesNotContain("FilePath", json);
            Assert.DoesNotContain("/home/user/secret/path/to/video.mp4", json);
        }
    }
}
