using CityPortal.Data;
using CityPortal.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CityPortal.Services;

// ─── Tenant resolution ───────────────────────────────────────────────────────

public interface ITenantContext
{
    Guid TenantId { get; }
    string TenantSlug { get; }
    string TenantName { get; }
}

public class TenantContext : ITenantContext
{
    public Guid TenantId { get; init; }
    public string TenantSlug { get; init; } = default!;
    public string TenantName { get; init; } = default!;
}

/// <summary>
/// Resolves current tenant from the URL prefix: /vantaa/forms/... or /espoo/forms/...
/// In production this could also use subdomain, header, or JWT claim.
/// </summary>
public class TenantResolver
{
    private readonly AppDbContext _db;

    public TenantResolver(AppDbContext db) => _db = db;

    public ITenantContext? Resolve(string tenantSlug)
    {
        var tenant = _db.Tenants.FirstOrDefault(t => t.Slug == tenantSlug);
        if (tenant == null) return null;
        return new TenantContext
        {
            TenantId = tenant.Id,
            TenantSlug = tenant.Slug,
            TenantName = tenant.Name
        };
    }
}

// ─── Form service ─────────────────────────────────────────────────────────────

public interface IFormService
{
    FormDefinition? GetForm(Guid tenantId, string slug);
    List<FormDefinition> GetAllForms(Guid tenantId);
}

public class FormService : IFormService
{
    private readonly AppDbContext _db;
    public FormService(AppDbContext db) => _db = db;

    public FormDefinition? GetForm(Guid tenantId, string slug)
    {
        return _db.FormDefinitions
                  .Include(f => f.Fields)
                  .FirstOrDefault(f => f.TenantId == tenantId && f.Slug == slug && f.IsActive);
    }

    public List<FormDefinition> GetAllForms(Guid tenantId)
    {
        return _db.FormDefinitions
                  .Include(f => f.Fields)
                  .Where(f => f.TenantId == tenantId && f.IsActive)
                  .ToList();
    }
}

// ─── Submission service ───────────────────────────────────────────────────────

public interface ISubmissionService
{
    FormSubmission Save(Guid tenantId, FormSubmission submission);
    FormSubmission? GetById(Guid tenantId, Guid id);
    List<FormSubmission> GetAll(Guid tenantId, string? statusFilter, string? formFilter);
    FormSubmission UpdateStatus(Guid tenantId, Guid id, string status, string? notes, string? assignedTo);
    void UpdateAttachments(Guid tenantId, Guid submissionId, List<AttachmentReference> attachments);
}

public class SubmissionService : ISubmissionService
{
    private readonly AppDbContext _db;
    public SubmissionService(AppDbContext db) => _db = db;

    public FormSubmission Save(Guid tenantId, FormSubmission submission)
    {
        if (submission.Id == Guid.Empty)
            submission.Id = Guid.NewGuid();
        submission.TenantId = tenantId;
        submission.SubmittedAt = DateTime.UtcNow;
        _db.FormSubmissions.Add(submission);
        _db.SaveChanges();
        return submission;
    }

    public FormSubmission? GetById(Guid tenantId, Guid id) =>
        _db.FormSubmissions.FirstOrDefault(s => s.TenantId == tenantId && s.Id == id);

    public List<FormSubmission> GetAll(Guid tenantId, string? statusFilter, string? formFilter)
    {
        var query = _db.FormSubmissions
            .Where(s => s.TenantId == tenantId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(statusFilter))
            query = query.Where(s => s.Status == statusFilter);
        if (!string.IsNullOrEmpty(formFilter))
            query = query.Where(s => s.FormSlug == formFilter);

        return query.OrderByDescending(s => s.SubmittedAt).ToList();
    }

    public FormSubmission UpdateStatus(Guid tenantId, Guid id,
        string status, string? notes, string? assignedTo)
    {
        var sub = GetById(tenantId, id)
            ?? throw new KeyNotFoundException();
        sub.Status = status;
        sub.InternalNotes = notes;
        sub.AssignedTo = assignedTo;
        _db.SaveChanges();
        return sub;
    }

    public void UpdateAttachments(Guid tenantId, Guid submissionId, List<AttachmentReference> attachments)
    {
        var sub = GetById(tenantId, submissionId)
            ?? throw new KeyNotFoundException();
        sub.Attachments = attachments;
        _db.SaveChanges();
    }
}

// ─── Validation service ───────────────────────────────────────────────────────

public class FormValidationService
{
    public Dictionary<string, string> Validate(
        List<FormField> fields,
        Dictionary<string, string> values,
        IFormFileCollection? files = null)
    {
        var errors = new Dictionary<string, string>();

        foreach (var field in fields)
        {
            // Skip info blocks, hidden fields, and map widgets
            if (field.FieldType is FieldTypes.Info or FieldTypes.Hidden or FieldTypes.Map) continue;

            // File fields: check uploaded files by matching the form input name
            if (field.FieldType is FieldTypes.File)
            {
                var inputName = $"Files[{field.FieldKey}]";
                var hasFile = files != null
                    && files.Any(f => f.Name == inputName && f.Length > 0);

                if (field.IsRequired && !hasFile)
                    errors[field.FieldKey] = $"{field.Label} is required.";

                continue;
            }

            values.TryGetValue(field.FieldKey, out var raw);
            var value = raw?.Trim() ?? string.Empty;

            // Required check
            if (field.IsRequired && string.IsNullOrEmpty(value))
            {
                errors[field.FieldKey] = $"{field.Label} is required.";
                continue;
            }

            // Regex check
            if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(field.ValidationRegex))
            {
                if (!Regex.IsMatch(value, field.ValidationRegex))
                    errors[field.FieldKey] = $"{field.Label} is not in the correct format.";
            }
        }

        return errors;
    }
}