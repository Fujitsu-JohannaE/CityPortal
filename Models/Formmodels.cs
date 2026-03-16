using System.Text.Json;

namespace CityPortal.Models;

// ─── Field type constants ────────────────────────────────────────────────────

public static class FieldTypes
{
    public const string Text = "text";
    public const string Email = "email";
    public const string Phone = "tel";
    public const string Textarea = "textarea";
    public const string Select = "select";
    public const string Radio = "radio";
    public const string Checkbox = "checkbox";
    public const string Date = "date";
    public const string Number = "number";
    public const string File = "file";
    public const string Hidden = "hidden";
    public const string Info = "info";
    public const string Map = "map";
}

public static class SubmissionStatus
{
    public const string New = "New";
    public const string InProgress = "InProgress";
    public const string Resolved = "Resolved";
    public const string Rejected = "Rejected";
}

// ─── Tenant ──────────────────────────────────────────────────────────────────

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;  // "vantaa" | "espoo" // used in URL prefix
    public bool IsActive { get; set; } = true;
}

// ─── Form definition ─────────────────────────────────────────────────────────

public class FormDefinition
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Slug { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public bool AllowAnonymous { get; set; } = true;
    public bool RequireSuomiFi { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public List<FormField> Fields { get; set; } = new();
}

// ─── Field definition ────────────────────────────────────────────────────────

public class FormField
{
    public Guid Id { get; set; }
    public Guid FormDefinitionId { get; set; }
    public string FieldKey { get; set; } = default!;
    public string Label { get; set; } = default!;
    public string FieldType { get; set; } = default!;
    public bool IsRequired { get; set; }
    public string? ValidationRegex { get; set; }
    public string? HelpText { get; set; }
    public string? Placeholder { get; set; }
    public string? Options { get; set; }   // JSON array: ["A","B","C"]
    public string? DefaultValue { get; set; }
    public int DisplayOrder { get; set; }
    public string? GroupName { get; set; }

    // Conditional display
    public string? ConditionalOnField { get; set; }
    public string? ConditionalOnValue { get; set; }

    // Helper: deserialise Options JSON → list
    public List<string> GetOptions() =>
        string.IsNullOrEmpty(Options)
            ? new()
            : JsonSerializer.Deserialize<List<string>>(Options) ?? new();
}

// ─── Submission (the JSON-column model) ──────────────────────────────────────

public class FormSubmission
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid FormDefinitionId { get; set; }
    public string FormSlug { get; set; } = default!;
    public string FormTitle { get; set; } = default!;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    // Identity
    public bool IsAnonymous { get; set; }
    public string? SuomiFiHetu { get; set; }   // stored encrypted in real app
    public string? SuomiFiName { get; set; }
    public string? AnonymousEmail { get; set; }

    // Case management
    public string Status { get; set; } = SubmissionStatus.New;
    public string? AssignedTo { get; set; }
    public string? InternalNotes { get; set; }

    // ★ The dynamic data lives here — JSON column
    public Dictionary<string, string> FormData { get; set; } = new();

    // Attachments: blob path references as JSON
    public List<AttachmentReference> Attachments { get; set; } = new();
}

public class AttachmentReference
{
    public string FileName { get; set; } = default!;
    public string BlobPath { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Tracks Microsoft Defender for Storage malware scan result.
    /// Values: "Pending", "Clean", "Malicious", "Error"
    /// </summary>
    public string MalwareScanResult { get; set; } = MalwareScanStatus.Pending;
}

public static class MalwareScanStatus
{
    public const string Pending = "Pending";
    public const string Clean = "Clean";
    public const string Malicious = "Malicious";
    public const string Error = "Error";
}

public static class AttachmentPolicy
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    public static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "application/pdf"
    };

    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf"
    };
}