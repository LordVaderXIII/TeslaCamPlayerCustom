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
			// Optimization: Switch on Type first to avoid unnecessary string checks for other types.
			switch (clip.Type)
			{
				case ClipType.Recent:
					return Recent;

				case ClipType.Saved:
					// If "Other" is enabled, it includes all Saved clips.
					// This acts as a catch-all for this type, skipping expensive string checks.
					if (DashcamOther)
						return true;

					var savedReason = clip.Event?.Reason;
					if (DashcamHonk && savedReason == CamEvents.UserInteractionHonk)
						return true;

					if (DashcamSaved && (savedReason is CamEvents.UserInteractionDashcamPanelSave or CamEvents.UserInteractionDashcamIconTapped))
						return true;

					return false;

				case ClipType.Sentry:
					// If "Other" is enabled, it includes all Sentry clips.
					if (SentryOther)
						return true;

					var sentryReason = clip.Event?.Reason;
					if (SentryObjectDetection && sentryReason == CamEvents.SentryAwareObjectDetection)
						return true;

					if (SentryAccelerationDetection && sentryReason?.StartsWith(CamEvents.SentryAwareAccelerationPrefix) == true)
						return true;

					return false;

				default:
					return false;
			}
		}
	}
}
