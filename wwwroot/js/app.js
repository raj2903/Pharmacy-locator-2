let map;
let userMarker;
let pharmacyMarkers = [];

$(document).ready(function () {
    initMap();

    $('#searchForm').on('submit', function (e) {
        e.preventDefault();
        clearMessages();
        const address = $('#address').val().trim();
        const zip = $('#zip').val().trim();
        const radius = parseFloat($('#radius').val());
        const unit = $('#unit').val();

        if (!address || !zip) {
            showFormMessage('Address and ZIP code are required.');
            return;
        }

        if (isNaN(radius) || radius <= 0) {
            showFormMessage('Radius must be greater than zero.');
            return;
        }

        searchPharmacies({ address, zip, radius, unit });
    });
});

function initMap() {
    map = L.map('map').setView([37.0902, -95.7129], 4);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; OpenStreetMap contributors'
    }).addTo(map);
}

function searchPharmacies(payload) {
    toggleLoading(true);
    showListMessage('');

    $.ajax({
        method: 'POST',
        url: '/api/geocode',
        data: JSON.stringify({ address: payload.address, zip: payload.zip }),
        contentType: 'application/json'
    })
        .done(function (geoResponse) {
            const { latitude, longitude, formattedAddress } = geoResponse;
            updateUserMarker(latitude, longitude, formattedAddress);
            fetchPharmacies({
                latitude,
                longitude,
                radius: payload.radius,
                unit: payload.unit
            });
        })
        .fail(function (xhr) {
            const message = xhr.responseJSON?.error || 'Unable to geocode that address.';
            showFormMessage(message);
            toggleLoading(false);
        });
}

function fetchPharmacies(payload) {
    $.ajax({
        method: 'POST',
        url: '/api/pharmacies',
        data: JSON.stringify(payload),
        contentType: 'application/json'
    })
        .done(function (response) {
            renderPharmacies(response.results || [], payload);
        })
        .fail(function (xhr) {
            const message = xhr.responseJSON?.error || 'Could not load pharmacies right now.';
            showListMessage(message);
        })
        .always(function () {
            toggleLoading(false);
        });
}

function updateUserMarker(lat, lng, label) {
    if (userMarker) {
        map.removeLayer(userMarker);
    }
    map.setView([lat, lng], 13);
    userMarker = L.marker([lat, lng], {
        icon: L.icon({
            iconUrl: 'https://cdn.jsdelivr.net/gh/pointhi/leaflet-color-markers@v1.0/img/marker-icon-red.png',
            shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
            iconSize: [25, 41],
            iconAnchor: [12, 41],
            popupAnchor: [1, -34],
            shadowSize: [41, 41]
        })
    }).addTo(map);
    userMarker.bindPopup(`<strong>Your location</strong><br>${label || 'Search center'}`).openPopup();
}

function renderPharmacies(pharmacies, payload) {
    pharmacyMarkers.forEach(m => map.removeLayer(m));
    pharmacyMarkers = [];
    $('#pharmacyList').empty();

    if (!pharmacies.length) {
        showListMessage('No pharmacies found for that search.');
        return;
    }

    const bounds = L.latLngBounds();
    pharmacies.forEach((pharmacy, index) => {
        const marker = L.marker([pharmacy.latitude, pharmacy.longitude]).addTo(map);
        const popupHtml = `<div class="popup"><strong>${pharmacy.name}</strong><br>${pharmacy.address}<br>${pharmacy.distanceText}` +
            `${pharmacy.phone ? `<br>Phone: ${pharmacy.phone}` : ''}` +
            `${pharmacy.website ? `<br><a href="${pharmacy.website}" target="_blank" rel="noopener">Website</a>` : ''}` +
            `<br><a href="https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(pharmacy.name + ' ' + pharmacy.address)}" target="_blank" rel="noopener">Open in Google Maps</a>` +
            `</div>`;
        marker.bindPopup(popupHtml);
        marker.on('click', function () {
            map.setView([pharmacy.latitude, pharmacy.longitude], 15);
        });
        pharmacyMarkers.push(marker);
        bounds.extend([pharmacy.latitude, pharmacy.longitude]);

        const listItem = $(
            `<li data-index="${index}">
                <div class="result-title">${pharmacy.name}</div>
                <div class="result-address">${pharmacy.address}</div>
                <div class="result-distance">Distance: ${pharmacy.distanceText}</div>
                ${pharmacy.phone ? `<div class="result-phone">Phone: ${pharmacy.phone}</div>` : ''}
                ${pharmacy.website ? `<a class="result-link" href="${pharmacy.website}" target="_blank" rel="noopener">Website</a>` : ''}
            </li>`
        );

        listItem.on('click', function () {
            focusOnMarker(index);
        });

        $('#pharmacyList').append(listItem);
    });

    if (userMarker) {
        bounds.extend(userMarker.getLatLng());
    }
    map.fitBounds(bounds, { padding: [30, 30] });
}

function focusOnMarker(index) {
    const marker = pharmacyMarkers[index];
    if (marker) {
        marker.openPopup();
        map.setView(marker.getLatLng(), 15);
    }
}

function toggleLoading(isLoading) {
    if (isLoading) {
        $('#loadingIndicator').removeClass('hidden');
        $('#searchButton').prop('disabled', true).text('Searching...');
    } else {
        $('#loadingIndicator').addClass('hidden');
        $('#searchButton').prop('disabled', false).text('Search Pharmacies');
    }
}

function showFormMessage(message) {
    $('#formMessage').text(message);
}

function showListMessage(message) {
    $('#listMessage').text(message);
}

function clearMessages() {
    $('#formMessage').text('');
    $('#listMessage').text('');
}
