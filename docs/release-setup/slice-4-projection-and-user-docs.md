# Slice 4 — Projection script, leak guard, user README, LICENSE

Read [00-overview.md](00-overview.md) first. Requires [Slice 2](slice-2-smoke-and-ci.md) (smoke script) and [Slice 3](slice-3-opt-in-https.md) (so the README documents the final HTTPS behaviour).

## Goal

A local, deterministic answer to "what exactly goes on `main`?": `scripts/project-main.sh <out-dir>` produces the release tree from the current commit, and that tree passes `smoke.sh` on its own. Plus the two user-facing files every release needs: `README.md` and `LICENSE`.

The release workflow (Slice 5) just calls this script — so everything about `main`'s contents is testable on a laptop, before any GitHub automation exists.

## Design — allowlist, strip, guard (decision Q13)

**1. Allowlist** — export only these committed paths from `HEAD` via `git archive HEAD -- <paths> | tar -x -C <out>` (committed content only; untracked junk can never leak):

```
Dockerfile
.dockerignore
compose.yaml
README.md
LICENSE
.gitignore
app/api/global.json
app/api/Directory.Build.props
app/api/Directory.Packages.props
app/api/src
app/client/package.json
app/client/pnpm-lock.yaml
app/client/pnpm-workspace.yaml
app/client/angular.json
app/client/tsconfig.json
app/client/tsconfig.app.json
app/client/.postcssrc.json
app/client/public
app/client/src
```

Not on the list, on purpose: `docs/`, `app/*/docs/`, `.claude/`, `app/*/.claude/`, `.github/`, `app/api/tests/`, `app/api/PersonalFinance.sln` (references the 7 test projects — deleted, not rewritten), `app/client/tsconfig.spec.json`, `app/client/eslint.config.js`, `app/client/.vscode/`, `app/client/.editorconfig`, `app/client/.gitignore` (root `.gitignore` covers it — verify), `app/client/README.md` (ng-new boilerplate), `scripts/`.

Re-check the list against `git ls-files app/client | rg -v '^app/client/src/'` and `git ls-files app/api | rg -v '^app/api/(src|tests)/'` when implementing — new build inputs may have appeared.

**2. Strip** inside the allowlisted dirs:
- `app/client/src/**/*.spec.ts` (40 files; `tsconfig.app.json` already excludes them, so the build is unaffected)
- `app/api/src/**/*.http` (`PersonalFinance.Api.http`, `api.http`)
- `appsettings.Development.json` — **keep** unless verified unused in Production (it is only loaded when `ASPNETCORE_ENVIRONMENT=Development`; removing it is cosmetic. Decide while implementing; default keep).

**3. Leak guard** — fail (non-zero, list offenders) if any file in `<out>` matches:
```
(^|/)tests?/            (^|/)docs/            (^|/)\.claude/        (^|/)\.github/
(^|/)(CLAUDE|TASK|AGENTS)\.md$                \.spec\.ts$           \.http$
\.Tests\.csproj$        \.sln$                \.db(-wal|-shm|-journal)?$   \.bak$
(^|/)\.env$             (^|/)node_modules/    (^|/)(bin|obj)/
```
and if any file is > 5 MB (catches accidental binaries/DBs). The guard is a second line of defence — the allowlist is the first.

Script rules: POSIX `sh`/`bash` + `git`, `tar`, `grep -E`, `find` only (it runs on GitHub runners). Refuses to run with a dirty working tree (it projects `HEAD`, and a dirty tree means you're testing something that isn't committed). Prints the file count and the top-level tree at the end.

Known harmless leftovers on `main`: `InternalsVisibleTo("…Tests")` strings in `.csproj` files; `angular.json`'s `test`/`lint` targets pointing at files that aren't there (`ng build` doesn't load them — verify); test-only devDependencies in `package.json` (must stay — the lockfile must match).

## README.md (root, user-facing — decision Q11)

Same file on `dev` and `main`. Sections:
1. What it is (2–3 lines, screenshot optional).
2. **Requirements:** Git + Docker (Compose v2). Nothing else.
3. **Quick start:** `git clone …`, `cd personal-finance-app`, `docker compose up -d`, wait for `Personal Finance is ready at http://localhost:8080` (`docker compose logs -f`), open the URL. First run builds from source — a few minutes.
4. **Your data:** lives in the Docker volume `personal-finance_pf-data`, never in the repo; the app starts empty; port is bound to `127.0.0.1` only (not reachable from your LAN) — and why that matters (no authentication).
5. **Stop / start:** `docker compose down` / `up -d` keeps data.
6. **Backup / restore** (stop first — the DB runs in WAL mode, copying it live can produce a torn backup):
   ```sh
   docker compose stop
   docker run --rm -v personal-finance_pf-data:/data -v "$PWD":/backup alpine \
     tar czf /backup/pf-backup-$(date +%F).tgz -C /data .
   docker compose start
   ```
   Restore into an empty volume:
   ```sh
   docker compose down -v
   docker compose up --no-start
   docker run --rm -v personal-finance_pf-data:/data -v "$PWD":/backup alpine \
     sh -c 'tar xzf /backup/pf-backup-YYYY-MM-DD.tgz -C /data && chown -R 1654:1654 /data'
   docker compose up -d
   ```
   (`1654` = the non-root `app` user of the .NET image.) Test this exact text; if `docker compose cp` turns out simpler and keeps ownership correct, use it instead.
7. **Reset:** `docker compose down -v` deletes all data. Warn clearly.
8. **Upgrade:** `git pull && docker compose up -d --build` (without `--build` the old image keeps running). Migrations run automatically; back up first.
9. **HTTPS (optional):** uncomment the two lines in `compose.yaml`, `docker compose up -d`, trust step per OS (from Slice 3 Findings).
10. **Troubleshooting:** port 8080 in use (change the left side of the port mapping and `App__PublicUrl`); stuck migration lock (from Slice 1 Findings); see logs with `docker compose logs app`.
11. **Contributing:** one line — "`main` is generated for each release; development happens on `dev`, PRs target `dev`."
12. License line.

## LICENSE

MIT (decision Q9), year 2026. Confirm with the owner the exact copyright holder string (legal name vs `InakiPoch`).

## Steps

1. Write `scripts/project-main.sh` (allowlist → strip → guard).
2. Write README + LICENSE; commit (the script projects `HEAD`).
3. Test as below.

## Done when

- [ ] `scripts/project-main.sh /tmp/pf-main` → guard passes; tree contains only expected files (`find /tmp/pf-main -type f | wc -l` recorded).
- [ ] `cd /tmp/pf-main && bash <repo>/scripts/smoke.sh` → `SMOKE OK` (proves the stripped tree builds: no `.sln`, no tests, no specs).
- [ ] Plant a file (temporarily add `app/api/src/foo.http` *and commit it on a scratch branch*) → projection fails with that path listed. Drop the scratch commit.
- [ ] README backup → `down -v` → restore commands, run verbatim, bring back the instrument created before backup.
- [ ] README read top-to-bottom by the owner as if new; every command copy-pasted works.
- [ ] TASK.md ledger line.

## Findings

2026-10-04:
- **Final file count: 781** (`find <out> -type f`; `fd` shows fewer because it honours `.gitignore`). Top level: `app`, `compose.yaml`, `Dockerfile`, `.dockerignore`, `.gitignore`, `LICENSE`, `README.md`. No allowlist additions needed; the re-check against `git ls-files` matched the Dockerfile COPY lines.
- **Smoke on the projected tree:** `bash scripts/smoke.sh <out>` → `SMOKE OK` (no `.sln`, tests or specs).
- **Leak guard:** a committed `app/api/src/docs/n.md` on a scratch branch fails the projection with the path listed. Note: planted `.http` / `*.spec.ts` files do **not** trip it, because the strip step removes them before the guard runs. The guard's `.http`/`.spec.ts` patterns only matter for such files outside the stripped dirs.
- **Backup method:** the README `tar` text, verbatim: backup → `down -v` → `up --no-start` → restore → `up -d` brought the created instrument back. `docker compose cp` not needed. The tarball is written by root inside the alpine container, so it is root-owned on the host.
- **`appsettings.Development.json`:** kept (only loaded under `ASPNETCORE_ENVIRONMENT=Development`; removal is cosmetic).
- **`.gitignore`:** root file did not cover `app/client/dist` or `.angular/cache` (the client `.gitignore` is not projected) → added both.
- **LICENSE holder:** `InakiPoch` (placeholder, owner to confirm).
- Not done here: the owner's top-to-bottom README read-through; the HTTPS README steps beyond the Slice 3 Linux NSS path.

## Out of scope

Committing to `main` (Slice 5). GitHub settings (Slice 6).
