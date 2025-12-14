window.teslaPano = {
    scene: null,
    camera: null,
    renderer: null,
    controls: null,
    dragControls: null,
    requestId: null,
    meshes: {},

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

        // Add some light for better visibility of 3D arrangement
        const ambientLight = new THREE.AmbientLight(0xffffff, 0.5);
        this.scene.add(ambientLight);
        const directionalLight = new THREE.DirectionalLight(0xffffff, 0.5);
        directionalLight.position.set(5, 10, 5);
        this.scene.add(directionalLight);

        // 2. Setup Camera
        this.camera = new THREE.PerspectiveCamera(75, width / height, 0.1, 100);
        this.camera.position.set(0, 5, 10); // Start outside, looking down/at car

        // 3. Setup Renderer
        this.renderer = new THREE.WebGLRenderer({ antialias: true });
        this.renderer.setSize(width, height);
        this.renderer.setPixelRatio(window.devicePixelRatio);
        container.appendChild(this.renderer.domElement);

        // 4. Setup OrbitControls
        if (typeof THREE.OrbitControls === 'undefined') {
            console.error("OrbitControls not loaded");
            return;
        }
        this.controls = new THREE.OrbitControls(this.camera, this.renderer.domElement);
        this.controls.enableDamping = true;
        this.controls.dampingFactor = 0.05;
        this.controls.enableZoom = true;
        this.controls.enablePan = true;
        this.controls.minDistance = 1;
        this.controls.maxDistance = 20;
        this.controls.target.set(0, 0, 0); // Look at center (car)

        // 5. Setup DragControls
        if (typeof THREE.DragControls !== 'undefined') {
            this.dragControls = new THREE.DragControls([], this.camera, this.renderer.domElement);
            this.dragControls.enabled = false;

            // Disable OrbitControls while dragging
            this.dragControls.addEventListener('dragstart', () => {
                this.controls.enabled = false;
            });
            this.dragControls.addEventListener('dragend', () => {
                this.controls.enabled = true;
            });
        } else {
            console.error("DragControls not loaded");
        }

        // 6. Create Plane Meshes
        // Scaled size in meters. Tesla video is ~4:3 or 16:9?
        // Prompt said 1920x1440 which is 4:3.
        // Plane width 2m. Height = 2 * (1440/1920) = 1.5m.
        const planeWidth = 2;
        const planeHeight = planeWidth * (1440 / 1920);

        const keys = Object.keys(videoElements);
        keys.forEach(key => {
            const videoEl = videoElements[key];
            if (!videoEl) return;

            const texture = new THREE.VideoTexture(videoEl);
            texture.minFilter = THREE.LinearFilter;
            texture.magFilter = THREE.LinearFilter;
            texture.format = THREE.RGBFormat;

            const geometry = new THREE.PlaneGeometry(planeWidth, planeHeight);
            // DoubleSide so we can see it from inside and outside
            const material = new THREE.MeshBasicMaterial({ map: texture, side: THREE.DoubleSide });
            const mesh = new THREE.Mesh(geometry, material);

            // Assign name for identification
            mesh.name = key;

            this.meshes[key] = mesh;
            this.scene.add(mesh);
        });

        // Add meshes to drag controls
        if (this.dragControls) {
            this.dragControls.getObjects().push(...Object.values(this.meshes));
        }

        // 7. Set Default Transforms
        // Note: Coordinates are X (Right), Y (Up), Z (Forward/Back?).
        // Three.js standard: Y is Up. Z is usually depth.
        // Prompt said: +z forward, +x right, +y up.

        // Front: Forward (+Z)
        this.setMeshTransform('Front', { position: [0, 1.4, 2.2], rotation: [0, 0, 0] });
        // Back: Backward (-Z). Rotate 180 (PI) around Y.
        this.setMeshTransform('Back', { position: [0, 0.8, -2.3], rotation: [0, Math.PI, 0] });

        // Left Repeater: Left (-X). Angled rearward.
        // Position: -0.9, 1.0, 1.2
        // Rotation: 45 deg rearward. Facing generally back-left?
        // If facing back-left, rotation around Y.
        // 0 is facing +Z (Front).
        // +90 is Left (+X? No, standard Right Hand Rule: Thumb=Y, Index=Z? No.)
        // Three.js: Right Handed system. Y up, X right, Z out of screen (towards viewer).
        // Wait, "Z out of screen" is standard. But prompt said "+z forward".
        // If +Z is forward, then we are looking down -Z usually?
        // Let's assume the prompt's coordinate system: +Z forward.
        // Then Back is -Z.
        // Front mesh at +2.2 Z. Facing +Z? If plane normal is +Z, and camera is at +inf Z looking -Z, we see front of plane.
        // If Front cam looks FORWARD, the image should be displayed on a plane that faces the viewer who is standing in front of the car?
        // No, in a 360 view, we want to see what the camera sees.
        // If I am at origin (driver seat), looking forward (+Z), I see the Front camera feed.
        // So the Front camera plane should be at +Z distance, facing -Z (towards origin)?
        // Or if I orbit *outside* the car, I see the car. The cameras project outwards.
        // Let's stick to the prompt's suggested defaults:
        // Front: rot [0,0,0]. If plane creates normally facing +Z, then [0,0,0] faces +Z.
        // If we want to view it from outside (looking at the car), we want the texture facing OUT.
        // If Front is at Z=2.2, facing Z=0, rotation should be PI?
        // The prompt says: "Front... rotation: [0, 0, 0]".
        // "Back... rotation: [0, Math.PI, 0]".
        // This suggests the planes face "Forward" by default (0,0,0) and we rotate them to face the direction of the camera.
        // The prompt assumes we orbit *around* the car.

        this.setMeshTransform('LeftRepeater', { position: [-0.9, 1.0, 1.2], rotation: [0, Math.PI / 4, 0] });
        this.setMeshTransform('RightRepeater', { position: [0.9, 1.0, 1.2], rotation: [0, -Math.PI / 4, 0] });

        // Pillars: Side facing.
        this.setMeshTransform('LeftBPillar', { position: [-0.9, 1.3, -0.5], rotation: [0, Math.PI / 2, 0] });
        this.setMeshTransform('RightBPillar', { position: [0.9, 1.3, -0.5], rotation: [0, -Math.PI / 2, 0] });

        // 8. Load Saved Configs (Override defaults)
        this.loadConfigs();

        // 9. Resize Handler
        this.onResize = () => {
            if (!container) return;
            const w = container.clientWidth;
            const h = container.clientHeight;
            this.camera.aspect = w / h;
            this.camera.updateProjectionMatrix();
            this.renderer.setSize(w, h);
        };
        window.addEventListener('resize', this.onResize);

        // 10. Animation Loop
        const animate = () => {
            this.requestId = requestAnimationFrame(animate);
            this.controls.update();
            this.renderer.render(this.scene, this.camera);
        };
        animate();
    },

    setMeshTransform: function(key, { position, rotation }) {
        const mesh = this.meshes[key];
        if (mesh) {
            if (position) mesh.position.set(...position);
            if (rotation) mesh.rotation.set(...rotation);
        }
    },

    getMeshTransform: function(key) {
        const mesh = this.meshes[key];
        if (mesh) {
            return {
                position: [mesh.position.x, mesh.position.y, mesh.position.z],
                rotation: [mesh.rotation.x, mesh.rotation.y, mesh.rotation.z]
            };
        }
        return null;
    },

    enableCalibration: function(enabled) {
        if (this.dragControls) {
            this.dragControls.enabled = enabled;
        }
    },

    saveConfigs: function() {
        const configs = {};
        Object.keys(this.meshes).forEach(key => {
            configs[key] = this.getMeshTransform(key);
        });
        try {
            localStorage.setItem('teslaCamConfigs', JSON.stringify(configs));
            console.log("Configs saved");
        } catch (e) {
            console.error("Failed to save configs", e);
        }
    },

    loadConfigs: function() {
        try {
            const saved = localStorage.getItem('teslaCamConfigs');
            if (saved) {
                const configs = JSON.parse(saved);
                Object.keys(configs).forEach(key => this.setMeshTransform(key, configs[key]));
            }
        } catch (e) {
            console.error("Failed to load configs", e);
        }
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
        }

        if (this.dragControls) {
            this.dragControls.dispose();
        }

        this.camera = null;
        this.renderer = null;
        this.meshes = {};
    }
};
