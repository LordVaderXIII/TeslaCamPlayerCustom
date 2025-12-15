window.teslaPano = {
    scene: null,
    camera: null,
    renderer: null,
    controls: null,
    transformControl: null,
    raycaster: null,
    mouse: null,
    selectedMesh: null,
    selectionBox: null,
    requestId: null,
    meshes: {},
    isCalibrationEnabled: false,

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

        // 5. Setup TransformControls and Raycaster
        if (typeof THREE.TransformControls !== 'undefined') {
            this.transformControl = new THREE.TransformControls(this.camera, this.renderer.domElement);
            this.transformControl.setMode('translate'); // Restrict to translation as requested

            // Disable OrbitControls while dragging the gizmo
            this.transformControl.addEventListener('dragging-changed', (event) => {
                this.controls.enabled = !event.value;
            });

            this.scene.add(this.transformControl);
        } else {
            console.error("TransformControls not loaded");
        }

        this.raycaster = new THREE.Raycaster();
        this.mouse = new THREE.Vector2();

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

        // 7. Set Default Transforms
        this.setMeshTransform('Front', { position: [0, 1.4, 2.2], rotation: [0, 0, 0] });
        this.setMeshTransform('Back', { position: [0, 0.8, -2.3], rotation: [0, Math.PI, 0] });
        this.setMeshTransform('LeftRepeater', { position: [-0.9, 1.0, 1.2], rotation: [0, Math.PI / 4, 0] });
        this.setMeshTransform('RightRepeater', { position: [0.9, 1.0, 1.2], rotation: [0, -Math.PI / 4, 0] });
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

        // 10. Click Handler for Selection
        this.onPointerDown = (event) => {
            if (!this.isCalibrationEnabled) return;

            // Calculate mouse position in normalized device coordinates
            // (-1 to +1) for both components
            const rect = this.renderer.domElement.getBoundingClientRect();
            this.mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
            this.mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

            this.raycaster.setFromCamera(this.mouse, this.camera);

            const intersects = this.raycaster.intersectObjects(Object.values(this.meshes));

            if (intersects.length > 0) {
                const object = intersects[0].object;
                this.selectObject(object);
            } else {
                // Deselect if clicked in empty space (optional, but good UX)
                // However, clicking on Gizmo should not deselect.
                // Raycaster won't hit gizmo lines easily.
                // But wait, if we click on Gizmo, we might be dragging.
                // TransformControls 'dragging-changed' handles the drag state.
                // But the initial click might be intercepted here?
                // TransformControls usually handles its own interaction.
                // We should only deselect if we didn't hit anything relevant.
                // Check if we are hovering over the gizmo? simpler: don't auto-deselect for now.
                // Or: TransformControls doesn't block raycaster?
                // Let's rely on explicit selection.
            }
        };
        this.renderer.domElement.addEventListener('pointerdown', this.onPointerDown);

        // 11. Animation Loop
        const animate = () => {
            this.requestId = requestAnimationFrame(animate);
            this.controls.update();
            if (this.selectionBox) {
                this.selectionBox.update();
            }
            this.renderer.render(this.scene, this.camera);
        };
        animate();
    },

    selectObject: function(object) {
        if (this.selectedMesh === object) return;

        // Cleanup previous selection
        if (this.selectionBox) {
            this.scene.remove(this.selectionBox);
            this.selectionBox = null;
        }

        this.selectedMesh = object;

        if (object) {
            // Attach gizmo
            if (this.transformControl) {
                this.transformControl.attach(object);
            }

            // Create visual outline (BoxHelper)
            this.selectionBox = new THREE.BoxHelper(object, 0xffff00);
            this.scene.add(this.selectionBox);
        } else {
            if (this.transformControl) {
                this.transformControl.detach();
            }
        }
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
        this.isCalibrationEnabled = enabled;

        if (!enabled) {
            // Deselect everything when calibration stops
            this.selectObject(null);
        }

        if (this.transformControl) {
            this.transformControl.enabled = enabled;
            this.transformControl.visible = enabled;
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
        if (this.onPointerDown && this.renderer && this.renderer.domElement) {
            this.renderer.domElement.removeEventListener('pointerdown', this.onPointerDown);
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

        if (this.transformControl) {
            this.transformControl.dispose();
        }

        this.camera = null;
        this.renderer = null;
        this.meshes = {};
        this.selectedMesh = null;
        this.selectionBox = null;
    }
};
