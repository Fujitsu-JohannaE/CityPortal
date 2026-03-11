using CityPortal.Data;
using CityPortal.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualBasic.FileIO;
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
    private readonly InMemoryTenantStore _store;

    public TenantResolver(InMemoryTenantStore store) => _store = store;

    public ITenantContext? Resolve(string tenantSlug)
    {
        var tenant = _store.FindTenantBySlug(tenantSlug);
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
    private readonly InMemoryTenantStore _store;
    public FormService(InMemoryTenantStore store) => _store = store;

    public FormDefinition? GetForm(Guid tenantId, string slug)
    {
        var db = _store.GetDb(tenantId);
        return db.FormDefinitions
                 .FirstOrDefault(f => f.Slug == slug && f.IsActive);
    }

    public List<FormDefinition> GetAllForms(Guid tenantId)
    {
        var db = _store.GetDb(tenantId);
        return db.FormDefinitions.Where(f => f.IsActive).ToList();
    }
}

// ─── Submission service ───────────────────────────────────────────────────────

public interface ISubmissionService
{
    FormSubmission Save(Guid tenantId, FormSubmission submission);
    FormSubmission? GetById(Guid tenantId, Guid id);
    List<FormSubmission> GetAll(Guid tenantId, string? statusFilter, string? formFilter);
    FormSubmission UpdateStatus(Guid tenantId, Guid id, string status, string? notes, string? assignedTo);
}

public class SubmissionService : ISubmissionService
{
    private readonly InMemoryTenantStore _store;
    public SubmissionService(InMemoryTenantStore store) => _store = store;

    public FormSubmission Save(Guid tenantId, FormSubmission submission)
    {
        submission.Id = Guid.NewGuid();
        submission.TenantId = tenantId;
        submission.SubmittedAt = DateTime.UtcNow;
        _store.GetDb(tenantId).Submissions.Add(submission);
        return submission;
    }

    public FormSubmission? GetById(Guid tenantId, Guid id) =>
        _store.GetDb(tenantId).Submissions.FirstOrDefault(s => s.Id == id);

    public List<FormSubmission> GetAll(Guid tenantId, string? statusFilter, string? formFilter)
    {
        var query = _store.GetDb(tenantId).Submissions.AsEnumerable();
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
        return sub;
    }
}

// ─── Validation service ───────────────────────────────────────────────────────

public class FormValidationService
{
    public Dictionary<string, string> Validate(
        List<FormField> fields,
        Dictionary<string, string> values)
    {
        var errors = new Dictionary<string, string>();

        foreach (var field in fields)
        {
            // Skip info blocks and hidden fields
            if (field.FieldType is FieldTypes.Info or FieldTypes.Hidden) continue;

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