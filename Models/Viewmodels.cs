namespace CityPortal.Models;

// ─── Passed to Form/Render.cshtml ────────────────────────────────────────────

public class FormViewModel
{
    public FormDefinition Definition { get; set; } = default!;
    public List<FormField> Fields { get; set; } = new();

    // POST values — name="Values[fieldKey]"
    public Dictionary<string, string> Values { get; set; } = new();

    // File uploads — name="Files[fieldKey]"
    public Dictionary<string, IFormFile?> Files { get; set; } = new();

    // Validation errors keyed by FieldKey
    public Dictionary<string, string> Errors { get; set; } = new();

    // Auth state
    public bool IsAuthenticated { get; set; }
    public Dictionary<string, string> PrefilledData { get; set; } = new();

    // Helpers used in Razor
    public string GetValue(string key)
    {
        if (Values.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v)) return v;
        if (PrefilledData.TryGetValue(key, out var p)) return p;
        return string.Empty;
    }

    public bool HasError(string key) => Errors.ContainsKey(key);
    public string GetError(string key) => Errors.GetValueOrDefault(key, string.Empty);

    // Group fields for fieldset rendering
    public IEnumerable<IGrouping<string, FormField>> GroupedFields =>
        Fields.OrderBy(f => f.DisplayOrder)
              .GroupBy(f => f.GroupName ?? string.Empty);
}

// ─── Passed to _FormField.cshtml partial ────────────────────────────────────

public class FormFieldViewModel
{
    public FormField Field { get; set; }
    public FormViewModel FormVm { get; set; }

    public FormFieldViewModel(FormField field, FormViewModel formVm)
    {
        Field = field;
        FormVm = formVm;
    }
}

// ─── Admin inbox list ────────────────────────────────────────────────────────

public class SubmissionListViewModel
{
    public List<FormSubmission> Submissions { get; set; } = new();
    public string? StatusFilter { get; set; }
    public string? FormFilter { get; set; }
    public List<string> AvailableForms { get; set; } = new();
    public string TenantName { get; set; } = default!;
}

// ─── Admin detail view ───────────────────────────────────────────────────────

public class SubmissionDetailViewModel
{
    public FormSubmission Submission { get; set; } = default!;
    public List<FormField> Fields { get; set; } = new();   // for label lookup
    public string TenantName { get; set; } = default!;

    // Render FormData as ordered label+value pairs using field definitions
    public IEnumerable<(string Label, string Value)> GetDisplayRows()
    {
        foreach (var field in Fields.OrderBy(f => f.DisplayOrder))
        {
            if (field.FieldType == FieldTypes.Hidden) continue;
            if (field.FieldType == FieldTypes.Info) continue;

            var value = Submission.FormData.GetValueOrDefault(field.FieldKey, "—");
            yield return (field.Label, value);
        }
    }
}