# Pharmacy Locator

This sample app lets users enter an address/ZIP code and search radius to find nearby pharmacies. Results appear on a Leaflet map and in a sidebar list.

## Stack
- Frontend: HTML, CSS, JavaScript, jQuery, Leaflet (OpenStreetMap tiles)
- Backend: ASP.NET Core minimal API (C#)

## Getting started
1. **Install .NET 8 SDK** if it's not already available. You can grab it from <https://dotnet.microsoft.com/download>.
2. Clone or open this repository and add your API keys in `appsettings.json`:
   ```json
   {
     "ApiKeys": {
       "OpenCage": "YOUR_OPENCAGE_API_KEY",
       "Foursquare": "YOUR_FOURSQUARE_API_KEY"
     },
     "AllowedHosts": "*"
   }
   ```
   - OpenCage: <https://opencagedata.com/> (geocoding)
   - Foursquare Places API: <https://foursquare.com/developers/home> (pharmacy search)
3. Restore and run the backend:
   ```bash
   dotnet run
   ```
4. Open the app in your browser at <http://localhost:5000> (or the port shown in the console).

## How it works
1. The user enters an address, ZIP code, and radius (miles or km) and clicks **Search Pharmacies**.
2. The frontend (jQuery) posts to `/api/geocode`, which calls the OpenCage geocoding API and returns latitude/longitude.
3. Using those coordinates, the frontend posts to `/api/pharmacies`, which calls the Foursquare Places API for nearby pharmacies within the chosen radius.
4. The map recenters on the search location, drops a red marker for the user, and adds markers for each pharmacy with popup details. The sidebar shows the same list; clicking an item opens its marker popup.

## Key files
- `Program.cs` – Minimal API endpoints for geocoding and pharmacy search plus DTOs and helpers.
- `appsettings.json` – API key placeholders.
- `wwwroot/index.html` – UI markup and form.
- `wwwroot/css/site.css` – Basic styling/layout for the form, map, and sidebar.
- `wwwroot/js/app.js` – jQuery + Leaflet logic for calling the API and rendering the map/list.

## Notes
- Error handling shows clear messages for missing/invalid input, API failures, or no results.
- The backend converts radius values to meters for the Places API; distances are returned in the chosen unit for display.
- Replace the API keys before running; both services offer free tiers suitable for testing.
