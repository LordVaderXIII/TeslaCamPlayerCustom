window.teslaPano = {
    scene: null,
    camera: null,
    renderer: null,
    controls: null, // Used for transform controls mainly now, as we use custom logic for look
    transformControl: null,
    raycaster: null,
    mouse: null,
    selectedMesh: null,
    selectionBox: null,
    requestId: null,
    meshes: {},
    halos: {}, // Store halo helper objects
    isCalibrationEnabled: false,

    // View state
    lat: 0,
    lon: 0,
    phi: 0,
    theta: 0,
    isUserInteracting: false,
    onPointerDownPointerX: 0,
    onPointerDownPointerY: 0,
    onPointerDownLon: 0,
    onPointerDownLat: 0,

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

        const ambientLight = new THREE.AmbientLight(0xffffff, 0.5);
        this.scene.add(ambientLight);
        const directionalLight = new THREE.DirectionalLight(0xffffff, 0.5);
        directionalLight.position.set(5, 10, 5);
        this.scene.add(directionalLight);

        // 2. Setup Camera (First Person View)
        this.camera = new THREE.PerspectiveCamera(75, width / height, 0.1, 100);
        this.camera.position.set(0, 1.4, 0); // Approx eye height inside car
        this.camera.target = new THREE.Vector3(0, 0, 0);

        // 3. Setup Renderer
        this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true }); // Alpha for transparency support
        this.renderer.setSize(width, height);
        this.renderer.setPixelRatio(window.devicePixelRatio);
        container.appendChild(this.renderer.domElement);

        // 4. Setup Controls (Custom Look Around)
        // Note: We are NOT using OrbitControls for the camera view anymore,
        // to strictly lock position and allow "First Person" look around.

        container.style.touchAction = 'none'; // Prevent scrolling on mobile
        container.addEventListener('pointerdown', this.onPointerDown.bind(this));
        container.addEventListener('pointermove', this.onPointerMove.bind(this));
        container.addEventListener('pointerup', this.onPointerUp.bind(this));
        container.addEventListener('wheel', this.onDocumentMouseWheel.bind(this));

        // 5. Setup TransformControls (for Calibration)
        if (typeof THREE.TransformControls !== 'undefined') {
            this.transformControl = new THREE.TransformControls(this.camera, this.renderer.domElement);
            this.transformControl.setMode('translate');

            this.transformControl.addEventListener('dragging-changed', (event) => {
                // If dragging gizmo, disable look-around interaction
                this.isUserInteracting = false;
            });

            this.scene.add(this.transformControl);
        }

        this.raycaster = new THREE.Raycaster();
        this.mouse = new THREE.Vector2();

        // 6. Create Curved Screens (Cylindrical Segments)
        // Radius of cylinder: 3m
        // Video Aspect: 4:3 (1440x1920 or similar, mostly 1280x960)
        // We want them to form a cylinder. 6 cameras.
        // ThetaLength = PI/3 (60 degrees).

        const radius = 3;
        const radialSegments = 32;
        const heightSegments = 1;
        const openEnded = true;
        const thetaLength = Math.PI / 3; // 60 degrees

        // Calculate height based on aspect ratio (assuming 4:3)
        // Arc length = radius * thetaLength = 3 * (PI/3) = PI = ~3.14m
        // If width is 3.14m, and aspect is 4:3, height = 3.14 * (3/4) = 2.35m
        const arcLength = radius * thetaLength;
        const planeHeight = arcLength * (3 / 4);

        const keys = Object.keys(videoElements);
        keys.forEach(key => {
            const videoEl = videoElements[key];
            if (!videoEl) return;

            const texture = new THREE.VideoTexture(videoEl);
            texture.minFilter = THREE.LinearFilter;
            texture.magFilter = THREE.LinearFilter;
            texture.format = THREE.RGBFormat;

            // Texture wrapping/orientation might need adjustment for Cylinder
            // Cylinder UVs wrap 0..1 around the whole cylinder usually,
            // but for a segment (thetaLength), it maps 0..1 to that segment?
            // Actually Three.js CylinderGeometry UVs map 0..1 to the *entire* 2PI usually?
            // Let's verify. If thetaLength is specified, UVs usually cover the segment 0..1.
            // But we need to check texture.center/rotation if video comes in flipped.
            // Usually video textures are fine.

            const material = new THREE.MeshBasicMaterial({
                map: texture,
                side: THREE.DoubleSide,
                transparent: true,
                opacity: 0.9
            });

            const geometry = new THREE.CylinderGeometry(
                radius, // radiusTop
                radius, // radiusBottom
                planeHeight,
                radialSegments,
                heightSegments,
                openEnded,
                0, // thetaStart
                thetaLength // thetaLength
            );

            // Invert scale X to make the video face "inward" correctly?
            // Cylinder geometry faces OUT. We are INSIDE.
            // DoubleSide material handles visibility.
            // But the text might be mirrored.
            // If we look from inside, standard UVs + DoubleSide:
            // Texture is mapped 0->1 left->right on the *outside*.
            // From inside, looking out, 0 is on the left? No, 0 is on your right.
            // We usually need to flip scale.x = -1 for inside views.
            geometry.scale(-1, 1, 1);

            const mesh = new THREE.Mesh(geometry, material);
            mesh.name = key;

            // Create Halo (Edges)
            // Use EdgesGeometry with threshold to avoid internal grid lines (15 degrees)
            const edges = new THREE.EdgesGeometry(geometry, 15);
            const line = new THREE.LineSegments(edges, new THREE.LineBasicMaterial({ color: 0x0088ff, transparent: true, opacity: 0.8, linewidth: 2 }));
            line.visible = true; // Default
            mesh.add(line);
            this.halos[key] = line;

            this.meshes[key] = mesh;
            this.scene.add(mesh);
        });

        // 7. Set Default Transforms (Arranged in a circle)
        // 0 deg = Front. 60 deg = Right Repeater?
        // Let's space them out by 60 degrees.
        // Cylinder centers are positioned at (0,0,0) by default but rotated?
        // No, CylinderGeometry creates the mesh around the origin.
        // We need to rotate the mesh around Y to place it in the circle.
        // Since we are using "TransformControls" which moves the *object*,
        // the object origin is the center of the cylinder arc.

        // Wait, if the geometry is created centered at origin (vertical axis),
        // moving the mesh moves the center of curvature.
        // To arrange them "surrounding" the center, we should KEEP them at (0,0,0) position
        // and only ROTATE them on Y axis?
        // Yes! If geometry radius is 3, the surface is at 3m.
        // So position should be (0,0,0) (or vertical offset) and rotation Y varies.
        // But the user wants "ability to be placed in all locations".
        // So we allow moving them. But defaults should be good.

        // Default layout:
        this.setMeshTransform('Front', { position: [0, 0, 0], rotation: [0, Math.PI, 0] }); // Facing back? No, looking forward.
        // Note: Camera looks at -Z by default (if using standard).
        // If we want Front camera to be in front (-Z), we need rotation.
        // Cylinder starts at thetaStart=0 (usually +X or +Z depending on coord system).
        // Let's assume standard Three.js: +Z is out of screen (Back), -Z is Into screen (Front).
        // We want Front video at -Z.

        // Let's refine defaults during verification. For now, space them 60 deg.
        // We offset Y by 0 (relative to camera 1.4? No, mesh y=0 means center at 0. Camera at 1.4.
        // We probably want mesh center at 1.4 too.

        const yPos = 1.4;
        this.setMeshTransform('Front', { position: [0, yPos, 0], rotation: [0, Math.PI, 0] });
        this.setMeshTransform('RightRepeater', { position: [0, yPos, 0], rotation: [0, Math.PI - (Math.PI/3), 0] });
        this.setMeshTransform('RightBPillar', { position: [0, yPos, 0], rotation: [0, Math.PI - (2*Math.PI/3), 0] });
        this.setMeshTransform('Back', { position: [0, yPos, 0], rotation: [0, 0, 0] });
        this.setMeshTransform('LeftBPillar', { position: [0, yPos, 0], rotation: [0, (2*Math.PI/3), 0] }); // Check signs
        this.setMeshTransform('LeftRepeater', { position: [0, yPos, 0], rotation: [0, (Math.PI/3), 0] });

        // 8. Load Saved Configs
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
            this.updateCamera(); // Handle drag look
            this.renderer.render(this.scene, this.camera);
        };
        animate();
    },

    // --- Interaction Logic ---

    onPointerDown: function(event) {
        if (event.isPrimary === false) return;

        // If clicking on Gizmo?
        // We need to check if we are in calibration mode and clicking an object.
        // But for "Look Around", we always allow dragging unless we are interacting with Gizmo.

        // Raycast for selection
        if (this.isCalibrationEnabled) {
            const rect = this.renderer.domElement.getBoundingClientRect();
            this.mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
            this.mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
            this.raycaster.setFromCamera(this.mouse, this.camera);
            const intersects = this.raycaster.intersectObjects(Object.values(this.meshes));

            if (intersects.length > 0) {
                // We clicked an object.
                // If gizmo is already active, TransformControls handles it?
                // We should select this object.
                this.selectObject(intersects[0].object);
                // If we clicked an object, we probably want to select it, not drag view?
                // But typically user might want to drag view even if mouse is over object (unless dragging object).
                // Let's rely on TransformControls 'dragging-changed' to disable view drag.
            } else {
                 // Clicked empty space.
                 // Maybe deselect?
            }
        }

        this.isUserInteracting = true;
        this.onPointerDownPointerX = event.clientX;
        this.onPointerDownPointerY = event.clientY;
        this.onPointerDownLon = this.lon;
        this.onPointerDownLat = this.lat;

        // Capture pointer to track outside container
        // event.target.setPointerCapture(event.pointerId);
        // Not strictly necessary if we listen on document, but we listen on container.
    },

    onPointerMove: function(event) {
        if (event.isPrimary === false) return;
        if (!this.isUserInteracting) return;

        // Calculate delta
        const clientX = event.clientX;
        const clientY = event.clientY;

        const factor = 0.15; // Sensitivity
        this.lon = (this.onPointerDownPointerX - clientX) * factor + this.onPointerDownLon;
        this.lat = (clientY - this.onPointerDownPointerY) * factor + this.onPointerDownLat;
    },

    onPointerUp: function(event) {
        if (event.isPrimary === false) return;
        this.isUserInteracting = false;
    },

    onDocumentMouseWheel: function(event) {
        // FOV Zoom
        const fov = this.camera.fov + event.deltaY * 0.05;
        this.camera.fov = THREE.MathUtils.clamp(fov, 30, 100);
        this.camera.updateProjectionMatrix();
    },

    updateCamera: function() {
        // Clamp latitude to avoid flipping
        this.lat = Math.max(-85, Math.min(85, this.lat));

        // Convert lat/lon to 3D point on unit sphere
        this.phi = THREE.MathUtils.degToRad(90 - this.lat);
        this.theta = THREE.MathUtils.degToRad(this.lon);

        const x = 500 * Math.sin(this.phi) * Math.cos(this.theta);
        const y = 500 * Math.cos(this.phi);
        const z = 500 * Math.sin(this.phi) * Math.sin(this.theta);

        // Look at that point
        this.camera.lookAt(x, y + this.camera.position.y, z);
        // Note: + camera.position.y to look "relative" to eye level if we want,
        // but (x,y,z) is direction vector effectively if large enough.
        // Actually, lookAt expects a world position.
        // Camera is at (0, 1.4, 0).
        // Target is (x,y,z).
        // Simple spherical coords from origin is fine.
    },

    // --- Management Logic ---

    selectObject: function(object) {
        if (this.selectedMesh === object) return;

        this.selectedMesh = object;

        if (object) {
            if (this.transformControl) {
                this.transformControl.attach(object);
            }
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
            this.selectObject(null);
        }

        if (this.transformControl) {
            this.transformControl.enabled = enabled;
            this.transformControl.visible = enabled;
        }
    },

    setTransformMode: function(mode) {
        if (this.transformControl) {
            this.transformControl.setMode(mode); // 'translate' or 'rotate'
        }
    },

    setHalo: function(enabled) {
        Object.values(this.halos).forEach(line => {
            line.visible = enabled;
        });
    },

    setOpacity: function(opacity) {
        Object.values(this.meshes).forEach(mesh => {
            if (mesh.material) {
                mesh.material.opacity = opacity;
            }
        });
    },

    saveConfigs: function() {
        const configs = {};
        Object.keys(this.meshes).forEach(key => {
            configs[key] = this.getMeshTransform(key);
        });
        try {
            localStorage.setItem('teslaCamConfigs_v2', JSON.stringify(configs)); // Use v2 to avoid conflict with old plane configs
            console.log("Configs saved");
        } catch (e) {
            console.error("Failed to save configs", e);
        }
    },

    loadConfigs: function() {
        try {
            const saved = localStorage.getItem('teslaCamConfigs_v2');
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
        // cleanup listeners

        if (this.renderer) {
            if (this.renderer.domElement && this.renderer.domElement.parentNode) {
                this.renderer.domElement.parentNode.removeChild(this.renderer.domElement);
            }
            this.renderer.dispose();
        }

        this.scene = null;
        this.camera = null;
        this.renderer = null;
        this.meshes = {};
        this.halos = {};
        this.selectedMesh = null;
        this.transformControl = null;
    }
};
