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
        public const string CurrentVersion = "2025-12-1.3.6";

        public static readonly List<VersionRelease> Releases = new List<VersionRelease>
        {
            new VersionRelease
            {
                Version = "2025-12-1.3.6",
                Date = "2025-12-19",
                Changes = new List<string>
                {
                    "perf: Optimize video player event binding to reduce JS Interop overhead"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-1.3.5",
                Date = "2025-12-11",
                Changes = new List<string>
                {
                    "Fix cache issue preventing 3D calibration mode from loading (missing JS function)"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-1.3.4",
                Date = "2025-12-11",
                Changes = new List<string>
                {
                    "perf: Cache event.json reads to reduce disk I/O"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-1.3.3",
                Date = "2025-12-11",
                Changes = new List<string>
                {
                    "Fix broken access control by enforcing authentication on API endpoints",
                    "Fix logs inaccessibility when authentication is disabled"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-1.3.2",
                Date = "2025-12-09",
                Changes = new List<string>
                {
                    "Fix mobile version display position"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-1.3.1",
                Date = "2025-12-09",
                Changes = new List<string>
                {
                    "Fix mobile 3D view layout (full screen) and consolidate exit buttons"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-1.3.0",
                Date = "2025-12-09",
                Changes = new List<string>
                {
                    "Fix 3D mode zoom functionality (now uses FOV)",
                    "Fix 3D mode exit toggle reliability"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-1.2.1",
                Date = "2025-12-09",
                Changes = new List<string>
                {
                    "Fix browser crash by limiting log entries to 1000"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-1.2.0",
                Date = "2025-12-09",
                Changes = new List<string>
                {
                    "feat: Add logs section with Jules error reporting"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-1.1.0",
                Date = "2025-12-09",
                Changes = new List<string>
                {
                    "feat: Add playback speed control"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-1.0.2",
                Date = "2025-12-09",
                Changes = new List<string>
                {
                    "Fix build failure due to missing AuthorizeAttribute namespace"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-1.0.1",
                Date = "2025-12-09",
                Changes = new List<string>
                {
                    "Fix build failure due to naming conflict in AuthController"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-1.0",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "feat: Implement authentication system with login screen and user settings",
                    "feat: Add user profile icon with settings menu"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-0.9",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "feat: Integrate Jules API for automated error reporting and bug fix PRs"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-0.8.4",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Fix mobile layout gap below video player"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-0.8.2",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Fix mobile video player size"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-0.8",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "feat: add version control display and changelog"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-0.7.1",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "fix(frontend): prevent iOS native player takeover"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-0.6.13",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Add skip forward/backward 5s buttons to ClipViewer"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-0.6.11",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Update UI to be mobile-friendly for iPhone 16 Pro Max"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-0.6.9",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Fix export layout, green bars, camera labels, and download link"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-0.6.7",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Fix FFmpeg invalid stream specifier in ExportService"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-0.6.5",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Fix exports failing due to ffmpeg syntax error and persist export jobs in database."
                }
            },
            new VersionRelease
            {
                Version = "2025-12-0.6.3",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Fix export job failure by handling empty inputs and improving seek strategy"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-0.6.1",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Fix export bug caused by Unknown camera and layout loop logic"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-0.5",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "feat: Add clip export feature with camera selection and timeline trimming"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-0.4.2",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Fix build failure by adding missing Cabin camera definitions"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-0.4",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "feat: Add camera labels to video player and update favicon"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-0.2.7",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Implement SQLite scan history and incremental sync"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-0.2.5",
                Date = "2025-12-08",
                Changes = new List<string>
                {
                    "Fix ClipsService loop logic and FfProbeService deadlock"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-0.2.3",
                Date = "2025-12-05",
                Changes = new List<string>
                {
                    "Fix camera swap interactions and clip reloading issues"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-0.2.1",
                Date = "2025-12-04",
                Changes = new List<string>
                {
                    "Fix crash due to unlimited ffprobe process spawning"
                }
            },
            new VersionRelease
            {
                Version = "2025-12-0.1",
                Date = "2025-12-04",
                Changes = new List<string>
                {
                    "feat: add support for pillar cameras and update viewer layout"
                }
            },
            new VersionRelease
            {
                Version = "---",
                Date = "---",
                Changes = new List<string>
                {
                    "Forked from Rene-Sackers/TeslaCamPlayer:master due to inactivity."
                }
            },
            new VersionRelease
            {
                Version = "2023-12-0.1.2",
                Date = "2023-12-14",
                Changes = new List<string>
                {
                    "Use .NET 8 SDK"
                }
            },
            new VersionRelease
            {
                Version = "2023-12-0.1.1",
                Date = "2023-12-14",
                Changes = new List<string>
                {
                    "Verygud"
                }
            },
            new VersionRelease
            {
                Version = "2023-12-0.1",
                Date = "2023-12-14",
                Changes = new List<string>
                {
                    "Impreovements"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.34",
                Date = "2023-07-28",
                Changes = new List<string>
                {
                    "Merge branch 'release'"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.33",
                Date = "2023-07-28",
                Changes = new List<string>
                {
                    "Auto update docker hub readme"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.32",
                Date = "2023-07-28",
                Changes = new List<string>
                {
                    "Remove build dependency"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.31",
                Date = "2023-07-28",
                Changes = new List<string>
                {
                    "Run gulp for Windows build"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.30",
                Date = "2023-07-28",
                Changes = new List<string>
                {
                    "Merge branch 'master' into release"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.29",
                Date = "2023-07-28",
                Changes = new List<string>
                {
                    "Many things"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.28",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Merge branch 'release'"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.27",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Filter & refresh"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.26",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Build docker image again"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.25",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Needs version"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.24",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Add version env to release step"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.23",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Fix release"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.22",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Fix release"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.21",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Fix release"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.20",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Don't trim dependant release"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.19",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Fix name"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.18",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Build SC and DP releases"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.17",
                Date = "2023-07-23",
                Changes = new List<string>
                {
                    "Fix release"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.16",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Publish release"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.15",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Runs on"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.14",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Windows build"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.13",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Merge branch 'release'"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.12",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Fix missing gulp compile"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.11",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Docker path"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.10",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Write permissions"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.9",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Permissions"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.8",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Fix!"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.7",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Fix?"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.6",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Build action"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.5",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Small fixes, added docker"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.4",
                Date = "2023-07-20",
                Changes = new List<string>
                {
                    "Very gud, yes"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.3",
                Date = "2023-07-18",
                Changes = new List<string>
                {
                    "Remove sample project"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.2",
                Date = "2023-07-18",
                Changes = new List<string>
                {
                    "Date picking"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1.1",
                Date = "2023-07-17",
                Changes = new List<string>
                {
                    "Ubiquity style timeline"
                }
            },
            new VersionRelease
            {
                Version = "2023-07-0.1",
                Date = "2023-07-17",
                Changes = new List<string>
                {
                    "Initial commit"
                }
            }
        };
    }
}