# Bolt's Journal

## 2024-05-22 - [Optimizing Telemetry Data Access]
**Learning:** The application processes telemetry data every frame during playback. The current implementation uses `reduce` over the entire array of frames to find the closest frame by time. Since the frames are naturally sorted by time, we can optimize this look-up.
**Action:** Replace linear search O(N) with binary search O(log N) in `telemetry-interop.js` to reduce CPU usage during playback, especially for longer clips with many telemetry frames.
