using Microsoft.AspNetCore.Mvc;

namespace CityPortal.Controllers;

/// <summary>
/// Simulates the Suomi.fi SAML2 authentication flow.
/// In production this redirects to Sustainsys.Saml2 middleware.
/// </summary>
[Route("{tenantSlug}/auth")]
public class AuthController : Controller
{
    [HttpGet("login")]
    public IActionResult Login(string tenantSlug)
    {
        ViewBag.TenantSlug = tenantSlug;
        ViewBag.ReturnUrl = TempData["ReturnUrl"]?.ToString() ?? $"/{tenantSlug}/forms";
        return View("Login");
    }

    // Simulate successful Suomi.fi callback — sets session claims
    [HttpPost("simulate-login")]
    public IActionResult SimulateLogin(string tenantSlug, string returnUrl,
        string name, string hetu, string address)
    {
        HttpContext.Session.SetString("SuomiFiName", name);
        HttpContext.Session.SetString("SuomiFiHetu", hetu);
        HttpContext.Session.SetString("SuomiFiAddress", address);
        return Redirect(returnUrl);
    }

    [HttpPost("logout")]
    public IActionResult Logout(string tenantSlug)
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Form", new { tenantSlug });
    }
}