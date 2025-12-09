# TeslaCamPlayer Agent Guidelines

This file describes the agents and tools in this codebase, providing context on how to interact with them and conventions to follow. Jules uses this file to better understand the code and generate more relevant plans and completions.

## Project Overview
This project is a .NET 8 Blazor Hosted application designed to view and export video clips from Tesla vehicles. It is containerized using Docker and often deployed on Unraid.

### Structure
- **Client**: Blazor WebAssembly frontend.
- **Server**: ASP.NET Core backend API.
- **Shared**: Shared models and logic.

## Key Services
### Server
- **`ClipsService`**: Scans the clips directory, parses filenames, and builds the clip database. Handles concurrency with `SemaphoreSlim`.
- **`JulesApiService`**: Integrates with the Jules AI API to report errors and request automated bug fixes. Limits sessions to 5 per day.
- **`ExportService`**: Manages video export jobs. Uses `ffmpeg` to composite multiple camera views (Main + Side views) into a single MP4 file.
  - **Layout Logic**:
    - Supports dynamic layouts based on camera count.
    - Uses `ffmpeg` complex filters for scaling and overlaying.
    - **Important**: When modifying export logic, ensure the filter complex graph always terminates with a map to `[outv]`.
- **`FfProbeService`**: Wraps `ffprobe` to extract metadata from video files. Must handle stdout/stderr asynchronously.

### Frontend
- **`VideoPlayer`**: A custom Blazor component for video playback. Uses `@key` to preserve state.
- **`ClipViewer`**: The main interface for viewing clips and selecting segments for export.

## Tools & Dependencies
- **FFmpeg/FFprobe**: Essential for video processing. Installed in the Docker container (Alpine-based).
- **Docker**: Images are based on Alpine Linux (`aspnet:8.0-alpine`, `node:20-alpine`) for minimal size.

## Conventions
- **Camera Enums**: defined in `Cameras.cs`. `Unknown` (-1) should be handled or filtered out in logic.
- **Async/Await**: Ensure all IO-bound operations (especially external process calls) are properly awaited.
- **Logging**: Use Serilog for structured logging.

## Input/Output
- **Exports**: Saved to `ExportedClips` in the clips root.
- **Database**: SQLite `teslacam.db` used for indexing.
- **Authentication**: Single-user (Admin) authentication system. Default is OFF. Controlled via `Users` table and environment variable `RESET_AUTH=true`.
- **Logs**: Application logs are stored in `Logs/` and accessible via the User Profile "Logs" section.
