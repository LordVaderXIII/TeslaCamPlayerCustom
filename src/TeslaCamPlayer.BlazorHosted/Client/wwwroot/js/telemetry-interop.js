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

            console.log(`Telemetry initialized. Found ${this.frames.length} data points.`);
            return this.frames.length > 0;

        } catch (e) {
            console.error("Error initializing telemetry:", e);
            return false;
        }
    },

    getTelemetry: function (timeSeconds) {
        if (!this.frames || this.frames.length === 0) return null;

        // Find the closest frame using binary search
        let low = 0;
        let high = this.frames.length - 1;
        let closest = null;

        while (low <= high) {
            const mid = Math.floor((low + high) / 2);
            if (this.frames[mid].time === timeSeconds) {
                closest = this.frames[mid];
                break;
            }

            if (this.frames[mid].time < timeSeconds) {
                low = mid + 1;
            } else {
                high = mid - 1;
            }
        }

        if (!closest) {
            const idx1 = high;
            const idx2 = low;

            const frame1 = (idx1 >= 0 && idx1 < this.frames.length) ? this.frames[idx1] : null;
            const frame2 = (idx2 >= 0 && idx2 < this.frames.length) ? this.frames[idx2] : null;

            if (!frame1) closest = frame2;
            else if (!frame2) closest = frame1;
            else closest = (Math.abs(frame2.time - timeSeconds) < Math.abs(frame1.time - timeSeconds)) ? frame2 : frame1;
        }

        // If the closest frame is more than 1 second away, treat as no data
        if (Math.abs(closest.time - timeSeconds) > 1.0) {
            return null;
        }

        const sei = closest.data;

        return {
            timestamp: new Date().toISOString(), // Placeholder
            gearState: this.mapGear(sei.gear_state),
            vehicleSpeedMps: sei.vehicle_speed_mps || 0,
            latitudeDeg: sei.latitude_deg || 0,
            longitudeDeg: sei.longitude_deg || 0,
            headingDeg: sei.heading_deg || 0,
            autopilotState: this.mapAutopilot(sei.autopilot_state),
            steeringWheelAngle: sei.steering_wheel_angle || 0,
            brakeApplied: sei.brake_applied || false,
            acceleratorPedalPosition: sei.accelerator_pedal_position || 0,
            blinkerOnLeft: sei.blinker_on_left || false,
            blinkerOnRight: sei.blinker_on_right || false
        };
    },

    mapGear: function(protoGear) {
         // Proto: PARK=0, DRIVE=1, REVERSE=2, NEUTRAL=3
         // C#: Unknown=0, Park=1, Reverse=2, Neutral=3, Drive=4
         switch(protoGear) {
             case 0: return 1; // Park
             case 1: return 4; // Drive
             case 2: return 2; // Reverse
             case 3: return 3; // Neutral
             default: return 0;
         }
    },

    mapAutopilot: function(protoAp) {
        // Proto: NONE=0, SELF_DRIVING=1, AUTOSTEER=2, TACC=3
        // C#: Inactive=0, Tacc=1, Autosteer=2, SelfDriving=3
        switch(protoAp) {
            case 0: return 0;
            case 1: return 3; // SD
            case 2: return 2; // Autosteer
            case 3: return 1; // TACC
            default: return 0;
        }
    },

    getPath: function() {
        if (!this.frames) return [];
        return this.frames.map(f => [f.data.latitude_deg, f.data.longitude_deg]);
    }
};
