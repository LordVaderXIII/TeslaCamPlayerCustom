using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TeslaCamPlayer.BlazorHosted.Server.Services;
using TeslaCamPlayer.BlazorHosted.Shared.Models;
using Xunit;
using Xunit.Abstractions;

namespace TeslaCamPlayer.BlazorHosted.Server.Tests.Services
{
    public class ClipsServiceTests
    {
        private readonly ITestOutputHelper _output;

        public ClipsServiceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void GetRecentClips_Benchmark()
        {
            // Setup
            var videoFiles = new List<VideoFile>();
            var startDate = new DateTime(2023, 1, 1, 12, 0, 0);

            // Generate 20000 minutes of footage -> 160,000 files
            // Increased to make sure we see a difference
            int minutes = 20000;

            for (int i = 0; i < minutes; i++)
            {
                var time = startDate.AddMinutes(i);
                videoFiles.Add(CreateVideoFile(time, Cameras.Front));
                videoFiles.Add(CreateVideoFile(time, Cameras.Back));
                videoFiles.Add(CreateVideoFile(time, Cameras.LeftRepeater));
                videoFiles.Add(CreateVideoFile(time, Cameras.RightRepeater));
                videoFiles.Add(CreateVideoFile(time, Cameras.LeftBPillar));
                videoFiles.Add(CreateVideoFile(time, Cameras.RightBPillar));
                videoFiles.Add(CreateVideoFile(time, Cameras.Fisheye));
                videoFiles.Add(CreateVideoFile(time, Cameras.Narrow));
            }

            // Shuffle them to make it realistic
            var rnd = new Random(42);
            var shuffledFiles = videoFiles.OrderBy(x => rnd.Next()).ToList();

            // Warmup
            ClipsService.GetRecentClips(shuffledFiles.Take(800).ToList()).ToList();

            // Benchmark
            var stopwatch = Stopwatch.StartNew();
            var clips = ClipsService.GetRecentClips(shuffledFiles).ToList();
            stopwatch.Stop();

            _output.WriteLine($"Processed {shuffledFiles.Count} files into {clips.Count} clips in {stopwatch.ElapsedMilliseconds} ms");

            Assert.NotEmpty(clips);
        }

        private VideoFile CreateVideoFile(DateTime startDate, Cameras camera)
        {
            return new VideoFile
            {
                StartDate = startDate,
                Duration = TimeSpan.FromMinutes(1),
                Camera = camera,
                ClipType = ClipType.Recent,
                FilePath = $"C:\\TeslaCam\\RecentClips\\{startDate:yyyy-MM-dd_HH-mm-ss}-{camera}.mp4"
            };
        }
    }
}
