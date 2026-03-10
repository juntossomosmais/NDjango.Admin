# EasyData

## What This Is

EasyData Admin Dashboard — a Django-admin-inspired CRUD dashboard for ASP.NET Core + Entity Framework Core. Auto-generates a full admin interface from a `DbContext`: list views with pagination/search/sorting, create/edit forms with FK dropdowns, and delete confirmations. All server-rendered HTML, no client-side framework.

## Build & Test Commands

```bash
# Start SQL Server (required for integration tests and sample project)
docker compose up -d db

# Build everything
dotnet build EasyData.sln

# Run all tests (~159 tests)
dotnet test EasyData.sln

# Run a single test project
dotnet test test/EasyData.AspNetCore.AdminDashboard.Tests/

# Run a single test
dotnet test EasyData.sln --filter "FullyQualifiedName~ForeignKeyTests.RestaurantAddForm_RendersCategory_DropdownAsync"

# Run the sample project (requires SQL Server running)
cd sample-project/src && dotnet run -- api
# Dashboard at http://localhost:8000/admin/
```

**Solution file:** `EasyData.sln` — includes all source and test projects.

## Architecture

### Project Dependency Graph

```
EasyData.Core                              # Metadata model (MetaData, MetaEntity, MetaEntityAttr)
├── EasyData.AspNetCore                    # ASP.NET Core integration (EasyDataManager, middleware)
├── EasyData.EntityFrameworkCore.Relational # EF Core → metadata loader (DbContextMetaDataLoader)
│
EasyData.AspNetCore.AdminDashboard         # The admin dashboard (depends on all three above)
```

All projects target **net8.0**.

### Request Flow

`AdminDashboardMiddleware` intercepts requests under the configured base path (e.g., `/admin`). It:
1. Checks authorization filters
2. Matches the URL against `DashboardRoutes` (regex-based route table)
3. Dispatches to the appropriate handler:
   - `RazorViewDispatcher` — renders HTML views (dashboard home, list, form, delete confirmation)
   - `ApiDispatcher` — handles form POST submissions (create, update, delete) and JSON lookups
   - `EmbeddedResourceDispatcher` — serves CSS/JS from embedded resources

### Key Data Flow

`DbContextMetaDataLoader` scans a `DbContext` to produce `MetaData` (entities + attributes). The dashboard reads this metadata to auto-generate views. CRUD operations go through `EasyDataManager` → `EasyDataManagerEF` which uses EF Core directly.

### Important Metadata Properties

On `MetaEntityAttr`:
- `ShowOnCreate` / `ShowOnEdit` / `ShowOnView` — control field visibility per view
- `IsEditable` — false for value-generated fields (renders as readonly text, excluded from form POST)
- `Kind == EntityAttrKind.Lookup` — FK navigation properties; `DataAttr` points to the underlying FK data attribute (e.g., `Restaurant` lookup → `RestaurantId`)
- `ValueGenerated` flags on EF Core properties drive `ShowOnCreate`/`ShowOnEdit`/`IsEditable`

### Form POST Handling

`ApiDispatcher.FormToJObject()` maps HTML form fields to a `JObject` for `EasyDataManagerEF`. For FK dropdowns, it uses `attr.DataAttr.PropName` (e.g., `CategoryId`) — the select element `name` attribute must match this.

### URL Routing Pattern

Routes are Django-style: `/{entityId}/` (list), `/{entityId}/add/` (create), `/{entityId}/{id}/change/` (edit), `/{entityId}/{id}/delete/` (delete). The `entityId` is the short class name from `MetaEntity.Id` (e.g., `Category`, `MenuItem`).

## Testing

Tests use **xUnit** + **FluentAssertions** + **Microsoft.AspNetCore.TestHost**. The `AdminDashboardFixture` creates a unique SQL Server database per test run and seeds restaurant domain data (Category, Restaurant, RestaurantProfile, Ingredient, MenuItem). Test fixture implements `IDisposable` to drop the DB after tests.

SQL Server must be running on `localhost:1433` (sa/Password1) — use `docker compose up -d db`.

## Auto-Generated Field Handling

EF Core properties with `HasDefaultValueSql()` or `ValueGeneratedOnAdd()` are automatically hidden from create forms and rendered as readonly text on edit forms. This is the intended pattern for timestamps (`CreatedAt`/`UpdatedAt`) and identity columns.

## Known Gaps

- **M2M relationships** not yet supported in the dashboard (planned Phase 5)
- Price fields with decimal types may show locale-specific formatting issues in HTML number inputs
