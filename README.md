# TeslaCam Player

A self-hosted web player for viewing Tesla Sentry and Dashcam clips.

## Features

- **Multi-Camera Support:** View up to 8 camera angles simultaneously, including:
  - Front, Back, Left/Right Repeaters
  - Left/Right Pillars (New!)
  - Fisheye & Narrow (Legacy/Debug)
- **Synchronized Playback:** All video feeds are synced by timestamp.
- **Event Markers:** Visualize the exact moment of a Sentry event on the timeline.
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
    restart: unless-stopped
```

## Usage

1.  Open your browser and navigate to `http://<your-server-ip>:8080`.
2.  The player will scan the `/TeslaCam` directory for clips. *Note: Initial scan might take a moment if you have thousands of files.*
3.  **Syncing:**
    -   **Sync New:** Scans for new files only.
    -   **Full Resync:** Clears the database and rescans everything.
    -   *Scan history is saved in a `teslacam.db` file located in your mapped clips folder.*
4.  Select an event from the list.
5.  Use the timeline to scrub through the video. The event trigger is marked with a red dot.

## Troubleshooting

-   **No Clips Found:** Ensure the volume mount `/TeslaCam` inside the container points to the directory *containing* `SentryClips`, `SavedClips`, etc. If you mount `SentryClips` directly to `/TeslaCam`, it won't work.
-   **Missing Cameras:** Not all Tesla models record all cameras. The player will only show cameras present in the folder.
-   **Playback Issues:** This app relies on the browser's native MP4 playback. Ensure your browser supports H.264/H.265.

## Development

See [CONTRIBUTING.md](docs/CONTRIBUTING.md) for instructions on how to build and test locally.

## License

MIT
