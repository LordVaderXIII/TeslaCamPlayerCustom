using System;
using TeslaCamPlayer.BlazorHosted.Shared.Models;

namespace TeslaCamPlayer.BlazorHosted.Server.Helpers;

public struct VideoFileMetadata
{
    public ClipType ClipType;
    public string EventFolderName; // null if none
    public DateTime Date;
    public Cameras Camera;
}

public static class TeslaCamFileParser
{
    public static bool TryParse(string path, out VideoFileMetadata metadata)
    {
        metadata = default;
        if (string.IsNullOrEmpty(path)) return false;

        var span = path.AsSpan();

        // 1. Check extension .mp4
        if (!span.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            return false;

        // 2. Parse Filename
        // Format: 2023-06-16_17-12-49-front.mp4
        // Length check: 19 (date) + 1 (dash) + min 4 (cam) + 4 (.mp4) = 28 chars minimum

        // Find last separator
        var lastSepIndex = span.LastIndexOfAny('/', '\\');
        var fileName = lastSepIndex == -1 ? span : span.Slice(lastSepIndex + 1);

        if (fileName.Length < 28) return false;

        // 3. Parse Date from Filename
        // 2023-06-16_17-12-49
        var dateSpan = fileName.Slice(0, 19);
        if (!TryParseDate(dateSpan, out var date))
            return false;
        metadata.Date = date;

        // 4. Parse Camera
        // -front.mp4
        // separator after date is at 19
        if (fileName[19] != '-') return false;

        var cameraPart = fileName.Slice(20, fileName.Length - 20 - 4); // remove extension
        if (!TryParseCamera(cameraPart, out var camera))
            return false;
        metadata.Camera = camera;

        // 5. Parse Directory Structure
        if (lastSepIndex == -1) return false; // Must be in a folder structure as per regex

        var dirSpan = span.Slice(0, lastSepIndex);

        // Check parent folder (could be Event or ClipType)
        var lastSepDir = dirSpan.LastIndexOfAny('/', '\\');
        var parentFolder = lastSepDir == -1 ? dirSpan : dirSpan.Slice(lastSepDir + 1);

        // Is parent folder a ClipType?
        if (TryParseClipType(parentFolder, out var clipType))
        {
            // Case: .../RecentClips/file.mp4
            metadata.ClipType = clipType;
            metadata.EventFolderName = null;
            return true;
        }

        // Case: .../SavedClips/EventFolder/file.mp4
        // Parent folder must be an event folder (Timestamp)
        // Regex for event: 20\d{2}\-[0-1][0-9]\-[0-3][0-9]_[0-2][0-9]\-[0-5][0-9]\-[0-5][0-9]
        // This is exactly the same format as file date: yyyy-MM-dd_HH-mm-ss
        if (!TryParseDate(parentFolder, out _)) // Just validate format
            return false;

        // It is an event folder. Store it.
        metadata.EventFolderName = parentFolder.ToString();

        // Now check Grandparent
        if (lastSepDir == -1) return false; // No grandparent

        var grandParentSpan = dirSpan.Slice(0, lastSepDir);
        var lastSepGrand = grandParentSpan.LastIndexOfAny('/', '\\');
        var grandParentFolder = lastSepGrand == -1 ? grandParentSpan : grandParentSpan.Slice(lastSepGrand + 1);

        if (TryParseClipType(grandParentFolder, out clipType))
        {
            metadata.ClipType = clipType;
            return true;
        }

        return false;
    }

    private static bool TryParseDate(ReadOnlySpan<char> span, out DateTime date)
    {
        date = default;
        // yyyy-MM-dd_HH-mm-ss
        if (span.Length != 19) return false;

        if (!int.TryParse(span.Slice(0, 4), out int year)) return false;
        if (span[4] != '-') return false;
        if (!int.TryParse(span.Slice(5, 2), out int month)) return false;
        if (span[7] != '-') return false;
        if (!int.TryParse(span.Slice(8, 2), out int day)) return false;
        if (span[10] != '_') return false;
        if (!int.TryParse(span.Slice(11, 2), out int hour)) return false;
        if (span[13] != '-') return false;
        if (!int.TryParse(span.Slice(14, 2), out int minute)) return false;
        if (span[16] != '-') return false;
        if (!int.TryParse(span.Slice(17, 2), out int second)) return false;

        try
        {
            date = new DateTime(year, month, day, hour, minute, second);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseCamera(ReadOnlySpan<char> span, out Cameras camera)
    {
        // back|front|left_repeater|right_repeater|left_pillar|right_pillar|fisheye|narrow

        if (span.Equals("front", StringComparison.Ordinal)) { camera = Cameras.Front; return true; }
        if (span.Equals("back", StringComparison.Ordinal)) { camera = Cameras.Back; return true; }
        if (span.Equals("left_repeater", StringComparison.Ordinal)) { camera = Cameras.LeftRepeater; return true; }
        if (span.Equals("right_repeater", StringComparison.Ordinal)) { camera = Cameras.RightRepeater; return true; }
        if (span.Equals("left_pillar", StringComparison.Ordinal)) { camera = Cameras.LeftBPillar; return true; }
        if (span.Equals("right_pillar", StringComparison.Ordinal)) { camera = Cameras.RightBPillar; return true; }
        if (span.Equals("fisheye", StringComparison.Ordinal)) { camera = Cameras.Fisheye; return true; }
        if (span.Equals("narrow", StringComparison.Ordinal)) { camera = Cameras.Narrow; return true; }

        camera = Cameras.Unknown;
        return false;
    }

    private static bool TryParseClipType(ReadOnlySpan<char> span, out ClipType clipType)
    {
        if (span.Equals("RecentClips", StringComparison.Ordinal)) { clipType = ClipType.Recent; return true; }
        if (span.Equals("SavedClips", StringComparison.Ordinal)) { clipType = ClipType.Saved; return true; }
        if (span.Equals("SentryClips", StringComparison.Ordinal)) { clipType = ClipType.Sentry; return true; }

        clipType = ClipType.Unknown;
        return false;
    }
}
