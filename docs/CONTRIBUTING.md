# Contributing to TeslaCam Player

## Project Structure

- **Backend:** ASP.NET Core (.NET 7/8). Handles file scanning, metadata parsing (ffmpeg), and serving video files.
- **Frontend:** Blazor WebAssembly. Handles the UI, video synchronization, and user interaction.

## Prerequisites

- .NET 8 SDK
- Node.js (for building frontend styles via Gulp)
- FFmpeg (required for runtime duration analysis)

## Local Setup

1.  **Clone the repo:**
    ```bash
    git clone https://github.com/yourusername/teslacamplayer.git
    cd teslacamplayer
    ```

2.  **Generate Test Data:**
    Use the provided script to create dummy clips.
    ```bash
    ./generate_dummy_clips.sh
    ```

3.  **Run the Server:**
    You can run the server directly, pointing it to your dummy clips.
    ```bash
    cd src/TeslaCamPlayer.BlazorHosted/Server
    # Set the environment variable for clips path
    export ClipsRootPath="../../../dummy_clips/TeslaCam"
    dotnet run
    ```
    The app should be available at `https://localhost:7198` (or similar, check console output).

4.  **Frontend Styles:**
    If you edit SCSS files:
    ```bash
    cd src/TeslaCamPlayer.BlazorHosted/Client
    npm install
    npm install -g gulp
    gulp default # or gulp watch
    ```

## Testing

-   **Dummy Clips:** The `generate_dummy_clips.sh` script creates a `SentryClips` folder with files named correctly for the parser (including `left_pillar`, `right_pillar`, etc.).
-   **Verification:** Open the web UI, select the "2024-05-20" event. Verify that the Main View (Front) and Side Views (Pillars, Repeaters, etc.) load correctly.

## Submission Process

1.  Fork the repo.
2.  Create a branch.
3.  Make changes.
4.  Submit a Pull Request.
