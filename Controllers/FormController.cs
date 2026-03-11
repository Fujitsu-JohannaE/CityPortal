using CityPortal.Models;
using CityPortal.Services;
using Microsoft.AspNetCore.Mvc;

namespace CityPortal.Controllers;

[Route("{tenantSlug}/forms")]
public class FormController : Controller
{
    private readonly TenantResolver _tenantResolver;
    private readonly IFormService _formService;
    private readonly ISubmissionService _submissionService;
    private readonly FormValidationService _validator;

    public FormController(
        TenantResolver tenantResolver,
        IFormService formService,
        ISubmissionService submissionService,
        FormValidationService validator)
    {
        _tenantResolver = tenantResolver;
        _formService = formService;
        _submissionService = submissionService;
        _validator = validator;
    }

    // ─── Index: list available forms for this tenant ─────────────────────────

    [HttpGet("")]
    public IActionResult Index(string tenantSlug)
    {
        var tenant = _tenantResolver.Resolve(tenantSlug);
        if (tenant == null) return NotFound();

        var forms = _formService.GetAllForms(tenant.TenantId);
        ViewBag.TenantName = tenant.TenantName;
        ViewBag.TenantSlug = tenant.TenantSlug;
        return View("Index", forms);
    }

    // ─── GET: render form ─────────────────────────────────────────────────────

    [HttpGet("{slug}")]
    public IActionResult Render(string tenantSlug, string slug)
    {
        var tenant = _tenantResolver.Resolve(tenantSlug);
        if (tenant == null) return NotFound();

        var definition = _formService.GetForm(tenant.TenantId, slug);
        if (definition == null) return NotFound();

        // Simulate: if Suomi.fi required and not authenticated, show login prompt
        bool isAuthenticated = HttpContext.Session.GetString("SuomiFiName") != null;
        if (definition.RequireSuomiFi && !isAuthenticated)
        {
            TempData["ReturnUrl"] = $"/{tenantSlug}/forms/{slug}";
            return RedirectToAction("Login", "Auth", new { tenantSlug });
        }

        var vm = BuildViewModel(definition, isAuthenticated, new(), new());
        return View("Render", vm);
    }

    // ─── POST: handle form submission ─────────────────────────────────────────

    [HttpPost("{slug}")]
    [ValidateAntiForgeryToken]
    public IActionResult Submit(
        string tenantSlug,
        string slug,
        [FromForm] Dictionary<string, string> Values,
        [FromForm] Dictionary<string, IFormFile?> Files)
    {
        var tenant = _tenantResolver.Resolve(tenantSlug);
        if (tenant == null) return NotFound();

        var definition = _formService.GetForm(tenant.TenantId, slug);
        if (definition == null) return NotFound();

        bool isAuthenticated = HttpContext.Session.GetString("SuomiFiName") != null;
        if (definition.RequireSuomiFi && !isAuthenticated)
            return Forbid();

        // Server-side validation
        var errors = _validator.Validate(definition.Fields, Values);
        if (errors.Any())
        {
            var vm = BuildViewModel(definition, isAuthenticated, Values, errors);
            return View("Render", vm);
        }

        // Build submission — FormData is the JSON column payload
        var submission = new FormSubmission
        {
            TenantId = tenant.TenantId,
            FormDefinitionId = definition.Id,
            FormSlug = definition.Slug,
            FormTitle = definition.Title,
            IsAnonymous = !isAuthenticated,
            SuomiFiName = HttpContext.Session.GetString("SuomiFiName"),
            SuomiFiHetu = HttpContext.Session.GetString("SuomiFiHetu"),
            AnonymousEmail = Values.GetValueOrDefault("reporterEmail")
                            ?? Values.GetValueOrDefault("requestorEmail"),
            FormData = Values,   // ← Dictionary<string,string> → JSON column
        };

        // Simulate file attachment (in production: upload to Azure Blob)
        foreach (var file in Files.Where(f => f.Value != null))
        {
            submission.Attachments.Add(new AttachmentReference
            {
                FileName = file.Value!.FileName,
                BlobPath = $"{tenantSlug}/{slug}/{submission.Id}/{file.Value.FileName}",
                ContentType = file.Value.ContentType,
                FileSizeBytes = file.Value.Length
            });
        }

        _submissionService.Save(tenant.TenantId, submission);

        TempData["SubmittedFormTitle"] = definition.Title;
        return RedirectToAction("Confirmation", new { tenantSlug, slug, id = submission.Id });
    }

    // ─── Confirmation ─────────────────────────────────────────────────────────

    [HttpGet("{slug}/confirmation/{id}")]
    public IActionResult Confirmation(string tenantSlug, string slug, Guid id)
    {
        var tenant = _tenantResolver.Resolve(tenantSlug);
        if (tenant == null) return NotFound();

        var submission = _submissionService.GetById(tenant.TenantId, id);
        ViewBag.TenantSlug = tenantSlug;
        ViewBag.TenantName = tenant.TenantName;
        return View("Confirmation", submission);
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private FormViewModel BuildViewModel(
        FormDefinition definition,
        bool isAuthenticated,
        Dictionary<string, string> values,
        Dictionary<string, string> errors)
    {
        // Pre-fill from simulated Suomi.fi session claims
        var prefill = new Dictionary<string, string>();
        if (isAuthenticated)
        {
            var name = HttpContext.Session.GetString("SuomiFiName") ?? "";
            var address = HttpContext.Session.GetString("SuomiFiAddress") ?? "";
            prefill["complainantName"] = name;
            prefill["complainantAddress"] = address;
            prefill["requestorName"] = name;
        }

        return new FormViewModel
        {
            Definition = definition,
            Fields = definition.Fields.OrderBy(f => f.DisplayOrder).ToList(),
            Values = values,
            Errors = errors,
            IsAuthenticated = isAuthenticated,
            PrefilledData = prefill
        };
    }
}