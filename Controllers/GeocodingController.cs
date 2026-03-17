using System.Globalization;
using Microsoft.AspNetCore.Mvc;

namespace CityPortal.Controllers;

/// <summary>
/// Proxies geocoding requests to the NLS Finland (Maanmittauslaitos) API.
/// Keeps the API key server-side — never exposed to the browser.
/// </summary>
[Route("api/geocoding")]
public class GeocodingController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiKey;

    public GeocodingController(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _apiKey = config["NlsGeocoding:ApiKey"]
            ?? throw new InvalidOperationException("NlsGeocoding:ApiKey is not configured");
    }

    [HttpGet("reverse")]
    public async Task<IActionResult> Reverse(
        [FromQuery(Name = "point.lat")] double lat,
        [FromQuery(Name = "point.lon")] double lon)
    {
        var client = _httpClientFactory.CreateClient("NlsGeocoding");

        var latStr = lat.ToString(CultureInfo.InvariantCulture);
        var lonStr = lon.ToString(CultureInfo.InvariantCulture);

        var url = $"https://avoin-paikkatieto.maanmittauslaitos.fi/geocoding/v2/pelias/reverse"
            + $"?point.lat={latStr}&point.lon={lonStr}&size=1&lang=fin"
            + $"&sources=interpolated-road-addresses"
            + $"&api-key={_apiKey}";

        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "Geocoding request failed");

        var json = await response.Content.ReadAsStringAsync();
        return Content(json, "application/json");
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string text,
        [FromQuery] string? municipality,
        [FromQuery(Name = "focus.lat")] double? focusLat,
        [FromQuery(Name = "focus.lon")] double? focusLon)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 3)
            return BadRequest("Search text must be at least 3 characters");

        var client = _httpClientFactory.CreateClient("NlsGeocoding");

        var encoded = Uri.EscapeDataString(text);
        var url = $"https://avoin-paikkatieto.maanmittauslaitos.fi/geocoding/v2/pelias/search"
            + $"?text={encoded}&size=5&lang=fin"
            + $"&sources=interpolated-road-addresses"
            + $"&api-key={_apiKey}";

        // Bias results toward user's current location (nearby addresses first)
        if (focusLat.HasValue && focusLon.HasValue)
        {
            url += $"&focus.point.lat={focusLat.Value.ToString(CultureInfo.InvariantCulture)}"
                 + $"&focus.point.lon={focusLon.Value.ToString(CultureInfo.InvariantCulture)}";
        }

        // Restrict to tenant's municipality unless the user typed a city name
        if (!string.IsNullOrWhiteSpace(municipality))
            url += $"&boundary.municipality={Uri.EscapeDataString(municipality)}";

        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "Geocoding search failed");

        var json = await response.Content.ReadAsStringAsync();
        return Content(json, "application/json");
    }
}
