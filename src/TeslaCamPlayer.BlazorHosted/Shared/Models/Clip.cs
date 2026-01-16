namespace TeslaCamPlayer.BlazorHosted.Shared.Models;

public class Clip
{
	public ClipType Type { get; }
	public ClipVideoSegment[] Segments { get; }
	public Event Event { get; init; }
	public DateTime StartDate { get; }
	public DateTime EndDate { get; }
	public double TotalSeconds { get; }
	public string ThumbnailUrl { get; init; }

	public Clip(ClipType type, ClipVideoSegment[] segments)
	{
		Type = type;
		Segments = segments.OrderBy(s => s.StartDate).ToArray();

		if (Segments.Length == 0)
		{
			StartDate = default;
			EndDate = default;
			TotalSeconds = 0;
			return;
		}

		// Optimization: Since Segments is already sorted by StartDate, accessing the first element is O(1)
		// compared to .Min() which is O(N).
		StartDate = Segments[0].StartDate;

		// Optimization: TeslaCam segments are sequential chunks. The last segment (by StartDate) will
		// also have the latest EndDate. Accessing by index is O(1) compared to .Max() which is O(N).
		EndDate = Segments[Segments.Length - 1].EndDate;

		TotalSeconds = EndDate.Subtract(StartDate).TotalSeconds;
	}

	public ClipVideoSegment SegmentAtDate(DateTime date)
		=> Segments.FirstOrDefault(s => s.StartDate <= date && s.EndDate >= date);
}