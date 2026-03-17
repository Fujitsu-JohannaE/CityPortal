# CityPortal — Architecture & Implementation Handoff

## Overview

CityPortal is a **multi-tenant municipal forms and case management platform** built for Finnish cities.
Citizens submit dynamic forms (complaints, service requests, permits) through a public-facing portal.
City officials manage submissions through an admin inbox with case tracking, status management, and attachment handling.

**Source code**: https://github.com/Fujitsu-JohannaE/CityPortal

---

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | .NET 8, ASP.NET Core MVC, C# 12 |
| Database | EF Core 8 + SQL Server (Azure SQL in production) |
| File Storage | Azure Blob Storage (`Azure.Storage.Blobs`) |
| Authentication | Suomi.fi simulation (session-based) |
| Frontend | Razor Views, Bootstrap 5, Bootstrap Icons |
| Maps | Leaflet.js with OpenStreetMap tiles |
| Geocoding | NLS Finland (Maanmittauslaitos) reverse geocoding API, proxied server-side |
| Malware Scanning | Microsoft Defender for Storage (async blob index tags) |
| Infrastructure | Terraform (Azure App Service, SQL, Storage, Defender) |

---

## Multi-Tenancy Architecture

- **Shared database** with `TenantId` discriminator column on all tables
- **URL-based tenant routing**: `/{tenantSlug}/forms/...`, `/{tenantSlug}/admin/...`
- **Shared Azure Blob Storage** account — files isolated by blob path prefix: `{tenantSlug}/{formSlug}/{submissionId}/{filename}`
- `TenantResolver` service resolves tenant from URL slug
- All queries are scoped by `TenantId` — no cross-tenant data leakage

---

## Dynamic Form Engine

Forms are **entirely database-driven** — no code changes needed to add/modify forms.

### Data Model

```
Tenant (1) ──→ (N) FormDefinition (1) ──→ (N) FormField
                        │
                        └──→ (N) FormSubmission (1) ──→ (N) AttachmentReference
```

### FormField Configuration

Each field row defines:
- `FieldKey` — unique key used in form data dictionary
- `FieldType` — `text`, `email`, `tel`, `textarea`, `select`, `radio`, `checkbox`, `date`, `number`, `file`, `map`, `info`, `hidden`
- `IsRequired`, `ValidationRegex`, `HelpText`, `Placeholder`
- `Options` — JSON array for select/radio: `["Option A", "Option B"]`
- `GroupName` — visual grouping in the form
- `DisplayOrder` — rendering order
- `ConditionalOnField` / `ConditionalOnValue` — show/hide field based on another field's value

### Form Submission Storage

- `FormData` column stores a `Dictionary<string, string>` as JSON — holds all field values
- `Attachments` column stores `List<AttachmentReference>` as JSON — blob path references
- Uploaded file names are also written back into `FormData` for display in admin views

---

## Key Implementation Patterns

### File Upload Handling

```
DO:   Use Request.Form.Files (IFormFileCollection) directly
DON'T: Use Dictionary<string, IFormFile?> model binding — ASP.NET Core skips nullable IFormFile
```

- Files are matched by input name pattern: `Files[fieldKey]`
- Multiple files per field supported (`multiple` attribute on input)
- Server-side validation: allowed extensions, content types, max 5 MB per file
- Submission ID is generated **before** the upload loop (used in blob paths)
- File names stored back in FormData dictionary for admin display

### Azure Blob Storage

- `BlobStorageService` wraps all blob operations with validation
- Upload path: `{tenantSlug}/{formSlug}/{submissionId}/{filename}`
- Download is proxied through the app — no direct blob URLs exposed to clients
- `try/catch` on all blob operations — returns 503 gracefully if storage is unavailable
- Attachment metadata saved to DB even if upload fails (for resilience)

### Microsoft Defender for Storage Integration

- Blobs are scanned asynchronously after upload
- Scan result read from blob index tag: `"Malware Scanning scan results"`
- Statuses: `Pending`, `Clean`, `Malicious`, `Error`
- Malicious files are blocked from download (HTTP 403)
- Admin UI shows scan status badges with manual refresh button

### Reverse Geocoding (Map Fields)

- Browser requests GPS permission → shows Leaflet map with draggable marker
- Coordinates sent to `/api/geocoding/reverse` server-side proxy
- Proxy calls NLS Finland Pelias API — **API key stays server-side**
- Address pre-fills the target text field
- **Critical**: Format lat/lon with `CultureInfo.InvariantCulture` — Finnish locale uses comma decimals which breaks URLs
- Coordinates stored in hidden inputs: `{fieldKey}_lat`, `{fieldKey}_lon`

### Conditional Fields

- Client-side JS (`conditional-fields.js`) shows/hides fields based on another field's value
- Driven by `data-conditional-on` and `data-conditional-value` attributes
- Server-side validation skips hidden conditional fields

---

## Project Structure

```
Controllers/
  FormController.cs      — Public form rendering, submission, file upload, download
  AdminController.cs     — Admin inbox, submission detail, attachment download, status updates
  AuthController.cs      — Suomi.fi login simulation
  GeocodingController.cs — NLS geocoding reverse proxy (attribute-routed: /api/geocoding)

Models/
  Formmodels.cs          — Tenant, FormDefinition, FormField, FormSubmission,
                           AttachmentReference, MalwareScanStatus, AttachmentPolicy

Services/
  TenantResolver.cs      — Resolves Tenant from URL slug
  FormService.cs         — CRUD for FormDefinitions and FormFields
  SubmissionService.cs   — CRUD for FormSubmissions
  FormValidationService.cs — Server-side field validation (including file validation)
  BlobStorageService.cs  — Azure Blob Storage upload/download/delete with Defender integration

Data/
  AppDbContext.cs        — EF Core context with JSON column configuration
  DbSeeder.cs            — Seeds demo tenants, forms, fields, and sample submissions

Views/
  Form/
    Index.cshtml         — List available forms for a tenant
    Render.cshtml        — Dynamic form rendering (uses _FormField partial)
    Confirmation.cshtml  — Post-submission success page with auto-redirect
  Admin/
    Inbox.cshtml         — Filterable submission list with status badges
    Detail.cshtml        — Submission detail with image previews, map, case management
  Shared/
    _Layout.cshtml       — Bootstrap 5 layout with tenant navigation
    _FormField.cshtml    — Renders individual form fields by type

wwwroot/
  js/
    location-map.js      — Leaflet map initialization, GPS, reverse geocoding, marker
    conditional-fields.js — Show/hide fields based on conditional logic
  lib/leaflet/           — Leaflet JS/CSS + fullscreen plugin (local files, no CDN)
  css/site.css           — Custom styles
```

---

## Configuration & Secrets Management

| Setting | Local Dev | Azure |
|---|---|---|
| SQL Connection | `appsettings.json` (LocalDB) | App Service Connection Strings (SQLAzure) |
| Blob Storage Connection | .NET User Secrets | App Service Connection Strings (Custom) |
| NLS Geocoding API Key | .NET User Secrets | App Service App Settings (`NlsGeocoding__ApiKey`) |
| Storage Account URI | `appsettings.json` | App Service App Settings (`AzureStorage__ServiceUri`) |

**Important**: Secrets (API keys, connection strings with keys) must **never** be in `appsettings.json`.
Use `dotnet user-secrets` locally and App Service Configuration in Azure.

---

## Azure Deployment Notes

- App Service runs on **Linux** — zip deploy must use **forward slashes** in file paths
- `Compress-Archive` (PowerShell) creates backslashes → static files fail silently on Linux
- Fix: Create zip programmatically with `System.IO.Compression` and replace `\` with `/`
- Visual Studio Publish (Zip Deploy) may have the same issue — verify static files after deploy
- `ASPNETCORE_ENVIRONMENT=Development` is set for the dev App Service (shows detailed errors)

---

## Lessons Learned / Gotchas

1. **ASP.NET Core `Dictionary<string, IFormFile?>` binding is broken** — the nullable `IFormFile?` causes the binder to skip files entirely. Use `Request.Form.Files` directly.

2. **Always generate entity IDs before using them in paths** — `Guid.NewGuid()` must be called before the blob upload loop, not set by `Save()` after.

3. **Download endpoints need the same error handling as upload endpoints** — if blob storage is unavailable, admin image thumbnails (which use `<img src="...">`) will trigger unhandled exceptions on every page load.

4. **Finnish locale breaks URL-embedded coordinates** — `double.ToString()` produces `60,17` instead of `60.17`. Always use `CultureInfo.InvariantCulture`.

5. **Windows → Linux zip path separators** — `Compress-Archive` uses backslashes. Linux App Service silently fails to extract files with `\` in paths. All static file updates (JS/CSS/images) appear to deploy but nothing changes.

6. **Leaflet fullscreen plugin** — the Mapbox-hosted version references `fullscreen.png` sprite images. Replace CSS icon references with Bootstrap Icons (already loaded) to avoid missing image dependencies.

---

## Prompt for Production Implementation

Use this as a starting prompt when implementing in the production application:

```
I need to implement a multi-tenant dynamic forms and case management system. Key requirements:

1. MULTI-TENANCY: Shared database with TenantId discriminator column. URL-based tenant routing (/{tenantSlug}/...). All queries scoped by TenantId.

2. DYNAMIC FORMS: Form definitions and fields stored in database. Field types: text, email, tel, textarea, select, radio, checkbox, date, number, file, map, info, hidden. Conditional field visibility. Form data stored as JSON dictionary column.

3. FILE UPLOADS: Azure Blob Storage with path prefix isolation per tenant. Server-side validation (extensions, content types, max size). Microsoft Defender for Storage malware scanning via blob index tags. Download proxied through app (no direct blob URLs). Graceful degradation when storage is unavailable.

4. MAP/LOCATION FIELDS: Leaflet.js map with GPS geolocation. Server-side reverse geocoding proxy to NLS Finland API (API key never exposed to client). Pre-fill address field from coordinates. Coordinates stored as hidden form values.

5. ADMIN PORTAL: Inbox with filterable submission list. Detail view with image previews, lightbox, map display. Case management (status, assignee, internal notes). Malware scan status badges with refresh.

6. SECURITY: Suomi.fi authentication integration. API keys and connection strings in User Secrets (dev) / App Service Configuration (Azure). Anti-forgery tokens on all POST forms. No direct blob storage URLs exposed.

The reference implementation is at: https://github.com/Fujitsu-JohannaE/CityPortal
```
