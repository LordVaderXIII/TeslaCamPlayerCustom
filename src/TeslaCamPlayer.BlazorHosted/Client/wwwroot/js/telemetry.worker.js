// Telemetry Worker
// Handles downloading and parsing video files in a background thread
// to prevent blocking the UI thread.

// Import dependencies
// Note: Paths are relative to the worker script location (js/)
importScripts('vendor/protobuf.min.js');
importScripts('vendor/dashcam-mp4.js');

self.onmessage = async function (e) {
    const { videoUrl, protoUrl } = e.data;

    try {
        console.log("Worker: Initializing telemetry for: " + videoUrl);

        // 1. Fetch the video file
        const response = await fetch(videoUrl);
        if (!response.ok) {
            throw new Error("Failed to fetch video: " + response.statusText);
        }
        const buffer = await response.arrayBuffer();

        // 2. Initialize Protobuf
        // Ensure DashcamHelpers is loaded
        if (!self.DashcamHelpers) {
            throw new Error("DashcamHelpers not found.");
        }

        // protoUrl is now an absolute URL passed from main thread
        const { SeiMetadata } = await self.DashcamHelpers.initProtobuf(protoUrl);
        if (!SeiMetadata) {
            throw new Error("Failed to initialize Protobuf metadata.");
        }

        // 3. Initialize Parser
        if (!self.DashcamMP4) {
             throw new Error("DashcamMP4 not found.");
        }
        const dashcam = new self.DashcamMP4(buffer);

        // 4. Parse Frames & Build Timeline
        const rawFrames = dashcam.parseFrames(SeiMetadata);
        const config = dashcam.getConfig();

        // Expand frame durations to absolute timestamps
        const timestamps = [];
        let t = 0;
        // config.durations contains the duration of each frame in ms
        for (const d of config.durations) {
            timestamps.push(t);
            t += d / 1000.0;
        }

        // Filter only frames with SEI data and map them
        // We cannot transfer complex objects easily if they contain functions,
        // but SEI data should be POJOs from protobufjs.
        const frames = rawFrames
            .filter(f => f.sei)
            .map(f => {
                return {
                    time: timestamps[f.index] || 0,
                    data: f.sei
                };
            });

        // 5. Return results
        self.postMessage({
            success: true,
            frames: frames
        });

    } catch (err) {
        console.error("Worker Error:", err);
        self.postMessage({
            success: false,
            error: err.message
        });
    }
};
