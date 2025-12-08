using System;
using System.Collections.Generic;

namespace TeslaCamPlayer.BlazorHosted.Shared.Models;

public class ExportRequest
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<Cameras> SelectedCameras { get; set; } = new();
    public Cameras MainCamera { get; set; }
    public bool IncludeAudio { get; set; }
    // We might need to pass the clip details or just the raw video file paths if the client knows them.
    // However, the client knows the 'Clip' object which contains Segments.
    // Passing the Clip Identifier (Event Folder Name + Timestamp) is probably better,
    // but the Clip object logic is complex (segments, etc).
    // Let's pass the EventFolderName and the StartTime of the Clip, so the server can reconstruct or find the clip.
    // Actually, simpler: Pass the list of VideoFile paths involved? No, that's too much data.
    // Pass the Clip's StartDate (which uniquely identifies it usually with EventFolderName).

    public string EventFolderName { get; set; }
    public DateTime ClipStartDate { get; set; }
}
