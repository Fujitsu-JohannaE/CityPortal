using CityPortal.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace CityPortal.Data;

public class AppDbContext : DbContext
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<FormDefinition> FormDefinitions => Set<FormDefinition>();
    public DbSet<FormField> FormFields => Set<FormField>();
    public DbSet<FormSubmission> FormSubmissions => Set<FormSubmission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ─── Tenant ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Tenant>(e =>
        {
            e.ToTable("Tenants");
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).HasMaxLength(200).IsRequired();
            e.Property(t => t.Slug).HasMaxLength(100).IsRequired();
            e.HasIndex(t => t.Slug).IsUnique();
        });

        // ─── FormDefinition ──────────────────────────────────────────────────
        modelBuilder.Entity<FormDefinition>(e =>
        {
            e.ToTable("FormDefinitions");
            e.HasKey(f => f.Id);
            e.Property(f => f.TenantId).IsRequired();
            e.Property(f => f.Slug).HasMaxLength(200).IsRequired();
            e.Property(f => f.Title).HasMaxLength(300).IsRequired();
            e.Property(f => f.Description).HasMaxLength(2000);

            e.HasIndex(f => new { f.TenantId, f.Slug }).IsUnique();

            e.HasOne<Tenant>()
             .WithMany()
             .HasForeignKey(f => f.TenantId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(f => f.Fields)
             .WithOne()
             .HasForeignKey(ff => ff.FormDefinitionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── FormField ───────────────────────────────────────────────────────
        modelBuilder.Entity<FormField>(e =>
        {
            e.ToTable("FormFields");
            e.HasKey(f => f.Id);
            e.Property(f => f.FieldKey).HasMaxLength(100).IsRequired();
            e.Property(f => f.Label).HasMaxLength(300).IsRequired();
            e.Property(f => f.FieldType).HasMaxLength(50).IsRequired();
            e.Property(f => f.ValidationRegex).HasMaxLength(500);
            e.Property(f => f.HelpText).HasMaxLength(1000);
            e.Property(f => f.Placeholder).HasMaxLength(300);
            e.Property(f => f.DefaultValue).HasMaxLength(500);
            e.Property(f => f.GroupName).HasMaxLength(200);
            e.Property(f => f.ConditionalOnField).HasMaxLength(100);
            e.Property(f => f.ConditionalOnValue).HasMaxLength(300);

            // Options is a JSON column — stores ["A","B","C"]
            e.Property(f => f.Options)
             .HasColumnType("nvarchar(max)");
        });

        // ─── FormSubmission (semi-structured: relational header + JSON columns) ──
        modelBuilder.Entity<FormSubmission>(e =>
        {
            e.ToTable("FormSubmissions");
            e.HasKey(s => s.Id);
            e.Property(s => s.TenantId).IsRequired();
            e.Property(s => s.FormDefinitionId).IsRequired();
            e.Property(s => s.FormSlug).HasMaxLength(200).IsRequired();
            e.Property(s => s.FormTitle).HasMaxLength(300).IsRequired();
            e.Property(s => s.SuomiFiHetu).HasMaxLength(20);
            e.Property(s => s.SuomiFiName).HasMaxLength(300);
            e.Property(s => s.AnonymousEmail).HasMaxLength(300);
            e.Property(s => s.Status).HasMaxLength(50).IsRequired();
            e.Property(s => s.AssignedTo).HasMaxLength(200);
            e.Property(s => s.InternalNotes).HasMaxLength(4000);

            e.HasIndex(s => new { s.TenantId, s.SubmittedAt });
            e.HasIndex(s => new { s.TenantId, s.Status });

            e.HasOne<Tenant>()
             .WithMany()
             .HasForeignKey(s => s.TenantId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne<FormDefinition>()
             .WithMany()
             .HasForeignKey(s => s.FormDefinitionId)
             .OnDelete(DeleteBehavior.NoAction);

            // ★ FormData — JSON column (Dictionary<string, string>)
            e.Property(s => s.FormData)
             .HasColumnType("nvarchar(max)")
             .HasConversion(
                 v => JsonSerializer.Serialize(v, JsonOptions),
                 v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonOptions)
                      ?? new Dictionary<string, string>(),
                 new ValueComparer<Dictionary<string, string>>(
                     (a, b) => JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions),
                     v => JsonSerializer.Serialize(v, JsonOptions).GetHashCode(),
                     v => JsonSerializer.Deserialize<Dictionary<string, string>>(
                              JsonSerializer.Serialize(v, JsonOptions), JsonOptions)!));

            // ★ Attachments — JSON column (List<AttachmentReference>)
            e.Property(s => s.Attachments)
             .HasColumnType("nvarchar(max)")
             .HasConversion(
                 v => JsonSerializer.Serialize(v, JsonOptions),
                 v => JsonSerializer.Deserialize<List<AttachmentReference>>(v, JsonOptions)
                      ?? new List<AttachmentReference>(),
                 new ValueComparer<List<AttachmentReference>>(
                     (a, b) => JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions),
                     v => JsonSerializer.Serialize(v, JsonOptions).GetHashCode(),
                     v => JsonSerializer.Deserialize<List<AttachmentReference>>(
                              JsonSerializer.Serialize(v, JsonOptions), JsonOptions)!));
        });
    }
}
