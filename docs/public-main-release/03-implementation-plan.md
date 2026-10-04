# Implementation Plan — Public `main`

Derived from [02-report.md](02-report.md). Planning only; nothing here is built. All work lands on `dev` (feature branches off it); `main` is only ever written by the release job. Sketches are illustrative, not drop-in — verify tag names, `outputPath` and action SHAs when implementing.

## What lands where

```
dev  (everything)                         main  (projection of a tagged dev commit)
├─ app/api/src/…                          ├─ app/api/src/…
├─ app/api/tests/…            ✗           ├─ app/client/src/… (no *.spec.ts)
├─ app/client/src/**/*.spec.ts ✗          ├─ Dockerfile
├─ docs/, app/*/docs/          ✗          ├─ compose.yaml   (image pinned to vX.Y.Z)
├─ .claude/, CLAUDE.md, TASK.md ✗         ├─ .env.example (optional)
├─ .github/workflows/*         ✗          ├─ README.md  (user quick start only)
├─ compose.override.yaml       ✗          └─ LICENSE
├─ .gitattributes (export-ignore rules) ✗
├─ Dockerfile, compose.yaml, README.md, LICENSE
└─ PersonalFinance.sln  (filtered/omitted on main)
```

`.github/workflows` stays on `dev` only: workflows run from the ref that triggers them, and release tags point at `dev` commits.

## Slices (each independently shippable, in order)

### Slice 1 — App runs from a single container (dev only, no CI yet)
1. **Static hosting in API:** `UseStaticFiles` + `MapFallbackToFile("index.html")`; relative production `apiUrl` (`/v1`) in `environment.ts`; keep CORS config for the `ng serve` dev path.
2. **Startup migration gate:** migrate Ledger → Financing → Subscriptions → Parties sequentially before hosted services/Outbox; `RunMigrationsOnStartup` flag (default on in the image, off in test hosts). Handle abandoned `__EFMigrationsLock` with a clear log message.
3. **Root `Dockerfile`** (context = repo root), three stages:
   - `node:22` → `pnpm install --frozen-lockfile` → `pnpm ng build --configuration production`
   - `sdk:10.0` → copy `global.json`, `Directory.*.props`, per-project `.csproj` + `packages.lock.json` → `dotnet restore <host.csproj> --locked-mode` → `publish -c Release --no-restore`; create `/data`
   - `aspnet:10.0-noble-chiseled-extra` → `COPY --from=api /out /app`, `COPY --from=web <dist>/browser /app/wwwroot`, `COPY --from=api --chown=1654:1654 /data /data`, `ENV ASPNETCORE_URLS=http://+:8080`, `ConnectionStrings__PersonalFinanceDb="Data Source=/data/personalfinance.db"`, OCI `source` label. Exec-form entrypoint only.
4. **`.dockerignore`:** `**/bin`, `**/obj`, `**/node_modules`, `**/tests`, `*.db*`, `docs`, `.git`.
5. **`compose.yaml`:**
   ```yaml
   name: personal-finance
   services:
     app:
       image: ghcr.io/inakipoch/personal-finance:0.0.0   # stamped by release job
       build: { context: . }
       ports: ["127.0.0.1:8080:8080"]    # loopback only
       restart: unless-stopped
       volumes: [pf-data:/data]
   volumes:
     pf-data: {}
   ```
   plus dev-only `compose.override.yaml` with `pull_policy: build`.
6. **Verify locally:** `docker compose up --build`, open `http://localhost:8080`, create data, `down` + `up` (persists), `down -v` + `up` (empty). Test the kill-mid-migration recovery once.

### Slice 2 — Public-repo hygiene (dev)
- `LICENSE` (owner decides), real `README.md` (quick start, backup, reset, upgrade, "PRs target `dev`"), `.gitattributes` with `export-ignore` rules (tests, specs, docs, `.claude`, `CLAUDE.md`, `TASK.md`, workflows, override file, `.gitattributes` itself), `.github/dependabot.yml` (`nuget`, `npm`, `docker`, `github-actions`).
- Rulesets: `dev` (PR + CI), `v*` tags (restricted), `main` (release identity only, no force-push/deletion).

### Slice 3 — CI additions (dev)
- Pin all actions to commit SHAs; top-level `permissions: contents: read`.
- Add job `docker-smoke`: build image (no push) → `docker compose up -d` → poll `/health` → assert empty data → `down -v`.
- Add `has-pending-model-changes` for the four contexts.

### Slice 4 — Release pipeline (dev → GHCR → main)
Single workflow `release.yml`, jobs in order:
1. `release-please` (Release PR from Conventional Commits; manifest/`simple` type). *Verify:* tags/releases created with `GITHUB_TOKEN` may not trigger other workflows — keep everything in this one workflow and gate later jobs on `release_created`.
2. `verify`: full build + test (reuse CI jobs).
3. `image`: QEMU + Buildx, `metadata-action` (semver + sha tags), `build-push-action` (`linux/amd64,linux/arm64`, `provenance`/`sbom`), then `actions/attest` with the digest.
4. `publish-main`: checkout tagged commit → `git archive HEAD | tar -x -C <clean>` → remove/replace `.sln` → stamp the version into `compose.yaml` → **leak guard** (fail on forbidden patterns) → commit on top of `origin/main` → push using the bypass identity.
5. `smoke-main`: fresh runner, checkout `main`, no registry login, `docker compose up -d`, poll `/health`, assert empty, `down -v`.
6. GitHub Release with changelog.

**Manual one-time step after the first image publish:** set the GHCR package to *public* and confirm it links to the repo.

### Slice 5 — Ongoing (scheduled)
- Weekly base-image rebuild check + image vulnerability scan; Dependabot PRs on `dev` only.

## Acceptance checklist
- [ ] On a machine with only Docker: `git clone <repo> && cd <repo> && docker compose up -d` → app at `http://localhost:8080`, DB empty.
- [ ] `git ls-files` on `main` contains no `tests/`, `*.spec.ts`, `docs/`, `.claude/`, `CLAUDE.md`, `TASK.md`, `*.db*`, workflows.
- [ ] Image pulls anonymously on `amd64` and `arm64`; `gh attestation verify` succeeds.
- [ ] Port bound to `127.0.0.1` only.
- [ ] `down` + `up` keeps data; `down -v` + `up` resets it; README documents both and backup.
- [ ] Upgrading from release N to N+1 migrates a populated volume without data loss.
- [ ] Force-push / direct push to `main` is rejected for humans.

## Risks to track
| Risk | Control |
|---|---|
| Dev-only file leaks to `main` | Leak guard in `publish-main` + `smoke-main` |
| GHCR package left private | Anonymous pull in `smoke-main` |
| Abandoned SQLite migration lock | Log message + documented recovery + flag |
| Bypass identity unavailable for Actions | GitHub App token fallback |
| Chiseled image tag/behaviour differs on .NET 10 | Confirm tags and user ID at Slice 1 |
| Unauthenticated finance data on LAN | Loopback-only port mapping |
