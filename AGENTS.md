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
- **3D Mode**: Panoramic playback using Three.js.

## Conventions
- **Versioning**: Update `src/TeslaCamPlayer.BlazorHosted/Client/Models/VersionInfo.cs` for every user-facing change.
- **Styling**: SCSS files are in `Client/wwwroot/scss`. Use `npx gulp` to compile.
- **Components**: MudBlazor is the primary UI library.
- **Interop**: JavaScript interop files are in `Client/wwwroot/js`.

## Recent Changes
- **Map & Layout**: The Map view replaces the Calendar widget (sidebar top) but leaves the Clip List visible. The Map has a fixed height (~350px).
- **Telemetry**: Telemetry data is extracted client-side via `telemetry-interop.js` and `dashcam-mp4.js`.
