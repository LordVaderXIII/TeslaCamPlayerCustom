window.telemetryInterop = {
    dashcam: null,
    frames: [],

    init: async function (videoUrl) {
        try {
            console.log("Initializing telemetry for: " + videoUrl);

            // 1. Fetch the video file
            const response = await fetch(videoUrl);
            if (!response.ok) {
                console.error("Failed to fetch video:", response.statusText);
                return false;
            }
            const buffer = await response.arrayBuffer();

            // 2. Initialize Protobuf
            // Ensure DashcamHelpers is loaded
            if (!window.DashcamHelpers) {
                console.error("DashcamHelpers not found. Ensure dashcam-mp4.js is loaded.");
                return false;
            }

            const { SeiMetadata } = await window.DashcamHelpers.initProtobuf('dashcam.proto');
            if (!SeiMetadata) {
                console.error("Failed to initialize Protobuf metadata.");
                return false;
            }

            // 3. Initialize Parser
            if (!window.DashcamMP4) {
                 console.error("DashcamMP4 not found.");
                 return false;
            }
            this.dashcam = new window.DashcamMP4(buffer);

            // 4. Parse Frames & Build Timeline
            const rawFrames = this.dashcam.parseFrames(SeiMetadata);
            const config = this.dashcam.getConfig();

            console.log(`Telemetry: Parsed ${rawFrames.length} video frames.`);

            // Expand frame durations to absolute timestamps
            const timestamps = [];
            let t = 0;
            // config.durations contains the duration of each frame in ms
            for (const d of config.durations) {
                timestamps.push(t);
                t += d / 1000.0;
            }

            // Filter only frames with SEI data and map them
            this.frames = rawFrames
                .filter(f => f.sei)
                .map(f => {
                    return {
                        time: timestamps[f.index] || 0,
                        data: f.sei
                    };
                });

            const seiFrameCount = this.frames.length;
            if (rawFrames.length > 0 && seiFrameCount === 0) {
                 console.warn("Telemetry warning: Video frames found but NO SEI metadata extracted. This might indicate a parser issue or missing SEI data.");
            }

            console.log(`Telemetry initialized. Found ${seiFrameCount} data points.`);
            return this.frames.length > 0;

        } catch (e) {
            console.error("Error initializing telemetry:", e);
            return false;
        }
    },

    getTelemetry: function (timeSeconds) {
        if (!this.frames || this.frames.length === 0) return null;

        // Find the closest frame
        // Optimized: Binary search is O(log N) vs Reduce O(N).
        // For a 10 min clip (18k frames), this is significantly faster (~14 ops vs 18000 ops).
        let low = 0;
        let high = this.frames.length - 1;

        // Find first element where frame.time >= timeSeconds
        while (low <= high) {
            const mid = (low + high) >>> 1;
            if (this.frames[mid].time < timeSeconds) {
                low = mid + 1;
            } else {
                high = mid - 1;
            }
        }

        let closest;
        if (low >= this.frames.length) {
            closest = this.frames[this.frames.length - 1];
        } else if (low === 0) {
            closest = this.frames[0];
        } else {
            const after = this.frames[low];
            const before = this.frames[low - 1];
            // If diffs are equal, prefer 'before' to match original behavior
            if ((after.time - timeSeconds) < (timeSeconds - before.time)) {
                 closest = after;
            } else {
                 closest = before;
            }
        }

        // If the closest frame is more than 1 second away, treat as no data
        if (Math.abs(closest.time - timeSeconds) > 1.0) {
            return null;
        }

        const sei = closest.data;

        return {
            timestamp: new Date().toISOString(), // Placeholder
            gearState: this.mapGear(sei.gearState),
            vehicleSpeedMps: sei.vehicleSpeedMps || 0,
            latitudeDeg: sei.latitudeDeg || 0,
            longitudeDeg: sei.longitudeDeg || 0,
            headingDeg: sei.headingDeg || 0,
            autopilotState: this.mapAutopilot(sei.autopilotState),
            steeringWheelAngle: sei.steeringWheelAngle || 0,
            brakeApplied: sei.brakeApplied || false,
            acceleratorPedalPosition: sei.acceleratorPedalPosition || 0,
            blinkerOnLeft: sei.blinkerOnLeft || false,
            blinkerOnRight: sei.blinkerOnRight || false
        };
    },

    mapGear: function(protoGear) {
        // C# and Proto enums match: Park=0, Drive=1, Reverse=2, Neutral=3
        return protoGear;
    },

    mapAutopilot: function(protoAp) {
        // C# and Proto enums match: None=0, SelfDriving=1, Autosteer=2, Tacc=3
        return protoAp;
    },

    getPath: function() {
        if (!this.frames) return [];
        return this.frames.map(f => [f.data.latitudeDeg, f.data.longitudeDeg]);
    }
};
