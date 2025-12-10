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
        // Disable built-in zoom (dolly) because it moves the camera.
        // We want FOV zoom for a 360 viewer.
        this.controls.enableZoom = false;
        this.controls.enablePan = false; // We want rotation, not panning the camera origin
        this.controls.rotateSpeed = -0.5; // Negative to reverse drag direction for "inside" feel

        // Limit zoom to stay inside
        const radius = 10;
        this.controls.minDistance = 0;
        this.controls.maxDistance = radius - 2.0;

        // Custom FOV Zoom Handler
        this.onWheel = (event) => {
            event.preventDefault();

            const zoomSpeed = 0.05;
            this.camera.fov += event.deltaY * zoomSpeed;
            this.camera.fov = Math.max(10, Math.min(100, this.camera.fov));
            this.camera.updateProjectionMatrix();
        };
        container.addEventListener('wheel', this.onWheel, { passive: false });

        // 5. Create Cylinder Segments
        const segmentHeight = 7.85;
        const segmentRadius = 10;

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
            const geometry = new THREE.CylinderGeometry(
                segmentRadius, segmentRadius, segmentHeight,
                32, 1, true,
                0, segmentAngle
            );

            const material = new THREE.MeshBasicMaterial({ map: texture, side: THREE.BackSide });
            const mesh = new THREE.Mesh(geometry, material);

            // Position and Rotate the slice
            mesh.rotation.y = seg.angleOffset * segmentAngle;

            // Flip for inside view
            mesh.scale.x = -1; // Mirror horizontally because we are looking from inside

            group.add(mesh);
        });

        // Rotate group so "Front" (offset 0) is at -Z.
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

        // Remove wheel listener
        // We need the container element.
        // Since we don't store it, we can query by ID if we knew it, or just rely on DOM removal clearing listeners (mostly).
        // But better to remove it if possible.
        // We didn't store container reference. Let's try to find it again or store it in init.
        // Actually, if we remove the DOM element (renderer), the listener on 'container' still exists if container persists.
        // The container is "pano-container" (passed in init).
        const container = document.getElementById("pano-container");
        if (container && this.onWheel) {
            container.removeEventListener('wheel', this.onWheel);
        }

        if (this.renderer) {
            if (this.renderer.domElement && this.renderer.domElement.parentNode) {
                this.renderer.domElement.parentNode.removeChild(this.renderer.domElement);
            }
            this.renderer.dispose();
        }

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

        if (this.controls) {
             this.controls.dispose();
             this.controls = null;
        }

        this.camera = null;
        this.renderer = null;
    }
};
