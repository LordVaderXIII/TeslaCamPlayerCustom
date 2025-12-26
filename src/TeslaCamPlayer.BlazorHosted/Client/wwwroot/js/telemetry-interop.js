window.telemetryInterop = {
    dashcam: null,
    frames: [],
    frameTimes: [], // accumulative time in seconds
    isReady: false,
    currentUrl: null,

    init: async function (videoUrl) {
        if (this.currentUrl === videoUrl && this.isReady) {
            return true;
        }

        console.log("Telemetry: Initializing for " + videoUrl);
        this.isReady = false;
        this.frames = [];
        this.frameTimes = [];
        this.currentUrl = videoUrl;

        try {
            // Verify DashcamHelpers dependency
            if (!window.DashcamHelpers) {
                throw new Error("DashcamHelpers not loaded");
            }

            // Ensure Protobuf is ready
            await window.DashcamHelpers.initProtobuf('dashcam.proto'); // Use relative path
            const { SeiMetadata } = window.DashcamHelpers.getProtobuf();

            // Fetch video file
            // Note: This fetches the whole file. For large files, this might be slow.
            const response = await fetch(videoUrl);
            if (!response.ok) throw new Error(`Failed to fetch video: ${response.statusText}`);
            const buffer = await response.arrayBuffer();

            // Parse
            this.dashcam = new window.DashcamMP4(buffer);
            const rawFrames = this.dashcam.parseFrames(SeiMetadata);
            const config = this.dashcam.getConfig();

            // Map frames to time
            // config.durations is array of duration (ms) for each frame
            let currentTime = 0;
            this.frames = [];
            this.frameTimes = [];

            // Only keep frames with SEI data to save memory
            // But we need to track time for ALL frames to stay in sync
            for (let i = 0; i < rawFrames.length; i++) {
                const frameDuration = (config.durations[i] || 33.33) / 1000.0; // convert ms to seconds

                if (rawFrames[i].sei) {
                    this.frames.push({
                        time: currentTime,
                        data: rawFrames[i].sei
                    });
                    this.frameTimes.push(currentTime);
                }

                currentTime += frameDuration;
            }

            console.log(`Telemetry: Parsed ${this.frames.length} data points.`);
            this.isReady = true;
            return true;
        } catch (e) {
            console.error("Telemetry: Initialization failed", e);
            this.isReady = false;
            return false;
        }
    },

    getTelemetry: function (timeSeconds) {
        if (!this.isReady || this.frames.length === 0) return null;

        // Binary search for efficiency
        let low = 0, high = this.frames.length - 1;
        let bestIndex = -1;

        while (low <= high) {
            const mid = Math.floor((low + high) / 2);
            if (this.frames[mid].time <= timeSeconds) {
                bestIndex = mid;
                low = mid + 1;
            } else {
                high = mid - 1;
            }
        }

        if (bestIndex === -1) return null; // Before first frame

        // If the closest frame is too far away (e.g. > 1 second), return null
        // This handles gaps or end of video
        if (Math.abs(this.frames[bestIndex].time - timeSeconds) > 1.0) return null;

        return this.frames[bestIndex].data;
    },

    getPath: function() {
        if (!this.isReady) return [];
        // Extract lat/lon from all frames
        const path = [];
        for(let f of this.frames) {
            // Check for valid lat/lon (sometimes 0/0)
            if (f.data.latitudeDeg && f.data.longitudeDeg && (Math.abs(f.data.latitudeDeg) > 0.1 || Math.abs(f.data.longitudeDeg) > 0.1)) {
                 path.push([f.data.latitudeDeg, f.data.longitudeDeg]);
            }
        }
        return path;
    }
};
