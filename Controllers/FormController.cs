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
    private readonly IBlobStorageService _blobStorage;

    public FormController(
        TenantResolver tenantResolver,
        IFormService formService,
        ISubmissionService submissionService,
        FormValidationService validator,
        IBlobStorageService blobStorage)
    {
        _tenantResolver = tenantResolver;
        _formService = formService;
        _submissionService = submissionService;
        _validator = validator;
        _blobStorage = blobStorage;
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
        ViewBag.TenantSlug = tenantSlug;
        ViewBag.TenantName = tenant.TenantName;
        return View("Render", vm);
    }

    // ─── POST: handle form submission ─────────────────────────────────────────

    [HttpPost("{slug}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(
        string tenantSlug,
        string slug,
        [FromForm] Dictionary<string, string> Values)
    {
        var tenant = _tenantResolver.Resolve(tenantSlug);
        if (tenant == null) return NotFound();

        var definition = _formService.GetForm(tenant.TenantId, slug);
        if (definition == null) return NotFound();

        bool isAuthenticated = HttpContext.Session.GetString("SuomiFiName") != null;
        if (definition.RequireSuomiFi && !isAuthenticated)
            return Forbid();

        // Server-side validation
        var formFiles = Request.Form.Files;
        var errors = _validator.Validate(definition.Fields, Values, formFiles);
        if (errors.Any())
        {
            var vm = BuildViewModel(definition, isAuthenticated, Values, errors);
            return View("Render", vm);
        }

        // Generate ID up front so blob paths are deterministic
        var submissionId = Guid.NewGuid();

        // Build submission — FormData is the JSON column payload
        var submission = new FormSubmission
        {
            Id = submissionId,
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

        // Upload file attachments to shared Azure Blob Storage
        // Track file names per field so we can store them in FormData
        var filesByField = new Dictionary<string, List<string>>();
        foreach (var file in formFiles.Where(f => f.Length > 0))
        {
            var fieldKey = file.Name.Replace("Files[", "").TrimEnd(']');
            var blobPath = $"{tenantSlug}/{slug}/{submissionId}/{file.FileName}";
            try
            {
                using var stream = file.OpenReadStream();
                var (uploadedPath, uploadError) = await _blobStorage.UploadAsync(
                    "form-attachments", blobPath, stream,
                    file.ContentType, file.FileName, file.Length);

                if (uploadError != null)
                {
                    errors[fieldKey] = uploadError;
                    var vm = BuildViewModel(definition, isAuthenticated, Values, errors);
                    return View("Render", vm);
                }

                submission.Attachments.Add(new AttachmentReference
                {
                    FileName = file.FileName,
                    BlobPath = uploadedPath!,
                    ContentType = file.ContentType,
                    FileSizeBytes = file.Length
                });
            }
            catch (Exception)
            {
                // Blob storage unavailable (e.g. Azurite not running locally)
                // Store attachment reference without upload so the form still works
                submission.Attachments.Add(new AttachmentReference
                {
                    FileName = file.FileName,
                    BlobPath = blobPath,
                    ContentType = file.ContentType,
                    FileSizeBytes = file.Length,
                    MalwareScanResult = MalwareScanStatus.Error
                });
            }

            // Track file name for FormData storage
            if (!filesByField.ContainsKey(fieldKey))
                filesByField[fieldKey] = new();
            filesByField[fieldKey].Add(file.FileName);
        }

        // Store uploaded file names in FormData so they appear in admin detail / JSON
        foreach (var (fieldKey, names) in filesByField)
        {
            Values[fieldKey] = string.Join(", ", names);
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

    // ─── Attachment download (proxied through app — no direct blob URLs) ──────

    [HttpGet("{slug}/attachment/{submissionId}/{fileName}")]
    public async Task<IActionResult> DownloadAttachment(
        string tenantSlug, string slug, Guid submissionId, string fileName)
    {
        var tenant = _tenantResolver.Resolve(tenantSlug);
        if (tenant == null) return NotFound();

        var submission = _submissionService.GetById(tenant.TenantId, submissionId);
        if (submission == null) return NotFound();

        // Verify the attachment belongs to this submission
        var attachment = submission.Attachments
            .FirstOrDefault(a => a.FileName == fileName);
        if (attachment == null) return NotFound();

        // Check Defender scan result — block malicious files
        if (attachment.MalwareScanResult == MalwareScanStatus.Malicious)
            return StatusCode(403, "Tiedosto on estetty haittaohjelmien vuoksi.");

        try
        {
            var result = await _blobStorage.DownloadAsync("form-attachments", attachment.BlobPath);
            if (result == null)
                return NotFound();

            return File(result.Content, result.ContentType, result.FileName);
        }
        catch
        {
            // Blob storage unavailable (e.g. Azurite not running locally)
            return StatusCode(503, "Tiedostopalvelu ei ole käytettävissä.");
        }
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