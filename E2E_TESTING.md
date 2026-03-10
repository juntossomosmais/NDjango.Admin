# E2E Testing Guide

End-to-end testing of the EasyData Admin Dashboard using the sample project and Playwright MCP.

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

## Dashboard Structure

The admin home at `/admin/` shows all 5 models in a table with Add/Change links:

| Model | URL prefix | Relationships |
|---|---|---|
| Category | `/admin/Category/` | Standalone |
| Restaurant | `/admin/Restaurant/` | Has 1:1 RestaurantProfile, has many MenuItems |
| RestaurantProfile | `/admin/RestaurantProfile/` | FK dropdown → Restaurant (1:1) |
| Ingredient | `/admin/Ingredient/` | Standalone (has M2M with MenuItem, not yet supported in UI) |
| MenuItem | `/admin/MenuItem/` | FK dropdown → Restaurant (N:1) |

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
- `Restaurant` — **dropdown (combobox)**, required, shows all Restaurants by name

**Test steps:**
1. **Ensure at least one Restaurant exists first**
2. Navigate to `/admin/RestaurantProfile/add/`
3. Verify the Restaurant dropdown (`combobox`) is present with restaurant names as options
4. Fill Capacity, OpeningHours, Website
5. Select a Restaurant from the dropdown
6. Click Save → redirects to list
7. Verify `RestaurantId` column in list matches the selected restaurant's ID
8. Edit: verify dropdown has the correct restaurant pre-selected

**FK dropdown details:**
- The `<select>` element has `name="RestaurantId"` (the FK property name, not the navigation property name "Restaurant")
- Options show the restaurant's display text (Name field), with the PK as the `value`

### MenuItem (N:1 FK to Restaurant)

**Editable fields on forms:**
- `Description` — textbox, required (has default empty string)
- `IsAvailable` — checkbox (default true)
- `Name` — textbox, required
- `Price` — number with step=any (spinbutton), required
- `Restaurant` — **dropdown (combobox)**, required, shows all Restaurants by name

**Test steps:**
1. **Ensure at least one Restaurant exists first**
2. Navigate to `/admin/MenuItem/add/`
3. Verify the Restaurant dropdown is present
4. Fill Name, Description, Price (use `.` as decimal separator, e.g., `14.99`)
5. Check/uncheck IsAvailable
6. Select a Restaurant
7. Click Save → redirects to list
8. Verify record in list with correct RestaurantId
9. Edit: verify all fields are pre-filled correctly, dropdown has correct selection

**Price locale note:** The list view may display prices with comma as decimal separator (e.g., `14,99`) depending on server locale. The form uses `<input type="number" step="any">`.

## Save Actions

Every create/edit form has three save buttons:

| Button | `_save_action` value | Redirect after save |
|---|---|---|
| Save | `save` | `/admin/{Model}/` (list) |
| Save and add another | `add_another` | `/admin/{Model}/add/` (new form) |
| Save and continue editing | `continue` | `/admin/{Model}/{id}/change/` (same record) |

## List View Features

- **Search:** Text box at top, submits as `?q=term` query param, filters via substring match
- **Sorting:** Click column headers to sort. URL: `?sort=FieldName&dir=asc` or `dir=desc`. Active sort shows arrow (▲/▼)
- **Pagination:** Shows page numbers at bottom when records exceed `DefaultRecordsPerPage` (25). URL: `?page=N`
- **Record count:** Shows "N {model_name}" above the table

## Sidebar

All pages (except dashboard home) have a left sidebar with:
- A text filter input ("Filter models...")
- All 5 models listed as links under "Models" heading
- Client-side JS filters the list as you type

## Verification Checklist

When running a full E2E test pass, verify in this order (respects FK dependencies):

1. **Dashboard home** — `/admin/` loads, shows all 5 models with Add/Change links
2. **Category** — full CRUD (create, list, edit, delete)
3. **Ingredient** — create with boolean field, edit, delete
4. **Restaurant** — full CRUD
5. **RestaurantProfile** — create with FK dropdown to Restaurant, edit, delete
6. **MenuItem** — create with FK dropdown to Restaurant, edit, delete
7. **Search** — search on any list page, verify filtered results
8. **Sorting** — click column header, verify sort direction toggle

## What's NOT Tested (Known Gaps)

- **M2M relationships:** MenuItem ↔ Ingredient many-to-many is defined in EF Core but the dashboard has no multi-select UI for it yet (Phase 5)
- **Pagination:** Only testable with 25+ records in a single model
- **Read-only mode:** `AdminDashboardOptions.IsReadOnly = true` hides all write controls
- **Authorization filters:** `LocalRequestsOnlyAuthorizationFilter`, custom `IAdminDashboardAuthorizationFilter`
- **Entity groups:** `AdminDashboardOptions.EntityGroups` for sidebar grouping
