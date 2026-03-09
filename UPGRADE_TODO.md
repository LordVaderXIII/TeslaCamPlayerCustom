# TeslaCamPlayer: Rust/Tauri Migration — Live Upgrade Checklist

This document is a part-by-part upgrade plan designed so the application remains **fully functional at every step**. No phase leaves the app broken. Each phase is independently shippable.

---

## How to Use This List

- Phases are sequential. Complete each phase before starting the next.
- Within a phase, tasks marked `[parallel]` can be worked simultaneously.
- Each phase ends with a **verification checkpoint** — confirm the app still works before moving on.
- The "app running" guarantee is maintained by keeping the existing stack alive until its replacement is tested and merged.

---

## Phase 1 — Rust Backend (Drop-in API Replacement)

**Goal:** Replace ASP.NET Core server with an Axum (Rust) server exposing the identical HTTP API. The existing Blazor frontend continues to work unchanged.

**Why this first:** Zero UI changes required. Easiest phase to validate — if the API responses match, the app works.

### Setup

- [ ] Install Rust toolchain (`rustup`) and verify `cargo` works
- [ ] Create `rust-server/` directory at repo root; initialize with `cargo init --name teslacam-server`
- [ ] Add dependencies to `Cargo.toml`: `axum`, `tokio` (full features), `serde`, `serde_json`, `sqlx` (sqlite, runtime-tokio), `tracing`, `tracing-subscriber`, `regex`, `anyhow`, `tower-http` (cors, static files, range)
- [ ] Set up CI to build the Rust binary alongside the existing .NET project

### Database Layer

- [ ] Write `sqlx` migration files matching the existing EF Core schema (VideoFiles, ExportJobs, Users tables)
- [ ] Implement `db.rs`: connection pool setup, query functions for each table
- [ ] Write integration test: insert a VideoFile row, read it back, verify fields match

### Clip Discovery Service

- [ ] Port `ClipsService.cs` to `clips_service.rs`
  - [ ] Directory scan using `walkdir` crate (lazy, recursive, handles symlinks)
  - [ ] Filename regex using the `regex` crate (same pattern as C# `GeneratedRegex`)
  - [ ] Parallel ffprobe calls using `tokio::task::spawn_blocking` with a `Semaphore` (max 10 concurrent)
  - [ ] Group files into clips by `StartDate` with 5-second gap tolerance (matching existing logic)
  - [ ] SQLite upsert for new/changed files; mark deleted files as removed
- [ ] Write unit test: feed a list of fake filenames, verify correct grouping into clips
- [ ] Write integration test: point at a real `/TeslaCam` directory, verify clip count matches ASP.NET output

### API Endpoints

- [ ] Implement `GET /Api/GetClips?syncMode={None|Incremental|Full}` — returns `Clip[]` as JSON
- [ ] Implement `GET /Api/Video/{path}` — streams MP4 with HTTP Range request support (`tower-http::services::ServeFile` or manual range handling)
- [ ] Implement `GET /Api/Thumbnail/{path}` — returns PNG file
- [ ] Implement auth endpoints: `POST /Auth/Login`, `POST /Auth/Logout`, `GET /Auth/Status`, `POST /Auth/UpdateAuth`
  - [ ] Port password hashing logic (bcrypt via `bcrypt` crate)
  - [ ] Cookie-based session (use `tower-sessions` or `axum-extra` cookie utilities)
- [ ] Implement export endpoints: `POST /Export/Start`, `GET /Export/Jobs`, `GET /Export/Download/{id}`
  - [ ] Port export queue logic: `tokio::sync::Semaphore` (max 5 concurrent jobs)
  - [ ] Use ffmpeg-next API instead of shell-spawned process for export
- [ ] Add rate limiting middleware on auth routes (matching existing: 5 req / 15 min)
- [ ] Add path traversal guard: `is_under_root_path()` equivalent in Rust

### Security & Middleware

- [ ] Add security headers middleware (CSP, X-Frame-Options, X-Content-Type-Options)
- [ ] Add CORS configuration matching existing ASP.NET Core setup
- [ ] Add request logging via `tracing` crate

### Verification Checkpoint 1

- [ ] Run Rust server on port 5001; run existing Blazor WASM against it (change `appsettings.json` base URL)
- [ ] Load clips list — verify same clips appear
- [ ] Play a video — verify range requests work; video plays without stutter
- [ ] Trigger an incremental sync — verify new files appear
- [ ] Test export — verify output file is valid MP4
- [ ] Test auth enable/disable — verify login/logout flow works
- [ ] Compare API response JSON from both servers for `/Api/GetClips` — must be byte-identical for key fields

### Cutover

- [ ] Update `docker-compose.yml` (or equivalent) to use the Rust binary instead of `dotnet`
- [ ] Remove ASP.NET Core `Server/` project from Docker build (keep source, just stop building it)
- [ ] Tag release: `v2.0.0-rust-backend`

---

## Phase 2 — Tauri Desktop Shell

**Goal:** Wrap the existing app (Blazor or React frontend + Rust backend) in a Tauri desktop application. Users install a native app instead of running Docker.

**Prerequisite:** Phase 1 complete. Rust backend is running.

**Why this second:** Gains native file system access, OS-native WebView (faster), and sets up the foundation for Phase 5's video pipeline.

### Setup

- [ ] Install Tauri prerequisites: Node.js, `@tauri-apps/cli`, `cargo-tauri`
- [ ] Scaffold Tauri project: `cargo tauri init` inside `tauri-app/` directory
- [ ] Configure `tauri.conf.json`:
  - [ ] Set `build.distDir` to point at Blazor's `wwwroot` output (or React build output later)
  - [ ] Set `build.devPath` to `http://localhost:5000` for development
  - [ ] Enable file system permissions for `/TeslaCam` directory
  - [ ] Configure `asset://` protocol for local video file serving

### Rust Backend Integration

- [ ] Embed the Phase 1 Rust backend as a Tauri "sidecar" or integrate directly as Tauri commands
  - Option A (simpler): Run the Axum HTTP server as a background thread inside the Tauri process; frontend still uses HTTP
  - Option B (faster IPC): Expose backend functions as Tauri `#[tauri::command]` handlers; remove HTTP layer entirely
  - **Recommendation:** Start with Option A (sidecar HTTP) — zero frontend changes required

### Video File Access

- [ ] Configure Tauri `allowlist` to grant read access to video directory
- [ ] For `GET /Api/Video/{path}`: switch from HTTP streaming to `tauri://localhost/` asset protocol for local files
  - This eliminates the HTTP round-trip for video data; OS handles the file read directly
- [ ] Test video playback via asset protocol — verify seeking and range requests still work

### Platform Testing

- [ ] Test on Windows: WebView2 must be installed (it's included in Windows 11, auto-downloadable on Windows 10)
- [ ] Test on macOS: WebKit used automatically
- [ ] Test on Linux: WebKitGTK required; document installation steps

### Build & Distribution

- [ ] Configure Tauri bundler for each platform:
  - [ ] Windows: NSIS installer or MSI
  - [ ] macOS: `.dmg` with code signing
  - [ ] Linux: `.deb` and `.AppImage`
- [ ] Set up GitHub Actions: build Tauri app for all 3 platforms on each push
- [ ] Code sign macOS build (requires Apple Developer account)
- [ ] Code sign Windows build (requires EV certificate or self-signed with warning)

### Preserve Docker Option

- [ ] Keep Docker deployment working: the Rust backend can still be run headlessly with `--no-tauri` flag
- [ ] Document: Docker = browser access from any device; Tauri = local desktop app

### Verification Checkpoint 2

- [ ] Install Tauri app on Windows and macOS
- [ ] Open app — verify clip list loads from local `/TeslaCam` directory without configuring any server URL
- [ ] Play a video — verify playback starts faster than web version (no HTTP overhead)
- [ ] Seek to middle of clip — verify seek is faster
- [ ] Docker mode still works — existing users unaffected

### Cutover

- [ ] Publish Tauri installers as GitHub Release assets
- [ ] Update README with install instructions for desktop app
- [ ] Tag release: `v2.1.0-tauri-shell`

---

## Phase 3 — React/TypeScript Frontend (Replaces Blazor)

**Goal:** Replace the Blazor WebAssembly UI with a React + TypeScript frontend. Identical functionality; no interop overhead.

**Prerequisite:** Phase 2 complete (or Phase 1 if skipping Tauri for now).

**Why this third:** With the backend already in Rust, the Blazor frontend is the last C# dependency. Removing it eliminates the C#→JS interop layer entirely.

### Setup

- [ ] Scaffold React app: `npm create vite@latest teslacam-ui -- --template react-ts`
- [ ] Install dependencies: `react-router-dom`, `@radix-ui/react-*` (or shadcn/ui), `leaflet`, `@types/leaflet`, `axios` or `ky` for HTTP
- [ ] Configure Vite dev server proxy to forward `/Api/*` to the Rust backend (port 5001)
- [ ] Set up SCSS compilation: install `sass` as Vite plugin (replaces `gulp`)

### Component Ports (in order of dependency)

- [ ] **Models** — Port `Clip.cs`, `VideoFile.cs`, `TelemetryData.cs` to TypeScript interfaces
- [ ] **API client** — Port `HttpClient` calls to typed `fetch` wrappers; return the same model shapes
- [ ] **ClipList** — Port `Index.razor` clip list: date grouping, search/filter, clip type tabs (Recent/Sentry/Saved)
- [ ] **VideoPlayer** — Port `VideoPlayer.razor` to a React `<video>` wrapper component with ref forwarding
- [ ] **ClipViewer** — Port `ClipViewer.razor.cs` (the hardest component):
  - [ ] 9-camera grid layout
  - [ ] Synchronized playback: replace Blazor timer with `requestAnimationFrame` loop at 60Hz
  - [ ] Segment transition logic
  - [ ] Timeline slider with drag handles for export range
  - [ ] Keyboard shortcuts (±5 sec skip)
- [ ] **TelemetryOverlay** — Port SVG/HTML overlay: speed, gear, steering angle, pedals, autopilot, blinkers
- [ ] **MapViewer** — Port Leaflet map: `useEffect` for init, `useRef` for map instance, GPS path rendering
- [ ] **ExportPanel** — Port export job queue UI: camera checkboxes, job status list, download link
- [ ] **AuthDialog** — Port login/logout UI and auth settings

### Sync Improvement Opportunity

Replace the current 1-second C# timer + `syncVideos()` JS call with a `requestAnimationFrame` loop directly in React:

```typescript
// React sync loop — runs at display refresh rate (60Hz)
const syncLoop = useCallback(() => {
  if (!playing) return;
  const masterTime = masterVideoRef.current?.currentTime ?? 0;
  for (const ref of cameraRefs) {
    if (!ref.current) continue;
    const drift = ref.current.currentTime - masterTime;
    if (Math.abs(drift) > 0.033) { // 1 frame at 30fps
      ref.current.currentTime = masterTime;
    }
  }
  rafIdRef.current = requestAnimationFrame(syncLoop);
}, [playing]);
```

This tightens the sync tolerance from 400ms to 33ms (one frame) without any backend changes.

- [ ] Implement `requestAnimationFrame`-based sync loop in ClipViewer
- [ ] Test: play 9 cameras simultaneously; measure drift with browser dev tools
- [ ] Tune sync threshold: start at 33ms; lower if cameras stay in sync reliably

### Styling

- [ ] Port SCSS from `Client/wwwroot/scss/` to Vite-managed SCSS
- [ ] Match existing dark theme and layout (sidebar + main content + video grid)
- [ ] Verify responsive layout on various window sizes

### Verification Checkpoint 3

- [ ] Side-by-side: React app and Blazor app showing the same clip
- [ ] Verify clip list, filtering, date grouping are identical
- [ ] Play 4-camera clip: verify tighter sync than Blazor version (drift < 100ms)
- [ ] Scrub timeline: verify seeking works and telemetry updates
- [ ] Map renders GPS path correctly
- [ ] Export flow: select cameras, set range, start export, download result
- [ ] Auth: enable password, log out, log back in

### Cutover

- [ ] Point Tauri `distDir` at React build output instead of Blazor `wwwroot`
- [ ] Update Docker image: replace Blazor static files with React build output served by the Rust backend
- [ ] Archive `Client/` project (keep in git history; do not delete immediately)
- [ ] Tag release: `v2.2.0-react-frontend`

---

## Phase 4 — Rust Telemetry Parser (Replaces dashcam-mp4.js)

**Goal:** Move SEI/protobuf telemetry extraction from the browser (JavaScript, full MP4 fetch) to the Rust backend (streaming parse, no full file load).

**Prerequisite:** Phase 1 complete.

**Why this fourth:** Eliminates 150MB browser memory allocations and slow MP4 fetches. Telemetry init becomes near-instant.

### Rust Implementation

- [ ] Add `prost` and `prost-build` to `Cargo.toml`
- [ ] Copy `dashcam.proto` to `rust-server/proto/dashcam.proto`
- [ ] Add `build.rs` to compile proto at build time: `prost_build::compile_protos(&["proto/dashcam.proto"], &["proto/"])?`
- [ ] Write `telemetry_parser.rs`:
  - [ ] Open MP4 with `mp4` crate (or manual box navigation) — read only `moov` box
  - [ ] Walk sample entries for the video track; extract SEI NAL units from each sample
  - [ ] Decode each SEI payload as `SeiMetadata` protobuf message using generated `prost` structs
  - [ ] Return `Vec<TelemetryFrame>` where each frame has `{ time_secs: f64, data: TelemetryData }`
- [ ] Write unit test: parse a real Tesla dashcam MP4, verify frame count and field values match `dashcam-mp4.js` output

### New API Endpoint

- [ ] Add `GET /Api/Telemetry/{path}` — returns full telemetry timeline as JSON array
  - Response: `[{ time: 0.0, speed: 12.5, gear: "D", lat: 37.123, lon: -122.456, ... }, ...]`
  - Parsed once per segment; cached in memory by file path
- [ ] Add `GET /Api/Telemetry/{path}/path` — returns GPS coordinate array only (for map rendering)

### Frontend Update

- [ ] Remove `telemetry-interop.js` and `dashcam-mp4.js` from the project (or mark as deprecated)
- [ ] Remove `TelemetryService.cs` (Blazor) or port to React: replace JS interop with `fetch('/Api/Telemetry/{path}')`
- [ ] On segment load, fetch telemetry timeline once; store in React state as an array
- [ ] For `getTelemetry(timeSeconds)`: do binary search in the frontend TypeScript (same algorithm, no network call per-frame)
- [ ] For GPS path: fetch from `/Api/Telemetry/{path}/path` once per segment; pass to Leaflet

### Verification Checkpoint 4

- [ ] Load a clip with telemetry: verify overlay shows speed, gear, heading
- [ ] Scrub through clip: verify telemetry updates correctly at each timestamp
- [ ] Check browser memory: no large ArrayBuffers in heap (DevTools Memory tab)
- [ ] Measure telemetry init time: should be < 200ms (vs. several seconds for full MP4 fetch)
- [ ] Verify GPS path renders on map

### Cutover

- [ ] Remove `vendor/dashcam-mp4.js` from `wwwroot`
- [ ] Remove `telemetry-interop.js` or leave as dead code with a deprecation comment
- [ ] Tag release: `v2.3.0-rust-telemetry`

---

## Phase 5 — Native Video Sync Engine (Replaces HTML5 `<video>`)

**Goal:** Replace browser `<video>` elements with ffmpeg-decoded frames rendered via Tauri. Enables hardware-accelerated decode, frame-accurate sync, and sub-33ms drift correction.

**Prerequisite:** Phase 2 (Tauri) complete.

**Warning:** This is the most complex phase. Allocate 4-6 weeks and budget for iteration.

### Rust Video Engine

- [ ] Add `ffmpeg-next` to `Cargo.toml` with features: `codec`, `format`, `hwaccel`
- [ ] Write `video_engine.rs`:
  - [ ] `VideoStream` struct: wraps `ffmpeg_next::format::input()`, decoder, hardware context
  - [ ] `open_stream(path: &Path, hw_device: HwDeviceType) -> Result<VideoStream>`
    - [ ] Try VideoToolbox (macOS), D3D12VA (Windows), VAAPI (Linux); fall back to software
  - [ ] `seek(pts: i64)` — calls `seek_to_frame()` with `AVSEEK_FLAG_BACKWARD`
  - [ ] `next_frame() -> Option<VideoFrame>` — returns decoded frame as `Arc<[u8]>` (shared memory)
  - [ ] `current_pts() -> i64` — returns most recently decoded frame's PTS
- [ ] Write `sync_engine.rs`:
  - [ ] Manages N `VideoStream` instances (one per active camera)
  - [ ] Runs on dedicated `tokio::task::spawn_blocking` thread at 60Hz
  - [ ] Master camera: stream 0 (front camera); all others slave to it
  - [ ] For each slave: if `|slave.pts - master.pts| > frame_duration`, call `slave.seek(master.pts)`
  - [ ] Emits decoded frames via `tokio::sync::broadcast::channel` to Tauri frontend

### Frame Delivery to WebView

- [ ] Option A — Canvas via IPC (simpler, slightly slower):
  - [ ] Encode each decoded frame as JPEG (use `image` crate) and send via Tauri IPC event
  - [ ] React: listen for frame events, draw to `<canvas>` elements using `drawImage`
  - [ ] Acceptable for 1080p at 30fps; may struggle at high frame rates with 9 cameras
- [ ] Option B — Shared memory (faster, complex):
  - [ ] Write decoded frames to a named shared memory segment
  - [ ] React: use `SharedArrayBuffer` + `ImageData` to read frames directly into canvas
  - [ ] Requires `Cross-Origin-Isolation` headers; configure Tauri accordingly
- [ ] **Start with Option A**; move to Option B if frame delivery becomes a bottleneck

### React Integration

- [ ] Replace `<video src="...">` elements with `<canvas ref={...}>` elements
- [ ] Remove JS-based `syncVideos()` (now done by Rust `sync_engine`)
- [ ] Remove `requestAnimationFrame` sync loop (replaced by Rust 60Hz thread)
- [ ] Add Tauri event listener: `listen('frame-{camera}', (event) => drawFrame(canvasRef, event.payload))`
- [ ] Implement playback controls via Tauri commands: `play()`, `pause()`, `seek(seconds)`, `set_speed(rate)`
- [ ] Timeline: subscribe to a `position-update` Tauri event (emitted by sync engine each frame)

### Verify Hardware Decode

- [ ] On macOS: verify `VideoToolbox` is used via ffmpeg log output (`hwaccel=videotoolbox`)
- [ ] On Windows: verify `D3D12VA` or `DXVA2` is used
- [ ] Measure CPU usage: should be < 10% for 9 simultaneous 1080p H.264 streams
- [ ] Compare with Phase 3 baseline (HTML5 video CPU usage)

### Verification Checkpoint 5

- [ ] Play 9 cameras simultaneously: verify all cameras are in sync to within 1 frame (33ms)
- [ ] Seek to 5:00 in a 10-minute clip: verify all cameras seek accurately to within 1 frame
- [ ] Measure frame delivery: < 16ms from Rust frame decoded to canvas rendered (60fps budget)
- [ ] Check memory usage: no large JS heap allocations (frames shared with Rust, not copied into JS GC)
- [ ] Hardware decode confirmed on each target platform

### Cutover

- [ ] Remove `VideoPlayer.razor` (or React `<video>` component)
- [ ] Remove all `<video>` element references from frontend
- [ ] Remove `site.js` `syncVideos()` function (now unused)
- [ ] Tag release: `v3.0.0-native-video`

---

## Phase 6 — Polish and Distribution

**Goal:** Production-ready release with auto-update, code signing, and updated documentation.

### Auto-Update

- [ ] Enable Tauri updater plugin
- [ ] Configure update endpoint: host `latest.json` on GitHub Pages or S3
- [ ] Test update flow: install old version, trigger update, verify new version installs

### Code Signing

- [ ] macOS: sign with Apple Developer certificate; notarize with Apple; verify Gatekeeper clears the app
- [ ] Windows: sign with EV certificate (or configure SmartScreen exception for self-signed)
- [ ] Linux: GPG-sign `.deb` package

### CI/CD

- [ ] GitHub Actions matrix build: `[windows-latest, macos-latest, ubuntu-latest]`
- [ ] Upload Tauri artifacts as GitHub Release assets on version tags
- [ ] Run Rust tests (`cargo test`) on every PR
- [ ] Run React tests (`vitest`) on every PR

### Documentation

- [ ] Update `README.md`: new install instructions (download installer vs. Docker)
- [ ] Update `README.md`: development setup for both Rust and React codebases
- [ ] Archive this `UPGRADE_TODO.md` in git history; it has served its purpose
- [ ] Tag release: `v3.1.0-release`

---

## Quick Reference: What Stays, What Goes

| Component | Status After All Phases |
|---|---|
| `Server/` (ASP.NET Core) | Removed — replaced by `rust-server/` |
| `Client/` (Blazor WASM) | Removed — replaced by `teslacam-ui/` (React) |
| `Shared/` (C# models) | Removed — models ported to TypeScript interfaces |
| `vendor/dashcam-mp4.js` | Removed — replaced by Rust protobuf parser |
| `telemetry-interop.js` | Removed — telemetry served via REST API |
| `site.js` (`syncVideos`) | Removed — replaced by Rust sync engine |
| `map.js` (Leaflet) | Kept — still used in React frontend |
| `dashcam.proto` | Kept — moved to `rust-server/proto/` |
| `docker-compose.yml` | Updated — runs Rust binary, serves React build |
| `CLAUDE.md` | Updated — reflects new Rust/Tauri architecture |

---

## Risk Register

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| Phase 5 canvas performance insufficient | Medium | High | Try SharedArrayBuffer; or use native Tauri window rendering (wry `WebView::evaluate_script` with pixel buffer) |
| Tauri WebView2 video codec gaps on Windows | Low | Medium | Phase 5 removes HTML5 video dependency entirely; not a concern after Phase 5 |
| Hardware decode not available on user hardware | Low | Low | ffmpeg falls back to software decode automatically |
| Rust learning curve slows development | High | Medium | Hire or pair with Rust-experienced developer during Phase 1 |
| macOS notarization requirements change | Low | Low | Monitor Apple Developer documentation; build process is automated in CI |
| SQLite schema migration bugs | Medium | Medium | Write migration tests; keep a schema version table |

---

*This checklist is a living document. Update checkboxes as tasks are completed and add new tasks as they are discovered during implementation.*
