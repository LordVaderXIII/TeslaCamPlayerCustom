using System.Collections.Generic;

namespace TeslaCamPlayer.BlazorHosted.Client.Models
{
    public class VersionRelease
    {
        public string Version { get; set; }
        public string Date { get; set; }
        public List<string> Changes { get; set; }
    }

    public static class VersionInfo
    {
        public const string CurrentVersion = "0.4";

        public static readonly List<VersionRelease> Releases = new List<VersionRelease>
        {
            new VersionRelease
            {
                Version = "0.4",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Added Jules API integration for automated error reporting and bug fixes.",
                    "Implemented persistent storage for export jobs using SQLite database.",
                    "Added ability to swap main and side camera views by clicking on a side view.",
                    "Added `playsinline` attribute to video player to prevent native player takeover on iOS."
                }
            },
            new VersionRelease
            {
                Version = "0.3",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Fixed layout gap below video player on mobile devices by enforcing correct flex behavior."
                }
            },
            new VersionRelease
            {
                Version = "0.2",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Fixed mobile layout issue where video frame was too large.",
                    "Improved mobile responsiveness for video player."
                }
            },
            new VersionRelease
            {
                Version = "0.1",
                Date = "2024-05-21",
                Changes = new List<string>
                {
                    "Initial version with version control tracking.",
                    "Added version display in top right corner.",
                    "Added changelog dialog."
                }
            }
        };
    }
}
