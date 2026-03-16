# NDjango.Admin

## Build & Test Commands

ALWAYS use `./scripts/filter-failed-tests.csx` when running tests.

```bash
# Start SQL Server (required for integration tests and sample project)
docker compose up -d db

# Build everything
dotnet build NDjango.Admin.sln
dotnet tool restore

# Run selective unit testing focusing on the classes you have changed
dotnet test NDjango.Admin.sln --filter "ForeignKeyTests" | dotnet dotnet-script ./scripts/filter-failed-tests.csx
# Run all tests when the selective runs are successful to ensure overall integrity (you MUST execute exactly like this):
dotnet test NDjango.Admin.sln | dotnet dotnet-script ./scripts/filter-failed-tests.csx

# Run a single test
dotnet test NDjango.Admin.sln --filter "FullyQualifiedName~ForeignKeyTests.RestaurantAddForm_RendersCategory_LookupFieldAsync" | dotnet dotnet-script ./scripts/filter-failed-tests.csx

# Run the sample project (requires SQL Server running)
cd sample-project/src && dotnet run -- api
# Dashboard at http://localhost:8000/admin/

# Coverage report (pipe any dotnet test or docker compose run through it)
# Shows per-file line/branch percentages + uncovered lines and partial branches
dotnet test NDjango.Admin.sln --settings "./runsettings.xml" | dotnet dotnet-script ./scripts/generate-coverage-report.csx
# Filter report to specific file(s) (case-insensitive substring match on path)
# The filter matches any file whose path contains the substring, so multiple files may appear
dotnet test NDjango.Admin.sln --settings "./runsettings.xml" --filter "ForeignKeyTests" | dotnet dotnet-script ./scripts/generate-coverage-report.csx -- "ApiDispatcher"
dotnet test NDjango.Admin.sln --settings "./runsettings.xml" --filter "FkLookupPopupTests" | dotnet dotnet-script ./scripts/generate-coverage-report.csx
```

### JavaScript (admin-dashboard.js)

Any change to `src/NDjango.Admin.AspNetCore.AdminDashboard/wwwroot/js/admin-dashboard.js` **must** include corresponding updates to its spec file (`admin-dashboard.spec.js` in the same directory). Tests must pass and maintain >99% code coverage.

```bash
# Install dependencies (once)
npm install

# Run JS tests with coverage
npm test

# Run a single test by name
npx jest --coverage --coverageReporters=text -t "filters items matching"
```

The spec uses `jest` + `jest-environment-jsdom`. The source file exposes global functions via a conditional CommonJS export (`if (typeof module !== 'undefined')`) so they are testable through `require()` while remaining transparent in the browser.



## Architecture

### Project Dependency Graph

```
NDjango.Admin.Core                              # Metadata model (MetaData, MetaEntity, MetaEntityAttr)
├── NDjango.Admin.AspNetCore                    # ASP.NET Core integration (NDjangoAdminManager, middleware)
├── NDjango.Admin.EntityFrameworkCore.Relational # EF Core → metadata loader (DbContextMetaDataLoader)
│
NDjango.Admin.AspNetCore.AdminDashboard         # The admin dashboard (depends on all three above)
```

### Project Structure

```
src/
  NDjango.Admin.Core/                              # Core metadata model
  NDjango.Admin.AspNetCore/                        # ASP.NET Core integration
  NDjango.Admin.EntityFrameworkCore.Relational/    # EF Core metadata loader
  NDjango.Admin.AspNetCore.AdminDashboard/         # Admin dashboard (this package)
    Authentication/                           # Login, permissions, password hashing, auth DB
    Authorization/                            # Auth filters
    Configuration/                            # DI extensions and options
    Dispatchers/                              # View rendering and API handlers
    Middleware/                               # Request pipeline
    Routing/                                  # URL dispatch
    Services/                                 # Metadata, entity grouping, composite manager
    ViewModels/                               # Form and list models
    wwwroot/                                  # Embedded CSS/JS

test/                                         # Integration & unit tests
sample-project/                               # Working example app
sample-project-sso/                           # SSO example (AWS IAM Identity Center)
```

### Request Flow

`AdminDashboardMiddleware` intercepts requests under the configured base path (e.g., `/admin`). It:
1. Checks authorization filters
2. Matches the URL against `DashboardRoutes` (regex-based route table)
3. Dispatches to the appropriate handler:
   - `RazorViewDispatcher` — renders HTML views (dashboard home, list, form, delete confirmation)
   - `ApiDispatcher` — handles form POST submissions (create, update, delete) and JSON lookups
   - `EmbeddedResourceDispatcher` — serves CSS/JS from embedded resources
   - `SamlDispatcher` — handles `/saml/init/` (SP-initiated redirect to IdP) and ACS callback (validates SAMLResponse, creates/updates user, syncs groups, sets cookie)

### Key Data Flow

`DbContextMetaDataLoader` scans a `DbContext` to produce `MetaData` (entities + attributes). The dashboard reads this metadata to auto-generate views. CRUD operations go through `NDjangoAdminManager` → `NDjangoAdminManagerEF` which uses EF Core directly.

### Important Metadata Properties

On `MetaEntityAttr`:
- `ShowOnCreate` / `ShowOnEdit` / `ShowOnView` — control field visibility per view
- `IsEditable` — false for value-generated fields (renders as readonly text, excluded from form POST)
- `Kind == EntityAttrKind.Lookup` — FK navigation properties; `DataAttr` points to the underlying FK data attribute (e.g., `Restaurant` lookup → `RestaurantId`)
- `ValueGenerated` flags on EF Core properties drive `ShowOnCreate`/`ShowOnEdit`/`IsEditable`

### Form POST Handling

`ApiDispatcher.FormToJObject()` maps HTML form fields to a `JObject` for `NDjangoAdminManagerEF`. For FK fields, it uses `attr.DataAttr.PropName` (e.g., `CategoryId`) — the raw ID text input `name` attribute must match this. FK fields render as a text input with a popup lookup icon (similar to Django's `raw_id_fields`).

### URL Routing Pattern

Routes are Django-style: `/{entityId}/` (list), `/{entityId}/add/` (create), `/{entityId}/{id}/change/` (edit), `/{entityId}/{id}/delete/` (delete). The `entityId` is the short class name from `MetaEntity.Id` (e.g., `Category`, `MenuItem`).

## Auto-Generated Field Handling

EF Core properties with `HasDefaultValueSql()` or `ValueGeneratedOnAdd()` are automatically hidden from create forms and rendered as readonly text on edit forms. This is the intended pattern for timestamps (`CreatedAt`/`UpdatedAt`) and identity columns.

## SAML SSO

When `EnableSaml = true`, the dashboard adds SAML 2.0 SSO alongside password login. The ACS callback (`/api/security/saml/callback`) is registered as a separate middleware branch via `app.Map()` outside the admin base path. IdP metadata can be auto-fetched at startup from `SamlMetadataUrl`, or certificate/SSO URL can be set manually. Group UUIDs from the SAML response are matched against `auth_group.name` to assign permissions. Uses the `AspNetSaml` NuGet package].