window.telemetryInterop = {
    worker: null,
    frames: [],
    currentVideoUrl: null,

    init: function (videoUrl) {
        return new Promise((resolve, reject) => {
            if (this.currentVideoUrl === videoUrl && this.frames.length > 0) {
                // Already loaded
                resolve(true);
                return;
            }

            console.log("Initializing telemetry worker for: " + videoUrl);
            this.frames = [];
            this.currentVideoUrl = videoUrl;

            // Terminate existing worker if any
            if (this.worker) {
                this.worker.terminate();
            }

            // Create new worker
            this.worker = new Worker('js/telemetry.worker.js');

            this.worker.onmessage = (e) => {
                const result = e.data;
                if (result.success) {
                    this.frames = result.frames;
                    console.log(`Telemetry worker initialized. Found ${this.frames.length} data points.`);
                    resolve(this.frames.length > 0);
                } else {
                    console.error("Telemetry worker failed:", result.error);
                    resolve(false);
                }
            };

            this.worker.onerror = (e) => {
                console.error("Telemetry worker error:", e);
                resolve(false);
            };

            // Resolve absolute path for proto file
            const protoUrl = new URL('dashcam.proto', document.baseURI).href;
            // Resolve absolute path for video file
            // videoUrl is likely relative (e.g. /Api/Video/...)
            const absoluteVideoUrl = new URL(videoUrl, document.baseURI).href;

            // Start the worker
            this.worker.postMessage({
                videoUrl: absoluteVideoUrl,
                protoUrl: protoUrl
            });
        });
    },

    getTelemetry: function (timeSeconds) {
        if (!this.frames || this.frames.length === 0) return null;

        // Find the closest frame
        // Assuming relatively small number of frames (e.g. 60 sec * 30 fps = 1800), linear search or reduce is fast enough.
        // Optimization: Could use binary search since frames are sorted by time
        const closest = this.frames.reduce((prev, curr) => {
            return (Math.abs(curr.time - timeSeconds) < Math.abs(prev.time - timeSeconds) ? curr : prev);
        });

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
