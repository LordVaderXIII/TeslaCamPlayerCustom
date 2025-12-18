using System;
using TeslaCamPlayer.BlazorHosted.Server.Helpers;
using Xunit;

namespace TeslaCamPlayer.BlazorHosted.Server.Tests.Helpers;

public class ParseFfProbeOutputHelperTests
{
    [Fact]
    public void GetDuration_ShouldParseStandardFfProbeOutput()
    {
        // Arrange
        var output = @"
ffmpeg version 4.4 Copyright (c) 2000-2021 the FFmpeg developers
  built with gcc 10.3.0 (Alpine 10.3.1_git20210424)
  configuration: ...
  libavutil      56. 70.100 / 56. 70.100
  libavcodec     58.134.100 / 58.134.100
  libavformat    58. 76.100 / 58. 76.100
  libavdevice    58. 13.100 / 58. 13.100
  libavfilter     7.110.100 /  7.110.100
  libswscale      5.  9.100 /  5.  9.100
  libswresample   3.  9.100 /  3.  9.100
  libpostproc    55.  9.100 / 55.  9.100
Input #0, mov,mp4,m4a,3gp,3g2,mj2, from 'video.mp4':
  Metadata:
    major_brand     : mp42
    minor_version   : 0
    compatible_brands: mp42mp41
    creation_time   : 2023-06-16T17:18:06.000000Z
  Duration: 00:00:59.03, start: 0.000000, bitrate: 4426 kb/s
    Stream #0:0(eng): Video: h264 (Main) (avc1 / 0x31637661), yuv420p(tv, bt709), 1280x960, 4423 kb/s, 36.17 fps, 36.17 tbr, 90k tbn, 180k tbc (default)
    Metadata:
      creation_time   : 2023-06-16T17:18:06.000000Z
      handler_name    : ?Main_Video_Media_Handler
      vendor_id       : [0][0][0][0]
      encoder         : AVC Coding";

        // Act
        var result = ParseFfProbeOutputHelper.GetDuration(output);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(59, result.Value.Seconds);
        // Note: The legacy parser extracts 30ms from ".03" correctly based on the regex capture group logic in .NET
        Assert.Equal(30, result.Value.Milliseconds);
    }

    [Theory]
    [InlineData("59.033000", 59.033)]
    [InlineData("123.456", 123.456)]
    [InlineData("0.5", 0.5)]
    [InlineData("  60.000  ", 60.0)]
    public void GetDuration_ShouldParseOptimizedOutput(string output, double expectedSeconds)
    {
        // Act
        var result = ParseFfProbeOutputHelper.GetDuration(output);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedSeconds, result.Value.TotalSeconds, 4);
    }
}
