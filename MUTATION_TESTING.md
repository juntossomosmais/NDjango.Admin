# Mutation Testing Guide

This document is a guide for running mutation testing with [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) on this project. Follow it when the user asks to mutation-test a class.

## Prerequisites

- Stryker.NET installed as a dotnet tool (`dotnet stryker`)
- SQL Server running — required by integration tests (see below)
- The helper script `scripts/parse-stryker-report.csx` for reading HTML reports

### SQL Server Setup

Before starting a new container, check if SQL Server is already running:

```bash
docker ps --filter "publish=1433" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
```

If a container is already listening on port 1433, **skip `docker compose up`** and reuse it. Each test run creates an isolated database with a unique GUID-based name (e.g., `NDjangoAdminAdminTest_<guid>`), so multiple worktrees and parallel test runs can safely share a single SQL Server instance without collisions.

Only start a new container if nothing is running:

```bash
docker compose up -d db
```

**Worktree note:** When working in git worktrees (`.claude/worktrees/`), the main repository typically already has SQL Server running. Starting a second container from a worktree will fail with a port conflict. Always check first.

## Workflow

The process is an iterative loop. Each cycle narrows the gap between the current mutation score and the target (100%). Stop only when all remaining survivors are provably equivalent mutants or NoCoverage on unreachable paths.

```
Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5
  Run       Read      Write     Run all    Re-run
  Stryker   report    tests     tests      Stryker
                                             │
                         ┌───────────────────┘
                         ▼
                  Score above target
                  and only equivalent
                  mutants remain? ──No──► Back to Phase 2
                         │
                        Yes
                         │
                        Done
```

### Phase 1 — Run Stryker

Run Stryker from the test project directory, scoped to the target class.

```bash
cd test/<TestProject> && \
dotnet stryker \
  -p "<SourceProject>.csproj" \
  -m "**/<Path>/<TargetClass>.cs" \
  -r "ClearText" -r "Html" \
  --break-on-initial-test-failure
```

**Key flags:**
- `-p` — the source project within the test project's references
- `-m` — restricts mutations to only the target file (glob pattern)
- `-r "ClearText" -r "Html"` — console output + browsable report
- `--break-on-initial-test-failure` — fail fast if existing tests don't pass before mutation begins

**How to find the right test project:** look for test files that reference the target class. The test project must have a `<ProjectReference>` to the source project containing the target class.

### Phase 2 — Analyze Survived Mutants

Parse the HTML report using the helper script:

```bash
dotnet dotnet-script ./scripts/parse-stryker-report.csx -- \
  test/<TestProject>/StrykerOutput/<timestamp>/reports/mutation-report.html \
  <TargetClassName>
```

This prints every survived and uncovered mutant with its line, mutator type, and replacement.

**Triage each survivor into one of three categories:**

1. **Killable — logic mutations** (flipped conditions, removed statements, negated returns). These reveal real blind spots in the tests. Prioritize these.
2. **Killable — boundary/string mutations** (changed operators like `>=` to `>`, emptied strings). These catch subtle bugs. Address after logic mutations.
3. **Equivalent mutants** — mutations that don't change observable behavior. Flag these and move on. Common examples:
   - `&&` → `||` where DI guarantees one operand is always true
   - String mutations in `ContainsKey("x")` where no key `"x"` or `""` exists in the dictionary for that code path
   - Removed statements in error paths unreachable through normal configuration

### Phase 3 — Write Tests to Kill Survivors

For each killable survivor, write a test that fails when the mutation is applied and passes with the original code. Then run focused tests:

```bash
dotnet test NDjango.Admin.sln --filter "NewTestClass" | dotnet dotnet-script ./scripts/filter-failed-tests.csx
```

**Test design tips for mutation killing:**
- **Removed statement**: Assert on the side effect of that statement (response body content, header value, downstream call)
- **Flipped condition (`&&` → `||`)**: Write two tests — one where only the left operand is true, one where only the right is true — and assert different behavior
- **Emptied string**: Assert the exact string content in the response or output
- **Negated return**: Assert the boolean result or the branching behavior it controls

### Phase 4 — Full Test Suite Validation

Run the entire suite to ensure new tests don't break anything:

```bash
dotnet test NDjango.Admin.sln | dotnet dotnet-script ./scripts/filter-failed-tests.csx
```

Only proceed to Phase 5 if all tests pass.

### Phase 5 — Re-run Stryker

Run the same command from Phase 1. Compare the new score against baseline.

```bash
dotnet dotnet-script ./scripts/parse-stryker-report.csx -- \
  test/<TestProject>/StrykerOutput/<new-timestamp>/reports/mutation-report.html \
  <TargetClassName>
```

If killable survivors remain, loop back to Phase 2. If only equivalent mutants and NoCoverage on unreachable paths remain, the cycle is done.

## Guidelines

- **Target score: 100%.** Every survivor must be investigated. Only stop when all remaining survivors are provably equivalent mutants or NoCoverage on unreachable paths — document why each one is unkillable.
- **Focused tests before full suite.** Fast feedback loop on new tests, then full regression — avoids wasting time if the new tests are broken.
- **Not every survivor needs killing.** Equivalent mutants are unkillable by definition. Chasing them wastes time. Flag them and stop.
- **One test file per mutation cycle.** Group all tests written during a cycle into a single file named `<TargetClass>MutationTests.cs` inside the relevant `Tests/` folder.
- **NoCoverage mutants** mean no test exercises that line at all. If the code path is reachable, write a test. If it's unreachable through normal setup (e.g., defensive code behind DI-validated configuration), skip it.
