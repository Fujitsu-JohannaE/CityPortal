using CityPortal.Data;
using CityPortal.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Register services — in production these would be scoped with EF Core DbContext
builder.Services.AddSingleton<InMemoryTenantStore>();   // simulates per-tenant DBs
builder.Services.AddSingleton<TenantResolver>();
builder.Services.AddSingleton<IFormService, FormService>();
builder.Services.AddSingleton<ISubmissionService, SubmissionService>();
builder.Services.AddSingleton<FormValidationService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

// Default redirect to Vantaa tenant
app.MapGet("/", () => Results.Redirect("/vantaa/forms"));

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

app.Run();