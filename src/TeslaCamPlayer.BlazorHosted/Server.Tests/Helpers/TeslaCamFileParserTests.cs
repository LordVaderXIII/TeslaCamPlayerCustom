using Xunit;
using TeslaCamPlayer.BlazorHosted.Server.Helpers;
using TeslaCamPlayer.BlazorHosted.Shared.Models;
using System;

namespace TeslaCamPlayer.BlazorHosted.Server.Tests.Helpers;

public class TeslaCamFileParserTests
{
    [Theory]
    [InlineData("C:\\TeslaCam\\SavedClips\\2023-06-16_17-18-06\\2023-06-16_17-12-49-front.mp4", ClipType.Saved, "2023-06-16_17-18-06", Cameras.Front)]
    [InlineData("/mnt/TeslaCam/SavedClips/2023-06-16_17-18-06/2023-06-16_17-12-49-back.mp4", ClipType.Saved, "2023-06-16_17-18-06", Cameras.Back)]
    [InlineData("/TeslaCam/SentryClips/2023-01-01_12-00-00/2023-01-01_12-00-00-left_repeater.mp4", ClipType.Sentry, "2023-01-01_12-00-00", Cameras.LeftRepeater)]
    [InlineData("/TeslaCam/RecentClips/2023-01-01_12-00-00-right_pillar.mp4", ClipType.Recent, null, Cameras.RightBPillar)]
    [InlineData("RecentClips/2023-01-01_12-00-00-fisheye.mp4", ClipType.Recent, null, Cameras.Fisheye)]
    [InlineData("SavedClips/2023-06-16_17-18-06/2023-06-16_17-12-49-narrow.mp4", ClipType.Saved, "2023-06-16_17-18-06", Cameras.Narrow)]
    public void TryParse_ValidPaths_ReturnsTrue(string path, ClipType expectedType, string? expectedEvent, Cameras expectedCamera)
    {
        // Act
        var result = TeslaCamFileParser.TryParse(path, out var metadata);

        // Assert
        Assert.True(result, $"Failed to parse path: {path}");
        Assert.Equal(expectedType, metadata.ClipType);
        Assert.Equal(expectedEvent, metadata.EventFolderName);
        Assert.Equal(expectedCamera, metadata.Camera);
    }

    [Theory]
    [InlineData("invalid.mp4")] // too short
    [InlineData("2023-06-16_17-12-49-unknown.mp4")] // unknown camera
    [InlineData("SavedClips/NotATimestamp/2023-06-16_17-12-49-front.mp4")] // invalid event folder
    [InlineData("UnknownClips/2023-06-16_17-18-06/2023-06-16_17-12-49-front.mp4")] // unknown type
    [InlineData("2023-06-16_17-12-49-front.txt")] // wrong extension
    public void TryParse_InvalidPaths_ReturnsFalse(string path)
    {
        var result = TeslaCamFileParser.TryParse(path, out _);
        Assert.False(result, $"Should fail to parse path: {path}");
    }
}
