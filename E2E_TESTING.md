# E2E Testing Guide

End-to-end testing of the NDjango.Admin Admin Dashboard using the sample project and Playwright MCP.

## Prerequisites

```bash
# SQL Server must be running
docker compose up -d db

# Wait for it to be healthy (~15s), then start the sample project
cd sample-project/src && dotnet run -- api
```

- **App URL:** `http://localhost:8000/admin/`
- **DB:** SQL Server on `localhost:1433`, sa/Password1, database `SampleProject`
- **Auto-setup:** The app calls `EnsureCreated()` on startup — no migrations needed
- **Authentication:** The sample project has `RequireAuthentication = true` and `CreateDefaultAdminUser = true`. Default credentials: **admin / admin**

**Important:** `EnsureCreated()` must run **before** `UseNDjangoAdminDashboard()` in `Configure()`. The auth bootstrap creates tables in the existing database — if the database doesn't exist yet, it will fail.

If port 8000 is already in use, kill the existing process first:
```bash
lsof -ti:8000 | xargs kill -9
```

## Playwright MCP Setup

Use the `mcp__playwright__browser_*` tools. Key tools:

| Tool | Purpose |
|---|---|
| `browser_navigate` | Go to a URL |
| `browser_snapshot` | Get accessibility tree (preferred over screenshots) |
| `browser_click` | Click an element by `ref` |
| `browser_fill_form` | Fill form fields by `ref` |

**Known issue:** If Chrome fails to launch with "Opening in existing browser session", clear the stale profile:
```bash
rm -rf ~/Library/Caches/ms-playwright/mcp-chrome-*
```

## Authentication Flow

When `RequireAuthentication = true`, all dashboard pages require login. The auth system uses cookie-based sessions with DataProtection encryption.

### Login

1. Any unauthenticated request to `/admin/*` redirects to `/admin/login/?next={originalPath}`
2. The login page shows a form with Username and Password fields and a "Log in" button
3. POST `/admin/login/` validates credentials against the `auth_user` table (SHA256 hash)
4. On success: sets `.NDjango.Admin.Auth` cookie, redirects to `?next` param or `/admin/`
5. On failure: re-renders login page with "Invalid credentials" error
6. Inactive users (`is_active = false`) are treated as invalid credentials

### Logout

1. Navigate to `/admin/logout/` or click "Log out" in the header
2. Clears the auth cookie
3. Redirects to `/admin/login/`

### SAML SSO Login (sample-project-sso only)

When `EnableSaml = true`, the login page shows a "Try single sign-on (SSO)" link pointing to `/admin/saml/init/`.

**IdP-initiated flow (recommended with AWS IAM Identity Center):**

1. Navigate to the AWS access portal (`https://<directory>.awsapps.com/start/#/?tab=applications`)
2. Click the application (e.g., "Identity - DEV")
3. AWS POSTs SAMLResponse to `/api/security/saml/callback`
4. Dashboard creates/updates the user, syncs group memberships, sets auth cookie, redirects to `/admin/`

**SP-initiated flow:**

1. Click "Try single sign-on (SSO)" on the login page
2. Browser redirects to the IdP SSO URL with a SAMLRequest
3. IdP authenticates and POSTs SAMLResponse back to the ACS URL

**Known issue:** SP-initiated login does not work with AWS IAM Identity Center — AWS returns 403 "No access" on its internal assertion endpoint. Use IdP-initiated login as a workaround.

**Group/permission mapping:**

- On each SSO login, the dashboard extracts group UUIDs from the SAML response attribute configured via `SamlGroupsAttribute`
- All existing `auth_user_groups` for the user are deleted and replaced with groups matching the SAML response
- Only groups whose `auth_group.name` matches a UUID from the SAML response are assigned
- AWS IAM Identity Center uses `http://schemas.xmlsoap.org/claims/Group` as the attribute name (not `groups`)

### Auth-Exempt Paths

These paths are served without authentication: `/css/*`, `/js/*`, `/login/`, `/logout/`, `/saml/*`

### Login Test Steps

1. Navigate to `/admin/` → should redirect to `/admin/login/?next=/admin/`
2. Verify login form has Username, Password fields and "Log in" button
3. Submit with wrong credentials → stays on login page with error
4. Submit with `admin` / `admin` → redirects to `/admin/`
5. Verify header shows "Welcome, admin" and "Log out" link
6. Click "Log out" → redirected to login page
7. Try to access `/admin/` again → redirected to login (cookie cleared)

## Dashboard Structure

The admin home at `/admin/` shows models organized in sections:

### User Models

| Model | URL prefix | Relationships | `IAdminSettings` |
|---|---|---|---|
| Category | `/admin/Category/` | Standalone | `SearchFields`: Name, Description |
| Restaurant | `/admin/Restaurant/` | Has 1:1 RestaurantProfile, has many MenuItems | `SearchFields`: Name |
| RestaurantProfile | `/admin/RestaurantProfile/` | FK text input + lookup popup → Restaurant (1:1) | None |
| Ingredient | `/admin/Ingredient/` | Standalone (has M2M with MenuItem, not yet supported in UI) | None |
| MenuItem | `/admin/MenuItem/` | FK text input + lookup popup → Restaurant (N:1) | None |

### Authentication and Authorization (when auth enabled)

| Model | URL prefix | Notes |
|---|---|---|
| AuthUser | `/admin/AuthUser/` | Username, Password (hashed), IsSuperuser, IsActive, LastLogin, DateJoined |
| AuthGroup | `/admin/AuthGroup/` | Named groups for permission assignment |
| AuthPermission | `/admin/AuthPermission/` | Auto-generated per entity (add/change/delete/view) |
| AuthGroupPermission | `/admin/AuthGroupPermission/` | Links groups to permissions |
| AuthUserGroup | `/admin/AuthUserGroup/` | Links users to groups |

These appear under the "Authentication and Authorization" heading in the sidebar and dashboard home.

## URL Patterns

| Action | URL | Method |
|---|---|---|
| List | `/admin/{Model}/` | GET |
| Add form | `/admin/{Model}/add/` | GET |
| Create | `/admin/{Model}/add/` | POST |
| Edit form | `/admin/{Model}/{id}/change/` | GET |
| Update | `/admin/{Model}/{id}/change/` | POST |
| Delete form | `/admin/{Model}/{id}/delete/` | GET |
| Delete | `/admin/{Model}/{id}/delete/` | POST |

## Model Details and Form Fields

All models inherit from `StandardEntity` which has `Id`, `CreatedAt`, `UpdatedAt`. These three fields are **auto-generated** — they do not appear on create forms and render as readonly text on edit forms. They are never submitted in form POST data.

### Category (standalone, no FK)

**Editable fields on forms:**
- `Name` — textbox, required, unique
- `Description` — textbox, required (has default empty string)

**Test steps:**
1. Navigate to `/admin/Category/add/`
2. Fill Name and Description
3. Click Save → redirects to `/admin/Category/` list
4. Click the ID link in the list → opens edit form at `/admin/Category/{id}/change/`
5. Verify Id, CreatedAt, UpdatedAt show as readonly text (class `readonly-value`), not inputs
6. Edit Name, click Save → redirects to list, verify updated value
7. Click ID → edit form → click Delete link → delete confirmation page
8. Click "Yes, I'm sure" → redirects to list, record gone

### Restaurant (standalone, no FK on its own form)

**Editable fields on forms:**
- `Name` — textbox, required
- `Address` — textbox, required
- `Phone` — textbox, required

**Test steps:**
1. Navigate to `/admin/Restaurant/add/`
2. Fill Name, Address, Phone
3. Click Save → redirects to list
4. Verify record appears in list
5. Test edit and delete same as Category

**Note:** Restaurant must exist before creating RestaurantProfile or MenuItem (they have FK to Restaurant).

### Ingredient (standalone, has boolean field)

**Editable fields on forms:**
- `Name` — textbox, required, unique
- `IsAllergen` — checkbox (unchecked = false, checked = true)

**Test steps:**
1. Navigate to `/admin/Ingredient/add/`
2. Fill Name, check/uncheck IsAllergen
3. Click Save → redirects to list
4. Verify IsAllergen shows as `True`/`False` in the list
5. Edit: verify checkbox state matches stored value

**Boolean handling:** Unchecked checkboxes are not submitted by browsers. The `ApiDispatcher.FormToJObject()` handles this by defaulting Bool fields to `false` when missing from form data.

### RestaurantProfile (1:1 FK to Restaurant)

**Editable fields on forms:**
- `Capacity` — number (spinbutton), required
- `OpeningHours` — textbox, required
- `Website` — textbox, required (has default empty string)
- `Restaurant` — **text input + lookup popup** (raw FK ID), required

**Test steps:**
1. **Ensure at least one Restaurant exists first**
2. Navigate to `/admin/RestaurantProfile/add/`
3. Verify the Restaurant field renders as a text input (`class="vForeignKeyRawIdAdminField"`) with a lookup icon (`class="related-lookup"`)
4. The lookup icon links to `/admin/Restaurant/?_to_field=id&_popup=1`
5. Type the Restaurant ID directly (e.g., `1`) or click the lookup icon to open a popup
6. Fill Capacity, OpeningHours, Website
7. Click Save → redirects to list
8. Verify `RestaurantId` column in list matches the entered ID
9. Edit: verify text input shows the current FK ID value (e.g., `1`)

**FK field details:**
- The `<input type="text">` element has `name="RestaurantId"` (the FK property name, not the navigation property name "Restaurant")
- The value is the raw FK ID (e.g., `1`), not a display name
- The lookup icon opens a popup window with the related entity's list view

### MenuItem (N:1 FK to Restaurant)

**Editable fields on forms:**
- `Description` — textbox, required (has default empty string)
- `IsAvailable` — checkbox (default true)
- `Name` — textbox, required
- `Price` — number with step=any (spinbutton), required
- `Restaurant` — **text input + lookup popup** (raw FK ID), required

**Test steps:**
1. **Ensure at least one Restaurant exists first**
2. Navigate to `/admin/MenuItem/add/`
3. Verify the Restaurant field renders as a text input (`class="vForeignKeyRawIdAdminField"`) with a lookup icon (`class="related-lookup"`)
4. The lookup icon links to `/admin/Restaurant/?_to_field=id&_popup=1`
5. Type the Restaurant ID directly (e.g., `1`) or click the lookup icon to open a popup
6. Fill Name, Description, Price (use `.` as decimal separator, e.g., `14.99`)
7. Check/uncheck IsAvailable
8. Click Save → redirects to list
9. Verify record in list with correct RestaurantId
10. Edit: verify all fields are pre-filled correctly, text input shows current FK ID

**Price locale note:** The list view may display prices with comma as decimal separator (e.g., `14,99`) depending on server locale. The form uses `<input type="number" step="any">`.

## Auth Entity Details

### AuthUser

**Editable fields on create form:**
- `Username` — textbox, required, unique
- `Password` — textbox, required (stored as SHA256 hex hash — the composite manager intercepts create/update to hash)

**Auto-generated / readonly fields:**
- `Id` — identity PK
- `IsSuperuser` — BIT, default false (value-generated, readonly)
- `IsActive` — BIT, default true (value-generated, readonly)
- `LastLogin` — DATETIME2, nullable (not shown on create form)
- `DateJoined` — DATETIME2, default GETUTCDATE() (not shown on create form)

**Test steps:**
1. Login as admin, navigate to `/admin/AuthUser/add/`
2. Fill Username and Password
3. Click Save → redirects to `/admin/AuthUser/` list
4. Verify user appears in list with `IsActive = True` (database default preserved)
5. Edit: verify Password shows the hashed value, not plaintext

**Known behavior:**
- Empty `LastLogin` on create is correctly skipped (not sent as empty string to DB)
- `IsActive` defaults to `true` via DB default — the form does NOT override this with `false`
- `IsSuperuser` defaults to `false` via DB default

### AuthGroup

**Editable fields:** `Name` — textbox, required, unique

### AuthPermission

**Fields:** `Name` (display name), `Codename` (unique, e.g., `add_category`)

Permissions are auto-generated at startup for every entity. Four per entity:
- `add_{entityname_lower}` / "Can add {EntityName}"
- `change_{entityname_lower}` / "Can change {EntityName}"
- `delete_{entityname_lower}` / "Can delete {EntityName}"
- `view_{entityname_lower}` / "Can view {EntityName}"

With 10 entities (5 user + 5 auth), expect 40 permissions total (25 per page, paginated).

### AuthGroupPermission

**Editable fields:**
- `GroupId` — FK text input + lookup popup to AuthGroup
- `PermissionId` — FK text input + lookup popup to AuthPermission

### AuthUserGroup

**Editable fields:**
- `UserId` — FK text input + lookup popup to AuthUser
- `GroupId` — FK text input + lookup popup to AuthGroup

## Permission Enforcement

Superusers (`is_superuser = true`) bypass all permission checks. For non-superuser users, permissions are resolved through groups:

`auth_user` → `auth_user_groups` → `auth_group` → `auth_group_permissions` → `auth_permission`

| Route | Required Permission |
|---|---|
| GET `/{entityId}/` | `view_{entityname}` |
| GET `/{entityId}/add/` | `add_{entityname}` |
| POST `/{entityId}/add/` | `add_{entityname}` |
| GET `/{entityId}/{id}/change/` | `change_{entityname}` |
| POST `/{entityId}/{id}/change/` | `change_{entityname}` |
| GET `/{entityId}/{id}/delete/` | `delete_{entityname}` |
| POST `/{entityId}/{id}/delete/` | `delete_{entityname}` |

Missing permission → 403 Forbidden.

### Permission Enforcement Test Steps

1. **Create a group** with specific permissions (e.g., `view_category`, `add_category`)
2. **Create a non-superuser** via `/admin/AuthUser/add/`
3. **Assign user to group** via `/admin/AuthUserGroup/add/`
4. **Login as the non-superuser**
5. Verify: can view Category list (`/admin/Category/`) → 200 OK
6. Verify: can access Category add form → 200 OK
7. Verify: cannot edit Category (no `change_category` perm) → 403
8. Verify: cannot delete Category (no `delete_category` perm) → 403
9. Verify: cannot access other entities without permissions → 403

## Save Actions

Every create/edit form has three save buttons:

| Button | `_save_action` value | Redirect after save |
|---|---|---|
| Save | `save` | `/admin/{Model}/` (list) |
| Save and add another | `add_another` | `/admin/{Model}/add/` (new form) |
| Save and continue editing | `continue` | `/admin/{Model}/{id}/change/` (same record) |

## List View Features

- **Search (conditional):** Search is **opt-in** via `IAdminSettings<T>.SearchFields`. Only entities that implement `IAdminSettings<T>` with non-empty `SearchFields` show the search box. When present, the text box submits as `?q=term` and filters via substring match on the configured fields only. Entities without `IAdminSettings` or with empty `SearchFields` do not render the search box and ignore `?q=` parameters.
  - **With search:** Category (Name, Description), Restaurant (Name)
  - **Without search:** RestaurantProfile, Ingredient, MenuItem, and all Auth entities
- **Sorting:** Click column headers to sort. URL: `?sort=FieldName&dir=asc` or `dir=desc`. Active sort shows arrow (▲/▼)
- **Pagination:** Shows page numbers at bottom when records exceed `DefaultRecordsPerPage` (25). URL: `?page=N`
- **Record count:** Shows "N {model_name}" above the table

## Sidebar

All pages (except dashboard home) have a left sidebar with:
- A text filter input ("Filter models...")
- User models listed under "Models" heading
- Auth models listed under "Authentication and Authorization" heading (when auth enabled)
- Client-side JS filters the list as you type

## Verification Checklist

When running a full E2E test pass, verify in this order:

### Phase 1: Authentication

1. **Login redirect** — `/admin/` redirects to `/admin/login/?next=/admin/`
2. **Login page** — shows Username, Password fields and "Log in" button
3. **Invalid login** — wrong credentials show error, stay on login page
4. **Valid login** — `admin`/`admin` redirects to dashboard, cookie set
5. **Header** — shows "Welcome, admin" and "Log out" link

### Phase 2: Dashboard Home & Sidebar

6. **Dashboard home** — `/admin/` loads, shows user models and "Authentication and Authorization" section
7. **Sidebar** — shows "Models" section and "Authentication and Authorization" section with all 10 entities

### Phase 3: User Entity CRUD (respects FK dependencies)

8. **Category** — full CRUD (create, list, edit, delete)
9. **Ingredient** — create with boolean field, edit, delete
10. **Restaurant** — full CRUD
11. **RestaurantProfile** — create with FK text input + lookup popup to Restaurant, edit, delete
12. **MenuItem** — create with FK text input + lookup popup to Restaurant, edit, delete

### Phase 3a: Conditional Search + FK Lookup Popup

#### Conditional Search

Search is opt-in via `IAdminSettings<T>.SearchFields`. The search box only appears on list views for entities that implement this interface with non-empty `SearchFields`.

**Test steps:**

12a. **Search box visible (Category)** — Navigate to `/admin/Category/`. Verify a search box (textbox "Search..." + "Search" button) is present in the toolbar. Category implements `IAdminSettings<Category>` with `SearchFields => new(x => x.Name, x => x.Description)`.

12b. **Search box visible (Restaurant)** — Navigate to `/admin/Restaurant/`. Verify the search box is present. Restaurant implements `IAdminSettings<Restaurant>` with `SearchFields => new(x => x.Name)`.

12c. **Search box hidden (RestaurantProfile)** — Navigate to `/admin/RestaurantProfile/`. Verify there is **no** search box in the toolbar — only the "Add" link. RestaurantProfile does not implement `IAdminSettings`.

12d. **Search box hidden (Ingredient)** — Navigate to `/admin/Ingredient/`. Verify there is **no** search box. Ingredient does not implement `IAdminSettings`.

12e. **Search box hidden (MenuItem)** — Navigate to `/admin/MenuItem/`. Verify there is **no** search box.

12f. **Search filters correctly** — With at least two categories (e.g., "Italian" and "Japanese"), navigate to `/admin/Category/?q=Italian`. Verify only the matching row appears (count shows "1 category") and "Japanese" is not in the results.

12g. **Search ignored on non-searchable entity** — Navigate to `/admin/RestaurantProfile/?q=something`. Verify all records still display (the `?q=` param is ignored when no `SearchFields` are configured).

#### FK Lookup Popup

FK fields render as a plain text input (showing the raw FK ID) plus a lookup icon, matching Django Admin's `raw_id_fields` pattern. No `<select>` dropdowns.

**Test steps:**

12h. **FK text input + lookup icon (MenuItem → Restaurant)** — Navigate to `/admin/MenuItem/add/`. Verify the Restaurant field renders as:
  - A text input with `class="vForeignKeyRawIdAdminField"`
  - A lookup link with `class="related-lookup"` containing a magnifying glass icon
  - The lookup link URL contains `/admin/Restaurant/?_to_field=id&_popup=1`
  - There is **no** `<select>` element for the FK field

12i. **FK text input + lookup icon (RestaurantProfile → Restaurant)** — Navigate to `/admin/RestaurantProfile/add/`. Same assertions as 12h.

12j. **Popup renders simplified layout** — Navigate directly to `/admin/Restaurant/?_popup=1`. Verify:
  - The page has `class="popup"` on the `<body>` tag
  - There is **no** `id="header"` element (no site header)
  - There is **no** `id="sidebar"` element (no navigation sidebar)
  - The table rows have links with `class="popup-select"` and `data-pk` attributes (not links to change views)

12k. **Popup respects conditional search** — Navigate to `/admin/Restaurant/?_popup=1`. Since Restaurant has `SearchFields`, the popup should show a search box. Navigate to `/admin/Ingredient/?_popup=1` (no `IAdminSettings`) — no search box.

12l. **Popup search preserves popup params** — Navigate to `/admin/Restaurant/?_popup=1&_to_field=id`. Verify the search form contains hidden inputs: `name="_popup" value="1"` and `name="_to_field" value="id"`.

12m. **Popup search filters results** — Navigate to `/admin/Restaurant/?_popup=1&_to_field=id&q=Bella`. Verify only matching restaurants appear.

12n. **FK value saves correctly** — On `/admin/MenuItem/add/`, type `1` in the Restaurant text input, fill other required fields, click Save. Verify redirect to list and `RestaurantId` column shows `1`.

12o. **FK value pre-filled on edit** — Navigate to the edit form of the MenuItem created in 12n. Verify the Restaurant text input shows `1` as its value.

### Phase 4: Auth Entity CRUD

13. **AuthPermission list** — verify auto-generated permissions exist (e.g., `add_category`, `view_category`)
14. **AuthGroup** — create a group
15. **AuthGroupPermission** — assign permissions to the group
16. **AuthUser** — create a non-superuser (verify `IsActive` defaults to `true`)
17. **AuthUserGroup** — assign user to group

### Phase 5: Permission Enforcement

18. **Login as non-superuser** — logout admin, login as new user
19. **Allowed actions** — verify user can perform actions matching assigned permissions
20. **Denied actions** — verify 403 for actions without permissions

### Phase 6: List Features

21. **Conditional search** — see Phase 3a above for detailed steps
22. **Sorting** — click column header, verify sort direction toggle
23. **Pagination** — AuthPermission list has 40 items (25/page), verify page 2

### Phase 7: Time-Limited Pagination COUNT

The dashboard runs `SELECT COUNT(*)` on every list view. On tables with millions of rows, this query can take seconds. The `PaginationCountTimeoutMs` option (default 200ms) cancels the COUNT query if it exceeds the timeout and displays a fallback value of 9,999,999,999 instead. Data rows load independently.

#### Setup — Seed millions of categories

From the **repository root**, with SQL Server and the sample app schema created:

```bash
docker exec -i ndjangoadmin-db-1 /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'Password1' -C -d SampleProject \
  -i /dev/stdin < sample-project/scripts/seed-millions-of-categories.sql
```

This inserts 5 million categories (~1–3 minutes).

24. **Fallback count on large table** — Navigate to `/admin/Category/`. Verify the count shows "9999999999 categories" (the fallback). Page should load in under 1 second.
25. **Data rows still render** — The first 25 categories should display normally despite the count timeout.
26. **Sliding window pagination** — Pagination should show a window of up to 10 page links with ellipsis (`...`) and a link to the last page (400000000).
27. **Page navigation works** — Click page 2. Verify different rows appear, count still shows the fallback.
28. **Small table unaffected** — Navigate to `/admin/Restaurant/`. Verify the real count (e.g., "100 restaurants") is shown, not the fallback. Pagination shows exact page count.
29. **Custom timeout (optional)** — Set `PaginationCountTimeoutMs = 5000` in `sample-project/src/Commands/ApiCommand.cs`, restart the app. Verify `/admin/Category/` now shows the real count (5000000) since 5 seconds is enough for the query to complete.
30. **Disabled timeout (optional)** — Set `PaginationCountTimeoutMs = -1`, restart. Verify the real count shows (query runs to completion regardless of duration).

#### Cleanup

```bash
docker exec -i ndjangoadmin-db-1 /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'Password1' -C -d SampleProject \
  -i /dev/stdin < sample-project/scripts/cleanup-seeded-categories.sql
```

### Phase 8: SAML SSO (requires sample-project-sso and AWS IAM Identity Center)

To run the SSO sample instead of the default sample project:

```bash
cd sample-project-sso/src && dotnet run -- api
```

31. **SSO link visible** — login page shows "Try single sign-on (SSO)" link
32. **IdP-initiated login** — from the AWS access portal, click the application → lands on `/admin/` as the SSO user
33. **Group sync** — create an `AuthGroup` with `name` matching an AWS group UUID, assign view permissions, logout, SSO login again → user can access model views
34. **Password login still works** — can still login with `admin`/`admin` alongside SSO

### Phase 9: Logout

35. **Logout** — click "Log out", verify redirect to login page
36. **Session cleared** — accessing `/admin/` after logout redirects to login

## What's NOT Tested (Known Gaps)

- **SP-initiated SAML login:** Does not work with AWS IAM Identity Center (403 "No access"). Only IdP-initiated is testable.
- **M2M relationships:** MenuItem ↔ Ingredient many-to-many is defined in EF Core but the dashboard has no multi-select UI for it yet (Phase 5)
- **Read-only mode:** `AdminDashboardOptions.IsReadOnly = true` hides all write controls
- **Custom authorization filters:** `LocalRequestsOnlyAuthorizationFilter`, custom `IAdminDashboardAuthorizationFilter`
- **Cookie expiration:** Default 24h, configurable via `AdminDashboardOptions.CookieExpiration`
- **Permission caching:** Permissions are cached in `HttpContext.Items` per request — no cross-request caching test
