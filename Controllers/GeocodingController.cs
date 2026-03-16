using System.Globalization;
using Microsoft.AspNetCore.Mvc;

namespace CityPortal.Controllers;

/// <summary>
/// Proxies reverse geocoding requests to the NLS Finland (Maanmittauslaitos) API.
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
}
