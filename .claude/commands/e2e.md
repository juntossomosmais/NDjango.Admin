---
description: Run NDjango.Admin Playwright E2E tests against the EF Core sample-project and/or the MongoDB sample-project (handles DBs, app startup, browser install, and cleanup).
argument-hint: [--backend ef|mongo|both] [--browser chromium|firefox|webkit] [--grep "pattern"] [--file path] [--headed] [--ui] [--debug] [--keep-server] [--saml] [--large-dataset] [--reuse-server] [--clean-auth]
allowed-tools: Bash, Read, Write, Edit
---

# /e2e — Run Playwright end-to-end tests

You are running the Playwright E2E suite for **NDjango.Admin**. The user invoked you with arguments: `$ARGUMENTS`.

The suite has two backend targets:

| Backend | Sample app | Port | Database | Playwright projects |
|---|---|---|---|---|
| `ef` | `sample-project/` (EF Core, SQL Server) | 8000 | `db` (compose service) | `chromium` / `firefox` / `webkit` |
| `mongo` | `sample-project-mongodb/` (MongoDB driver) | 8001 | `mongoClusterSetup` (compose service) | `chromium-mongo` / `firefox-mongo` / `webkit-mongo` |

Be terse. Show progress in one-line updates between steps. Do **not** narrate every command — just run them and report results.

---

## 1. Parse arguments

Recognized flags from `$ARGUMENTS`:

| Flag | Effect |
|---|---|
| `--backend <name>` | `both` (default), `ef`, or `mongo`. Selects which sample-project(s) to run and which Playwright project(s) to invoke. |
| `--browser <name>` | `chromium` (default), `firefox`, or `webkit`. Maps to `--project=<browser>` for `ef`, `--project=<browser>-mongo` for `mongo`, or both when `--backend both`. |
| `--grep "<pattern>"` | Forwarded to Playwright as `-g "<pattern>"` (regex match on test title). |
| `--file <path>` | Run a single spec file (relative to `playwright/`, e.g. `tests-sql-server/crud/category.spec.ts` or `tests-mongodb/crud/category.spec.ts`). When `--file` points into `tests-mongodb/`, treat the backend as `mongo` even if `--backend` was not passed. |
| `--headed` | Pass `--headed` to Playwright. Disables auto-cleanup. |
| `--ui` | Pass `--ui` to Playwright (Watch mode UI). Disables auto-cleanup. |
| `--debug` | Pass `--debug` to Playwright. Disables auto-cleanup. |
| `--keep-server` | Leave the started sample-project(s) running after tests finish. |
| `--reuse-server` | If a sample-project is already running on its port, attach to it instead of starting a new one. Default is to detect this automatically per backend. |
| `--saml` | Set `PLAYWRIGHT_SAML_ENABLED=true` (Phase 9 SAML tests). **EF only** — has no effect with `--backend mongo`. Requires `sample-project-sso` and AWS IAM Identity Center, not the default sample. |
| `--large-dataset` | Set `PLAYWRIGHT_LARGE_DATASET=true` (Phase 8 pagination COUNT timeout). **EF only**. Requires the 5M-category seed (`sample-project/scripts/seed-millions-of-categories.sql`). |
| `--clean-auth` | Delete the storage-state file(s) for the selected backend(s) (`playwright/.auth/admin.json` for ef, `playwright/.auth/admin-mongo.json` for mongo) to force re-authentication. |

If no arguments are given, run **both** backends on `chromium` headless with cleanup enabled (`--backend both --browser chromium`).

---

## 2. Pre-flight checks

Bail with a clear error message if any of these fail:

- `docker --version` succeeds.
- `dotnet --version` reports 8.x or higher.
- `node --version` reports v20+.
- The current working directory is the repo root (must contain `NDjango.Admin.sln` and `playwright/`).
- For `--backend ef` or `--backend both`: `sample-project/` must exist.
- For `--backend mongo` or `--backend both`: `sample-project-mongodb/` must exist.

---

## 3. Databases

Run only the compose services needed for the selected backend(s). Both commands are idempotent — a no-op if the service is already healthy.

For `ef`:

```bash
docker compose up --detach --wait --wait-timeout 180 db
```

For `mongo`:

```bash
docker compose up --detach --wait --wait-timeout 180 --remove-orphans mongoClusterSetup
```

Do **not** drop databases — the user has not authorized destructive operations and both suites are designed to coexist with accumulated data via the `gotoLatest()` / `?sort=Id&dir=desc` helper.

> ⚠️ **If this step CREATES the SQL container** (output shows `Created/Started`, not `Running`), any pre-existing EF sample-project on :8000 is now talking to a brand-new empty database — its schema is gone and login will 500. Step 4.1 has a deep health check that catches this; do not silently reuse the stale app. Same goes for the Mongo cluster: a recreated `mongo` container drops all collections, which the seeder will re-populate but only on a fresh app start.

---

## 4. Sample apps — start in background (or reuse)

For `--backend ef` run section 4-EF only. For `--backend mongo` run section 4-MONGO only. For `--backend both` run **both** (parallel is fine — the apps are independent).

### 4-EF — sample-project on :8000

1. **Detect existing server.** Two-stage check:

   **Stage A — port + HTTP:**
   ```bash
   curl --silent --fail --max-time 2 http://localhost:8000/admin/login/ > /dev/null
   ```
   - Exit 0 → an HTTP listener is up. Continue to Stage B.
   - Non-zero + port unbound (`lsof -ti:8000` empty) → port is free; skip to step 4-EF.2.
   - Non-zero + port bound by something else → abort: "port 8000 is occupied by a non-NDjango process — free it first (`lsof -ti:8000 | xargs kill`)".

   **Stage B — deep health check:**
   ```bash
   curl --silent --output /dev/null --write-out '%{http_code}' \
     --data 'username=admin&password=admin' http://localhost:8000/admin/login/
   ```
   - `302` → app is healthy. Treat as **reused**: skip steps 4-EF.2–4-EF.4 and skip its cleanup.
   - `500` → DB is broken (typically: docker recreated SQL). Kill and restart:
     ```bash
     pkill -f "SampleProject api" 2>/dev/null
     sleep 1
     ```
   - `200` → wrong credentials. Tell the user; do not auto-kill.
   - Anything else → treat as 500.

2. **Generate the secret** (required by `DataProtectionConfigurator`):

   ```bash
   export NDJANGO_SECRET_KEY="$(openssl rand -base64 48)"
   ```

3. **Start the app** in background with absolute paths:

   ```bash
   cd /absolute/path/to/repo/sample-project/src
   nohup env NDJANGO_SECRET_KEY="$NDJANGO_SECRET_KEY" dotnet run -- api \
     > /tmp/ndjango-e2e-sample.log 2>&1 &
   echo $! > /tmp/ndjango-e2e-sample.pid
   ```

4. **Wait for readiness** (up to 90s):

   ```bash
   for i in $(seq 1 90); do
     curl --silent --fail http://localhost:8000/admin/login/ > /dev/null && break
     sleep 1
   done
   ```

   On failure, dump the last 80 lines of `/tmp/ndjango-e2e-sample.log` and abort.

### 4-MONGO — sample-project-mongodb on :8001

Mirror of 4-EF with these substitutions:

| Item | EF value | Mongo value |
|---|---|---|
| Port | 8000 | 8001 |
| Working dir | `sample-project/src` | `sample-project-mongodb/src` |
| Process name (for `pkill`) | `SampleProject api` | `SampleProjectMongo api` |
| Log file | `/tmp/ndjango-e2e-sample.log` | `/tmp/ndjango-e2e-sample-mongo.log` |
| PID file | `/tmp/ndjango-e2e-sample.pid` | `/tmp/ndjango-e2e-sample-mongo.pid` |
| Schema bootstrap | EF `EnsureCreated()` + auth bootstrapper | `DataSeeder` (idempotent) + auth bootstrapper |
| Stage B failure mode | SQL container recreated → 500 | Mongo container recreated → seeder runs on next startup; existing app may 500 if its driver lost connection |

The Mongo sample reads `ConnectionStrings:MongoDB` from `appsettings.json`; the dev default points at `mongodb://localhost:27017/?directConnection=true`. Same `NDJANGO_SECRET_KEY` works for both apps — generate it once and export, then start both.

If `--backend both`, you may start the two apps in parallel (background both before waiting). They listen on different ports and use different databases, so there's no contention.

---

## 5. Playwright dependencies

From `playwright/`:

```bash
cd playwright
[ -d node_modules ] || npm install
npx playwright install --with-deps <browser>   # only for the requested browser
```

If `--clean-auth`, remove the storage state for each selected backend before continuing:
- `ef`: `rm -f playwright/.auth/admin.json`
- `mongo`: `rm -f playwright/.auth/admin-mongo.json`

The respective `setup` / `setup-mongo` project will re-authenticate on the next run.

---

## 6. Build the Playwright command

Map `--backend` + `--browser` to `--project` flags:

| `--backend` | `--browser` | `--project` flag(s) |
|---|---|---|
| `both` (default) | `chromium` (default) | `--project=chromium --project=chromium-mongo` |
| `both` | `firefox` | `--project=firefox --project=firefox-mongo` |
| `ef` | `chromium` | `--project=chromium` |
| `ef` | `firefox` | `--project=firefox` |
| `mongo` | `chromium` | `--project=chromium-mongo` |
| `mongo` | `firefox` | `--project=firefox-mongo` |

The corresponding `setup` / `setup-mongo` projects are pulled in automatically via `dependencies` declarations in `playwright.config.ts`; do not pass them manually.

Append flags from arguments:
- `-g "<pattern>"` if `--grep`.
- The spec path if `--file` (relative to `playwright/`).
- `--headed` / `--ui` / `--debug` if present.

Prefix env vars when those flags are present (EF only — they have no effect on Mongo projects):
- `PLAYWRIGHT_SAML_ENABLED=true` for `--saml`.
- `PLAYWRIGHT_LARGE_DATASET=true` for `--large-dataset`.

Run the command directly (foreground) so the user sees the live test output. Stream the result. Do **not** background this step — `--ui` and `--debug` need terminal interaction.

---

## 7. Cleanup

Skip cleanup entirely if any of these are true: `--keep-server`, `--ui`, `--debug`, or you reused an existing server (the Stage B 302 path).

> If step 4 detected a broken pre-existing server and you killed/restarted it, the running process is yours — clean it up here. The "don't stop what you didn't start" rule does not apply when the existing process was unhealthy.

For each backend you started, run its own teardown:

**EF cleanup:**

```bash
[ -f /tmp/ndjango-e2e-sample.pid ] && kill "$(cat /tmp/ndjango-e2e-sample.pid)" 2>/dev/null
pkill -f "SampleProject api" 2>/dev/null
rm -f /tmp/ndjango-e2e-sample.pid
sleep 1
lsof -i :8000 -P -n 2>/dev/null && echo "warning: port 8000 still bound" || true
```

**Mongo cleanup:**

```bash
[ -f /tmp/ndjango-e2e-sample-mongo.pid ] && kill "$(cat /tmp/ndjango-e2e-sample-mongo.pid)" 2>/dev/null
pkill -f "SampleProjectMongo api" 2>/dev/null
rm -f /tmp/ndjango-e2e-sample-mongo.pid
sleep 1
lsof -i :8001 -P -n 2>/dev/null && echo "warning: port 8001 still bound" || true
```

Leave the SQL Server and MongoDB containers running — they are shared dev resources.

---

## 8. Reporting

After Playwright exits:

- **Pass:** report `<passed> passed, <skipped> skipped (<duration>)` per project. Mention that `playwright/playwright-report/` is the latest HTML report (`(cd playwright && npm run test:report)` to open).
- **Fail:** list each failing test by title (prefix with the project name when `--backend both`), then point at the HTML report and at `/tmp/ndjango-e2e-sample.log` (EF) and/or `/tmp/ndjango-e2e-sample-mongo.log` (Mongo).
- If the run was aborted (timeout, missing dependency, etc.), explain the root cause and the fix.

Do **not** re-run failed tests automatically — the user decides whether to retry.

---

## Operational notes (lessons learned during the original migration)

These are **not** instructions for you — they are facts about how this suite behaves. Use them when diagnosing failures:

- **`workers: 1` and `fullyParallel: false`** in `playwright.config.ts` are intentional. Each backend shares one DB; parallel workers collide on FK constraints (notably the unique index on `(GroupId, PermissionId)` and `(UserId, GroupId)` in EF; equivalent in Mongo via the auth bootstrapper).
- **Default credentials are `admin / admin`** for both samples (`CreateDefaultAdminUser = true`, `DefaultAdminPassword = "admin"`).
- **Schema/collections persist between runs.** Neither suite truncates. The `ListPage.gotoLatest()` helper sorts `?sort=Id&dir=desc` so freshly-created rows appear on page 1; use it in any new test that needs to find a record it just created.
- **`gotoLatest()` is a no-op for composite-PK / junction entities on EF Core** (e.g. `MenuItemIngredient`) — they have no `Id` column. Two valid patterns: pass an explicit data attribute via `gotoLatest({ sort: 'MenuItemId', dir: 'desc' })`, **or** verify state through the composite-key change URL (200 = exists, 400/404 = gone). MongoDB junctions use a single `ObjectId` PK so `gotoLatest()` works normally.
- **EF junctions use comma-separated composite-key URLs** (`/admin/MenuItemIngredient/{menuItemId},{ingredientId}/change/`). Mongo junctions use a single ObjectId.
- **`AuthPermission` / `MongoAuthPermission` does not implement `IAdminSettings`**, so `?q=` is ignored. Helpers iterate paginated pages with an exact `^codename$` match.
- **Bulk action and save redirects include `?_msg=...&_msg_level=success`**. Match URLs with `(\?|$)`, never `$` alone.
- **EF `AuthUser`** has DB defaults for `IsActive` and `IsSuperuser` (rendered readonly). **Mongo `MongoAuthUser` does NOT** — every non-PK field is editable, so `IsActive` must be explicitly checked or the user is created inactive and login is rejected. The `auth-setup-mongo.ts` helper does this automatically.
- **Mongo FK-like fields render as plain text inputs** (no `vForeignKeyRawIdAdminField` class, no lookup popup). Use `MongoFormPage.expectObjectIdInput(name)` rather than `expectFkInput`.
- **Mongo has no FK constraints** — deleting a parent (Restaurant, MenuItem, Ingredient) does NOT cascade to junction documents. EF Core does cascade.
- **The `webServer` config in `playwright.config.ts` is opt-in** via `PLAYWRIGHT_START_SERVER`, and only points at the EF sample. This `/e2e` command always manages the lifecycle manually because it needs to inject `NDJANGO_SECRET_KEY` for both apps and start them on different ports.
- **Auth setup persists per-backend storage state** at `playwright/.auth/admin.json` (EF) and `playwright/.auth/admin-mongo.json` (Mongo). Use `--clean-auth` to force-purge them when debugging auth flakes.
- **`echo $!` after `nohup … &` captures the dotnet-run wrapper PID, not the listener PID.** `dotnet run` execs into a child binary that holds the port. Always pair `kill $(cat …pid)` with `pkill -f "<AssemblyName> api"`. Assembly names: `SampleProject` (EF), `SampleProjectMongo` (Mongo).
- **Symptom: `auth.setup.ts` fails with "Expected pattern: /\/admin\/$/, Received: …/admin/login/" while a `curl POST /admin/login/` returns 500.** Root cause is almost always: a sample-project from a previous session is still running while `docker compose up` recreated its database container, leaving the app pointed at an empty DB. Step 4 Stage B catches this; if you see the symptom, kill the stale app and let the start step launch a fresh one.
- **Password hashing does NOT depend on `NDJANGO_SECRET_KEY`** (PBKDF2). Cookie auth does. Changing the secret invalidates browser cookies but not stored password hashes — `admin/admin` keeps working as long as the auth-user row survives.
