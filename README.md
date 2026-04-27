# NDjango.Admin Admin Dashboard

[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=juntossomosmais_NDjango.Admin&metric=coverage)](https://sonarcloud.io/summary/new_code?id=juntossomosmais_NDjango.Admin)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=juntossomosmais_NDjango.Admin&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=juntossomosmais_NDjango.Admin)

A Django-admin-inspired admin dashboard for ASP.NET Core. Automatically generates a full admin interface from your data model — list views with pagination/sorting and opt-in search, create/edit forms with FK lookup popups, and delete confirmations.

**Supported providers:**
- **Entity Framework Core** — full CRUD from your `DbContext`
- **MongoDB** — full CRUD dashboard from your MongoDB collections, with optional authentication

## What you get

- **Dashboard home** at `/admin/` listing all entities with Add/Change links
- **List view** with column sorting, pagination, opt-in search via `IAdminSettings<T>`, and bulk actions (checkboxes + action dropdown)
- **Bulk actions** — built-in "Delete selected" with confirmation page, plus custom user-defined actions via `AdminActionList<TPk>`
- **Create/Edit forms** that auto-detect fields, hide auto-generated properties (`Id`, `CreatedAt`, `UpdatedAt`), and render FK relationships as text input + lookup popup (like Django's `raw_id_fields`)
- **M2M relationships** via explicit junction/through entities with composite key support (EF Core) — junction tables appear as first-class entities in the dashboard, matching Django Admin's pattern
- **Delete confirmation** page (single record and bulk)
- **Sidebar** with model navigation and filtering
- **Zero client-side framework dependency** — all HTML is server-rendered

## Quick start

### Entity Framework Core

#### 1. Install packages

Add a reference to the `NDjango.Admin.AspNetCore.AdminDashboard` project.

#### 2. Register services

```csharp
// Program.cs or Startup.ConfigureServices
services.AddNDjangoAdminDashboard<AppDbContext>(new AdminDashboardOptions
{
    Authorization = new[] { new AllowAllAdminDashboardAuthorizationFilter() },
    DashboardTitle = "My Admin",
});
```

#### 3. Add the middleware

```csharp
// Program.cs or Startup.Configure
app.UseNDjangoAdminDashboard("/admin");
```

That's it. Navigate to `/admin/` and you have a working admin panel.

### MongoDB

#### 1. Install packages

Add references to `NDjango.Admin.MongoDB` and `NDjango.Admin.AspNetCore.AdminDashboard.Core`.

#### 2. Register MongoDB and admin services

```csharp
using MongoDB.Driver;
using NDjango.Admin.AspNetCore.AdminDashboard;
using NDjango.Admin.AspNetCore.AdminDashboard.Authorization;
using NDjango.Admin.MongoDB;

// Register MongoDB
services.AddSingleton<IMongoClient>(sp => new MongoClient("mongodb://localhost:27017"));
services.AddSingleton<IMongoDatabase>(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase("MyDatabase"));

// Register admin dashboard for MongoDB
services.AddNDjangoAdminDashboardMongo(
    new AdminDashboardOptions
    {
        Authorization = new[] { new AllowAllAdminDashboardAuthorizationFilter() },
        DashboardTitle = "My Admin (MongoDB)",
    },
    mongo =>
    {
        mongo.AddCollection<Product>("products");
        mongo.AddCollection<Customer>("customers");
        mongo.AddCollection<Order>("orders");
    }
);
```

#### 3. Add the middleware

```csharp
app.UseNDjangoAdminDashboard("/admin");
```

Navigate to `/admin/` and you have a working admin dashboard for your MongoDB collections with list views, detail views, create/edit/delete forms, search, sort, and pagination.

#### MongoDB document requirements

- Documents should have an `Id` property (or a property marked with `[BsonId]`) — this is used as the primary key for URL routing
- Use `[BsonIgnoreExtraElements]` on document classes for forward compatibility
- Use `[BsonIgnore]` to hide properties from the dashboard
- Use `[BsonElement("name")]` to specify the stored field name
- Implement `IAdminSettings<T>` to enable search on specific fields

#### Auto-timestamp convention (MongoDB)

Properties named `CreatedAt`, `UpdatedAt`, `CreatedDate`, `UpdatedDate`, `CreationDate`, or `ModificationDate` (of type `DateTime` or `DateTimeOffset`, including nullable variants) are automatically treated as system-managed timestamps:
- Hidden from create forms
- Shown as read-only on edit forms
- Automatically set by the manager on create/update (`DateTime.UtcNow`)

This matches the EF Core behavior where `HasDefaultValueSql()` hides auto-generated fields. No attribute or configuration is needed — just follow the naming convention.

#### Per-collection read-only mode (MongoDB)

By default, all MongoDB collections are editable (full CRUD). To make specific collections read-only (list + detail views only, no create/edit/delete):

```csharp
services.AddNDjangoAdminDashboardMongo(
    new AdminDashboardOptions { DashboardTitle = "My Admin" },
    mongo =>
    {
        mongo.AddCollection<Product>("products");                     // editable (default)
        mongo.AddCollection<AuditLog>("audit_logs", readOnly: true); // read-only
    }
);
```

#### MongoDB authentication (optional)

The MongoDB provider supports the same cookie-based authentication as EF Core. Auth data (users, groups, permissions) is stored in dedicated collections in the same MongoDB database as your application data.

```csharp
services.AddSingleton<IMongoClient>(sp => new MongoClient("mongodb://localhost:27017"));
services.AddSingleton<IMongoDatabase>(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase("MyDatabase"));

services.AddNDjangoAdminDashboardMongo(
    new AdminDashboardOptions
    {
        Authorization = new[] { new AllowAllAdminDashboardAuthorizationFilter() },
        DashboardTitle = "My Admin (MongoDB)",
        CreateDefaultAdminUser = true,
        DefaultAdminPassword = "admin",
    },
    mongo =>
    {
        mongo.AddCollection<Product>("products");
        mongo.AddCollection<Customer>("customers");
    }
);

app.UseNDjangoAdminDashboard("/admin");
```

The MongoDB provider:
- Creates 5 auth collections (`auth_users`, `auth_groups`, `auth_permissions`, `auth_group_permissions`, `auth_user_groups`) with unique indexes
- Seeds CRUD permissions for each registered collection
- Creates a default `admin` superuser (when `CreateDefaultAdminUser = true`)
- Auth entities (Users, Groups, Permissions) are editable through the dashboard while user collections follow the configured editability

All authentication options (`CreateDefaultAdminUser`, `CookieName`, `CookieExpiration`, `SkipStorageInitialization`) work identically for both providers. SAML SSO is also supported — see the [SAML SSO section](#saml-sso-optional).

#### MongoDB limitations

- **No FK lookups** — references between collections (e.g., `ObjectId RestaurantId`) display as plain IDs, not lookup popups

## Configuration

### Authorization

```csharp
using NDjango.Admin.AspNetCore.AdminDashboard.Authorization;

// Allow all (development only)
new AllowAllAdminDashboardAuthorizationFilter()

// Restrict to localhost
new LocalRequestsOnlyAuthorizationFilter()

// Custom: implement IAdminDashboardAuthorizationFilter
```

### Options

All options are passed to `AddNDjangoAdminDashboard` at service registration:

```csharp
services.AddNDjangoAdminDashboard<AppDbContext>(new AdminDashboardOptions
{
    DashboardTitle = "My Admin",
    DefaultRecordsPerPage = 50,
});
```

| Option | Default | Description |
|---|---|---|
| `DashboardTitle` | `"Admin Dashboard"` | Title shown in the header |
| `AppPath` | `"/"` | Base path of your application |
| `DefaultRecordsPerPage` | `25` | Pagination page size |
| `IsReadOnly` | `false` | Disable all write operations |
| `EntityGroups` | `null` | Group entities in the sidebar (dictionary of group name to entity names) |

### Authentication

Cookie-based authentication is **always on** — every dashboard route requires login. The auth subsystem is modeled on Django Admin (users, groups, permissions). Auth storage is created automatically in your existing database via a background hosted service after the host starts. This works for both EF Core (SQL Server tables) and MongoDB (auth collections with unique indexes).

```csharp
// ConfigureServices
services.AddNDjangoAdminDashboard<AppDbContext>(new AdminDashboardOptions
{
    DashboardTitle = "My Admin",
    CreateDefaultAdminUser = true,
    DefaultAdminPassword = "admin",
});

// Configure
app.UseNDjangoAdminDashboard("/admin");
```

#### `NDJANGO_SECRET_KEY` (required)

The auth cookie is encrypted and signed with AES-GCM keys derived from the `NDJANGO_SECRET_KEY` environment variable. The variable is **required** at startup — `AddNDjangoAdminDashboard*` throws `InvalidOperationException` if it's missing or shorter than 32 characters.

Generate a secret:

```bash
openssl rand -base64 48
```

Provide it to every replica. In Kubernetes use a `Secret` (never a `ConfigMap`):

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: ndjango-admin-secret
stringData:
  NDJANGO_SECRET_KEY: "<output from openssl>"
---
# In the Deployment spec.template.spec.containers[*].env:
env:
  - name: NDJANGO_SECRET_KEY
    valueFrom:
      secretKeyRef:
        name: ndjango-admin-secret
        key: NDJANGO_SECRET_KEY
```

Why it matters:
- **Multi-pod / multi-replica deployments**: every replica derives the same keys from the same secret, so a cookie issued by Pod A is accepted by Pod B. Without a shared secret each replica generates ephemeral keys and users hit a redirect-to-login loop whenever the load balancer routes them to a different pod.
- **Pod restarts**: cookies issued before a restart remain valid afterwards.
- **Forge resistance**: anyone with the secret can forge any user's cookie (including a superuser's). Treat it as a production credential — Kubernetes Secret, never committed to Git, rotate on suspected leak.
- **Rotation**: changing the secret invalidates every existing cookie (all users re-login). This is intentional; for zero-downtime rotation, use one of the alternative key persistence stores (shared volume, AWS SSM, Mongo-backed `IXmlRepository`) configured directly on `services.AddDataProtection()`.

Behavior:
- All dashboard pages require login (unauthenticated requests redirect to `/admin/login/`)
- A default superuser `admin` is created on first startup (when `CreateDefaultAdminUser = true`)
- Permissions are auto-generated for every entity (`add_`, `view_`, `change_`, `delete_`)
- Superusers bypass all permission checks; regular users need permissions assigned through groups
- Auth entities (Users, Groups, Permissions) appear in the dashboard under "Authentication and Authorization" and are manageable through the same CRUD interface
- Auth storage initialization (DDL, permission seeding, admin user creation) runs asynchronously in a `BackgroundService` after the host starts — it does not block app startup
- While the bootstrap is in progress, the dashboard returns `503 Service Unavailable` with a `Retry-After: 1` header

| Option | Default | Description |
|---|---|---|
| `CreateDefaultAdminUser` | `false` | Create an `admin` superuser on startup if it doesn't exist |
| `DefaultAdminPassword` | `"admin"` | Password for the default admin user |
| `CookieName` | `".NDjango.Admin.Auth"` | Name of the authentication cookie |
| `CookieExpiration` | `24 hours` | How long the session cookie remains valid |
| `SkipStorageInitialization` | `false` | Skip auth table creation, permission seeding, and default admin user creation. Useful for integration tests or externally managed schemas |

**Important:** Your database must exist before the auth bootstrap hosted service runs. If you use `EnsureCreated()`, call it **before** `UseNDjangoAdminDashboard()`:

```csharp
// Correct order
using var scope = app.ApplicationServices.CreateScope();
using var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
db.Database.EnsureCreated();

app.UseNDjangoAdminDashboard("/admin");
```

#### Integration tests with `SkipStorageInitialization`

When testing with `WebApplicationFactory`, set `SkipStorageInitialization = true` to prevent the hosted service from connecting to a database that may not exist:

```csharp
services.AddNDjangoAdminDashboard<AppDbContext>(new AdminDashboardOptions
{
    SkipStorageInitialization = true,
});
```

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

### Conditional search fields

By default, list views do **not** show a search box. Search is opt-in per entity, matching Django Admin's `search_fields` behavior. To enable search, implement `IAdminSettings<T>` on your entity class and specify which properties to search:

```csharp
using NDjango.Admin;

public class Category : IAdminSettings<Category>
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }

    // Search box appears on the Category list view, filtering by Name and Description
    public PropertyList<Category> SearchFields => new(x => x.Name, x => x.Description);
}
```

- Entities that implement `IAdminSettings<T>` with non-empty `SearchFields` show a search box on their list view. The search filters by substring match on the configured fields only.
- Entities without `IAdminSettings<T>`, or with an empty `SearchFields`, do not show a search box and ignore `?q=` query parameters.
- `PropertyList<T>` uses expression-based selectors (`x => x.Name`) for compile-time safety — typos in property names cause build errors, not runtime surprises.

### FK lookup popup

Foreign key fields render as a plain text input showing the raw FK ID plus a magnifying glass lookup icon, matching Django Admin's `raw_id_fields` pattern. Clicking the icon opens a popup window with the related entity's list view, where the user can search (if the related entity has `SearchFields` configured) and select a record. The popup closes and fills the FK ID automatically.

This replaces preloaded `<select>` dropdowns, which don't scale when the related table has thousands of records.

```
┌──────────────────────────────────────────────────┐
│ Restaurant: *                                    │
│ ┌──────────┐  🔍                                 │
│ │ 1        │  ← click to open popup              │
│ └──────────┘                                     │
└──────────────────────────────────────────────────┘
```

The popup opens a simplified version of the related entity's list view (no header, no sidebar) and respects conditional search — if the related entity has `SearchFields`, the popup includes a search box.

### Many-to-many relationships

M2M relationships are supported via explicit junction/through entities, following Django Admin's pattern where the through model is treated as a first-class entity with its own list, add, edit, and delete pages.

#### EF Core — composite primary keys

Create an explicit junction entity with a composite key and register it as a `DbSet`:

```csharp
// Junction entity — does NOT inherit from StandardEntity (no auto Id/timestamps)
public class MenuItemIngredient
{
    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; }

    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; }
}

// DbContext
public DbSet<MenuItemIngredient> MenuItemIngredients { get; set; }

// OnModelCreating
modelBuilder.Entity<MenuItemIngredient>(entity =>
{
    entity.HasKey(e => new { e.MenuItemId, e.IngredientId });

    entity.HasOne(e => e.MenuItem)
        .WithMany()
        .HasForeignKey(e => e.MenuItemId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasOne(e => e.Ingredient)
        .WithMany()
        .HasForeignKey(e => e.IngredientId)
        .OnDelete(DeleteBehavior.Cascade);
});
```

The dashboard automatically:
- Shows the junction entity in the sidebar and dashboard home
- Renders both FK fields as text inputs with lookup popups on the **add** form
- Renders both FK fields as **read-only** on the **edit** form (changing PKs would change the record's identity)
- Encodes composite keys in URLs as comma-separated values: `/admin/MenuItemIngredient/1,3/change/`
- Supports bulk delete with composite keys
- Returns 400 for malformed composite key URLs

Parent entities (`MenuItem`, `Ingredient`) do **not** show the M2M relationship on their forms — collection navigations are skipped, matching Django Admin's behavior with explicit `through` models.

#### MongoDB — junction collections

MongoDB junction documents use a standard single `ObjectId` PK (no composite keys):

```csharp
public class MenuItemIngredient : StandardDocument
{
    public ObjectId MenuItemId { get; set; }
    public ObjectId IngredientId { get; set; }
}

// Registration
mongo.AddCollection<MenuItemIngredient>("menuItemIngredients");
```

The `MenuItemId` and `IngredientId` fields render as plain text inputs for ObjectId hex strings (no lookup popups — consistent with all MongoDB FK-like fields). Standard single-key CRUD applies.

**Key difference from EF Core:** MongoDB has no foreign key constraints, so deleting a parent document does **not** cascade-delete junction records.

### Bulk actions

List views include Django-style bulk actions: select rows via checkboxes, pick an action from a dropdown, and click "Go". A built-in "Delete selected" action is available on every editable entity. You can register custom actions per entity via `IAdminSettings<T>`.

#### Built-in delete action

Every editable entity automatically gets a "Delete selected {entity name}" action. Selecting records and running it redirects to a confirmation page showing the records to be deleted, with "Yes, I'm sure" and "No, take me back" options — identical to Django Admin's bulk delete flow.

#### Custom actions

Define custom actions by adding an `Actions` property to your entity's `IAdminSettings<T>` implementation. Actions use `AdminActionList<TPk>` where `TPk` is your entity's primary key type — selected IDs are automatically parsed to the correct type.

```csharp
using NDjango.Admin;

public class Restaurant : IAdminSettings<Restaurant>
{
    public int Id { get; set; }
    public string Name { get; set; }

    public PropertyList<Restaurant> SearchFields => new(x => x.Name);

    public AdminActionList<int> Actions => new AdminActionList<int>()
        .Add("mark_featured", "Mark selected restaurants as featured",
            handler: async (sp, selectedIds) =>
            {
                // sp is IServiceProvider — resolve any service you need
                var db = sp.GetRequiredService<AppDbContext>();
                await db.Restaurants
                    .Where(r => selectedIds.Contains(r.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsFeatured, true));

                return AdminActionResult.Success(
                    $"Successfully marked {selectedIds.Count} restaurant(s) as featured.");
            })
        .Add("export_csv", "Export selected to CSV",
            handler: async (sp, selectedIds) =>
            {
                // ... your export logic ...
                return AdminActionResult.Success($"Exported {selectedIds.Count} records.");
            },
            allowEmptySelection: true);  // allow running with no rows selected
}
```

The handler receives:
- `IServiceProvider` — resolve services from the DI container (DbContext, custom services, etc.)
- `IReadOnlyList<TPk>` — the selected record IDs, already parsed to the PK type

Return `AdminActionResult.Success(message)` or `AdminActionResult.Error(message)`. The message is displayed as a flash banner (green for success, red for error) on the list page after redirect.

#### Behavior details

- Actions only appear on list views when the entity is **editable** and **not in popup mode**
- The "Delete selected" action is always first in the dropdown
- Custom actions appear after the built-in delete action, in registration order
- By default, actions require at least one row selected. Set `allowEmptySelection: true` to allow running with no selection
- If the handler throws an exception, a generic error message is shown
- Flash messages are one-time — they disappear on the next navigation (passed via query params, not stored in session)

### Time-limited pagination COUNT

On tables with millions of rows, `SELECT COUNT(*)` can take seconds and block the list view. The dashboard cancels the COUNT query if it exceeds `PaginationCountTimeoutMs` and shows a fallback value instead. Data rows load independently and are not affected.

```csharp
services.AddNDjangoAdminDashboard<AppDbContext>(new AdminDashboardOptions
{
    PaginationCountTimeoutMs = 200,  // default: 200ms; set -1 to disable
});
```

| Option | Default | Description |
|---|---|---|
| `PaginationCountTimeoutMs` | `200` | Max time (ms) for the COUNT query. If exceeded, a fallback count is shown. Set to `-1` to disable. |

To reproduce and test with large data, see [`sample-project/E2E_TESTING.md`](sample-project/E2E_TESTING.md).

### SAML SSO (optional)

The dashboard supports SAML 2.0 single sign-on as an additional login method alongside password authentication. When enabled, the login page shows a "Try single sign-on (SSO)" link.

```csharp
services.AddNDjangoAdminDashboard<AppDbContext>(new AdminDashboardOptions
{
    EnableSaml = true,
    SamlMetadataUrl = "https://portal.sso.us-east-1.amazonaws.com/saml/metadata/...",
    SamlIssuer = "http://localhost:8000/admin",
    SamlAcsUrl = "http://localhost:8000/api/security/saml/callback",
    SamlGroupsAttribute = "http://schemas.xmlsoap.org/claims/Group",
});

app.UseNDjangoAdminDashboard("/admin");
```

On SSO login, the dashboard maps IdP group UUIDs to `auth_group.name` entries. Create groups whose names match the IdP group UUIDs, assign permissions to those groups, and SSO users automatically inherit them. Group memberships are fully replaced on each login.

| Option | Default | Description |
|---|---|---|
| `EnableSaml` | `false` | Show SSO link on login page and enable SAML endpoints |
| `SamlMetadataUrl` | `null` | IdP metadata URL — auto-extracts certificate and SSO URL at startup |
| `SamlIdpSsoUrl` | `null` | IdP SSO endpoint (extracted from metadata if not set) |
| `SamlCertificate` | `null` | IdP X.509 signing certificate base64 (extracted from metadata if not set) |
| `SamlIssuer` | `null` | SP entity ID — must match the SAML audience configured in the IdP |
| `SamlAcsUrl` | `null` | Full ACS callback URL — must match the ACS URL configured in the IdP |
| `SamlGroupsAttribute` | `"groups"` | SAML attribute name containing group IDs (AWS uses `http://schemas.xmlsoap.org/claims/Group`) |

## Sample projects

### sample-project (EF Core + SQL Server)

```bash
# Start SQL Server (required)
docker compose up -d db

# Run the app
cd sample-project/src
dotnet run -- api
```

Open `http://localhost:8000/admin/` to see the dashboard with restaurant domain models (Category, Restaurant, RestaurantProfile, Ingredient, MenuItem, MenuItemIngredient, Gift). Category and Restaurant have `IAdminSettings` with search fields configured; the other models demonstrate the no-search path. FK fields (e.g., MenuItem → Restaurant) use the lookup popup. MenuItemIngredient demonstrates M2M relationships via a junction entity with a composite primary key. Restaurant also demonstrates a custom bulk action ("Mark selected restaurants as featured") alongside the built-in bulk delete.

### sample-project-mongodb (MongoDB)

```bash
# Start MongoDB with replica set (required)
docker compose up -d mongo mongoClusterSetup

# Run the app
cd sample-project-mongodb/src
dotnet run -- api
```

Open `http://localhost:8001/admin/` to see the dashboard with the same restaurant domain models translated to MongoDB documents. Demonstrates full CRUD, `ObjectId` primary keys, cross-collection references, M2M via junction collection (MenuItemIngredient), `IAdminSettings` search, cookie-based authentication, and all supported data types. Default login: `admin` / `admin`.

### sample-project-sso

Demonstrates SAML SSO with AWS IAM Identity Center. See [`sample-project-sso/README.md`](sample-project-sso/README.md) for configuration details and known issues.

## Known Gaps

- **MongoDB FK lookups** — references between MongoDB collections display as plain ObjectId strings, not lookup popups like the EF Core provider
- **MongoDB cascade delete** — deleting a parent document does not auto-delete junction records (MongoDB has no FK constraints)
- **FK display names in list views** — FK columns show raw IDs, not the related entity's display name (applies to both providers)