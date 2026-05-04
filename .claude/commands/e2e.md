---
description: Run NDjango.Admin Playwright E2E tests against the EF Core sample-project (handles DB, app startup, browser install, and cleanup).
argument-hint: [--browser chromium|firefox|webkit] [--grep "pattern"] [--file path] [--headed] [--ui] [--debug] [--keep-server] [--saml] [--large-dataset] [--reuse-server] [--clean-auth]
allowed-tools: Bash, Read, Write, Edit
---

# /e2e — Run Playwright end-to-end tests

You are running the Playwright E2E suite for **NDjango.Admin** against the EF Core `sample-project/`. The user invoked you with arguments: `$ARGUMENTS`.

Be terse. Show progress in one-line updates between steps. Do **not** narrate every command — just run them and report results.

---

## 1. Parse arguments

Recognized flags from `$ARGUMENTS`:

| Flag | Effect |
|---|---|
| `--browser <name>` | `chromium` (default), `firefox`, or `webkit`. Maps to `--project=<name>`. |
| `--grep "<pattern>"` | Forwarded to Playwright as `-g "<pattern>"` (regex match on test title). |
| `--file <path>` | Run a single spec file (relative to `playwright/`, e.g. `tests-sql-server/crud/category.spec.ts`). |
| `--headed` | Pass `--headed` to Playwright. Disables auto-cleanup of the dev server. |
| `--ui` | Pass `--ui` to Playwright (Watch mode UI). Disables auto-cleanup. |
| `--debug` | Pass `--debug` to Playwright. Disables auto-cleanup. |
| `--keep-server` | Leave the sample-project running after tests finish. |
| `--reuse-server` | If the sample-project is already running on :8000, attach to it instead of starting a new one. Default is to detect this automatically. |
| `--saml` | Set `PLAYWRIGHT_SAML_ENABLED=true` (Phase 9 SAML tests). Requires `sample-project-sso` and AWS IAM Identity Center — not the default sample. |
| `--large-dataset` | Set `PLAYWRIGHT_LARGE_DATASET=true` (Phase 8 pagination COUNT timeout). Requires the 5M-category seed (`sample-project/scripts/seed-millions-of-categories.sql`). |
| `--clean-auth` | Delete `playwright/.auth/admin.json` to force re-authentication. |

If no arguments are given, run all tests on `chromium` headless with cleanup enabled.

---

## 2. Pre-flight checks

Bail with a clear error message if any of these fail:

- `docker --version` succeeds.
- `dotnet --version` reports 8.x or higher.
- `node --version` reports v20+.
- The current working directory is the repo root (must contain `NDjango.Admin.sln`, `playwright/`, and `sample-project/`).

---

## 3. Database — SQL Server

Start the SQL Server container if it is not already healthy:

```bash
docker compose up --detach --wait --wait-timeout 180 db
```

Re-running this when the container is already up is a no-op. Do **not** drop the database — the user has not authorized destructive DB operations and the test suite is designed to coexist with accumulated data via the `gotoLatest()` helper (sort by `Id desc`).

> ⚠️ **If this step CREATES the container** (output mentions `Container ndjangoadmin-db-1 Created/Started` rather than `Running`), then any pre-existing sample-project on :8000 is now talking to a brand-new empty database — its schema is gone and login will 500. Step 4.1 has a deep health check that catches this; do not silently reuse the stale app.

---

## 4. Sample-project — start in background (or reuse)

The `sample-project` listens on `http://localhost:8000`.

1. **Detect existing server.** Two-stage check:

   **Stage A — port + HTTP:**
   ```bash
   curl --silent --fail --max-time 2 http://localhost:8000/admin/login/ > /dev/null
   ```
   - Exit 0 → an HTTP listener is up. Continue to Stage B.
   - Non-zero + port unbound (`lsof -ti:8000` empty) → port is free; skip to step 4.2.
   - Non-zero + port bound by something else → abort: "port 8000 is occupied by a non-NDjango process — free it first (`lsof -ti:8000 | xargs kill`)".

   **Stage B — deep health check (post-docker):** the GET above only confirms a static HTML page is being served. It does NOT prove the DB is reachable, especially when step 3 just recreated the SQL container. POST a login attempt:
   ```bash
   curl --silent --output /dev/null --write-out '%{http_code}' \
     --data 'username=admin&password=admin' http://localhost:8000/admin/login/
   ```
   - `302` → app is healthy and admin/admin works. Treat as **reused**: skip steps 4.2–4.4 and skip cleanup (you didn't start it).
   - `500` (most common failure) → app is alive but its DB is broken (typically: docker recreated SQL Server out from under it). The app cannot be salvaged in-place — kill it and restart fresh:
     ```bash
     pkill -f "SampleProject api" 2>/dev/null
     sleep 1
     ```
     Then continue to step 4.2. Briefly inform the user why you killed it (e.g. "pre-existing sample-project on :8000 was returning 500 on login because the SQL container had been recreated; restarting it cleanly").
   - `200` (login page returned again) → wrong credentials. The DB has a different `admin` row than the defaults. Tell the user to either set `ADMIN_USERNAME`/`ADMIN_PASSWORD` overrides or kill the app manually; do not auto-kill on a 200.
   - Anything else (timeout, connection reset) → kill via `pkill -f "SampleProject api"` and restart, same as the 500 path.

2. **Generate the secret** — required by `DataProtectionConfigurator`. Without it the app aborts at startup with `NDJANGO_SECRET_KEY environment variable is required.`:

   ```bash
   export NDJANGO_SECRET_KEY="$(openssl rand -base64 48)"
   ```

3. **Start the app** in background with the secret in scope. Use absolute paths — relative `cd sample-project/src` then `cd -` is brittle across consecutive Bash invocations because shell state can drift:

   ```bash
   export NDJANGO_SECRET_KEY="$(openssl rand -base64 48)"
   cd /absolute/path/to/repo/sample-project/src
   nohup env NDJANGO_SECRET_KEY="$NDJANGO_SECRET_KEY" dotnet run -- api \
     > /tmp/ndjango-e2e-sample.log 2>&1 &
   echo $! > /tmp/ndjango-e2e-sample.pid
   ```

   The pid recorded by `echo $!` is the **immediate** background job (typically the `dotnet run` wrapper), not the actual listener. `dotnet run` forks into a child `bin/Debug/net8.0/SampleProject` process that owns port 8000. Cleanup must therefore use `pkill -f "SampleProject api"` (see step 7), not just `kill $(cat ...pid)`.

4. **Wait for readiness** (up to 90s, polling every 1s):

   ```bash
   for i in $(seq 1 90); do
     curl --silent --fail http://localhost:8000/admin/login/ > /dev/null && break
     sleep 1
   done
   ```

   On a warm build cache the listener can come up in 2–3s; that is **expected**, not a sign that the wrong process answered. Confirm by `lsof -i :8000` — the listener should be `SampleProject`.

   If it never comes up, dump the last 80 lines of `/tmp/ndjango-e2e-sample.log` and abort. Common causes: secret key not exported, SQL Server still booting, port collision.

The first startup also runs `EnsureCreated()` on the schema and creates the default `admin / admin` user (`CreateDefaultAdminUser = true` in `sample-project/src/Commands/ApiCommand.cs`).

---

## 5. Playwright dependencies

From `playwright/`:

```bash
cd playwright
[ -d node_modules ] || npm install
npx playwright install --with-deps <browser>   # only for the requested browser
```

The browser binary lives at `~/Library/Caches/ms-playwright/` on macOS and `~/.cache/ms-playwright/` on Linux. Skip the install if it's already there. If the user passed `--clean-auth`, remove `playwright/.auth/admin.json` before continuing — the `setup` project will re-authenticate on the next run.

---

## 6. Build the Playwright command

Always include `--project=<browser>`. The `setup` project (login admin → save `storageState`) is pulled in automatically via `dependencies: ['setup']` in `playwright.config.ts`; do not pass `--project=setup` manually.

Append flags from arguments:
- `-g "<pattern>"` if `--grep`.
- The spec path if `--file` (relative to `playwright/`).
- `--headed` / `--ui` / `--debug` if present.

Prefix env vars when those flags are present:
- `PLAYWRIGHT_SAML_ENABLED=true` for `--saml`.
- `PLAYWRIGHT_LARGE_DATASET=true` for `--large-dataset`.

Run the command directly (foreground) so the user sees the live test output. Stream the result. Do **not** background this step — `--ui` and `--debug` need terminal interaction.

---

## 7. Cleanup

Skip cleanup entirely if any of these are true: `--keep-server`, `--ui`, `--debug`, or you reused an existing server (the Stage B 302 path in step 4.1).

> Note: if step 4.1 detected a broken pre-existing server and you killed/restarted it, the running process is yours — clean it up here. The "don't stop what you didn't start" rule does not apply when the existing process was unhealthy.

Otherwise:

```bash
[ -f /tmp/ndjango-e2e-sample.pid ] && kill "$(cat /tmp/ndjango-e2e-sample.pid)" 2>/dev/null
pkill -f "SampleProject api" 2>/dev/null   # kills the actual listener even when the pidfile only captured the dotnet-run wrapper
rm -f /tmp/ndjango-e2e-sample.pid
sleep 1
lsof -i :8000 -P -n 2>/dev/null && echo "warning: port 8000 still bound" || true
```

Leave the SQL Server container running — it's a shared dev resource and other workflows may depend on it.

---

## 8. Reporting

After Playwright exits:

- **Pass:** report `<passed> passed, <skipped> skipped (<duration>)`. Mention that `playwright/playwright-report/` is the latest HTML report (`(cd playwright && npm run test:report)` to open).
- **Fail:** list each failing test by title, then point at the HTML report and at `/tmp/ndjango-e2e-sample.log` for app-side errors.
- If the run was aborted (timeout, missing dependency, etc.), explain the root cause and the fix.

Do **not** re-run failed tests automatically — the user decides whether to retry.

---

## Operational notes (lessons learned during the original migration)

These are **not** instructions for you — they are facts about how this suite behaves. Use them when diagnosing failures:

- **`workers: 1` and `fullyParallel: false`** in `playwright.config.ts` are intentional. The dashboard shares one DB; parallel workers collide on FK constraints (notably the unique index on `(GroupId, PermissionId)` and `(UserId, GroupId)`).
- **Default credentials are `admin / admin`** because the sample sets `CreateDefaultAdminUser = true` and `DefaultAdminPassword = "admin"`. The setup project relies on these defaults; if they change, update `helpers/admin-urls.ts` (`ADMIN_USERNAME` / `ADMIN_PASSWORD`).
- **Schema is recreated via `EnsureCreated()`, but rows persist** between local runs. The suite does **not** truncate. The `ListPage.gotoLatest()` helper sorts `?sort=Id&dir=desc` so freshly-created rows appear on page 1; use that pattern in any new test that needs to find a record it just created.
- **`gotoLatest()` is a no-op for composite-PK / junction entities** (e.g. `MenuItemIngredient`) — they have no `Id` column, and `RazorViewDispatcher` silently drops invalid sort fields and falls back to default order. As accumulated data grows, freshly-created junction rows drift below page 1 and any `rowByText` / `rowCheckbox` lookup will fail. Two valid patterns: pass an explicit data attribute via `gotoLatest({ sort: 'MenuItemId', dir: 'desc' })` (the freshly-created parent has the highest id, so its junctions surface on page 1), **or** skip the list view and verify state through the composite-key change URL (200 = exists, 400/404 = gone).
- **Junction entities use comma-separated composite-key URLs** like `/admin/MenuItemIngredient/{menuItemId},{ingredientId}/change/`. Tests verify junction existence/absence via direct GET on those URLs (404 = gone) rather than scanning list pages — this is the failsafe when sort-based pagination tricks won't work.
- **`AuthPermission` does not implement `IAdminSettings`**, so `?q=` is ignored. The helpers iterate paginated pages with an exact `^codename$` match; do not regress to substring search.
- **Bulk action and save redirects include `?_msg=...&_msg_level=success`**. Match URLs with `(\?|$)`, never `$` alone.
- **`AuthUser` create form renders `LastLogin`** as a `datetime-local` input. Only `IsSuperuser` and `DateJoined` are hidden (DB defaults); `IsActive` is a readonly value defaulting to `True`.
- **Popup-select link text is the entity Id**, not the human-readable name. Filter the `<tr>` (which contains all columns) when matching by name in popup mode.
- **The `webServer` config in `playwright.config.ts` is opt-in** via `PLAYWRIGHT_START_SERVER`. This `/e2e` command always manages the lifecycle manually because it needs to inject `NDJANGO_SECRET_KEY`.
- **Auth setup persists `playwright/.auth/admin.json`**. Every other project loads it via `storageState`. The setup project still re-runs each invocation as a project dependency; do not depend on the file being long-lived. Use `--clean-auth` to force-purge it before debugging auth flakes.
- **The first run after `npm install` will download a browser** (~92 MiB for chromium). Subsequent runs are cache hits. If a CI step shows "Executable doesn't exist at …chrome-headless-shell", that's the missing-browser symptom — re-run `npx playwright install --with-deps`.
- **Symptom: `auth.setup.ts` fails with "Expected pattern: /\/admin\/$/, Received: …/admin/login/" while a `curl POST /admin/login/` returns 500.** Root cause is almost always: a sample-project process from a previous session was still running when `docker compose up db` recreated the SQL Server container, leaving the app pointed at an empty DB. Step 4.1 Stage B (POST login → expect 302) catches this; if you see this symptom, kill the stale app (`pkill -f "SampleProject api"`) and let step 4.2–4.4 start a fresh one. The `auth_user` table will be re-seeded by `EnsureCreated()` + `AuthBootstrapper` on first request.
- **Password hashing does NOT depend on `NDJANGO_SECRET_KEY`** (`PasswordHasher` uses a stand-alone PBKDF2 implementation). Cookie auth (`AdminCookieAuthService`) does. So changing the secret between runs invalidates existing browser cookies but not stored password hashes — `admin/admin` keeps working as long as the `auth_user` row survives.
- **`echo $!` after `nohup … &` captures the dotnet-run wrapper PID, not the listener PID.** `dotnet run` execs into a child `bin/Debug/net8.0/SampleProject` that holds port 8000. Cleanup with only `kill $(cat /tmp/ndjango-e2e-sample.pid)` may orphan the listener. Always pair it with `pkill -f "SampleProject api"`.
