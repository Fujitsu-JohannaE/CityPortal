using Azure.Identity;
using Azure.Storage.Blobs;
using CityPortal.Data;
using CityPortal.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ─── EF Core — SQL Server ────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ─── Azure Blob Storage (shared account for all tenants) ─────────────────────
// Local dev  → Azurite via connection string (appsettings.Development.json)
// Production → Managed Identity via storage account URI (appsettings.json)
builder.Services.AddSingleton(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("AzureBlobStorage");
    if (!string.IsNullOrEmpty(connectionString))
        return new BlobServiceClient(connectionString);

    var storageUri = builder.Configuration["AzureStorage:ServiceUri"]
        ?? throw new InvalidOperationException(
            "Configure either ConnectionStrings:AzureBlobStorage or AzureStorage:ServiceUri");
    return new BlobServiceClient(new Uri(storageUri), new DefaultAzureCredential());
});
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

// ─── Application services (scoped — one per HTTP request, matching DbContext) ─
builder.Services.AddScoped<TenantResolver>();
builder.Services.AddScoped<IFormService, FormService>();
builder.Services.AddScoped<ISubmissionService, SubmissionService>();
builder.Services.AddScoped<FormValidationService>();

// ─── HttpClient for NLS geocoding proxy ──────────────────────────────────────
builder.Services.AddHttpClient("NlsGeocoding");

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

// ─── Apply migrations and seed demo data at startup ──────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await DbSeeder.SeedAsync(db);
}

// Default redirect to Vantaa tenant
app.MapGet("/", () => Results.Redirect("/vantaa/forms"));

app.MapControllerRoute(
    name: "tenant-admin-attachment-scan",
    pattern: "{tenantSlug}/admin/submission/{submissionId}/attachment/{fileName}/scan",
    defaults: new { controller = "Admin", action = "RefreshScanStatus" });

app.MapControllerRoute(
    name: "tenant-admin-attachment",
    pattern: "{tenantSlug}/admin/submission/{submissionId}/attachment/{fileName}",
    defaults: new { controller = "Admin", action = "DownloadAttachment" });

app.MapControllerRoute(
    name: "tenant-admin-status",
    pattern: "{tenantSlug}/admin/submission/{id}/status",
    defaults: new { controller = "Admin", action = "UpdateStatus" });

app.MapControllerRoute(
    name: "tenant-admin-detail",
    pattern: "{tenantSlug}/admin/submission/{id}",
    defaults: new { controller = "Admin", action = "Detail" });

app.MapControllerRoute(
    name: "tenant-admin",
    pattern: "{tenantSlug}/admin",
    defaults: new { controller = "Admin", action = "Inbox" });

app.MapControllerRoute(
    name: "tenant-auth",
    pattern: "{tenantSlug}/auth/{action}",
    defaults: new { controller = "Auth" });

app.MapControllerRoute(
    name: "tenant-forms",
    pattern: "{tenantSlug}/forms/{action=Index}/{slug?}",
    defaults: new { controller = "Form" });

await app.RunAsync();