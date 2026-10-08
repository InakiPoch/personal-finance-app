# Releasing & distribution

`dev` is the only human branch. `main` is a **generated projection** of a tagged `dev` commit, written by CI via a deploy key. Users clone `main` and run `docker compose up`, which builds from source (no registry). Rationale: [ADR-0013](adr/0013-main-is-a-generated-source-projection.md).

## Cut a release

1. Merge to `dev`; CI green.
2. Dry run (publishes to the `release-test` branch instead of `main`):
   `git tag v1.0.X-rc.N origin/dev && git push origin v1.0.X-rc.N`. Tags are immutable once the ruleset is active, so use a new rc number each time.
3. Real release: `git tag vX.Y.Z origin/dev && git push origin vX.Y.Z`.

## What `.github/workflows/release.yml` does (trigger: `push: tags: v*`)

- `guard`: tag matches `^v\d+\.\d+\.\d+(-rc\.\d+)?$` and the commit is an ancestor of `origin/dev`. `-rc.` → `release-test`, else `main`.
- `verify`: reuses `ci.yml` (`workflow_call`).
- `publish`: `scripts/project-main.sh` (allowlist via `git archive`, strip, leak guard: forbidden patterns and files > 5 MB), then `scripts/smoke.sh` on the projected tree as a gate; then SSH with secret `RELEASE_DEPLOY_KEY`, `rsync --delete`, one commit `chore(release): <tag>` by `github-actions[bot]` with trailers `Source-Tag`, `Source-Commit`, `Workflow-Run`, plain `git push` (never force). No changes → no-op.
- `smoke-main`: fresh checkout of the published commit, hygiene check on `git ls-files`, smoke again.
- `github-release`: only for `main`; `gh release create --verify-tag --generate-notes`.

Failure before push leaves `main` untouched. Failure in `smoke-main` leaves the commit on `main`: fix on `dev` and release the next version. Never force-push; migrations are forward-only (no downgrades).

## CI (`.github/workflows/ci.yml`)

Runs on pushes to any branch (not tags), PRs to `main`/`dev`, `workflow_dispatch`, `workflow_call`. Jobs: `build-test` (locked restore, test the `.sln`), `client-build-test` (lint, prod build, headless Chrome tests), `docker-smoke` (shellcheck + `scripts/smoke.sh`).

## One-time GitHub setup (already done)

- Deploy key `personal-finance-release` (write) stored as secret `RELEASE_DEPLOY_KEY`.
- Rulesets: `main-release-only` (blocks update/deletion/non-fast-forward; deploy key bypasses), `dev-safety` (deletion, non-fast-forward), `release-tags` (`refs/tags/v*`: deletion, update). An owner push to `main` is rejected.
- Public repo, MIT license; `dev` history is public.

## Runtime machinery

- `Dockerfile`: node:22-alpine (client) → sdk:10.0 (builds the host csproj directly; the `.sln` is not shipped on `main`) → aspnet:10.0-noble-chiseled-extra, non-root (UID 1654). Ports 8080/8443. Sets `Database__MigrateOnStartup=true`.
- `compose.yaml`: project `personal-finance`, `127.0.0.1:8080:8080` only, named volume `pf-data:/data`, `restart: unless-stopped`. HTTPS lines are commented out. No `compose.override.yaml`.
- Startup: migrations run sequentially (Ledger, Financing, Subscriptions, Parties) before hosted services, then static files + SPA fallback; logs `Personal Finance is ready at <App:PublicUrl>`.
- Opt-in HTTPS: .NET generates a self-signed ECDSA P-256 cert (CN=localhost, SAN localhost + 127.0.0.1, 397 days, reused if > 30 days left) in `/data/https/` (PFX no password + PEM). No HSTS. Trust verified only on Linux Chrome/Brave (NSS).
- `scripts/smoke.sh` uses its own project `pf-smoke` so it never touches the real volume; checks health, SPA routes, `/v1` 404, instrument create, persistence across down/up, reset on `down -v`. Env: `BASE_URL`, `CURL_OPTS=-k`.
- Gotchas: `docker compose up` does not rebuild after `git pull` (use `--build`); any clone on one machine shares volume `personal-finance_pf-data`; use `COMPOSE_PROJECT_NAME=pf-scratch` for destructive experiments.

## Still open

- First real release through protected `main` (v1.0.1): the deploy-key bypass is proven only on `release-test`.
- Owner README read-through; clean-clone browser walkthrough; HTTPS trust on non-Linux/Firefox.
- Killing the process mid-migration in the non-transactional `PRAGMA foreign_keys = 0` path (EF warning 20410) is untested.
- Explicitly cut: Dependabot, scheduled scans, automated upgrade test, release-please, GHCR, multi-arch, attestations.
