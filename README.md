# TeslaCam Player

A self-hosted web player for viewing Tesla Sentry and Dashcam clips.

## Features

- **Multi-Camera Support:** View up to 8 camera angles simultaneously, including:
  - Front, Back, Left/Right Repeaters
  - Left/Right Pillars (New!)
  - Fisheye & Narrow (Legacy/Debug)
- **Synchronized Playback:** All video feeds are synced by timestamp.
- **Event Markers:** Visualize the exact moment of a Sentry event on the timeline.
- **Clip Export:** Export custom clips with multiple camera angles merged into a single video file.
- **Responsive Layout:** Large main view (Front) with a grid of side cameras.
- **Dockerized:** Easy deployment on any Docker host (Unraid, Synology, Linux, etc.).

## Prerequisites

- **Docker:** You need a machine running Docker.
- **TeslaCam Clips:** Access to your TeslaCam folder (USB drive or network copy). The folder structure should look like `TeslaCam/{RecentClips,SavedClips,SentryClips}`.

## Installation

### Unraid

1.  **Community Applications:** (If available) Search for "TeslaCamPlayer".
2.  **Manual Install (Docker Template):**
    - Go to the **Docker** tab.
    - Click **Add Container**.
    - Switch to **Advanced View** (optional, but helps).
    - **Name:** TeslaCamPlayer
    - **Repository:** `ghcr.io/yourusername/teslacamplayer:latest` (Replace with actual image if hosted, or build locally)
    - **Network Type:** Bridge
    - **Web Port:**
        - Container Port: `80`
        - Host Port: `8080` (or any free port)
    - **Clips Volume:**
        - Container Path: `/TeslaCam`
        - Host Path: `/mnt/user/appdata/teslacam/clips` (Path to your clips)
    - Click **Apply**.

### Docker Run

```bash
docker run -d \
  --name teslacam-player \
  -p 8080:80 \
  -v /path/to/your/TeslaCam:/TeslaCam \
  --restart unless-stopped \
  ghcr.io/yourusername/teslacamplayer:latest
```

### Docker Compose

```yaml
version: '3'
services:
  teslacam:
    image: ghcr.io/yourusername/teslacamplayer:latest
    ports:
      - "8080:80"
    volumes:
      - /path/to/your/TeslaCam:/TeslaCam
    environment:
      - JULES_API_KEY=your_jules_api_key_here
      - JULES_SOURCE=sources/github/your_github_username/your_repo_name
      - RESET_AUTH=false # Set to true to reset authentication to OFF
    restart: unless-stopped
```

## Authentication

Version 1.0 introduces a simple authentication system (disabled by default).

- **Enable Auth:** Click the User Profile icon (top right) -> Settings -> Toggle "Enable Authentication".
- **Reset Auth:** If you get locked out, set the environment variable `RESET_AUTH=true` in your Docker configuration and restart the container. This will disable authentication, allowing you to log in as Admin without a password and reconfigure it.

## Error Reporting and Auto-Fixes

This application integrates with the **Jules API** to automatically report backend errors and request bug fixes via GitHub Pull Requests.

To enable this feature, you must configure the following environment variables in your Docker container:

- `JULES_API_KEY`: Your Jules API Key. (See [Jules Docs](https://jules.google/docs/api/reference/authentication) to generate one).
- `JULES_SOURCE`: The Jules Source identifier for your repository, in the format `sources/github/OWNER/REPO`.

**Privacy Note:** Error reports include the error message, stack trace, application version, and environment type. Code snippets from the stack trace may be included if available.

## Usage

1.  Open your browser and navigate to `http://<your-server-ip>:8080`.
2.  The player will scan the `/TeslaCam` directory for clips. *Note: Initial scan might take a moment if you have thousands of files.*
3.  **Syncing:**
    -   **Sync New:** Scans for new files only.
    -   **Full Resync:** Clears the database and rescans everything.
    -   *Scan history is saved in a `teslacam.db` file located in your mapped clips folder.*
4.  Select an event from the list.
5.  Use the timeline to scrub through the video. The event trigger is marked with a red dot.
6.  **Exporting Clips:**
    - Click the **Export Clip** (movie creation icon) button in the toolbar.
    - Drag the green (start) and red (end) brackets on the timeline to select the export range.
    - Use the checkboxes on each camera view to include or exclude specific cameras.
    - Click **Export** to start the job.
    - Click the **Downloads** icon to view export status and download completed clips.

## Troubleshooting

-   **No Clips Found:** Ensure the volume mount `/TeslaCam` inside the container points to the directory *containing* `SentryClips`, `SavedClips`, etc. If you mount `SentryClips` directly to `/TeslaCam`, it won't work.
-   **Missing Cameras:** Not all Tesla models record all cameras. The player will only show cameras present in the folder.
-   **Playback Issues:** This app relies on the browser's native MP4 playback. Ensure your browser supports H.264/H.265.

## Development

See [CONTRIBUTING.md](docs/CONTRIBUTING.md) for instructions on how to build and test locally.

## License

MIT
