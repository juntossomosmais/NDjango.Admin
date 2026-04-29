# E2E Testing Guide

End-to-end testing of the NDjango.Admin Admin Dashboard using the sample project and Playwright MCP.

## Prerequisites

```bash
# SQL Server must be running
docker compose up --detach --wait --wait-timeout 120 db

# Wait for it to be healthy (~15s), then start the sample project
cd sample-project/src && dotnet run -- api
```

- **App URL:** `http://localhost:8000/admin/`
- **DB:** SQL Server on `localhost:1433`, sa/Password1, database `SampleProject`
- **Auto-setup:** The app calls `EnsureCreated()` on startup — no migrations needed
- **Authentication:** Authentication is always enabled. The sample project sets `CreateDefaultAdminUser = true`, so default credentials are **admin / admin**.

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

All dashboard pages require login. The auth system uses cookie-based sessions with DataProtection encryption.

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
| Restaurant | `/admin/Restaurant/` | Has 1:1 RestaurantProfile, has many MenuItems | `SearchFields`: Name, `Actions`: "Mark selected restaurants as featured" |
| RestaurantProfile | `/admin/RestaurantProfile/` | FK text input + lookup popup → Restaurant (1:1) | None |
| Ingredient | `/admin/Ingredient/` | Standalone | None |
| MenuItem | `/admin/MenuItem/` | FK text input + lookup popup → Restaurant (N:1) | None |
| MenuItemIngredient | `/admin/MenuItemIngredient/` | M2M junction: FK lookup → MenuItem + FK lookup → Ingredient (composite PK) | None |
| Gift | `/admin/Gift/` | Standalone; has DateOnly, TimeOnly, TimeSpan, DateTimeOffset fields | None |

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
| Execute bulk action | `/admin/{Model}/action/` | POST |
| Bulk delete confirmation | `/admin/{Model}/action/delete/` | GET |
| Bulk delete execute | `/admin/{Model}/action/delete/` | POST |

**Composite key URLs:** For entities with composite primary keys (e.g., `MenuItemIngredient`), the `{id}` segment contains comma-separated key values. Example: `/admin/MenuItemIngredient/1,3/change/` where `1` is `MenuItemId` and `3` is `IngredientId`. Individual key values containing commas are percent-encoded as `%2C`.

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

**Bulk actions:** Restaurant defines a custom action via `AdminActionList<int>`:
- "Mark selected restaurants as featured" — returns a success message with the count of selected IDs. This is a demo action (no database side effects).
- The built-in "Delete selected restaurants" action is also available (auto-added for all editable entities).

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

### MenuItemIngredient (M2M junction entity — composite PK)

`MenuItemIngredient` is a junction/through table for the many-to-many relationship between `MenuItem` and `Ingredient`. It does **not** inherit from `StandardEntity` — it has no `Id`, `CreatedAt`, or `UpdatedAt` fields. Its primary key is a composite of the two FK fields.

**Fields on forms:**
- `MenuItem` — **text input + lookup popup** (raw FK ID), required, maps to `MenuItemId`. Read-only on edit form (part of composite PK)
- `Ingredient` — **text input + lookup popup** (raw FK ID), required, maps to `IngredientId`. Read-only on edit form (part of composite PK)

**Composite key URL pattern:** `/admin/MenuItemIngredient/{menuItemId},{ingredientId}/change/`

**Test steps:**
1. **Ensure at least one MenuItem and one Ingredient exist first**
2. Navigate to `/admin/MenuItemIngredient/add/`
3. Verify both fields render as text inputs (`class="vForeignKeyRawIdAdminField"`) with lookup icons
4. Enter valid MenuItem ID and Ingredient ID
5. Click Save → redirects to list
6. Click the row link → opens edit form at composite key URL (e.g., `/admin/MenuItemIngredient/1,3/change/`)
7. Verify both FK fields are **read-only** on the edit form
8. Test delete via the edit form's Delete link

**FK field details:**
- The MenuItem `<input>` has `name="MenuItemId"` (FK property name)
- The Ingredient `<input>` has `name="IngredientId"` (FK property name)
- Lookup popups open `/admin/MenuItem/?_to_field=id&_popup=1` and `/admin/Ingredient/?_to_field=id&_popup=1` respectively

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

With 11 entities (6 user + 5 auth), expect 44 permissions total (25 per page, paginated).

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
- **Bulk actions (Django-style):** When not in popup mode and entity is editable, the list view shows:
  - **Checkboxes:** Each row has a checkbox (`name="_selected_ids"`, `value="{pk}"`). The header has a select-all checkbox.
  - **Action bar:** Below the toolbar — a `<select name="action">` dropdown with "---" placeholder + available actions, a "Go" button, and a counter ("0 of N selected").
  - **Built-in action:** "Delete selected {entityNamePlural}" is auto-added for all editable entities.
  - **Custom actions:** Entities implementing `IAdminSettings<T>` with an `Actions` property (returning `AdminActionList<TPk>`) register additional actions. Restaurant has "Mark selected restaurants as featured".
  - **Flash messages:** After action execution, a success (green) or error (red) banner is displayed via `_msg` / `_msg_level` query parameters on redirect.
  - **Form:** The action bar + table are wrapped in `<form id="changelist-form" method="post" action="/{basePath}/{entityId}/action/">`.
  - **Selected row highlighting:** Checked rows get a yellow background (`tr.selected` CSS class).

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
7. **Sidebar** — shows "Models" section and "Authentication and Authorization" section with all 11 entities (6 user + 5 auth)

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

### Phase 3b: Many-to-Many (M2M) Relationships via Junction Entity

The `MenuItemIngredient` entity is an explicit junction/through table that links `MenuItem` and `Ingredient` in a many-to-many relationship. It uses a **composite primary key** (`MenuItemId`, `IngredientId`) — no auto-generated `Id`. The dashboard treats it as a first-class entity with its own list, add, edit, and delete pages, matching Django Admin's pattern for M2M with explicit `through` models.

**Key behaviors:**
- The junction entity appears in the dashboard sidebar and home page like any other entity
- Both FK fields render as text inputs with lookup popup icons (same as regular FK fields)
- URLs use comma-separated composite keys: `/admin/MenuItemIngredient/{menuItemId},{ingredientId}/change/`
- On the edit form, both FK/PK fields are **read-only** (changing them would change the record's identity)
- Deleting a junction record removes only the relationship, not the parent entities
- Parent entities (`MenuItem`, `Ingredient`) do NOT show the M2M relationship on their forms

**Prerequisites:** At least one `MenuItem` and one `Ingredient` must exist before creating junction records. Use the IDs from the seeded data or create new ones first.

#### Junction Entity in Dashboard

12p. **MenuItemIngredient appears in dashboard** — Navigate to `/admin/`. Verify `MenuItemIngredient` (or "Menu Item Ingredients") appears in the entity list alongside other models.

12q. **MenuItemIngredient appears in sidebar** — Navigate to any entity list page. Verify `MenuItemIngredient` appears in the left sidebar under the "Models" section.

#### Junction Entity Add Form (Composite Key Create)

12r. **Add form renders two FK lookup fields** — Navigate to `/admin/MenuItemIngredient/add/`. Verify:
  - Two FK fields are shown: `MenuItem` and `Ingredient`
  - Each renders as a text input with `class="vForeignKeyRawIdAdminField"` and a lookup icon with `class="related-lookup"`
  - The MenuItem lookup icon links to `/admin/MenuItem/?_to_field=id&_popup=1`
  - The Ingredient lookup icon links to `/admin/Ingredient/?_to_field=id&_popup=1`
  - There are NO `Id`, `CreatedAt`, or `UpdatedAt` fields (junction entity does not inherit from `StandardEntity`)
  - Three save buttons are present: "Save", "Save and add another", "Save and continue editing"

12s. **Create junction record via Save** — On `/admin/MenuItemIngredient/add/`:
  1. Enter a valid MenuItem ID (e.g., `1`) in the MenuItem field
  2. Enter a valid Ingredient ID (e.g., `1`) in the Ingredient field
  3. Click "Save"
  4. Verify redirect to `/admin/MenuItemIngredient/` list
  5. Verify the new record appears in the list showing `MenuItemId` and `IngredientId` columns

12t. **Create junction record via Save and continue editing** — On `/admin/MenuItemIngredient/add/`:
  1. Enter a MenuItem ID and Ingredient ID (use a combination not yet created, e.g., MenuItem `2`, Ingredient `2`)
  2. Click "Save and continue editing"
  3. Verify redirect to `/admin/MenuItemIngredient/{menuItemId},{ingredientId}/change/` (composite key in URL)
  4. Verify the URL contains the comma-separated composite key (e.g., `/admin/MenuItemIngredient/2,2/change/`)

12u. **Create junction record via Save and add another** — On the edit form from step 12t, click "Save and add another". Verify redirect to `/admin/MenuItemIngredient/add/` (blank form).

#### Junction Entity List View (Composite Key Links)

12v. **List view shows composite key edit links** — Navigate to `/admin/MenuItemIngredient/`. Verify:
  - The table shows columns for `MenuItemId` and `IngredientId`
  - The first visible column is a clickable link
  - The link URL uses comma-separated composite key: `/admin/MenuItemIngredient/{menuItemId},{ingredientId}/change/`
  - Record count shows the correct number of junction records

12w. **List view checkboxes use composite key values** — Inspect the checkboxes in the list view. Each checkbox `value` should contain the encoded composite key (e.g., `1,1`).

#### Junction Entity Edit Form (Read-Only PK Fields)

12x. **Edit form loads with composite key URL** — Navigate to `/admin/MenuItemIngredient/{menuItemId},{ingredientId}/change/` (use values from a record created above). Verify:
  - The page loads successfully (200 OK)
  - Both FK fields show the correct pre-filled values
  - Both FK/PK fields are rendered as **read-only text** (not editable inputs) — since they are part of the composite primary key, changing them would change the record's identity
  - A "Delete" link is present

#### Junction Entity Delete

12y. **Delete junction record** — From the edit form of a junction record:
  1. Click the "Delete" link
  2. Verify redirect to `/admin/MenuItemIngredient/{menuItemId},{ingredientId}/delete/` (composite key in URL)
  3. Verify the confirmation page shows "Are you sure?" with the record details
  4. Click "Yes, I'm sure"
  5. Verify redirect to `/admin/MenuItemIngredient/` list
  6. Verify the record is gone from the list

12z. **Delete only removes relationship, not parents** — After deleting a junction record:
  1. Navigate to `/admin/MenuItem/`. Verify the parent MenuItem still exists
  2. Navigate to `/admin/Ingredient/`. Verify the parent Ingredient still exists

#### Junction Entity Bulk Delete (Composite Keys)

12aa. **Bulk delete junction records** — On `/admin/MenuItemIngredient/`:
  1. Create 2 test junction records if needed (e.g., MenuItem 1 + Ingredient 3, MenuItem 1 + Ingredient 4 — create test Ingredients first if needed)
  2. Select both records via checkboxes
  3. Select "Delete selected menu item ingredients" from the action dropdown
  4. Click "Go"
  5. Verify redirect to bulk delete confirmation page showing "2 menu item ingredients"
  6. Click "Yes, I'm sure"
  7. Verify redirect to list with success message "Successfully deleted 2 menu item ingredients."
  8. Verify both records are gone

#### Malformed Composite Key URLs

12ab. **Invalid composite key returns 400** — Navigate to `/admin/MenuItemIngredient/INVALID/change/`. Verify a 400 Bad Request response (not a 500 server error).

12ac. **Single value for composite key returns 400** — Navigate to `/admin/MenuItemIngredient/1/change/`. Verify a 400 Bad Request response (expects two comma-separated values).

#### Cascade Delete from Parent

12ad. **Deleting parent cascades to junction records** — This tests EF Core cascade delete:
  1. Create a temporary Ingredient (e.g., "TempIngredient")
  2. Create a junction record linking any MenuItem to TempIngredient
  3. Verify the junction record appears in `/admin/MenuItemIngredient/` list
  4. Delete the TempIngredient from `/admin/Ingredient/`
  5. Verify the junction record was also deleted (cascade delete)

#### Cleanup

12ae. **Clean up test data** — Delete any test junction records, MenuItems, and Ingredients created during M2M testing. Verify entity counts return to seeded values.

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
23. **Pagination** — AuthPermission list has 44 items (25/page), verify page 2

### Phase 7: Bulk Actions (Django-Style)

The list view supports Django-style bulk actions: select rows via checkboxes, pick an action from a dropdown, execute. Includes a built-in "Delete selected" action with confirmation page, custom user-defined actions, and flash messages for feedback.

#### Action Bar and Checkboxes

42. **Action bar renders on editable entity** — Navigate to `/admin/Restaurant/`. Verify:
    - An action dropdown (`<select name="action">`) is visible with options: "---" (placeholder), "Delete selected restaurants", "Mark selected restaurants as featured"
    - A "Go" button next to the dropdown
    - A counter text showing "0 of N selected" (where N is the number of rows)
    - Each table row has a checkbox (`name="_selected_ids"`)
    - The table header has a select-all checkbox

43. **Action bar renders on entity without custom actions** — Navigate to `/admin/Category/`. Verify the action dropdown contains only "---" and "Delete selected categories" (no custom actions).

44. **Action bar hidden in popup mode** — Navigate to `/admin/Restaurant/?_popup=1`. Verify:
    - There is **no** action dropdown
    - There are **no** row checkboxes
    - There is **no** select-all checkbox

45. **Action bar hidden when read-only** — If the dashboard or entity is configured as read-only (`IsReadOnly = true`), the action bar and checkboxes should not render.

#### Checkbox Interactions (JavaScript)

46. **Select-all toggle** — On `/admin/Restaurant/`, click the header select-all checkbox:
    - All row checkboxes become checked
    - The counter updates to "N of N selected" (all rows)
    - All rows get yellow highlight (`tr.selected` CSS class)
    - Uncheck the header checkbox → all rows unchecked, counter resets to "0 of N selected"

47. **Individual checkbox updates counter** — Check 2 of N rows individually:
    - Counter shows "2 of N selected"
    - Only checked rows are highlighted yellow
    - Header checkbox remains unchecked (not all rows selected)

48. **Uncheck one row unchecks header** — Check the header (select all), then uncheck a single row:
    - Header checkbox becomes unchecked
    - Counter shows "N-1 of N selected"

49. **Check all individually checks header** — Check every row checkbox one by one. When all are checked, the header checkbox should also become checked.

#### Custom Action Execution

50. **Execute custom action** — On `/admin/Restaurant/`:
    1. Check one or more restaurant rows
    2. Select "Mark selected restaurants as featured" from the dropdown
    3. Click "Go"
    4. Verify redirect back to `/admin/Restaurant/` with a **green success banner** showing "Successfully marked N restaurant(s) as featured."
    5. Verify the flash message disappears on subsequent navigation (it's a one-time query param)

51. **Custom action with multiple selections** — Select 3 restaurants, execute "Mark selected restaurants as featured". Verify message says "3 restaurant(s)".

#### Built-In Delete Action (Bulk Delete)

52. **Delete selected redirects to confirmation** — On `/admin/Restaurant/`:
    1. Create 2 test restaurants (e.g., "TestDel1", "TestDel2") if needed
    2. Check both rows
    3. Select "Delete selected restaurants" from dropdown
    4. Click "Go"
    5. Verify redirect to `/admin/Restaurant/action/delete/?ids={id1}&ids={id2}` (bulk delete confirmation page)

53. **Bulk delete confirmation page content** — On the confirmation page, verify:
    - Heading: "Are you sure?"
    - Summary text: "2 restaurants" (count + plural entity name)
    - An "Objects" section listing each selected record with its field values
    - A "Yes, I'm sure" button (submits POST form)
    - A "No, take me back" link pointing to `/admin/Restaurant/`
    - Hidden inputs with `name="_selected_ids"` for each selected ID
    - The sidebar is visible (not a popup page)

54. **Confirm bulk delete** — Click "Yes, I'm sure":
    1. Verify redirect to `/admin/Restaurant/` with a **green success banner**: "Successfully deleted 2 restaurants."
    2. Verify the deleted records no longer appear in the list
    3. Verify the record count dropped by 2

55. **Cancel bulk delete** — Repeat the delete flow but click "No, take me back":
    1. Verify redirect to `/admin/Restaurant/` list
    2. Verify no records were deleted (count unchanged)

#### Empty Selection Guards

56. **Empty selection with non-allowEmpty action** — On `/admin/Restaurant/`:
    1. Do NOT check any rows
    2. Select "Delete selected restaurants" (which has `AllowEmptySelection = false`)
    3. Click "Go"
    4. Verify either: JavaScript prevents form submission (client-side guard), or the server redirects back to the list with no action taken

57. **No action selected** — Check some rows but leave the dropdown on "---". Click "Go". Verify nothing happens (JavaScript guard prevents submission).

#### Flash Messages

58. **Success message styling** — After a successful action, verify the message banner:
    - Has green background color
    - Contains the success text
    - Is positioned between the page heading and the changelist content

59. **Error message styling** — If an action handler returns an error (e.g., via `AdminActionResult.Error()`), verify:
    - The message banner has red/pink background
    - Contains the error text
    - Uses `_msg_level=error` in the redirect URL

60. **Message is transient** — After seeing a flash message, navigate away and come back. Verify the message is gone (it's a one-time query param, not stored in session).

#### Bulk Delete with Different Entities

61. **Bulk delete on Category** — Select categories, delete via bulk action, confirm, verify deleted.

62. **Bulk delete single record** — Select just one record, go through bulk delete flow. Verify confirmation says "1 {entityName}" (singular) and success message says "Successfully deleted 1 {entityName}."

### Phase 8: Time-Limited Pagination COUNT

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

### Phase 9: SAML SSO (requires sample-project-sso and AWS IAM Identity Center)

To run the SSO sample instead of the default sample project:

```bash
cd sample-project-sso/src && dotnet run -- api
```

31. **SSO link visible** — login page shows "Try single sign-on (SSO)" link
32. **IdP-initiated login** — from the AWS access portal, click the application → lands on `/admin/` as the SSO user
33. **Group sync** — create an `AuthGroup` with `name` matching an AWS group UUID, assign view permissions, logout, SSO login again → user can access model views
34. **Password login still works** — can still login with `admin`/`admin` alongside SSO

### Phase 9a: Gift — Date/Time ISO Formatting (uses Gift model)

The Gift model (`sample-project/src/Models.cs`) exercises all date/time types: `DateOnly`, `TimeOnly`, `TimeSpan`, and `DateTimeOffset`. Form inputs must render values in ISO format and preserve them through create/update round-trips. `DateTimeOffset` must preserve the timezone offset (rendered as `type="text"`, not `datetime-local`).

#### Input Types

| Property | CLR Type | Expected HTML Input | Value Format |
|---|---|---|---|
| `ExpirationDate` | `DateOnly` | `type="date"` | `yyyy-MM-dd` |
| `AvailableFrom` | `TimeOnly` | `type="text"` | `HH:mm:ss` |
| `PreparationTime` | `TimeSpan` | `type="text"` | `hh:mm:ss` |
| `ShippedAt` | `DateTimeOffset` | `type="text"` | `yyyy-MM-ddTHH:mm:sszzz` |

#### Test Steps

37. **Add form renders correct input types** — Navigate to `/admin/Gift/add/`. Inspect the HTML:
    - `id_ExpirationDate` is `<input type="date">`
    - `id_ShippedAt` is `<input type="text">` (not `datetime-local`)
    - `id_AvailableFrom` is `<input type="text">`
    - `id_PreparationTime` is `<input type="text">`

38. **Create with all date/time types** — Fill the Gift form:
    - `Name`: any unique name
    - `ExpirationDate`: `2029-12-25`
    - `ShippedAt`: `2028-06-15T10:30:00+05:30` (non-UTC offset)
    - `AvailableFrom`: `14:30:00`
    - `PreparationTime`: `02:15:00`
    - Fill all other required fields (Barcode, Price, Weight, Rating, QuantityInStock, MinAge, TrackingCode, Description, Notes)
    - Click "Save and continue editing" → redirects to `/admin/Gift/{id}/change/`

39. **Edit form shows ISO values** — On the edit form, verify:
    - `ExpirationDate` input value is `2029-12-25`
    - `ShippedAt` input value is `2028-06-15T10:30:00+05:30` (offset preserved)
    - `AvailableFrom` input value is `14:30:00`
    - `PreparationTime` input value is `02:15:00`

40. **Update date/time fields round-trip** — Change the values:
    - `ExpirationDate`: `2031-01-15`
    - `ShippedAt`: `2030-11-20T18:45:00-03:00` (different offset)
    - `AvailableFrom`: `09:00:00`
    - `PreparationTime`: `04:30:00`
    - Click "Save and continue editing"
    - Verify all updated values appear correctly in the edit form, including the `-03:00` offset on `ShippedAt`

41. **Delete gift** — Click Delete → confirm → redirects to list, record gone

### Phase 10: Logout

35. **Logout** — click "Log out", verify redirect to login page
36. **Session cleared** — accessing `/admin/` after logout redirects to login

## What's NOT Tested (Known Gaps)

- **SP-initiated SAML login:** Does not work with AWS IAM Identity Center (403 "No access"). Only IdP-initiated is testable.
- **Read-only mode:** `AdminDashboardOptions.IsReadOnly = true` hides all write controls and bulk action bar
- **Cookie expiration:** Default 24h, configurable via `AdminDashboardOptions.CookieExpiration`
- **Permission caching:** Permissions are cached in `HttpContext.Items` per request — no cross-request caching test
- **Custom action error result (E2E):** The sample project's "Mark selected restaurants as featured" always returns success. The error flash message (red banner) can only be E2E-tested if a custom action returning `AdminActionResult.Error()` is added to the sample project. The error path is covered by integration tests (`ActionTests.cs`)
- **Custom action handler exception (E2E):** When a custom action handler throws an exception, the server catches it and redirects with a generic error message. This is covered by integration tests but not E2E-tested in the sample project
- **`AllowEmptySelection = true` (E2E):** No sample entity has a custom action with `allowEmptySelection: true`, so the "allow empty selection" path is only tested via integration tests
