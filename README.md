# CityPortal

Multitenant municipal forms and case management platform — built as an architecture demo.
Finnish cities can deploy the same codebase with fully tenant-specific forms, fields, and validation rules, configured entirely through the database without code changes.

**Live stack:** .NET 8 · ASP.NET Core MVC · EF Core · Azure SQL · Azure Blob Storage · Terraform

---

## For recruiters and technical leads

A few things that may stand out if you're evaluating this as a portfolio piece:

- **Database-driven dynamic forms** — form definitions, field types, validation rules, and conditional visibility are all stored in SQL. Adding a new form to a new city requires zero code changes and zero deployments.
- **Multitenant architecture** — shared database with `TenantId` discriminator on all tables, URL-based tenant routing (`/{tenantSlug}/forms/...`), and full query isolation. No cross-tenant data leakage by design.
- **Hybrid data model** — relational structure for form definitions and tenant config, JSON columns for flexible submission payloads (`Dictionary<string, string>`) and attachment metadata. This is a deliberate trade-off documented below.
- **Azure-native** — Azure SQL, Azure Blob Storage, Microsoft Defender for Storage (async malware scanning via blob index tags), and App Service. Infrastructure defined with Terraform.
- **Production-level patterns** — secrets managed via .NET User Secrets locally and App Service Configuration in Azure, server-side geocoding proxy (NLS Finland API key never exposed to client), graceful degradation when blob storage is unavailable.

**Tech stack at a glance:**

| Layer | Technology |
|---|---|
| Backend | .NET 8, ASP.NET Core MVC, C# 12 |
| ORM | EF Core 8 |
| Database | SQL Server (Azure SQL in production) |
| File storage | Azure Blob Storage |
| Auth | Suomi.fi simulation (session-based) |
| Frontend | Razor Views, Bootstrap 5 |
| Maps | Leaflet.js + NLS Finland geocoding API |
| Malware scanning | Microsoft Defender for Storage |
| Infrastructure | Terraform (App Service, SQL, Storage, Defender) |

---

## For developers — the architecture problem this solves

### The problem

Municipal form systems typically face a dilemma: either you hardcode forms in code (fast to build, painful to change) or you build a full-blown form builder (flexible, but expensive and complex). This project explores a middle path: a lightweight database-driven engine where non-technical administrators can manage forms through SQL or a simple admin UI — without touching the codebase.

### How the dynamic form engine works

Forms are entirely database-driven. The data model looks like this:

```
Tenant (1) ──→ (N) FormDefinition (1) ──→ (N) FormField
                        │
                        └──→ (N) FormSubmission (1) ──→ (N) AttachmentReference
```

Each `FormField` row defines everything the rendering engine needs:

| Property | Purpose |
|---|---|
| `FieldType` | `text`, `email`, `textarea`, `select`, `radio`, `checkbox`, `date`, `file`, `map`, `hidden`, `info` |
| `IsRequired` | Server- and client-side validation |
| `ValidationRegex` | Custom pattern validation per field |
| `Options` | JSON array for select/radio: `["Option A", "Option B"]` |
| `ConditionalOnField` / `ConditionalOnValue` | Show/hide field based on another field's value |
| `DisplayOrder` | Rendering sequence |
| `GroupName` | Visual grouping in the rendered form |

The Razor view iterates this list and delegates rendering to a `_FormField.cshtml` partial, which switches on `FieldType`. The result: one view file handles every form across every tenant.

### Why JSON columns for submission data?

Submission values are stored as `Dictionary<string, string>` in a JSON column rather than as typed relational rows. This was a deliberate choice:

- **Form fields change over time** — if a city adds a new field, old submissions don't have that field. A relational model would need nullable columns for every possible field or a separate value table (EAV). The JSON column handles sparse data naturally.
- **No schema migration per form change** — adding a field to a form is an INSERT into `FormField`, not an `ALTER TABLE`.
- **Trade-off accepted** — querying specific field values across submissions is harder with JSON. For this use case (case management, not analytics), the flexibility outweighs the query cost. An analytics layer would need a different approach.

### Conditional field visibility

Fields can declare a dependency on another field's value:

```sql
-- Show "describe_damage" only when "incident_type" = "damage"
ConditionalOnField = 'incident_type'
ConditionalOnValue = 'damage'
```

Client-side JavaScript (`conditional-fields.js`) reads `data-conditional-on` and `data-conditional-value` attributes and toggles visibility. Server-side validation skips hidden conditional fields — both layers are consistent.

### Multitenant isolation

Tenancy is resolved from the URL slug on every request by `TenantResolver`. All EF Core queries include a `.Where(x => x.TenantId == tenant.Id)` scope — there is no global query filter, which is intentional to keep the isolation explicit and auditable. Blob storage uses path prefix isolation: `{tenantSlug}/{formSlug}/{submissionId}/{filename}`.

### File upload pattern — what not to do

One non-obvious ASP.NET Core gotcha discovered during development: model binding with `Dictionary<string, IFormFile?>` silently drops files when `IFormFile` is nullable. The fix is to read files directly from `Request.Form.Files` and match by input name pattern (`Files[fieldKey]`). This is documented in the code and in `ARCHITECTURE-HANDOFF.md`.

### Azure deployment gotcha — Windows to Linux zip paths

`Compress-Archive` in PowerShell produces zip files with backslash path separators. Linux App Service silently fails to extract these — static files deploy but never change. The fix is to use `System.IO.Compression` directly and replace `\` with `/` in entry names. This cost several hours and is worth knowing about.

---

## Project structure

```
Controllers/
  FormController.cs        — Public form rendering, submission, file upload, download
  AdminController.cs       — Admin inbox, detail view, status management
  AuthController.cs        — Suomi.fi login simulation
  GeocodingController.cs   — Server-side reverse geocoding proxy (NLS Finland)

Models/
  FormModels.cs            — Tenant, FormDefinition, FormField, FormSubmission,
                             AttachmentReference, MalwareScanStatus

Services/
  TenantResolver.cs        — Resolves Tenant from URL slug
  FormService.cs           — Form and field CRUD
  SubmissionService.cs     — Submission CRUD
  FormValidationService.cs — Server-side field validation including file validation
  BlobStorageService.cs    — Azure Blob Storage with Defender integration

Data/
  AppDbContext.cs           — EF Core context with JSON column configuration
  DbSeeder.cs               — Seeds demo tenants, forms, fields, sample submissions

Views/
  Form/Render.cshtml        — Dynamic form renderer (delegates to _FormField partial)
  Admin/Inbox.cshtml        — Filterable submission list
  Admin/Detail.cshtml       — Submission detail, image preview, case management
  Shared/_FormField.cshtml  — Renders individual fields by FieldType

Infra/
  main.tf                   — App Service, Azure SQL, Blob Storage, Defender for Storage
```

---

## Running locally

**Prerequisites:** .NET 8 SDK, SQL Server (LocalDB works), Azure Storage account or Azurite emulator.

```bash
git clone https://github.com/Fujitsu-JohannaE/CityPortal
cd CityPortal

# Set secrets (never commit these)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\\mssqllocaldb;Database=CityPortal;Trusted_Connection=True;"
dotnet user-secrets set "ConnectionStrings:AzureBlobStorage" "<your-connection-string>"
dotnet user-secrets set "NlsGeocoding:ApiKey" "<your-nls-api-key>"

dotnet ef database update   # runs migrations + seeds demo data
dotnet run
```

Demo tenants seeded: `helsinki`, `tampere`, `oulu` — navigate to `http://localhost:5000/helsinki/forms` to start.

---

## Architecture decisions

For the full rationale behind key decisions — including the hybrid relational/JSON data model, file upload handling, Defender for Storage integration, and Azure deployment notes — see [`ARCHITECTURE-HANDOFF.md`](./ARCHITECTURE-HANDOFF.md).

---

## What's next

A React/TypeScript frontend branch is planned — the backend will be refactored into a pure REST API (`[ApiController]`, JSON responses) while keeping the same dynamic form engine. This will demonstrate the same architecture under two different frontend paradigms and make the trade-offs between server-side rendering and SPA explicit.
