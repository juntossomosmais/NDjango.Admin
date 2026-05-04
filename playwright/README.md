# NDjango.Admin — Playwright E2E

End-to-end tests for the NDjango.Admin Dashboard using the EF Core sample project (`sample-project/`). Migrated from `E2E_TESTING.md` (the long-form manual test guide) to executable Playwright tests in TypeScript.

## Stack

- **Playwright Test** v1.59.x (TypeScript)
- **Node.js** 20.x / 22.x / 24.x
- Targets the sample-project running on `http://localhost:8000` against SQL Server on `localhost:1433`

## Layout

```
playwright/
├── playwright.config.ts            # Top-level config (projects, baseURL, webServer)
├── tests-sql-server/
│   ├── auth.setup.ts               # Login as admin once → saves storage state
│   ├── authentication/             # Phase 1: login/logout
│   ├── dashboard/                  # Phase 2: dashboard home + sidebar
│   ├── crud/                       # Phase 3: Category, Restaurant, Ingredient, RestaurantProfile, MenuItem
│   ├── search-and-popup/           # Phase 3a: conditional search + FK lookup popup
│   ├── m2m/                        # Phase 3b: MenuItemIngredient junction (composite PK)
│   ├── auth-entities/              # Phase 4: AuthUser/Group/Permission/UserGroup/GroupPermission
│   ├── permissions/                # Phase 5: permission enforcement (403 for missing perms)
│   ├── list-features/              # Phase 6: sorting + pagination
│   ├── bulk-actions/               # Phase 7: action bar, checkboxes, custom + delete actions, flash messages
│   ├── pagination-timeout/         # Phase 8: COUNT timeout fallback (requires 5M categories)
│   ├── saml/                       # Phase 9: SAML SSO (skipped by default)
│   ├── gift/                       # Phase 9a: Gift date/time round-trip
│   └── logout/                     # Phase 10: session cleared verification
├── fixtures/
│   ├── admin.ts                    # Authenticated test (default)
│   └── anonymous.ts                # Unauthenticated test (login/logout flows)
├── pages/                          # Page Objects: LoginPage, DashboardPage, ListPage, FormPage, etc.
└── helpers/                        # admin-urls, random, data factories, auth-setup
```

## Prerequisites

1. Install Node 20+ and npm.
2. Install browsers and dependencies:

   ```bash
   cd playwright
   npm install
   npx playwright install --with-deps
   ```

3. Start SQL Server (from the repo root):

   ```bash
   docker compose up --detach --wait --wait-timeout 120 db
   ```

4. Start the sample app (from the repo root):

   ```bash
   cd sample-project/src && dotnet run -- api
   ```

   The app listens on `http://localhost:8000` and creates the schema (`EnsureCreated()`) plus the default `admin/admin` user.

   > Alternative: set `PLAYWRIGHT_START_SERVER=1` so Playwright starts the app via `webServer`. The DB still needs to be up first.

## Running

```bash
# All tests, default browser (chromium)
npm test

# Specific browser
npm run test:chromium
npm run test:firefox
npm run test:webkit

# Headed (watch the browser) / UI mode (interactive)
npm run test:headed
npm run test:ui

# Single file
npx playwright test tests-sql-server/crud/category.spec.ts

# Single test by title pattern
npx playwright test -g "boolean field"

# Open the latest HTML report
npm run test:report
```

## Test isolation strategy

- **Auth**: a `setup` project logs in once as `admin/admin` and persists the cookie state into `.auth/admin.json`. Every other project loads that storage state automatically. Tests that need to be unauthenticated import `@fixtures/anonymous` instead.
- **Database**: the suite shares a single SQL Server database. `workers: 1` and `fullyParallel: false` are set deliberately because the dashboard does not isolate test data per worker. Each test creates its own records with unique names (`uniqueName('Cat')`) and cleans up only when the assertion requires it; cross-test leakage is acceptable because rows are scoped to unique names.
- **No mocking**: this is true E2E. The browser drives the real ASP.NET Core dashboard talking to a real EF Core context.

## Phase 8 — Pagination timeout

Requires the 5M-category seed. Run from repo root:

```bash
docker exec -i ndjangoadmin-db-1 /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'Password1' -C -d SampleProject \
  -i /dev/stdin < sample-project/scripts/seed-millions-of-categories.sql

PLAYWRIGHT_LARGE_DATASET=true npx playwright test tests-sql-server/pagination-timeout
```

Cleanup:

```bash
docker exec -i ndjangoadmin-db-1 /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'Password1' -C -d SampleProject \
  -i /dev/stdin < sample-project/scripts/cleanup-seeded-categories.sql
```

## Phase 9 — SAML SSO

Skipped by default. Enabling requires `sample-project-sso` running plus AWS IAM Identity Center configuration. Run with `PLAYWRIGHT_SAML_ENABLED=true`. SP-initiated login is documented as broken on AWS IAM Identity Center; the IdP-initiated path is an external manual flow.

## Environment variables

| Variable | Default | Effect |
|---|---|---|
| `PLAYWRIGHT_BASE_URL` | `http://localhost:8000` | Dashboard origin under test |
| `NDJANGO_ADMIN_USER` | `admin` | Username used by `auth.setup.ts` |
| `NDJANGO_ADMIN_PASSWORD` | `admin` | Password used by `auth.setup.ts` |
| `PLAYWRIGHT_START_SERVER` | unset | Set truthy to let Playwright start `dotnet run -- api` via `webServer` |
| `PLAYWRIGHT_LARGE_DATASET` | unset | Enable Phase 8 tests |
| `PLAYWRIGHT_SAML_ENABLED` | unset | Enable Phase 9 SAML tests |
| `CI` | unset | Enables retries=2, github reporter, forbidOnly |

## Codegen

To inspect the live admin and generate a snippet, with the app running:

```bash
npx playwright codegen http://localhost:8000/admin/
```

## Troubleshooting

- **Chrome fails to launch after upgrade**: clear the MCP profile cache:
  ```bash
  rm -rf ~/Library/Caches/ms-playwright/mcp-chrome-*
  ```
- **`auth.setup` fails**: ensure the dashboard is reachable at `PLAYWRIGHT_BASE_URL/admin/` and that the default admin (`admin/admin`) exists. The sample project sets `CreateDefaultAdminUser = true`.
- **403 on Phase 5 tests**: the test creates a non-superuser and switches sessions; failures usually mean the prior admin session leaked into the new context. The test calls `clearCookies()` between roles.
