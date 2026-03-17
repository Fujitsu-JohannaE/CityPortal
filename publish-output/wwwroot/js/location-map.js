/**
 * location-map.js
 *
 * For each .location-map element on the page:
 * 1. Asks the browser for GPS permission (Geolocation API)
 * 2. Shows the user's location on a Leaflet map (OpenStreetMap tiles)
 * 3. Reverse geocodes coordinates → address via NLS Finland (Maanmittauslaitos) API
 * 4. Pre-fills the target address field with the suggested address
 * 5. Allows clicking the map to pick a different location
 *
 * NLS API: https://avoin-paikkatieto.maanmittauslaitos.fi/geocoding/v2/pelias/reverse
 * (open channel — no API key required)
 */
document.addEventListener('DOMContentLoaded', () => {

    const mapContainers = document.querySelectorAll('.location-map');
    if (!mapContainers.length) return;

    mapContainers.forEach(container => {
        const mapId = container.id;
        const fieldKey = mapId.replace('map-', '');
        const targetFieldKey = container.dataset.targetAddressField;
        const statusEl = document.getElementById(`map-status-${fieldKey}`);
        const latInput = document.getElementById(`${fieldKey}_lat`);
        const lonInput = document.getElementById(`${fieldKey}_lon`);

        // Find the target address input by its form name
        const addressInput = document.querySelector(
            `input[name="Values[${targetFieldKey}]"]`
        );

        // ── Initialize Leaflet map (default: Helsinki area) ──────────────
        const defaultLat = 60.17;
        const defaultLon = 24.94;

        const map = L.map(mapId, {
            fullscreenControl: {
                position: 'topleft',
                title: {
                    'false': 'Koko näyttö',
                    'true': 'Poistu koko näytöstä'
                }
            }
        }).setView([defaultLat, defaultLon], 13);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
            maxZoom: 19
        }).addTo(map);

        // Fix tile rendering after fullscreen toggle
        map.on('fullscreenchange', () => {
            setTimeout(() => map.invalidateSize(), 100);
        });

        let marker = null;

        function setMarker(lat, lon) {
            if (marker) {
                marker.setLatLng([lat, lon]);
            } else {
                marker = L.marker([lat, lon], { draggable: true }).addTo(map);

                // Allow dragging the marker to refine location
                marker.on('dragend', () => {
                    const pos = marker.getLatLng();
                    updateLocation(pos.lat, pos.lng);
                });
            }
            map.setView([lat, lon], 16);
        }

        function updateLocation(lat, lon) {
            // Store coordinates in hidden inputs
            if (latInput) latInput.value = lat.toFixed(6);
            if (lonInput) lonInput.value = lon.toFixed(6);

            setMarker(lat, lon);
            reverseGeocode(lat, lon);
        }

        // ── Reverse geocode via server-side proxy (API key stays server-side) ─
        function reverseGeocode(lat, lon) {
            if (statusEl) {
                statusEl.innerHTML =
                    '<i class="bi bi-arrow-repeat me-1 spin"></i>Haetaan osoitetta...';
            }

            const url = `/api/geocoding/reverse?point.lat=${lat}&point.lon=${lon}`;

            fetch(url)
                .then(res => res.json())
                .then(data => {
                    if (data.features && data.features.length > 0) {
                        const feature = data.features[0];
                        const props = feature.properties || {};

                        // Build address from Pelias response
                        const address = buildAddress(props);

                        // Pre-fill the address field (only if user hasn't typed something)
                        if (addressInput && !addressInput._userEdited) {
                            addressInput.value = address;
                            // Highlight briefly
                            addressInput.classList.add('border-success');
                            setTimeout(() => addressInput.classList.remove('border-success'), 2000);
                        }

                        if (statusEl) {
                            statusEl.innerHTML =
                                `<i class="bi bi-geo-alt-fill text-success me-1"></i>`
                                + `<strong>${address}</strong>`
                                + `<br><span class="text-muted" style="font-size:0.75rem">`
                                + `${lat.toFixed(5)}, ${lon.toFixed(5)}`
                                + ` — Klikkaa karttaa tai siirrä merkkiä tarkentaaksesi sijaintia</span>`;
                        }

                        // Update marker popup
                        if (marker) {
                            marker.bindPopup(`<strong>${address}</strong>`).openPopup();
                        }
                    } else {
                        if (statusEl) {
                            statusEl.innerHTML =
                                `<i class="bi bi-geo-alt me-1"></i>`
                                + `Osoitetta ei löytynyt (${lat.toFixed(5)}, ${lon.toFixed(5)})`
                                + `<br><span class="text-muted" style="font-size:0.75rem">`
                                + `Klikkaa karttaa tai siirrä merkkiä</span>`;
                        }
                    }
                })
                .catch(err => {
                    console.warn('Reverse geocoding failed:', err);
                    if (statusEl) {
                        statusEl.innerHTML =
                            `<i class="bi bi-exclamation-triangle text-warning me-1"></i>`
                            + `Osoitehaku epäonnistui — koordinaatit: ${lat.toFixed(5)}, ${lon.toFixed(5)}`;
                    }
                });
        }

        /**
         * Build a readable Finnish address from Pelias response properties.
         * Typical props: { name, street, housenumber, postalcode, locality, region }
         */
        function buildAddress(props) {
            const parts = [];

            if (props.street) {
                let street = props.street;
                if (props.housenumber) street += ' ' + props.housenumber;
                parts.push(street);
            } else if (props.name) {
                parts.push(props.name);
            }

            if (props.postalcode || props.locality) {
                const cityPart = [props.postalcode, props.locality]
                    .filter(Boolean).join(' ');
                parts.push(cityPart);
            }

            return parts.join(', ') || props.label || 'Tuntematon sijainti';
        }

        // ── Click map to pick location ───────────────────────────────────
        map.on('click', (e) => {
            updateLocation(e.latlng.lat, e.latlng.lng);
        });

        // ── Track if user manually edits the address field ───────────────
        if (addressInput) {
            addressInput.addEventListener('input', () => {
                addressInput._userEdited = true;
            });
            // Reset flag if field is cleared
            addressInput.addEventListener('change', () => {
                if (!addressInput.value.trim()) {
                    addressInput._userEdited = false;
                }
            });
        }

        // ── Request GPS location ─────────────────────────────────────────
        if ('geolocation' in navigator) {
            if (statusEl) {
                statusEl.innerHTML =
                    '<i class="bi bi-crosshair me-1"></i>'
                    + 'Pyydetään sijaintilupaa...';
            }

            navigator.geolocation.getCurrentPosition(
                // Success
                (position) => {
                    const lat = position.coords.latitude;
                    const lon = position.coords.longitude;
                    updateLocation(lat, lon);
                },
                // Error
                (error) => {
                    console.warn('Geolocation error:', error.code, error.message);
                    let msg = 'Klikkaa karttaa valitaksesi sijainnin.';
                    switch (error.code) {
                        case 1: // PERMISSION_DENIED
                            msg = 'Sijaintilupa evätty. ' + msg;
                            break;
                        case 2: // POSITION_UNAVAILABLE
                            msg = 'Sijaintitietoa ei saatavilla. ' + msg;
                            break;
                        case 3: // TIMEOUT
                            msg = 'Sijaintihaun aikakatkaisu. ' + msg;
                            break;
                    }
                    if (statusEl) {
                        statusEl.innerHTML =
                            `<i class="bi bi-hand-index me-1 text-primary"></i>${msg}`;
                    }
                },
                // Options
                {
                    enableHighAccuracy: false,
                    timeout: 10000,
                    maximumAge: 60000
                }
            );
        } else {
            if (statusEl) {
                statusEl.innerHTML =
                    '<i class="bi bi-exclamation-circle text-danger me-1"></i>'
                    + 'Selaimesi ei tue paikannusta. Klikkaa karttaa valitaksesi sijainnin.';
            }
        }
    });
});
