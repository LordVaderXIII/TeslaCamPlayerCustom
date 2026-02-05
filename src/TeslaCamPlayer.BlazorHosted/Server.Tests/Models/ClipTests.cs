using System;
using System.Linq;
using TeslaCamPlayer.BlazorHosted.Shared.Models;
using Xunit;

namespace TeslaCamPlayer.BlazorHosted.Server.Tests.Models
{
    public class ClipTests
    {
        private Clip CreateClip(params (int start, int end)[] segmentTimes)
        {
            var baseTime = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var segments = segmentTimes.Select(t => new ClipVideoSegment
            {
                StartDate = baseTime.AddSeconds(t.start),
                EndDate = baseTime.AddSeconds(t.end)
            }).ToArray();

            return new Clip(ClipType.Recent, segments);
        }

        [Fact]
        public void SegmentAtDate_ShouldReturnSegment_WhenDateIsInside()
        {
            var clip = CreateClip((10, 20), (30, 40));
            var baseTime = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            var result = clip.SegmentAtDate(baseTime.AddSeconds(15));
            Assert.NotNull(result);
            Assert.Equal(baseTime.AddSeconds(10), result.StartDate);
        }

        [Fact]
        public void GetSegmentAtOrAfter_ShouldReturnSegment_WhenDateIsInside()
        {
            var clip = CreateClip((10, 20), (30, 40));
            var baseTime = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            var result = clip.GetSegmentAtOrAfter(baseTime.AddSeconds(15));
            Assert.NotNull(result);
            Assert.Equal(baseTime.AddSeconds(10), result.StartDate);
        }

        [Fact]
        public void GetSegmentAtOrAfter_ShouldReturnNextSegment_WhenDateIsInGap()
        {
            var clip = CreateClip((10, 20), (30, 40));
            var baseTime = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            var result = clip.GetSegmentAtOrAfter(baseTime.AddSeconds(25));
            Assert.NotNull(result);
            Assert.Equal(baseTime.AddSeconds(30), result.StartDate);
        }

        [Fact]
        public void GetSegmentAtOrAfter_ShouldReturnFirstSegment_WhenDateIsBeforeFirst()
        {
            var clip = CreateClip((10, 20), (30, 40));
            var baseTime = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            var result = clip.GetSegmentAtOrAfter(baseTime.AddSeconds(5));
            Assert.NotNull(result);
            Assert.Equal(baseTime.AddSeconds(10), result.StartDate);
        }

        [Fact]
        public void GetSegmentAtOrAfter_ShouldReturnNull_WhenDateIsAfterLast()
        {
            var clip = CreateClip((10, 20), (30, 40));
            var baseTime = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            var result = clip.GetSegmentAtOrAfter(baseTime.AddSeconds(45));
            Assert.Null(result);
        }
    }
}
