using CityPortal.Models;
using CityPortal.Services;
using Microsoft.AspNetCore.Mvc;

namespace CityPortal.Controllers;

[Route("{tenantSlug}/admin")]
public class AdminController : Controller
{
    private readonly TenantResolver _tenantResolver;
    private readonly IFormService _formService;
    private readonly ISubmissionService _submissionService;
    private readonly IBlobStorageService _blobStorage;

    public AdminController(
        TenantResolver tenantResolver,
        IFormService formService,
        ISubmissionService submissionService,
        IBlobStorageService blobStorage)
    {
        _tenantResolver = tenantResolver;
        _formService = formService;
        _submissionService = submissionService;
        _blobStorage = blobStorage;
    }

    // ─── Inbox ────────────────────────────────────────────────────────────────

    [HttpGet("")]
    public IActionResult Inbox(string tenantSlug, string? status, string? form)
    {
        var tenant = _tenantResolver.Resolve(tenantSlug);
        if (tenant == null) return NotFound();

        var submissions = _submissionService.GetAll(tenant.TenantId, status, form);
        var allForms = _formService.GetAllForms(tenant.TenantId);

        var vm = new SubmissionListViewModel
        {
            Submissions = submissions,
            StatusFilter = status,
            FormFilter = form,
            AvailableForms = allForms.Select(f => f.Slug).ToList(),
            TenantName = tenant.TenantName
        };

        ViewBag.TenantSlug = tenantSlug;
        return View("Inbox", vm);
    }

    // ─── Detail ───────────────────────────────────────────────────────────────

    [HttpGet("submission/{id}")]
    public IActionResult Detail(string tenantSlug, Guid id)
    {
        var tenant = _tenantResolver.Resolve(tenantSlug);
        if (tenant == null) return NotFound();

        var submission = _submissionService.GetById(tenant.TenantId, id);
        if (submission == null) return NotFound();

        var definition = _formService.GetForm(tenant.TenantId, submission.FormSlug);

        var vm = new SubmissionDetailViewModel
        {
            Submission = submission,
            Fields = definition?.Fields ?? new(),
            TenantName = tenant.TenantName
        };

        ViewBag.TenantSlug = tenantSlug;
        return View("Detail", vm);
    }

    // ─── Update status ────────────────────────────────────────────────────────

    [HttpPost("submission/{id}/status")]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateStatus(
        string tenantSlug, Guid id,
        string status, string? internalNotes, string? assignedTo)
    {
        var tenant = _tenantResolver.Resolve(tenantSlug);
        if (tenant == null) return NotFound();

        _submissionService.UpdateStatus(tenant.TenantId, id, status, internalNotes, assignedTo);
        return RedirectToAction("Detail", new { tenantSlug, id });
    }

    // ─── Attachment download (admin) ─────────────────────────────────────────

    [HttpGet("submission/{submissionId}/attachment/{fileName}")]
    public async Task<IActionResult> DownloadAttachment(
        string tenantSlug, Guid submissionId, string fileName)
    {
        var tenant = _tenantResolver.Resolve(tenantSlug);
        if (tenant == null) return NotFound();

        var submission = _submissionService.GetById(tenant.TenantId, submissionId);
        if (submission == null) return NotFound();

        var attachment = submission.Attachments
            .FirstOrDefault(a => a.FileName == fileName);
        if (attachment == null) return NotFound();

        if (attachment.MalwareScanResult == MalwareScanStatus.Malicious)
            return StatusCode(403, "Tiedosto on estetty haittaohjelmien vuoksi.");

        var result = await _blobStorage.DownloadAsync("form-attachments", attachment.BlobPath);
        if (result == null)
            return NotFound();

        // For images, allow inline display; for PDFs, force download
        if (result.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return File(result.Content, result.ContentType);

        return File(result.Content, result.ContentType, result.FileName);
    }

    // ─── Refresh scan status for an attachment ───────────────────────────────

    [HttpPost("submission/{submissionId}/attachment/{fileName}/scan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshScanStatus(
        string tenantSlug, Guid submissionId, string fileName)
    {
        var tenant = _tenantResolver.Resolve(tenantSlug);
        if (tenant == null) return NotFound();

        var submission = _submissionService.GetById(tenant.TenantId, submissionId);
        if (submission == null) return NotFound();

        var attachment = submission.Attachments
            .FirstOrDefault(a => a.FileName == fileName);
        if (attachment == null) return NotFound();

        var scanResult = await _blobStorage.GetMalwareScanResultAsync(
            "form-attachments", attachment.BlobPath);
        attachment.MalwareScanResult = scanResult;

        _submissionService.UpdateAttachments(tenant.TenantId, submissionId, submission.Attachments);

        return RedirectToAction("Detail", new { tenantSlug, id = submissionId });
    }
}