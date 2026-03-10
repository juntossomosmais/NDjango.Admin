# EasyData Admin Dashboard

A Django-admin-inspired CRUD dashboard for ASP.NET Core + Entity Framework Core. Automatically generates a full admin interface from your `DbContext` — list views with pagination/search/sorting, create/edit forms with FK dropdowns, and delete confirmations.

## What you get

- **Dashboard home** at `/admin/` listing all entities with Add/Change links
- **List view** with search, column sorting, and pagination
- **Create/Edit forms** that auto-detect fields, hide auto-generated properties (`Id`, `CreatedAt`, `UpdatedAt`), and render FK relationships as dropdowns
- **Delete confirmation** page
- **Sidebar** with model navigation and filtering
- **Zero client-side framework dependency** — all HTML is server-rendered

## Quick start

### 1. Install packages

Add a reference to the `EasyData.AspNetCore.AdminDashboard` project (or NuGet package when published).

### 2. Register services

```csharp
// Program.cs or Startup.ConfigureServices
services.AddEasyDataAdminDashboard<AppDbContext>();
```

### 3. Add the middleware

```csharp
// Program.cs or Startup.Configure
app.UseEasyDataAdminDashboard("/admin", new AdminDashboardOptions
{
    Authorization = new[] { new AllowAllAdminDashboardAuthorizationFilter() },
    DashboardTitle = "My Admin",
});
```

That's it. Navigate to `/admin/` and you have a working admin panel.

## Configuration

### Authorization

```csharp
using EasyData.AspNetCore.AdminDashboard.Authorization;

// Allow all (development only)
new AllowAllAdminDashboardAuthorizationFilter()

// Restrict to localhost
new LocalRequestsOnlyAuthorizationFilter()

// Custom: implement IAdminDashboardAuthorizationFilter
```

### Options

| Option | Default | Description |
|---|---|---|
| `DashboardTitle` | `"Admin Dashboard"` | Title shown in the header |
| `AppPath` | `"/"` | Base path of your application |
| `DefaultRecordsPerPage` | `25` | Pagination page size |
| `IsReadOnly` | `false` | Disable all write operations |
| `EntityGroups` | `null` | Group entities in the sidebar (dictionary of group name to entity names) |

### Auto-generated fields

Fields configured with `ValueGeneratedOnAdd` or `HasDefaultValueSql` in EF Core are automatically:
- Hidden from create forms
- Shown as read-only text on edit forms

Example for timestamp fields:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    foreach (var entityType in modelBuilder.Model.GetEntityTypes()
        .Where(t => typeof(StandardEntity).IsAssignableFrom(t.ClrType)))
    {
        modelBuilder.Entity(entityType.ClrType)
            .Property(nameof(StandardEntity.CreatedAt))
            .HasDefaultValueSql("GETUTCDATE()");

        modelBuilder.Entity(entityType.ClrType)
            .Property(nameof(StandardEntity.UpdatedAt))
            .HasDefaultValueSql("GETUTCDATE()");
    }
}
```

## Sample project

A working sample is at `./sample-project`. To run it:

```bash
# Start SQL Server (required)
docker compose up -d db

# Run the app
cd sample-project/src
dotnet run -- api
```

Open `http://localhost:8000/admin/` to see the dashboard with restaurant domain models (Category, Restaurant, RestaurantProfile, Ingredient, MenuItem).

## Running tests

```bash
# Start SQL Server
docker compose up -d db

# Run all tests (159 total)
dotnet test EasyData.sln
```

## Project structure

```
src/
  EasyData.Core/                              # Core metadata model
  EasyData.AspNetCore/                        # ASP.NET Core integration
  EasyData.EntityFrameworkCore.Relational/    # EF Core metadata loader
  EasyData.AspNetCore.AdminDashboard/         # Admin dashboard (this package)
    Authorization/                            # Auth filters
    Configuration/                            # DI extensions and options
    Dispatchers/                              # View rendering and API handlers
    Middleware/                               # Request pipeline
    Routing/                                  # URL dispatch
    Services/                                 # Metadata and entity grouping
    ViewModels/                               # Form and list models
    wwwroot/                                  # Embedded CSS/JS

test/                                         # Integration & unit tests
sample-project/                               # Working example app
```
