using System;
using TeslaCamPlayer.BlazorHosted.Shared.Models;

namespace TeslaCamPlayer.BlazorHosted.Client.Models
{
	public class EventFilterValues
	{
		public bool DashcamHonk { get; set; } = true;

		public bool DashcamSaved { get; set; } = true;

		public bool DashcamOther { get; set; } = true;

		public bool SentryObjectDetection { get; set; } = true;

		public bool SentryAccelerationDetection { get; set; } = true;

		public bool SentryOther { get; set; } = true;

		public bool Recent { get; set; } = true;

		public bool IsInFilter(Clip clip)
		{
            // Optimization: Cache properties to avoid repeated access and reduce overhead
            var type = clip.Type;

			// Check broad category filters first (Type-based)
			// These allow skipping reason checks entirely if the whole category is enabled
			if (Recent && type == ClipType.Recent)
				return true;
			
			if (DashcamOther && type == ClipType.Saved)
				return true;

			if (SentryOther && type == ClipType.Sentry)
				return true;

			// Check specific reason filters (Type-agnostic)
			var reason = clip.Event?.Reason;
			if (reason == null)
				return false;

			if (DashcamHonk && reason == CamEvents.UserInteractionHonk)
				return true;

			if (DashcamSaved && (reason == CamEvents.UserInteractionDashcamPanelSave || reason == CamEvents.UserInteractionDashcamIconTapped))
				return true;

			if (SentryObjectDetection && reason == CamEvents.SentryAwareObjectDetection)
				return true;

			if (SentryAccelerationDetection && reason.StartsWith(CamEvents.SentryAwareAccelerationPrefix, StringComparison.Ordinal))
				return true;

			return false;
		}
	}
}
