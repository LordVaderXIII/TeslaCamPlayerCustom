# AI Agents Guide

This document provides context and instructions for AI agents working on the TeslaCamPlayer codebase.

## Project Structure
- **Client**: Blazor WebAssembly application (UI).
- **Server**: ASP.NET Core host (API, file serving, video processing).
- **Shared**: Common models and DTOs.

## Key Features
- **Clip Viewer**: Synchronized playback of multiple camera angles (Front, Left, Right, Back).
- **Telemetry**: Displays driving data (Speed, Gear, Autopilot) extracted from video SEI metadata.
- **Map View**: Visualizes the vehicle's path on an OpenStreetMap using Leaflet.

## Conventions
- **Read this file before coding**: Always review `AGENTS.md` at the start of each task.
- **Versioning**: Update `src/TeslaCamPlayer.BlazorHosted/Client/Models/VersionInfo.cs` for every user-facing change.
- **Release format**: Use industry-standard semantic version progression in `VersionInfo` release entries.
- **Change docs**: Document all code changes in commit/PR summaries.
- **Styling**: SCSS files are in `Client/wwwroot/scss`. Use `npx gulp` to compile when needed.
- **Components**: MudBlazor is the primary UI library.
- **Interop**: JavaScript interop files are in `Client/wwwroot/js`.

## Troubleshooting
- If the UI shows **"An unhandled error has occurred"**, check browser console first for JS interop failures.
- Typical failure pattern: cached/stale script references causing missing JS functions called from Blazor.
- Ensure interop functions referenced from C# exist in currently loaded JS files.

## Recent Changes
- **Map & Layout**: The Map view replaces the Calendar widget (sidebar top) but leaves the Clip List visible. The Map has a fixed height (~350px).
- **Telemetry**: Telemetry data is extracted client-side via `telemetry-interop.js` and `dashcam-mp4.js`.
- **3D Mode**: Removed from the app. Do not add new references to `teslaPano`/Three.js unless explicitly requested.
