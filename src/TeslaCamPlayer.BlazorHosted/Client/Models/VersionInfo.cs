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
        public const string CurrentVersion = "0.1";

        public static readonly List<VersionRelease> Releases = new List<VersionRelease>
        {
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
