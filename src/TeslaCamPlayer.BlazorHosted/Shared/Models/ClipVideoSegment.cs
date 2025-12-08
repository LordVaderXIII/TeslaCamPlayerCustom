namespace TeslaCamPlayer.BlazorHosted.Shared.Models;

public class ClipVideoSegment
{
	public DateTime StartDate { get; init; }
	public DateTime EndDate { get; init; }
	public VideoFile CameraFront { get; init; }
	public VideoFile CameraLeftRepeater { get; init; }
	public VideoFile CameraRightRepeater { get; init; }
	public VideoFile CameraBack { get; init; }
	public VideoFile CameraLeftBPillar { get; init; }
	public VideoFile CameraRightBPillar { get; init; }
	public VideoFile CameraFisheye { get; init; }
	public VideoFile CameraNarrow { get; init; }
	public VideoFile CameraCabin { get; init; }

	public VideoFile[] VideoFiles => [
		CameraFront,
		CameraLeftRepeater,
		CameraRightRepeater,
		CameraBack,
		CameraLeftBPillar,
		CameraRightBPillar,
		CameraFisheye,
		CameraNarrow,
		CameraCabin
	];

	public int CameraAnglesCount() => VideoFiles.Count(f => f != null);

}