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
        public const string CurrentVersion = "8.0";

        public static readonly List<VersionRelease> Releases = new List<VersionRelease>
        {
            new VersionRelease
            {
                Version = "8.0",
                Date = "2025-12-09",
                Changes = new List<string>
                {
                    "Merge pull request #22 from LordVaderXIII/jules/update-version-docs-10146979834830408376"
                }
            },
            new VersionRelease
            {
                Version = "7.9",
                Date = "2025-12-09",
                Changes = new List<string>
                {
                    "Bump version to 0.4 and update changelog"
                }
            },
            new VersionRelease
            {
                Version = "7.8",
                Date = "2025-12-09",
                Changes = new List<string>
                {
                    "Merge pull request #21 from LordVaderXIII/jules-api-integration"
                }
            },
            new VersionRelease
            {
                Version = "7.7",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "feat: Integrate Jules API for automated error reporting and bug fix PRs"
                }
            },
            new VersionRelease
            {
                Version = "7.6",
                Date = "2025-12-09",
                Changes = new List<string>
                {
                    "Merge pull request #20 from LordVaderXIII/fix/mobile-video-gap"
                }
            },
            new VersionRelease
            {
                Version = "7.5",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Fix mobile layout gap below video player"
                }
            },
            new VersionRelease
            {
                Version = "7.4",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Merge pull request #19 from LordVaderXIII/mobile-layout-fix"
                }
            },
            new VersionRelease
            {
                Version = "7.3",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Fix mobile video player size and increment version to 0.2"
                }
            },
            new VersionRelease
            {
                Version = "7.2",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Merge pull request #18 from LordVaderXIII/version-control-display"
                }
            },
            new VersionRelease
            {
                Version = "7.1",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "feat: add version control display and changelog"
                }
            },
            new VersionRelease
            {
                Version = "7.0",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Merge pull request #17 from LordVaderXIII/ios-video-fix"
                }
            },
            new VersionRelease
            {
                Version = "6.9",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "fix(frontend): prevent iOS native player takeover"
                }
            },
            new VersionRelease
            {
                Version = "6.8",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Merge pull request #16 from LordVaderXIII/feature/skip-buttons"
                }
            },
            new VersionRelease
            {
                Version = "6.7",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Add skip forward/backward 5s buttons to ClipViewer"
                }
            },
            new VersionRelease
            {
                Version = "6.6",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Merge pull request #15 from LordVaderXIII/mobile-friendly-ui"
                }
            },
            new VersionRelease
            {
                Version = "6.5",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Update UI to be mobile-friendly for iPhone 16 Pro Max"
                }
            },
            new VersionRelease
            {
                Version = "6.4",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Merge pull request #14 from LordVaderXIII/fix-export-bugs"
                }
            },
            new VersionRelease
            {
                Version = "6.3",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Fix export layout, green bars, camera labels, and download link"
                }
            },
            new VersionRelease
            {
                Version = "6.2",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Merge pull request #13 from LordVaderXIII/bugfix/export-ffmpeg-stream-specifier"
                }
            },
            new VersionRelease
            {
                Version = "6.1",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Fix FFmpeg invalid stream specifier in ExportService"
                }
            },
            new VersionRelease
            {
                Version = "6.0",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Merge pull request #12 from LordVaderXIII/fix-exports-and-persist-jobs"
                }
            },
            new VersionRelease
            {
                Version = "5.9",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Fix exports failing due to ffmpeg syntax error and persist export jobs in database."
                }
            },
            new VersionRelease
            {
                Version = "5.8",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Merge pull request #11 from LordVaderXIII/fix-export-ffmpeg-failure"
                }
            },
            new VersionRelease
            {
                Version = "5.7",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Fix export job failure by handling empty inputs and improving seek strategy"
                }
            },
            new VersionRelease
            {
                Version = "5.6",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Merge pull request #10 from LordVaderXIII/fix-export-bug-unknown-camera"
                }
            },
            new VersionRelease
            {
                Version = "5.5",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Fix export bug caused by Unknown camera and layout loop logic"
                }
            },
            new VersionRelease
            {
                Version = "5.4",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Merge pull request #9 from LordVaderXIII/clip-export-feature"
                }
            },
            new VersionRelease
            {
                Version = "5.3",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "feat: Add clip export feature with camera selection and timeline trimming"
                }
            },
            new VersionRelease
            {
                Version = "5.2",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Merge pull request #8 from LordVaderXIII/fix-build-cabin-camera"
                }
            },
            new VersionRelease
            {
                Version = "5.1",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Fix build failure by adding missing Cabin camera definitions"
                }
            },
            new VersionRelease
            {
                Version = "5.0",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Merge pull request #7 from LordVaderXIII/video-player-labels-favicon"
                }
            },
            new VersionRelease
            {
                Version = "4.9",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "feat: Add camera labels to video player and update favicon"
                }
            },
            new VersionRelease
            {
                Version = "4.8",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Merge pull request #6 from LordVaderXIII/feature/sqlite-sync"
                }
            },
            new VersionRelease
            {
                Version = "4.7",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Implement SQLite scan history and incremental sync"
                }
            },
            new VersionRelease
            {
                Version = "4.6",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Merge pull request #5 from LordVaderXIII/bugfix/grouping-and-deadlock"
                }
            },
            new VersionRelease
            {
                Version = "4.5",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Fix ClipsService loop logic and FfProbeService deadlock"
                }
            },
            new VersionRelease
            {
                Version = "4.4",
                Date = "2025-12-05",
                Changes = new List<string>
                {
                    "Merge pull request #4 from LordVaderXIII/bugfix/camera-swap-and-reload"
                }
            },
            new VersionRelease
            {
                Version = "4.3",
                Date = "2025-12-05",
                Changes = new List<string>
                {
                    "Fix camera swap interactions and clip reloading issues"
                }
            },
            new VersionRelease
            {
                Version = "4.2",
                Date = "2025-12-04",
                Changes = new List<string>
                {
                    "Merge pull request #3 from LordVaderXIII/bugfix/limit-concurrency"
                }
            },
            new VersionRelease
            {
                Version = "4.1",
                Date = "2025-12-04",
                Changes = new List<string>
                {
                    "Fix crash due to unlimited ffprobe process spawning"
                }
            },
            new VersionRelease
            {
                Version = "4.0",
                Date = "2025-12-04",
                Changes = new List<string>
                {
                    "Merge pull request #1 from LordVaderXIII/feature/add-pillar-cameras-and-layout"
                }
            },
            new VersionRelease
            {
                Version = "3.9",
                Date = "2025-12-04",
                Changes = new List<string>
                {
                    "feat: add support for pillar cameras and update viewer layout"
                }
            },
            new VersionRelease
            {
                Version = "3.8",
                Date = "2023-12-14",
                Changes = new List<string>
                {
                    "Use .NET 8 SDK"
                }
            },
            new VersionRelease
            {
                Version = "3.7",
                Date = "2023-12-14",
                Changes = new List<string>
                {
                    "Verygud"
                }
            },
            new VersionRelease
            {
                Version = "3.6",
                Date = "2023-12-14",
                Changes = new List<string>
                {
                    "Impreovements"
                }
            },
            new VersionRelease
            {
                Version = "3.5",
                Date = "2023-07-28",
                Changes = new List<string>
                {
                    "Merge branch 'release'"
                }
            },
            new VersionRelease
            {
                Version = "3.4",
                Date = "2023-07-28",
                Changes = new List<string>
                {
                    "Auto update docker hub readme"
                }
            },
            new VersionRelease
            {
                Version = "3.3",
                Date = "2023-07-28",
                Changes = new List<string>
                {
                    "Remove build dependency"
                }
            },
            new VersionRelease
            {
                Version = "3.2",
                Date = "2023-07-28",
                Changes = new List<string>
                {
                    "Run gulp for Windows build"
                }
            },
            new VersionRelease
            {
                Version = "3.1",
                Date = "2023-07-28",
                Changes = new List<string>
                {
                    "Merge branch 'master' into release"
                }
            },
            new VersionRelease
            {
                Version = "3.0",
                Date = "2023-07-28",
                Changes = new List<string>
                {
                    "Many things"
                }
            },
            new VersionRelease
            {
                Version = "2.9",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Merge branch 'release'"
                }
            },
            new VersionRelease
            {
                Version = "2.8",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Filter & refresh"
                }
            },
            new VersionRelease
            {
                Version = "2.7",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Build docker image again"
                }
            },
            new VersionRelease
            {
                Version = "2.6",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Needs version"
                }
            },
            new VersionRelease
            {
                Version = "2.5",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Add version env to release step"
                }
            },
            new VersionRelease
            {
                Version = "2.4",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Fix release"
                }
            },
            new VersionRelease
            {
                Version = "2.3",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Fix release"
                }
            },
            new VersionRelease
            {
                Version = "2.2",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Fix release"
                }
            },
            new VersionRelease
            {
                Version = "2.1",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Don't trim dependant release"
                }
            },
            new VersionRelease
            {
                Version = "2.0",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Fix name"
                }
            },
            new VersionRelease
            {
                Version = "1.9",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Build SC and DP releases"
                }
            },
            new VersionRelease
            {
                Version = "1.8",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Fix release"
                }
            },
            new VersionRelease
            {
                Version = "1.7",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Publish release"
                }
            },
            new VersionRelease
            {
                Version = "1.6",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Runs on"
                }
            },
            new VersionRelease
            {
                Version = "1.5",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Windows build"
                }
            },
            new VersionRelease
            {
                Version = "1.4",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Merge branch 'release'"
                }
            },
            new VersionRelease
            {
                Version = "1.3",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Fix missing gulp compile"
                }
            },
            new VersionRelease
            {
                Version = "1.2",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Docker path"
                }
            },
            new VersionRelease
            {
                Version = "1.1",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Write permissions"
                }
            },
            new VersionRelease
            {
                Version = "1.0",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Permissions"
                }
            },
            new VersionRelease
            {
                Version = "0.9",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Fix!"
                }
            },
            new VersionRelease
            {
                Version = "0.8",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Fix?"
                }
            },
            new VersionRelease
            {
                Version = "0.7",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Build action"
                }
            },
            new VersionRelease
            {
                Version = "0.6",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Small fixes, added docker"
                }
            },
            new VersionRelease
            {
                Version = "0.5",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Very gud, yes"
                }
            },
            new VersionRelease
            {
                Version = "0.4",
                Date = "2023-07-18",
                Changes = new List<string>
                {
                    "Remove sample project"
                }
            },
            new VersionRelease
            {
                Version = "0.3",
                Date = "2023-07-18",
                Changes = new List<string>
                {
                    "Date picking"
                }
            },
            new VersionRelease
            {
                Version = "0.2",
                Date = "2023-07-17",
                Changes = new List<string>
                {
                    "Ubiquity style timeline"
                }
            },
            new VersionRelease
            {
                Version = "0.1",
                Date = "2023-07-17",
                Changes = new List<string>
                {
                    "Initial commit"
                }
            }
        };
    }
}