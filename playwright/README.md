# NDjango.Admin — Playwright E2E

End-to-end tests for the NDjango.Admin Dashboard. Two backends are exercised side by side:

- **EF Core** sample (`sample-project/`) on `http://localhost:8000` against SQL Server. Migrated from `E2E_TESTING.md`.
- **MongoDB** sample (`sample-project-mongodb/`) on `http://localhost:8001` against MongoDB. Migrated from `E2E_TESTING_MONGO.md`.

Each backend has its own Playwright project (`chromium`, `chromium-mongo`, …) with an isolated authenticated storage-state file.

## Stack

- **Playwright Test** v1.59.x (TypeScript)
- **Node.js** 20.x / 22.x / 24.x

## Layout

```
playwright/
├── playwright.config.ts            # Per-project baseURL (EF :8000, Mongo :8001) + storageState
├── tests-sql-server/               # EF Core suite (uses sample-project on :8000)
│   ├── auth.setup.ts               # Login as admin once → saves .auth/admin.json
│   ├── authentication/             # Phase 1
│   ├── dashboard/                  # Phase 2
│   ├── crud/                       # Phase 3
│   ├── search-and-popup/           # Phase 3a
│   ├── m2m/                        # Phase 3b (composite PK)
│   ├── auth-entities/              # Phase 4
│   ├── permissions/                # Phase 5
│   ├── list-features/              # Phase 6
│   ├── bulk-actions/               # Phase 7
│   ├── pagination-timeout/         # Phase 8 (opt-in, 5M-row seed)
│   ├── saml/                       # Phase 9 (opt-in, sample-project-sso)
│   ├── gift/                       # Phase 9a
│   └── logout/                     # Phase 10
├── tests-mongodb/                  # MongoDB suite (uses sample-project-mongodb on :8001)
│   ├── auth-mongo.setup.ts         # Login → .auth/admin-mongo.json
│   ├── authentication/             # Phase 1
│   ├── dashboard/                  # Phase 2 (Restaurant + Shop + Auth groups)
│   ├── crud/                       # Phase 3 (ObjectId references, no FK lookup popups)
│   ├── m2m/                        # Phase 3a (single-ObjectId PK on junction)
│   ├── auth-entities/              # Phase 7 (MongoAuth* entities)
│   ├── permissions/                # Phase 8
│   ├── list-features/              # Phases 4, 5, 9 (search, sort, pagination)
│   ├── bulk-actions/               # Phase 6
│   ├── breadcrumbs/                # Phase 10
│   └── logout/                     # Phase 11
├── fixtures/
│   ├── admin.ts                    # EF authenticated test
│   ├── admin-mongo.ts              # Mongo authenticated test (uses MongoFormPage)
│   ├── anonymous.ts                # EF unauthenticated
│   └── anonymous-mongo.ts          # Mongo unauthenticated
├── pages/                          # LoginPage, DashboardPage, ListPage, FormPage,
│                                   # MongoFormPage, DeleteConfirmationPage, PopupPage
└── helpers/                        # admin-urls, random, object-id, data, data-mongo,
                                    # auth-setup, auth-setup-mongo
```

## Prerequisites

1. Install Node 20+ and npm.
2. Install browsers and dependencies:

   ```bash
   cd playwright
   npm install
   npx playwright install --with-deps
   ```

3. Start the databases (from the repo root):

   ```bash
   # SQL Server (for the EF Core suite)
   docker compose up --detach --wait --wait-timeout 120 db

   # MongoDB replica set (for the MongoDB suite)
   docker compose up --detach --wait --wait-timeout 120 --remove-orphans mongoClusterSetup
   ```

4. Set `NDJANGO_SECRET_KEY` (mandatory for the dashboard's cookie data-protection):

   ```bash
   export NDJANGO_SECRET_KEY=$(openssl rand -base64 48)
   ```

5. Start the sample apps (each in its own terminal):

   ```bash
   # EF Core sample on :8000
   cd sample-project/src && dotnet run -- api

   # MongoDB sample on :8001
   cd sample-project-mongodb/src && dotnet run -- api
   ```

   Both apps create the schema (or seed the collections) plus the default `admin/admin` user.

   > Alternative: set `PLAYWRIGHT_START_SERVER=1` so Playwright starts the EF app via `webServer`. (No equivalent for the Mongo sample yet — start it manually.)

## Running

```bash
# All EF Core tests on chromium (default)
npx playwright test --project=chromium

# All MongoDB tests on chromium
npx playwright test --project=chromium-mongo

# Both backends, chromium only
npx playwright test --project=chromium --project=chromium-mongo

# Other browsers
npx playwright test --project=firefox-mongo
npx playwright test --project=webkit-mongo

# Single file
npx playwright test tests-mongodb/crud/category.spec.ts

# Single test by title pattern
npx playwright test -g "boolean field"

# Headed / UI / debug
npx playwright test --project=chromium-mongo --headed
npx playwright test --project=chromium-mongo --ui
npx playwright test --project=chromium-mongo --debug

# Open the latest HTML report
npm run test:report
```

## Test isolation strategy

- **Auth**: each backend has its own setup project (`setup` for EF, `setup-mongo` for Mongo) that logs in once as `admin/admin` and persists cookie state into a per-backend file (`.auth/admin.json` or `.auth/admin-mongo.json`). Sibling projects load that storage state automatically. Tests that need to be unauthenticated import `@fixtures/anonymous` or `@fixtures/anonymous-mongo`.
- **Database**: each backend shares a single database (SQL Server for EF, MongoDB for Mongo). `workers: 1` and `fullyParallel: false` are set deliberately because the dashboard does not isolate test data per worker. Each test creates its own records with unique names (`uniqueName('Cat')`) and cleans up only when the assertion requires it; cross-test leakage is acceptable because rows are scoped to unique names.
- **No mocking**: this is true E2E. The browser drives the real ASP.NET Core dashboard talking to a real EF Core context or MongoDB driver.

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
| `PLAYWRIGHT_BASE_URL` | `http://localhost:8000` | Origin for the EF Core projects (`chromium`, `firefox`, `webkit`) |
| `PLAYWRIGHT_MONGO_BASE_URL` | `http://localhost:8001` | Origin for the MongoDB projects (`chromium-mongo`, …) |
| `NDJANGO_ADMIN_USER` | `admin` | Username used by both setup projects |
| `NDJANGO_ADMIN_PASSWORD` | `admin` | Password used by both setup projects |
| `PLAYWRIGHT_START_SERVER` | unset | Set truthy to let Playwright start the **EF Core** sample via `webServer` |
| `PLAYWRIGHT_LARGE_DATASET` | unset | Enable Phase 8 (EF Core) pagination-timeout tests |
| `PLAYWRIGHT_SAML_ENABLED` | unset | Enable Phase 9 (EF Core) SAML tests |
| `NDJANGO_SECRET_KEY` | (required) | Cookie data-protection secret (≥ 32 chars). Both sample apps will refuse to start without it. |
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
