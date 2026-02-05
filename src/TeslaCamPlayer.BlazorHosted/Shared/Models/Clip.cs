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
	{
		var result = GetSegmentAtOrAfter(date);
		if (result != null && result.StartDate <= date && result.EndDate >= date)
		{
			return result;
		}
		return null;
	}

	/// <summary>
	/// Uses binary search to find the segment containing the date, or the next segment if the date is in a gap.
	/// Returns null if the date is after the last segment.
	/// </summary>
	public ClipVideoSegment GetSegmentAtOrAfter(DateTime date)
	{
		if (Segments.Length == 0)
			return null;

		int left = 0;
		int right = Segments.Length - 1;

		while (left <= right)
		{
			int mid = left + (right - left) / 2;
			var segment = Segments[mid];

			if (segment.StartDate == date)
				return segment;

			if (segment.StartDate < date)
				left = mid + 1;
			else
				right = mid - 1;
		}

		// 'left' is now the index of the first segment with StartDate > date.

		// Check if it falls within the previous segment (if any)
		if (left > 0)
		{
			var prevSegment = Segments[left - 1];
			if (prevSegment.EndDate >= date)
			{
				return prevSegment;
			}
		}

		// If we are here, date is either before first segment (left=0) or in a gap.
		// We return the next segment (at index 'left'), or null if we are at the end.
		if (left < Segments.Length)
		{
			return Segments[left];
		}

		return null;
	}
}