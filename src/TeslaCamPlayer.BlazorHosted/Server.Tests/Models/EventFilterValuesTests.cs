using System;
using TeslaCamPlayer.BlazorHosted.Client.Models;
using TeslaCamPlayer.BlazorHosted.Shared.Models;
using Xunit;

namespace TeslaCamPlayer.BlazorHosted.Server.Tests.Models;

public class EventFilterValuesTests
{
    private static Clip CreateClip(ClipType type, string? reason = null)
    {
        var segment = new ClipVideoSegment
        {
            StartDate = new DateTime(2023, 1, 1),
            EndDate = new DateTime(2023, 1, 1).AddMinutes(1)
        };

        var clip = new Clip(type, new[] { segment });

        if (reason != null)
        {
            return new Clip(type, new[] { segment })
            {
                Event = new Event { Reason = reason }
            };
        }

        return clip;
    }

    [Fact]
    public void IsInFilter_Recent()
    {
        var filter = new EventFilterValues { Recent = true };
        var clip = CreateClip(ClipType.Recent);
        Assert.True(filter.IsInFilter(clip));

        filter.Recent = false;
        Assert.False(filter.IsInFilter(clip));
    }

    [Fact]
    public void IsInFilter_Saved_Honk()
    {
        // By default DashcamHonk, DashcamSaved, DashcamOther are true
        var filter = new EventFilterValues {
            DashcamHonk = true,
            DashcamSaved = false,
            DashcamOther = false
        };
        var clip = CreateClip(ClipType.Saved, CamEvents.UserInteractionHonk);

        Assert.True(filter.IsInFilter(clip));

        filter.DashcamHonk = false;
        Assert.False(filter.IsInFilter(clip));
    }

    [Fact]
    public void IsInFilter_Saved_PanelSave()
    {
        var filter = new EventFilterValues {
            DashcamHonk = false,
            DashcamSaved = true,
            DashcamOther = false
        };
        var clip = CreateClip(ClipType.Saved, CamEvents.UserInteractionDashcamPanelSave);
        Assert.True(filter.IsInFilter(clip));

        filter.DashcamSaved = false;
        Assert.False(filter.IsInFilter(clip));
    }

    [Fact]
    public void IsInFilter_Saved_IconTapped()
    {
        var filter = new EventFilterValues {
            DashcamHonk = false,
            DashcamSaved = true,
            DashcamOther = false
        };
        var clip = CreateClip(ClipType.Saved, CamEvents.UserInteractionDashcamIconTapped);
        Assert.True(filter.IsInFilter(clip));
    }

    [Fact]
    public void IsInFilter_Saved_Other_IncludesHonk()
    {
        // DashcamOther = true should include Honk clips even if DashcamHonk = false
        var filter = new EventFilterValues {
            DashcamHonk = false,
            DashcamSaved = false,
            DashcamOther = true
        };
        var clip = CreateClip(ClipType.Saved, CamEvents.UserInteractionHonk);
        Assert.True(filter.IsInFilter(clip));
    }

    [Fact]
    public void IsInFilter_Saved_Other_IncludesUnknownReason()
    {
        var filter = new EventFilterValues {
            DashcamHonk = false,
            DashcamSaved = false,
            DashcamOther = true
        };
        var clip = CreateClip(ClipType.Saved, "some_unknown_reason");
        Assert.True(filter.IsInFilter(clip));
    }

    [Fact]
    public void IsInFilter_Sentry_ObjectDetection()
    {
        var filter = new EventFilterValues {
            SentryObjectDetection = true,
            SentryAccelerationDetection = false,
            SentryOther = false
        };
        var clip = CreateClip(ClipType.Sentry, CamEvents.SentryAwareObjectDetection);
        Assert.True(filter.IsInFilter(clip));

        filter.SentryObjectDetection = false;
        Assert.False(filter.IsInFilter(clip));
    }

    [Fact]
    public void IsInFilter_Sentry_Acceleration()
    {
        var filter = new EventFilterValues {
            SentryObjectDetection = false,
            SentryAccelerationDetection = true,
            SentryOther = false
        };
        var clip = CreateClip(ClipType.Sentry, CamEvents.SentryAwareAccelerationPrefix + "0.5");
        Assert.True(filter.IsInFilter(clip));

        filter.SentryAccelerationDetection = false;
        Assert.False(filter.IsInFilter(clip));
    }

    [Fact]
    public void IsInFilter_Sentry_Other_IncludesObjectDetection()
    {
        // SentryOther = true should include Sentry clips even if specific filters are false
        var filter = new EventFilterValues {
            SentryObjectDetection = false,
            SentryAccelerationDetection = false,
            SentryOther = true
        };
        var clip = CreateClip(ClipType.Sentry, CamEvents.SentryAwareObjectDetection);
        Assert.True(filter.IsInFilter(clip));
    }

    [Fact]
    public void IsInFilter_MixedTypes()
    {
        // Recent=true, but clip is Saved -> False
        var filter = new EventFilterValues {
            Recent = true,
            DashcamHonk = false,
            DashcamSaved = false,
            DashcamOther = false
        };
        var clip = CreateClip(ClipType.Saved, "some_reason");
        Assert.False(filter.IsInFilter(clip));
    }

    [Fact]
    public void IsInFilter_NullEvent()
    {
        var filter = new EventFilterValues { DashcamOther = true };
        var clip = CreateClip(ClipType.Saved); // Event is null
        Assert.True(filter.IsInFilter(clip)); // Should be included by DashcamOther checking Type=Saved
    }
}
