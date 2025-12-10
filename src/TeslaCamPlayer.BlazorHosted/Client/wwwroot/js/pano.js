window.teslaPano = {
    scene: null,
    camera: null,
    renderer: null,
    controls: null,
    requestId: null,
    videos: {},

    init: function (containerId, videoElements) {
        this.dispose(); // Ensure clean state

        const container = document.getElementById(containerId);
        if (!container) {
            console.error("Pano container not found: " + containerId);
            return;
        }

        const width = container.clientWidth;
        const height = container.clientHeight;

        // 1. Setup Scene
        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color(0x111111);

        // 2. Setup Camera
        this.camera = new THREE.PerspectiveCamera(75, width / height, 0.1, 100);
        this.camera.position.set(0, 0, 0.1); // Start slightly offset to avoid gimbal lock at exact 0

        // 3. Setup Renderer
        this.renderer = new THREE.WebGLRenderer({ antialias: true });
        this.renderer.setSize(width, height);
        this.renderer.setPixelRatio(window.devicePixelRatio);
        container.appendChild(this.renderer.domElement);

        // 4. Setup Controls
        // Ensure OrbitControls is loaded
        if (typeof THREE.OrbitControls === 'undefined') {
            console.error("OrbitControls not loaded");
            return;
        }
        this.controls = new THREE.OrbitControls(this.camera, this.renderer.domElement);
        this.controls.enableDamping = true;
        this.controls.dampingFactor = 0.05;
        this.controls.enableZoom = true;
        this.controls.enablePan = false; // We want rotation, not panning the camera origin
        this.controls.rotateSpeed = -0.5; // Negative to reverse drag direction for "inside" feel (dragging background)

        // Limit zoom to stay inside
        const radius = 10;
        this.controls.minDistance = 0.1;
        this.controls.maxDistance = radius - 2.0;

        // 5. Create Cylinder Segments
        // Order: Left Repeater -> Left Pillar -> Front -> Right Pillar -> Right Repeater -> Back
        // We map these to a circle.
        // Front is Center (0 deg).
        // Left is +Angle, Right is -Angle (or vice versa depending on system).
        // Let's assume standard CCW from +X.
        // But for "Inside" view, let's just place them relative to -Z (Front).

        // Configuration for segments
        // We use 6 segments of 60 degrees (PI/3).
        // Total 360.
        // Height calculation: Video 4:3.
        // Arc Length = R * theta = 10 * PI/3 = 10.47
        // Height = 10.47 * (3/4) = 7.85
        const segmentHeight = 7.85;
        const segmentRadius = 10;

        // Map of logical position to Camera Key
        // Angles are in radians.
        // 0 is usually +X in ThreeJS Cylinder.
        // We want Front at -Z. -Z is 90 deg (PI/2) from +X? Or 270?
        // Let's just create a Group and rotate it later.

        // We will arrange segments linearly in circle:
        // Segment 1 (Front): Center.
        // Segment 2 (Left Pillar): Left of Front.
        // ...

        const segments = [
            { key: "Front",         angleOffset: 0 },
            { key: "LeftBPillar",   angleOffset: 1 },  // +60 deg
            { key: "LeftRepeater",  angleOffset: 2 },  // +120 deg
            { key: "Back",          angleOffset: 3 },  // +180 deg
            { key: "RightRepeater", angleOffset: 4 },  // +240 deg (-120)
            { key: "RightBPillar",  angleOffset: 5 }   // +300 deg (-60)
        ];

        const group = new THREE.Group();
        this.scene.add(group);

        const segmentAngle = Math.PI / 3; // 60 deg

        segments.forEach(seg => {
            const videoEl = videoElements[seg.key];
            if (!videoEl) {
                console.warn("Missing video element for: " + seg.key);
                return;
            }

            // Create Video Texture
            const texture = new THREE.VideoTexture(videoEl);
            texture.minFilter = THREE.LinearFilter;
            texture.magFilter = THREE.LinearFilter;
            texture.format = THREE.RGBFormat;

            // Create Segment Geometry
            // CylinderGeometry(radiusTop, radiusBottom, height, radialSegments, heightSegments, openEnded, thetaStart, thetaLength)
            // thetaStart: default starts at +X axis (0).
            const geometry = new THREE.CylinderGeometry(
                segmentRadius, segmentRadius, segmentHeight,
                32, 1, true,
                0, segmentAngle // Create a generic 60 deg slice starting at 0
            );

            // Correct UVs if needed? Standard Cylinder UVs should map full texture to the thetaLength slice.
            // Actually, standard cylinder UVs map u=0..1 to theta=0..2PI.
            // If we use thetaLength < 2PI, UVs usually cover the whole segment?
            // Wait, CylinderGeometry UV generation:
            // u = ( currentAngle ) / ( 2 * PI ) usually?
            // If so, we need to remap UVs to 0..1 for just this slice.
            // EASIER APPROACH: PlaneGeometry curved or just remap UVs.
            // Let's check ThreeJS docs or behavior.
            // Actually, simpler is to use `planeGeometry` and bend it, OR just map UVs manually.

            // Let's try manual UV fix for the slice.
            // Or use a helper function to create geometry.

            // Alternative: Scale X = -1 for inside view.

            const material = new THREE.MeshBasicMaterial({ map: texture, side: THREE.BackSide });
            const mesh = new THREE.Mesh(geometry, material);

            // Position and Rotate the slice
            // The slice is created from 0 to 60 deg.
            // We want to rotate it to the correct slot.
            // seg.angleOffset * 60 deg.
            mesh.rotation.y = seg.angleOffset * segmentAngle;

            // Fix UVs:
            // Access position attribute to calculate UVs?
            // Actually, let's use a simpler primitive: Plane, positioned and rotated?
            // No, user wants curved "Sphere/Cylinder".
            // Let's assume CylinderGeometry allows correct mapping or we accept slight distortion/tiling.
            // Wait, if I create a cylinder sector, ThreeJS maps 0..1 U to the WHOLE circumference usually?
            // "The u coordinate is calculated by the angle... u = phi / ( 2 * PI )"
            // So if I have 60 deg, u goes from 0 to 1/6. The video will be squished 6x!
            // I need to fix UVs.

            // Flip for inside view
            mesh.scale.x = -1; // Mirror horizontally because we are looking from inside

            group.add(mesh);
        });

        // Rotate group so "Front" (offset 0) is at -Z.
        // Currently Front is 0..60 deg (starting at +X).
        // Center of Front slice is +30 deg.
        // We want Center of Front slice to be at -Z (which is +90 deg or 270 deg?).
        // -Z is 270 deg (3PI/2) in standard math (if X=0).
        // Let's just rotate the Group until it looks right.
        // Start with -PI/2 - (segmentAngle/2).
        // Trial and error or logic:
        // +X is 0. Front is 0..60. Center 30.
        // We want Center to be at 90 deg (Left)? No, -90 (Right)?
        // User looks at -Z.
        // Let's rotate group by -90 deg - 30 deg = -120 deg.
        group.rotation.y = -Math.PI / 2 - (segmentAngle / 2);

        // Handle Resize
        this.onResize = () => {
            if (!container) return;
            const w = container.clientWidth;
            const h = container.clientHeight;
            this.camera.aspect = w / h;
            this.camera.updateProjectionMatrix();
            this.renderer.setSize(w, h);
        };
        window.addEventListener('resize', this.onResize);

        // Animation Loop
        const animate = () => {
            this.requestId = requestAnimationFrame(animate);
            this.controls.update();
            this.renderer.render(this.scene, this.camera);
        };
        animate();
    },

    dispose: function () {
        if (this.requestId) {
            cancelAnimationFrame(this.requestId);
            this.requestId = null;
        }
        if (this.onResize) {
            window.removeEventListener('resize', this.onResize);
        }
        if (this.renderer) {
            this.renderer.domElement.remove();
            this.renderer.dispose();
        }
        // Dispose textures/materials?
        // Ideally yes, traverse scene.
        if (this.scene) {
            this.scene.traverse((object) => {
                if (object.geometry) object.geometry.dispose();
                if (object.material) {
                    if (object.material.map) object.material.map.dispose();
                    object.material.dispose();
                }
            });
            this.scene = null;
        }
        this.controls = null;
        this.camera = null;
        this.renderer = null;
    }
};

