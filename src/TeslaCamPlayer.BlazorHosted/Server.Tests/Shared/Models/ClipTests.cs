using Xunit;
using TeslaCamPlayer.BlazorHosted.Shared.Models;
using System;

namespace TeslaCamPlayer.BlazorHosted.Server.Tests.Shared.Models
{
    public class ClipTests
    {
        private Clip CreateClip(DateTime start, int segmentCount)
        {
            var segments = new ClipVideoSegment[segmentCount];
            for (int i = 0; i < segmentCount; i++)
            {
                segments[i] = new ClipVideoSegment
                {
                    StartDate = start.AddMinutes(i),
                    EndDate = start.AddMinutes(i + 1)
                };
            }

            return new Clip(ClipType.Recent, segments);
        }

        [Fact]
        public void SegmentAtDate_ReturnsCorrectSegment_WhenDateIsInside()
        {
            var start = new DateTime(2023, 1, 1, 12, 0, 0);
            var clip = CreateClip(start, 3);
            // S1: 12:00-12:01, S2: 12:01-12:02, S3: 12:02-12:03

            var target = start.AddMinutes(1).AddSeconds(30); // 12:01:30 -> S2
            var segment = clip.SegmentAtDate(target);

            Assert.NotNull(segment);
            Assert.Equal(start.AddMinutes(1), segment.StartDate);
        }

        [Fact]
        public void SegmentAtDate_ReturnsCorrectSegment_WhenDateIsExactStart()
        {
            var start = new DateTime(2023, 1, 1, 12, 0, 0);
            var clip = CreateClip(start, 3);

            var target = start.AddMinutes(1); // 12:01:00 -> S1 or S2?
            // S1: 12:00-12:01. S2: 12:01-12:02.
            // Logic: StartDate <= date && EndDate >= date.
            // S1: 12:00 <= 12:01 (True) && 12:01 >= 12:01 (True). MATCH.
            // S2: 12:01 <= 12:01 (True) && 12:02 >= 12:01 (True). MATCH.
            // FirstOrDefault returns S1.

            var segment = clip.SegmentAtDate(target);

            Assert.NotNull(segment);
            Assert.Equal(start, segment.StartDate); // Should return S1 (index 0)
        }

        [Fact]
        public void SegmentAtDate_ReturnsCorrectSegment_WhenDateIsExactEnd()
        {
             var start = new DateTime(2023, 1, 1, 12, 0, 0);
            var clip = CreateClip(start, 1);
            // S1: 12:00-12:01

            var target = start.AddMinutes(1); // 12:01:00

            var segment = clip.SegmentAtDate(target);

            Assert.NotNull(segment);
            Assert.Equal(start, segment.StartDate);
        }

        [Fact]
        public void SegmentAtDate_ReturnsNull_WhenDateIsBefore()
        {
            var start = new DateTime(2023, 1, 1, 12, 0, 0);
            var clip = CreateClip(start, 3);

            var target = start.AddSeconds(-1);

            var segment = clip.SegmentAtDate(target);

            Assert.Null(segment);
        }

        [Fact]
        public void SegmentAtDate_ReturnsNull_WhenDateIsAfter()
        {
            var start = new DateTime(2023, 1, 1, 12, 0, 0);
            var clip = CreateClip(start, 3);
            // Ends at 12:03:00

            var target = start.AddMinutes(3).AddSeconds(1);

            var segment = clip.SegmentAtDate(target);

            Assert.Null(segment);
        }

        [Fact]
        public void SegmentAtDate_ReturnsNull_WhenDateIsInGap()
        {
             // Create clip with gap
            var s1 = new ClipVideoSegment { StartDate = new DateTime(2023, 1, 1, 12, 0, 0), EndDate = new DateTime(2023, 1, 1, 12, 1, 0) };
            var s2 = new ClipVideoSegment { StartDate = new DateTime(2023, 1, 1, 12, 2, 0), EndDate = new DateTime(2023, 1, 1, 12, 3, 0) };
            var clip = new Clip(ClipType.Recent, new[] { s1, s2 });

            var target = new DateTime(2023, 1, 1, 12, 1, 30); // 12:01:30

            var segment = clip.SegmentAtDate(target);

            Assert.Null(segment);
        }
    }
}
