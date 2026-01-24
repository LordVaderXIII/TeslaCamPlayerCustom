using System;
using System.Collections.Generic;
using System.Linq;
using TeslaCamPlayer.BlazorHosted.Server.Services;
using TeslaCamPlayer.BlazorHosted.Shared.Models;
using Xunit;

namespace TeslaCamPlayer.BlazorHosted.Server.Tests.Services
{
    public class ClipsServiceTests
    {
        [Fact]
        public void GetRecentClips_ShouldGroupAndSegmentCorrectly()
        {
            // Arrange
            var baseTime = new DateTime(2024, 1, 1, 12, 0, 0);

            // Create a sequence of video files
            // Segment 1: 12:00:00 - 12:01:00 (Front, Left, Right)
            // Segment 2: 12:01:00 - 12:02:00 (Front, Left, Right) -> Should be part of same clip
            // Gap: 10 minutes
            // Segment 3: 12:12:00 - 12:13:00 (Front) -> Should be a new clip

            var files = new List<VideoFile>
            {
                // Segment 1
                new VideoFile { StartDate = baseTime, Duration = TimeSpan.FromMinutes(1), Camera = Cameras.Front, ClipType = ClipType.Recent },
                new VideoFile { StartDate = baseTime, Duration = TimeSpan.FromMinutes(1), Camera = Cameras.LeftRepeater, ClipType = ClipType.Recent },
                new VideoFile { StartDate = baseTime, Duration = TimeSpan.FromMinutes(1), Camera = Cameras.RightRepeater, ClipType = ClipType.Recent },

                // Segment 2 (Continuous)
                new VideoFile { StartDate = baseTime.AddMinutes(1), Duration = TimeSpan.FromMinutes(1), Camera = Cameras.Front, ClipType = ClipType.Recent },
                new VideoFile { StartDate = baseTime.AddMinutes(1), Duration = TimeSpan.FromMinutes(1), Camera = Cameras.LeftRepeater, ClipType = ClipType.Recent },
                new VideoFile { StartDate = baseTime.AddMinutes(1), Duration = TimeSpan.FromMinutes(1), Camera = Cameras.RightRepeater, ClipType = ClipType.Recent },

                // Segment 3 (New Clip after gap)
                new VideoFile { StartDate = baseTime.AddMinutes(12), Duration = TimeSpan.FromMinutes(1), Camera = Cameras.Front, ClipType = ClipType.Recent },
            };

            // Act
            var clips = ClipsService.GetRecentClips(files).ToList();

            // Assert
            Assert.Equal(2, clips.Count);

            // Verify Clips are ordered by StartDate Descending (Newest first)
            var laterClip = clips[0]; // 12:12
            var earlyClip = clips[1]; // 12:00

            Assert.Equal(baseTime.AddMinutes(12), laterClip.StartDate);
            Assert.Equal(baseTime, earlyClip.StartDate);

            // Check segments of earlyClip
            Assert.Equal(2, earlyClip.Segments.Length);

            // Segments are ordered Ascending by time (Clip constructor sorts them)
            Assert.Equal(baseTime, earlyClip.Segments[0].StartDate);
            Assert.Equal(baseTime.AddMinutes(1), earlyClip.Segments[1].StartDate);

            // Verify cameras in a segment
            var seg1 = earlyClip.Segments[0]; // 12:00
            Assert.NotNull(seg1.CameraFront);
            Assert.NotNull(seg1.CameraLeftRepeater);
            Assert.NotNull(seg1.CameraRightRepeater);
            Assert.Null(seg1.CameraBack);

            // Verify later clip
            Assert.Single(laterClip.Segments);
            Assert.Equal(baseTime.AddMinutes(12), laterClip.Segments[0].StartDate);
        }
    }
}
