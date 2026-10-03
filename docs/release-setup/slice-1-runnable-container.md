# Slice 1 — Runnable container

Read [00-overview.md](00-overview.md) first. Requires [Slice 0](slice-0-prerequisites.md) (Docker installed).

## Goal

From the repo root, `docker compose up` builds one image from source (Angular + .NET), creates/migrates the SQLite DB in a named volume, and serves the SPA and the API from one origin at `http://localhost:8080`.

This is the tracer bullet: after this slice the user story already works on `dev`. Everything after it is automation, hardening and packaging.

## Design

```
browser ──► 127.0.0.1:8080 ──► container :8080  (ASP.NET, Kestrel)
                                 ├── /v1/*        module endpoints (unchanged)
                                 ├── /health      health check (unchanged)
                                 ├── /openapi/... (unchanged)
                                 ├── static files from /app/wwwroot (Angular dist)
                                 └── fallback → index.html (SPA deep routes)
                               /data (volume pf-data) → personalfinance.db (+ -wal/-shm)
```

One container, one process — matches the modular-monolith design (one SQLite writer, Outbox + scheduler in-process). No nginx, no second service.

## Steps

### 1. Startup migrations (API)

In `app/api/src/Bootstrap/PersonalFinance.Api/Program.cs`, between `builder.Build()` and `app.Run()`, when config `Database:MigrateOnStartup` is `true`:

- Migrate every module `DbContext` **sequentially in order Ledger → Financing → Subscriptions → Parties**.
- The context types are `internal` to module assemblies, so the host cannot name them. Copy the approach in `tests/PersonalFinance.Api.Tests/ApiWebApplicationFactory.cs`: before `Build()`, scan `builder.Services` for service types assignable to `DbContext`, order by the name map, then after `Build()` resolve each from a scope and `await context.Database.MigrateAsync()`.
- Put that logic in a host helper (e.g. `Helpers/DatabaseMigrationHelper.cs`, static, name ends in `Helper` per `.claude/rules/csharp-style.md`). Optional but nice: make `ApiWebApplicationFactory` call the same helper so the order is single-sourced (`InternalsVisibleTo("PersonalFinance.Api.Tests")` already exists).
- **Default `false`** in `appsettings.json` (so tests and `dotnet run` behave as today); the Docker image sets `Database__MigrateOnStartup=true`.
- Migrating before `app.Run()` guarantees `OutboxWorker` and `AccrueInstallments` (hosted services) start only after the schema exists — no extra gate needed.
- Log each module migrated, and on failure log a clear message and let the process exit non-zero (compose `restart: unless-stopped` will retry; logs show why).

**Abandoned migration lock:** EF Core 9+ takes a migrations lock (SQLite: a `__EFMigrationsLock` table row). If the container is killed mid-migration the row can survive and the next start blocks/fails. Verify the actual behaviour in step 7; document the recovery (delete the lock row or the table via a one-off container) in the README troubleshooting section in Slice 4. Do not build auto-recovery code unless the test shows it is needed.

### 2. Static hosting + SPA fallback (API)

In `Program.cs`:
- `app.UseStaticFiles()` (or `MapStaticAssets()` — pick whichever works with a plain `wwwroot` copied at image build time; `UseStaticFiles` is the safe default since the files are not part of the .NET build).
- After all endpoint mapping: `app.MapFallbackToFile("index.html")`.
- Ensure unknown `/v1/...` paths still return 404 JSON, not `index.html` (otherwise the client gets HTML for a typo'd API call). Simplest: a constrained fallback, e.g. `app.MapFallback("/v1/{**rest}", () => Results.NotFound())` before the SPA fallback, or check `MapFallbackToFile`'s behaviour first — keep the smallest thing that passes the check in step 7.
- In local `dotnet run` there is no `wwwroot`; static/fallback must not break dev (it just 404s for `/`). Verify `dotnet run` + `ng serve` still work.

### 3. Ready log line

On `app.Lifetime.ApplicationStarted`, log once: `Personal Finance is ready at {Url}`. Take the URL from config `App:PublicUrl` (set in `compose.yaml` to `http://localhost:8080`); if unset, skip the line. This is the "opens" requirement (Q2) — the container cannot open a browser, so the log tells the user where to go.

### 4. `UseHttpsRedirection`

Leave as-is in this slice (in the container it only logs "Failed to determine the https port for redirect"). Slice 3 makes it conditional on HTTPS being enabled. Note it in the slice findings if the warning is noisy.

### 5. Root `Dockerfile` (build context = repo root)

Three stages. Sketch — verify image tags and paths while implementing:

```dockerfile
# syntax=docker/dockerfile:1

FROM node:22-alpine AS web
WORKDIR /src/app/client
RUN corepack enable
COPY app/client/package.json app/client/pnpm-lock.yaml app/client/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile
COPY app/client/ ./
RUN pnpm ng build --configuration production

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src/app/api
COPY app/api/global.json app/api/Directory.Build.props app/api/Directory.Packages.props ./
COPY app/api/src/ ./src/
RUN dotnet restore src/Bootstrap/PersonalFinance.Api/PersonalFinance.Api.csproj --locked-mode
RUN dotnet publish src/Bootstrap/PersonalFinance.Api/PersonalFinance.Api.csproj -c Release --no-restore -o /out
RUN mkdir -p /data

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
WORKDIR /app
COPY --from=api /out ./
COPY --from=web /src/app/client/dist/client/browser ./wwwroot
COPY --from=api --chown=1654:1654 /data /data
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__PersonalFinanceDb="Data Source=/data/personalfinance.db" \
    Database__MigrateOnStartup=true
LABEL org.opencontainers.image.source="https://github.com/InakiPoch/personal-finance-app"
EXPOSE 8080
ENTRYPOINT ["dotnet", "PersonalFinance.Api.dll"]
```

Things to verify, not assume:
- **`corepack` + pnpm 11.8.0** on `node:22-alpine` (corepack reads `packageManager` from `package.json`). If corepack is missing/unsigned-key issues appear, `npm i -g pnpm@11.8.0` is the fallback.
- **SDK image vs `global.json` (10.0.111, `latestFeature`)**: the `sdk:10.0` image must be ≥ 10.0.111 or restore fails. If it is older, pin `sdk:10.0.1xx` explicitly.
- **`aspnet:10.0-noble-chiseled-extra` tag exists** and the non-root user is UID `1654` (`app`). `-extra` includes ICU + tzdata; strictly not needed (no culture/time-zone use) but avoids globalization surprises for ~30 MB. If you choose plain `-chiseled`, set `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` and re-run the smoke checks.
- **Chiseled images have no shell, no curl** → `ENTRYPOINT` must be exec form, and no Docker `HEALTHCHECK` with curl. Not needed: the smoke script polls `/health` from outside.
- **Dist path** `dist/client/browser` — confirm after the first build.
- **Layer caching** (copying only `*.csproj` + `packages.lock.json` before restore) is a later optimisation; copy `src/` wholesale first. `# ponytail:`-style note in the Dockerfile if you skip it.
- **Native SQLite** (`e_sqlite3`) ships in the `Microsoft.Data.Sqlite` package for linux-x64/arm64 — confirm the app opens the DB in the chiseled image.

### 6. `.dockerignore` and `compose.yaml` (repo root)

`.dockerignore` (keeps the build context small and keeps tests/docs out of the image even on `dev`):
```
.git
**/bin
**/obj
**/node_modules
**/dist
**/.angular
**/tests
**/*.spec.ts
**/*.db*
docs
**/docs
**/.claude
.github
```

`compose.yaml`:
```yaml
name: personal-finance
services:
  app:
    build: .
    ports:
      - "127.0.0.1:8080:8080"
    environment:
      App__PublicUrl: "http://localhost:8080"
    volumes:
      - pf-data:/data
    restart: unless-stopped
volumes:
  pf-data: {}
```

No `image:` key (Q1: build from source, never pull). The volume's full name becomes `personal-finance_pf-data` — README backup commands in Slice 4 depend on that name, so keep `name: personal-finance`.

**Upgrade gotcha:** `docker compose up` does not rebuild an existing image. After `git pull`, users need `docker compose up -d --build`. The README (Slice 4) must say so.

### 7. Verify (manual — Slice 2 automates it)

Run from a **clean clone**, not the working tree, so untracked local files can't make it pass by accident:

```fish
git clone --branch chore/deploy-groundwork . /tmp/pf-clean   # after committing the slice
cd /tmp/pf-clean
docker compose up -d --build
docker compose logs -f app    # wait for "Personal Finance is ready at http://localhost:8080"
```

Checks:
- `curl -sf localhost:8080/health` → healthy.
- `curl -s localhost:8080/` → HTML containing `<app-root`.
- `curl -s localhost:8080/reports` → same HTML (SPA fallback).
- `curl -s localhost:8080/v1/instruments` → empty list (check the actual response shape).
- `curl -s -o /dev/null -w '%{http_code}' localhost:8080/v1/does-not-exist` → 404, not 200.
- Open `http://localhost:8080` in a browser; create one instrument; navigate between features; reload on a deep route.
- `docker compose down && docker compose up -d` → instrument still there.
- `docker compose down -v && docker compose up -d` → empty again.
- `ss -ltn | rg 8080` → bound to `127.0.0.1`, not `0.0.0.0`.
- Kill-mid-migration: `docker compose down -v`, `up -d`, `docker kill` within ~1 s of start, `up -d` again → record whether it recovers or blocks on the migration lock; write the recovery steps in Findings.
- Regression: `dotnet test --solution PersonalFinance.sln` (from `app/api/`) and the client lint/build/test still pass; `dotnet run` + `ng serve` dev loop unchanged.

## Done when

- [ ] All checks above pass from a clean clone.
- [x] API + client test suites green.
- [x] api `CLAUDE.md` "the host does not auto-migrate" line updated; TASK.md ledger line added.

## Findings

2026-10-03:
- Images (resolved at build): `node:22-alpine` (Node 22.23.3), `sdk:10.0` (SDK 10.0.401, satisfies `global.json` 10.0.111 — no pinning), `aspnet:10.0-noble-chiseled-extra` (tag exists; runs as UID 1654; `/data` owned by it, WAL files created fine). Final image ≈ 304 MB. The Dockerfile is the doc sketch unchanged plus a `# ponytail:` note on skipped layer caching.
- pnpm 11.8.0 via corepack works on alpine; `strictDepBuilds` + `allowBuilds` caused no problem. Needs network at the `pnpm install` step. Dist path `dist/client/browser` confirmed.
- `GET /v1/instruments` on an empty DB → `200 {"rows":[]}`. `POST /v1/instruments {"type":"debit","name":"Checking"}` → `201 {"id","type"}`; list rows are `{id,type,name,cutoffDate,nextClosingDate}`.
- **Blocker found and fixed:** `SqliteConnectionStringHelper.Resolve` evaluated `SolutionRootLocatorHelper.FindSolutionRoot()` eagerly, even with `ConnectionStrings__PersonalFinanceDb` set, and threw in the image (no `.sln`). The fallback is now lazy.
- **Migration lock:** killing the container mid-migration (reproduced 5/5, during Financing) leaves a stale `__EFMigrationsLock` row; the next start then hung forever polling `INSERT OR IGNORE INTO "__EFMigrationsLock"` — container "Up", `/health` dead, `restart: unless-stopped` never fires. DB itself stayed intact (integrity + FK checks clean). Fixed with a startup guard (`DROP TABLE IF EXISTS "__EFMigrationsLock"` before each context; safe: one process, one file) plus regression test `DatabaseMigrationHelperTests`. Manual recovery for older images: `docker compose stop`, then `docker run --rm -v personal-finance_pf-data:/data alpine sh -c 'apk add -q sqlite && sqlite3 /data/personalfinance.db "delete from __EFMigrationsLock"'`, then `docker compose up -d` (Slice 4 README troubleshooting).
- **Not reproduced:** a kill inside the non-transactional `PRAGMA foreign_keys = 0` of `AllowCardlessCreditorFinancedPlan` (EF warning 20410, a table rebuild) — a half-applied migration there is untested and the guard does not cover it. README must say: back up the volume before upgrading.
- Noise: `Failed to determine the https port for redirect` is logged once (Slice 3 removes it); `The WebRootPath was not found` on static paths in `dotnet run` without `wwwroot` (harmless, dev only); EF logs every SQL command at info level, burying the ready line — consider `Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Warning` in the image/compose (not done).
- Cold build ≈ 60 s; the clean-clone compose build measured 6.8 s only because layers were cached.
- Regression: API 570 green, client lint clean, client 519/519, `dotnet run` + `ng serve` dev loops unchanged.

## Out of scope

Smoke script/CI (Slice 2), HTTPS (Slice 3), stripping files / README (Slice 4).
