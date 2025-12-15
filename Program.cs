using System.Text.Json;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection("ApiKeys"));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/geocode", async Task<IResult> (GeocodeRequest request, IHttpClientFactory httpClientFactory, IOptions<ApiKeyOptions> options) =>
{
    if (string.IsNullOrWhiteSpace(request.Address) || string.IsNullOrWhiteSpace(request.Zip))
    {
        return Results.BadRequest(new { error = "Address and ZIP code are required." });
    }

    var apiKey = options.Value.OpenCage;
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return Results.Problem("Geocoding API key is missing. Set ApiKeys:OpenCage in configuration.");
    }

    var query = Uri.EscapeDataString($"{request.Address}, {request.Zip}");
    var url = $"https://api.opencagedata.com/geocode/v1/json?q={query}&key={apiKey}";

    try
    {
        var client = httpClientFactory.CreateClient();
        var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            return Results.Problem($"Geocoding request failed with status code {response.StatusCode}.");
        }

        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);

        if (!document.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
        {
            return Results.BadRequest(new { error = "No coordinates found for that address." });
        }

        var firstResult = results[0];
        var geometry = firstResult.GetProperty("geometry");
        var formatted = firstResult.TryGetProperty("formatted", out var formattedElement) ? formattedElement.GetString() : "";

        var geocode = new GeocodeResponse
        {
            Latitude = geometry.GetProperty("lat").GetDouble(),
            Longitude = geometry.GetProperty("lng").GetDouble(),
            FormattedAddress = formatted ?? request.Address
        };

        return Results.Ok(geocode);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Geocoding failed: {ex.Message}");
    }
}).WithName("Geocode");

app.MapPost("/api/pharmacies", async Task<IResult> (PharmacySearchRequest request, IHttpClientFactory httpClientFactory, IOptions<ApiKeyOptions> options) =>
{
    if (request.Latitude is null || request.Longitude is null)
    {
        return Results.BadRequest(new { error = "Latitude and longitude are required." });
    }

    if (request.Radius <= 0)
    {
        return Results.BadRequest(new { error = "Radius must be greater than zero." });
    }

    var apiKey = options.Value.Foursquare;
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return Results.Problem("Places API key is missing. Set ApiKeys:Foursquare in configuration.");
    }

    var unit = string.Equals(request.Unit, "km", StringComparison.OrdinalIgnoreCase) ? "km" : "mi";
    var radiusMeters = unit == "mi"
        ? request.Radius * 1609.34
        : request.Radius * 1000;

    var client = httpClientFactory.CreateClient();
    client.BaseAddress = new Uri("https://api.foursquare.com");
    client.DefaultRequestHeaders.Remove("Authorization");
    client.DefaultRequestHeaders.Add("Authorization", apiKey);

    var uri = $"/v3/places/search?query=pharmacy&ll={request.Latitude},{request.Longitude}&radius={(int)radiusMeters}&sort=DISTANCE&limit=20";

    try
    {
        var response = await client.GetAsync(uri);
        if (!response.IsSuccessStatusCode)
        {
            return Results.Problem($"Pharmacy search failed with status code {response.StatusCode}.");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        var list = new List<PharmacyResult>();
        if (document.RootElement.TryGetProperty("results", out var results))
        {
            foreach (var item in results.EnumerateArray())
            {
                var location = item.GetProperty("location");
                var geocodes = item.GetProperty("geocodes");
                var mainGeo = geocodes.GetProperty("main");
                var distanceMeters = item.TryGetProperty("distance", out var distanceElement) ? distanceElement.GetDouble() : 0;
                var distanceValue = unit == "mi" ? distanceMeters / 1609.34 : distanceMeters / 1000;
                var phone = item.TryGetProperty("tel", out var telElement) ? telElement.GetString() : null;
                var website = item.TryGetProperty("website", out var websiteElement) ? websiteElement.GetString() : null;
                var formattedAddress = location.TryGetProperty("formatted_address", out var formattedAddressElement)
                    ? formattedAddressElement.GetString()
                    : BuildAddress(location);

                list.Add(new PharmacyResult
                {
                    Name = item.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "Pharmacy" : "Pharmacy",
                    Address = formattedAddress ?? "",
                    DistanceText = $"{distanceValue:F1} {unit}",
                    Latitude = mainGeo.GetProperty("latitude").GetDouble(),
                    Longitude = mainGeo.GetProperty("longitude").GetDouble(),
                    Phone = phone,
                    Website = website
                });
            }
        }

        var responseDto = new PharmacySearchResponse
        {
            Results = list,
            Unit = unit
        };

        return Results.Ok(responseDto);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Pharmacy search failed: {ex.Message}");
    }
}).WithName("Pharmacies");

app.Run();

static string BuildAddress(JsonElement location)
{
    var parts = new List<string>();
    if (location.TryGetProperty("address", out var address))
    {
        parts.Add(address.GetString() ?? string.Empty);
    }

    if (location.TryGetProperty("locality", out var city))
    {
        parts.Add(city.GetString() ?? string.Empty);
    }

    if (location.TryGetProperty("region", out var region))
    {
        parts.Add(region.GetString() ?? string.Empty);
    }

    if (location.TryGetProperty("country", out var country))
    {
        parts.Add(country.GetString() ?? string.Empty);
    }

    return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}

record ApiKeyOptions
{
    public string? OpenCage { get; init; }
    public string? Foursquare { get; init; }
}

record GeocodeRequest
{
    public string? Address { get; init; }
    public string? Zip { get; init; }
}

record GeocodeResponse
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string FormattedAddress { get; init; } = string.Empty;
}

record PharmacySearchRequest
{
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double Radius { get; init; }
    public string? Unit { get; init; }
}

record PharmacyResult
{
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string DistanceText { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string? Phone { get; init; }
    public string? Website { get; init; }
}

record PharmacySearchResponse
{
    public string Unit { get; init; } = "mi";
    public IEnumerable<PharmacyResult> Results { get; init; } = Array.Empty<PharmacyResult>();
}
