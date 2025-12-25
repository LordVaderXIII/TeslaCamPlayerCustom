window.mapInterop = {
    map: null,
    marker: null,
    polyline: null,

    initMap: function (elementId) {
        if (this.map) return;

        // Default to Tesla HQ or 0,0
        this.map = L.map(elementId).setView([0, 0], 13);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '© OpenStreetMap'
        }).addTo(this.map);

        // Custom marker icon could be added here
        this.marker = L.marker([0, 0]).addTo(this.map);
        this.polyline = L.polyline([], {color: 'blue'}).addTo(this.map);

        // Fix for map not rendering correctly in hidden tab/div
        setTimeout(() => {
            this.map.invalidateSize();
        }, 100);
    },

    updatePosition: function (lat, lon, heading) {
        if (!this.map) return;
        const latLng = [lat, lon];
        this.marker.setLatLng(latLng);

        // Only center if we are far off? Or always?
        // Let's pan to it.
        this.map.panTo(latLng);
    },

    setPath: function (latLons) {
        if (!this.map || !this.polyline) return;
        this.polyline.setLatLngs(latLons);
        if (latLons.length > 0) {
            this.map.fitBounds(this.polyline.getBounds());
        }
    },

    invalidateSize: function () {
        if (this.map) this.map.invalidateSize();
    },

    destroy: function () {
        if (this.map) {
            this.map.remove();
            this.map = null;
        }
    }
};
