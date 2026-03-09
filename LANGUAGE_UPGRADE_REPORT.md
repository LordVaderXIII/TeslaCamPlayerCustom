# TeslaCamPlayer: Language Upgrade Report

**Date:** March 9, 2026
**Scope:** Full-stack migration analysis from Blazor WebAssembly / ASP.NET Core to a higher-performance stack, with emphasis on multi-camera synchronized video playback.

---

## Executive Summary

TeslaCamPlayer is built on **Blazor WebAssembly** (frontend) and **ASP.NET Core** (backend). While this stack delivered a functional product quickly, it carries fundamental architectural constraints that limit performance exactly where it matters most: **synchronized playback of up to 9 simultaneous camera feeds**.

The recommended replacement is **Rust** as the core language, paired with the **Tauri** framework for the desktop application shell. A TypeScript/React frontend (replacing Blazor) would sit inside Tauri's native WebView, retaining web development ergonomics while gaining direct access to OS-level video hardware and true multi-threading.

This is not a cosmetic upgrade. The current stack requires video synchronization to cross a C#-to-JavaScript interop bridge on a 1-second polling timer, with a .NET garbage collector that can introduce unpredictable pauses at any moment. Rust eliminates both of these problems entirely.

---

## 1. Current Stack: What Works and What Doesn't

### What Works

- Rapid feature development via Blazor components and MudBlazor
- Clean REST API structure for clip discovery and video streaming
- EF Core / SQLite for persistent clip metadata and export job tracking
- JavaScript interop for telemetry extraction and map rendering

### Where the Current Stack Falls Short

#### 1.1 The C#-to-JavaScript Interop Tax

Every video synchronization tick crosses the .NET managed runtime boundary into the browser's JavaScript engine. This is not free. Each `syncVideos()` call from `ClipViewer.razor.cs:372` involves:

1. .NET runtime marshaling arguments to a JS-compatible format
2. A context switch from the .NET WASM runtime to the browser JS engine
3. DOM access to 9 `<video>` elements
4. Return value marshaling back to .NET

With 9 cameras and a 1-second polling interval, this overhead is manageable at rest, but during fast scrubbing, segment transitions, or export operations, it compounds. The 0.4-second sync tolerance in `syncVideos()` exists precisely because finer tolerances are unreliable with this architecture.

#### 1.2 .NET Garbage Collector Pauses

Blazor WebAssembly runs the .NET GC inside the browser sandbox. GC pauses are unpredictable — typically 5-50ms but occasionally longer. During synchronized playback, a GC pause in the main thread causes the sync timer to skip a beat. Because all 9 `<video>` elements run independently in the browser, they drift during any pause. The current 0.4-second tolerance exists as a workaround for this drift.

#### 1.3 Single-Threaded WASM Execution

The WebAssembly specification historically mandates single-threaded execution (SharedArrayBuffer and WASM threads are available but require specific HTTP headers and browser support). This means:

- Clip scanning (`ClipsService.cs`) runs `AsParallel()` on the **server**, but the client UI is fully single-threaded
- Telemetry parsing (fetching an entire MP4 as `ArrayBuffer` in `telemetry-interop.js`) blocks the rendering loop during fetch
- Segment transitions, which must load 9 new video URLs simultaneously, compete with the UI render cycle

#### 1.4 HTTP Streaming Overhead for Local Files

Video files are served via `/Api/Video/{path}` — an HTTP range request that traverses: disk → ASP.NET Core middleware → HTTP stack → browser → HTML5 video decoder.

For a local application watching files on the same machine (the primary use case for dashcam review), this is a wasteful round trip. A native app could memory-map video files directly, bypassing the entire HTTP stack and reducing seek latency dramatically.

#### 1.5 JavaScript MP4 Parsing for Telemetry

`vendor/dashcam-mp4.js` fetches entire MP4 video files as `ArrayBuffer` in the browser to extract SEI metadata. For a 10-minute Tesla dashcam clip at ~150MB, this means:

- 150MB transferred over HTTP (even on localhost, this is a slow operation)
- 150MB held in the browser's JS heap
- Protobuf decoding in interpreted JavaScript

This is why telemetry init is asynchronous and can fail silently — the browser may reject the allocation or the fetch may time out.

---

## 2. Recommended Language: Rust

### Why Rust?

Rust is uniquely suited to this workload for four reasons:

**1. Zero garbage collection.** Rust's ownership and borrowing system eliminates the GC entirely. Memory is freed deterministically at the end of scope. This means zero pause-induced sync drift during video playback.

**2. True multi-threading with memory safety.** Rust's `Send` and `Sync` traits enforce thread safety at compile time. A dedicated sync thread can monitor all camera timestamps at microsecond resolution, without the risk of data races. `tokio` provides an async runtime for I/O-bound work (clip scanning, DB queries) while CPU-bound video work runs on a separate thread pool.

**3. Direct hardware access via ffmpeg.** The `ffmpeg-next` Rust crate provides safe bindings to libavcodec/libavformat. This enables:
   - Hardware-accelerated video decode (VideoToolbox on macOS, DXVA2/D3D12 on Windows, VAAPI on Linux)
   - Streaming MP4 parsing (read only the moov box for SEI frames; never load the full file)
   - Sub-millisecond seek operations via direct demuxer control

**4. Small, self-contained binaries.** A Rust binary for the backend server is typically 5-10MB with no runtime dependencies. Compare to ASP.NET Core's requirement for the .NET runtime (or a self-contained publish at 60-80MB).

### Why Tauri (not Electron)?

Tauri is a Rust-based desktop application framework. It provides a thin native shell around the OS's built-in WebView (WebKit on macOS/Linux, WebView2 on Windows), allowing a web-based UI while running Rust code natively for all performance-critical operations.

| Criterion | Electron | Tauri |
|---|---|---|
| Runtime | Bundled Chromium (~150MB) | OS native WebView (~600KB overhead) |
| Memory usage | 200-400MB baseline | 10-30MB baseline |
| Backend language | Node.js (JavaScript/TypeScript) | Rust |
| IPC overhead | JSON over pipes | Serde-serialized messages (fast) |
| Video decode | Chromium only | OS APIs (VideoToolbox, D3D12, VAAPI) |
| GC | V8 GC | None |
| Build size | 100-200MB installer | 5-15MB installer |

For a video-heavy application with a local file system, Tauri is strictly better than Electron.

### Why TypeScript/React for the UI (not a native UI)?

The current Blazor UI is relatively mature — it handles clip lists, timeline scrubbing, camera grids, telemetry overlays, and map rendering. Rewriting this in a native UI toolkit (egui, Qt, etc.) would be a significant undertaking with few benefits for the UI layer. React or Svelte inside Tauri's WebView:

- Retains web developer ergonomics for UI work
- Gets hardware-accelerated rendering via the OS WebView
- Can still use `<video>` elements for camera display (WebView2 / WebKit have excellent video support)
- Allows Leaflet.js map and MudBlazor-equivalent component libraries to be reused

The Rust backend handles all performance-critical operations; the frontend is only responsible for display.

---

## 3. Multi-Camera Sync: The Core Performance Argument

This section explains in detail why the current approach cannot achieve tight synchronization, and how Rust resolves this.

### 3.1 Current Sync Architecture

```
[1-second JS timer]
       |
       v
ClipViewer.SyncVideosTick() [C# / .NET WASM]
       |  (interop call — crosses managed/unmanaged boundary)
       v
syncVideos(mainVideo, otherVideos, 0.4) [JavaScript]
       |
       v
For each of 8 secondary cameras:
  if |main.currentTime - camera.currentTime| > 0.4:
    camera.currentTime = main.currentTime
```

**Problems:**
- 1-second polling means cameras can drift up to 1 second before correction
- 0.4-second threshold means small drift is never corrected (accepted as noise)
- Any GC pause during playback causes the timer to fire late
- The interop call itself adds ~2-5ms of latency per tick
- `currentTime` on HTML5 video elements is not frame-accurate; it reflects decoder buffer state

### 3.2 Rust Sync Architecture

In a Rust/Tauri application, a dedicated sync thread monitors all camera streams at the demuxer level:

```
[Dedicated sync thread — runs at 60Hz or faster]
       |
       v
Read PTS (presentation timestamp) from each camera's decoder
       |
       v
Compute drift = camera[i].pts - master_camera.pts
       |
       v
If |drift| > 1 frame duration (typically 33ms for 30fps):
  Seek camera[i] to master_pts directly via avformat_seek_file()
```

**Gains:**
- Polling at 60Hz instead of 1Hz — drift detected and corrected within one frame
- No interop boundary — all operations happen in native Rust code
- Threshold drops from 400ms to 33ms (one frame), or even sub-frame if needed
- `avformat_seek_file()` with `AVSEEK_FLAG_FRAME` seeks to exact frame boundaries
- No GC pauses — sync thread is never preempted by garbage collection
- PTS values come from the container, not from a browser's approximation of `currentTime`

### 3.3 Hardware-Accelerated Decode

Tesla dashcam footage is H.264 encoded. Decoding 9 simultaneous H.264 streams in software (which Blazor/browser HTML5 video may fall back to) is CPU-intensive. The ffmpeg hardware acceleration APIs route decode to:

- **macOS**: VideoToolbox (Apple's GPU video engine) — handles 4K H.264 in real time with near-zero CPU
- **Windows**: DXVA2 or D3D12VA — Intel/AMD/NVIDIA GPU decode
- **Linux**: VAAPI or NVDEC — GPU decode on supported hardware

With hardware decode, 9 simultaneous 1080p H.264 streams consume roughly 3-8% CPU on modern hardware. In software decode, the same workload can saturate a 4-core CPU.

### 3.4 Streaming SEI/Telemetry Parsing

Instead of fetching the entire MP4 into a browser ArrayBuffer, the Rust backend can:

1. Open the MP4 with `libavformat`
2. Seek to the `moov` box and read only the SEI NAL units from the metadata track
3. Decode protobuf frames using the `prost` crate
4. Return the full telemetry timeline to the frontend in a single API call

This eliminates the 150MB browser allocation entirely. The `prost` crate compiles `.proto` files at build time, generating zero-allocation Rust structs from the protobuf schema — far faster than runtime Protobuf.js decoding in JavaScript.

---

## 4. Proposed Replacement Stack

| Layer | Current | Recommended |
|---|---|---|
| Frontend framework | Blazor WebAssembly (C#) | React + TypeScript |
| UI components | MudBlazor | shadcn/ui or Radix UI |
| Map | Leaflet.js (JS interop) | Leaflet.js (direct, no interop) |
| Desktop shell | Browser / Docker web app | Tauri (Rust) |
| Backend language | C# / ASP.NET Core | Rust / Axum |
| Video decode | HTML5 `<video>` | ffmpeg-next (libavcodec) |
| Video sync | JS polling at 1Hz | Rust sync thread at 60Hz |
| Telemetry parsing | dashcam-mp4.js + Protobuf.js | prost crate (compile-time codegen) |
| Database | EF Core + SQLite | sqlx + SQLite (async, no ORM) |
| Logging | Serilog | tracing crate |
| Export | ffmpeg via shell spawn | ffmpeg-next API calls |

---

## 5. Migration Effort Estimate

### Phase 1 — Rust Backend (replaces ASP.NET Core)
**Effort: 3-4 weeks**

- Rewrite `ClipsService` in Rust: directory scanning, filename regex parsing (`regex` crate), parallelized ffprobe calls (`tokio::task::spawn_blocking`)
- Rewrite `ApiController` in Axum: `/api/clips`, `/api/video/{path}` (with range request support), `/api/thumbnail`
- Rewrite `ExportService`: `tokio::sync::Semaphore` limiting concurrency, ffmpeg-next for actual export
- Rewrite `FfProbeService`: use `ffprobe` or `ffmpeg-next` to read duration
- Replace EF Core + SQLite with `sqlx` migrations
- Drop-in replacement: the existing Blazor frontend continues to work unchanged against the new Rust API
- **Risk**: Low. Same HTTP API shape; Blazor frontend never knows the difference.

### Phase 2 — Tauri Shell (replaces Docker/browser deployment)
**Effort: 2-3 weeks**

- Initialize Tauri project wrapping existing Blazor (or future React) frontend
- Configure Tauri to expose the Rust backend as IPC commands instead of HTTP
- File system permissions: grant Tauri access to `/TeslaCam` directory
- Update video serving: instead of HTTP streaming, serve via Tauri's `asset://` protocol or direct IPC
- **Risk**: Medium. Tauri's WebView differences between macOS and Windows require testing.

### Phase 3 — React/TypeScript Frontend (replaces Blazor)
**Effort: 4-5 weeks**

- Scaffold React app with TypeScript and Vite
- Port components: `ClipList`, `ClipViewer` (camera grid), `TelemetryOverlay`, `MapViewer`
- Replace MudBlazor with shadcn/ui (similar component set, React-native)
- Port JavaScript interop functions to direct calls (no C#→JS bridge needed)
- Retain Leaflet.js integration (already JavaScript; removal of Blazor interop simplifies this)
- **Risk**: Medium. UI parity takes time, but the logic is well-understood from the existing code.

### Phase 4 — Rust Telemetry Parser (replaces dashcam-mp4.js)
**Effort: 1-2 weeks**

- Add `prost-build` to Tauri's Rust backend; compile `dashcam.proto` at build time
- Write a streaming MP4 parser using `mp4` or `nom` crate: read only SEI NAL units
- Expose telemetry timeline as an IPC command: `get_telemetry_path(video_path) -> Vec<TelemetryFrame>`
- Frontend calls IPC instead of fetching the full MP4 into JS memory
- **Risk**: Low. Well-defined protobuf schema; existing JS implementation is the reference.

### Phase 5 — Native Video Sync Engine (replaces HTML5 `<video>`)
**Effort: 4-6 weeks**

- Use ffmpeg-next to open and decode each camera's MP4 stream
- Render decoded frames to surfaces exposed to the WebView via Tauri's custom protocol
- Implement 60Hz sync thread: compare PTSs, seek lagging cameras
- Hardware decode paths: VideoToolbox (macOS), D3D12VA (Windows), VAAPI (Linux)
- **Risk**: High. Frame-to-WebView rendering requires careful handling of surface lifetimes and pixel format conversion. Consider using an `<canvas>` element with frames transferred via shared memory.

### Phase 6 — Polish and Deployment
**Effort: 1-2 weeks**

- Tauri auto-updater integration
- Code signing for macOS and Windows distributables
- Replace Docker deployment with Tauri installer
- Update documentation and README

---

## 6. Alternative Languages Considered

### Go + React/TypeScript
- **Pros**: Simple concurrency model (`goroutines`), fast HTTP server (`net/http`), easy to learn
- **Cons**: Has a garbage collector (less severe than .NET, but still present); no native video decode APIs; cgo required for ffmpeg bindings (complex build); less suitable than Rust for the sync-critical path
- **Verdict**: Good choice for the backend API layer; poor choice for the video sync engine

### C++ with Qt Multimedia
- **Pros**: Maximum control; Qt's `QMediaPlayer` with `QVideoWidget` handles multi-camera natively; `libavcodec` integration is mature
- **Cons**: Memory safety must be manually maintained; Qt licensing (GPL or commercial); significantly higher developer maintenance burden; slower iteration for UI work
- **Verdict**: Highest raw performance ceiling, but impractical maintenance cost for a small project

### Swift (macOS only)
- **Pros**: AVFoundation is the best video framework available on any platform; `AVPlayer` with `AVSynchronizedLayer` provides frame-accurate multi-camera sync out of the box
- **Cons**: macOS/iOS only; Tesla dashcam review is common on Windows and Linux (Docker)
- **Verdict**: Ideal if macOS-only support is acceptable; ruled out by cross-platform requirement

### Kotlin Multiplatform + Compose
- **Pros**: JVM ecosystem familiarity; Compose Multiplatform for desktop UI
- **Cons**: JVM GC pauses (same problem as .NET); no direct hardware video API; desktop Compose is less mature than web
- **Verdict**: Not recommended for this workload

---

## 7. Risks and Trade-offs

### Rust Learning Curve
Rust's ownership and borrowing system has a well-documented steep learning curve. Developers coming from C# or JavaScript typically require 4-8 weeks before becoming productive. The `async` ecosystem in Rust (`tokio`, `async-std`) adds additional complexity.

**Mitigation**: Start with the backend rewrite (Phase 1) where Rust's advantages are clear and the code is more algorithmic than systems-level. Avoid Phase 5 (native video pipeline) until the team has Rust fluency.

### Tauri WebView Differences
Tauri uses WebKit on macOS/Linux and WebView2 (Chromium-based) on Windows. WebKit's H.264 support, CSS rendering, and JavaScript engine can behave differently from WebView2. This primarily affects the `<video>` element behavior and CSS layout.

**Mitigation**: Test on all target platforms before each phase ships. Phase 5 (native video) removes `<video>` dependency entirely, eliminating this risk.

### Loss of Browser Deployability
The current Docker-based deployment means any user with a browser can access TeslaCamPlayer remotely (e.g., on a NAS). A Tauri desktop app loses this capability.

**Mitigation**: Retain the Axum HTTP server as an optional "server mode" (same binary, different startup flag). This allows both desktop and headless server operation.

### Ecosystem Maturity
The Rust multimedia ecosystem (`ffmpeg-next`, `symphonia`, `mp4`) is younger than the .NET/JavaScript equivalents. Some APIs have fewer examples and less Stack Overflow coverage.

**Mitigation**: `ffmpeg-next` wraps libffmpeg — the most battle-tested video library in existence. The Rust bindings are thin; most questions can be answered by referencing the libffmpeg C documentation.

---

## 8. Conclusion

The current Blazor WebAssembly / ASP.NET Core stack is adequate for a basic video viewer but is architecturally limited for the core use case of tight multi-camera synchronization. The 0.4-second sync tolerance, 1-second polling interval, and GC-susceptible sync timer are symptoms of a platform not designed for real-time media workloads.

**Rust with Tauri** resolves every one of these limitations: no GC pauses, true multi-threading, direct ffmpeg integration for hardware decode, and a 60Hz sync engine that corrects drift within a single frame.

The migration is practical in six phases, with each phase delivering incremental improvement while keeping the app running. Phase 1 alone (Rust backend) delivers faster clip scanning, lower memory usage, and faster export operations. Phase 5 (native video pipeline) is where the multi-camera sync story transforms from "workable" to "best-in-class."

The recommended starting point is **Phase 1: Rust backend**, using Axum to replace ASP.NET Core. This is the lowest-risk phase, delivers measurable performance gains, and builds the team's Rust competency before tackling the harder video work.
