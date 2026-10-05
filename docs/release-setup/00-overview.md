# Release Setup — Overview

Planning docs for turning `main` into the public, clone-and-run release of PersonalFinance. **Planning only — nothing here is built yet.** Each slice is a vertical tracer bullet: implement it, run its "Done when" checks, and only then start the next one.

Background research (read only if you need the *why* behind a decision not explained here):
- `docs/public-main-release/03-implementation-plan.md` — the original 5-slice plan.
- `docs/public-main-release/council/council-transcript-2026-10-02.md` — council critique of that plan.
- `docs/public-main-release/main-branch-release-review.md` — final review/recommendation.

This folder **supersedes** those documents wherever they disagree. The biggest divergence: the research assumed `main` pulls a prebuilt GHCR image; the owner decided `main` **builds from source**. That single decision removes GHCR, multi-arch, attestations, the GHCR-public bootstrap and the anonymous-pull gate from scope.

## The user story (the one hard invariant)

> A person with only Git and Docker runs `git clone <repo> && cd <repo> && docker compose up`. The image builds from source, the app starts on an empty database, and it is reachable at `http://localhost:8080`. Nothing in the checkout is development material.

## Deployment contract

| Rule | Meaning |
|---|---|
| `dev` is the engineering branch | All human work lands on `dev` (feature branches off it, PRs into it). Tests, docs, `.claude/`, CI all live here. |
| `main` is generated | Written **only** by the release workflow. Humans never commit to it. It is already the GitHub default branch, so `git clone` lands on it. |
| A release = a `vX.Y.Z` tag on a `dev` commit | The tag is the immutable source of truth. `main` is a projection of that tagged tree. |
| One squashed commit per release on `main` | Linear history on top of the existing init commit (`9b0d48a`), never force-pushed. The commit message records the tag and source SHA (provenance). |
| `main` builds from source | `compose.yaml` has `build: .`, no `image:`, no registry. First `up` takes a few minutes. |
| Empty DB on first run | DB lives in the named volume `pf-data` at `/data/personalfinance.db`. Image contains schema (migrations), never data. Migrations run on startup. |
| Loopback only | Port bound to `127.0.0.1:8080` — finance data is never exposed on the LAN. |
| HTTP by default, HTTPS opt-in | `http://localhost` is already a browser "secure context"; HTTPS with an auto-generated self-signed cert is available behind one env var (Slice 3). |
| Same repo goes public | `personal-finance-app` becomes public at the end (Slice 6), MIT license. Note: **`dev` and its full history become public too** — accepted by the owner. |

## Decisions log (from the 2026-10-02 grilling session)

| # | Decision | Rationale / rejected alternative |
|---|---|---|
| Q1 | `main` builds from source | Matches "the project fully builds". Rejected: pull a GHCR image (heavy pipeline, GHCR visibility bootstrap) — can be added later without changing the user command. |
| Q2 | "Opens" = reachable URL + a clear `Personal Finance is ready at …` log line | A container cannot open a host browser. Rejected: wrapper start script (breaks "just `docker compose up`"). |
| Q2b | HTTPS groundwork, opt-in | A trusted cert cannot be installed into the host trust store from a container; self-signed means a browser warning. Default stays HTTP. |
| Q3 | GitHub Actions produces `main` | Owner preference over a local script. |
| Q4 | One squash commit per release on top of `main` | No force-push, clean history, provenance in the message. |
| Q5 | Manual tags (`git tag v1.0.0`) | Rejected for now: release-please (tooling cost before value). |
| Q6 | `127.0.0.1:8080` | Loopback only. `main` is already the default branch. |
| Q8 | HTTP default, HTTPS opt-in | See Q2b. |
| Q9 | Repo public at first release, MIT | Strangers can't clone a private repo; public also unlocks free rulesets. |
| Q10 | **Cut:** Dependabot, scheduled scans (maintenance slice), automated N→N+1 upgrade test | Dependabot/scheduled workflows only run from the *default* branch, which would force `.github/` onto `main`. Upgrade test has no N yet — README documents upgrade; tested manually. |
| Q11 | Root `README.md` is user-facing | `export-ignore`/projection can drop files but not rewrite them; the same README must serve both branches. Dev guidance stays in `.claude/` + `docs/` (stripped). |
| Q12 | One public repo (not a private-source + public-release pair) | Fewer moving parts; owner accepts `dev` being visible. |
| Q13 | Projection by **allowlist** + strip + leak guard | Fails safe: a forgotten file breaks the smoke test loudly instead of leaking silently. Rejected: `.gitattributes export-ignore` denylist. |
| Q14 | First version `v1.0.0` | — |
| — | Delete `PersonalFinance.sln` on `main` | It references 7 test projects that won't exist there. Dockerfile builds the host `.csproj` directly. |
| — | Dry runs use `vX.Y.Z-rc.N` tags → publish to scratch branch `release-test` | `workflow_dispatch` only fires if the workflow exists on the default branch (`main`), which never has `.github/`. |
| — | No `compose.override.yaml` | Only existed in the research to switch between "pull image" and "build". With build-from-source, one `compose.yaml` serves `dev` and `main`. |

## Codebase facts that shape the plan (verified 2026-10-02)

- **No startup migrations today** (`app/api/src/Bootstrap/PersonalFinance.Api/Program.cs`). Four DbContexts (Ledger, Financing, Subscriptions, Parties), one SQLite file, per-module history tables `__EFMigrationsHistory_<Module>`. Reporting's `vw_*` views are created by module migrations. The test host (`tests/PersonalFinance.Api.Tests/ApiWebApplicationFactory.cs`) already migrates in order **Ledger → Financing → Subscriptions → Parties** by scanning `IServiceCollection` for `DbContext` types — copy that approach (the context types are `internal` to module assemblies).
- **Hosted services:** `OutboxWorker` (Infrastructure) and `AccrueInstallments` (`FinancingModule.cs:85`, a `SchedulerBase : BackgroundService`). Both start inside `app.Run()`, so migrating between `builder.Build()` and `app.Run()` is enough ordering.
- **No data seeding** (`HasData` absent; one migration `20260928165524_AddCreditorInstallmentPayments` has an `INSERT … SELECT` backfill that is a no-op on an empty DB). A fresh DB is empty → "empty DB on `main`" is satisfied by construction. No `*.db` is tracked (`.gitignore` covers `*.db*`).
- **Time zone does not matter in the container:** commit `251dc56` made the client send its local `today=`; the host clock is UTC. No `TimeZoneInfo` lookups, no culture formatting.
- **No static file hosting yet:** no `UseStaticFiles`/`MapFallbackToFile`. `UseHttpsRedirection()` is unconditional (harmless without an HTTPS port, but logs a warning).
- **CORS** (`Cors:AllowedOrigins` = `http://localhost:4200`) is only needed for `ng serve`; same-origin hosting doesn't use it.
- **OpenAPI** `/openapi/v1.json` is always served; Scalar UI is Development-only.
- **Client:** project `client`, output `app/client/dist/client/browser` (Angular 20 default, no explicit `outputPath`). Prod `environment.ts` (`app/client/src/app/environments/`) already uses relative `/v1`. Routes: `''` → `reports`, `**` → `reports`; lazy features `reports`, `instruments`, `financing`, `ledger`, `subscriptions`, `parties`, `creditors`. `tsconfig.app.json` excludes `*.spec.ts`. `packageManager: pnpm@11.8.0`; Node 22 (CI). `pnpm-lock.yaml` **is** tracked. `pnpm-workspace.yaml` and `public/favicon.ico` are build inputs.
- **API build:** `global.json` SDK `10.0.111` (`rollForward: latestFeature`), central package management (`Directory.Packages.props`), `packages.lock.json` tracked per project. No `src` project references `tests/` (enforced by Architecture tests); `InternalsVisibleTo("…Tests")` strings are harmless without the test projects.
- **GitHub:** repo `InakiPoch/personal-finance-app`, **private**, personal account, free tier → **no rulesets/branch protection until public**. Default branch already `main`. No tags, no releases. `main` = init commit `9b0d48a` only (scaffold `api.csproj`, `.sln`, client scaffold — all replaced by the first release). Single workflow `.github/workflows/ci.yml` (jobs `build-test`, `client-build-test`; triggers: push any branch, PR to `main`/`dev`, dispatch).
- **Local machine:** CachyOS (Arch). **Docker is not installed** (Slice 0).

## Slices

| # | Doc | Delivers | Depends on |
|---|---|---|---|
| 0 | [slice-0-prerequisites.md](slice-0-prerequisites.md) | Docker installed, history secrets scan, release branch set up | — |
| 1 | [slice-1-runnable-container.md](slice-1-runnable-container.md) | `docker compose up` runs the whole app from the repo root | 0 |
| 2 | [slice-2-smoke-and-ci.md](slice-2-smoke-and-ci.md) | `scripts/smoke.sh` + `docker-smoke` CI job | 1 |
| 3 | [slice-3-opt-in-https.md](slice-3-opt-in-https.md) | Self-signed HTTPS behind one env var | 1, 2 |
| 4 | [slice-4-projection-and-user-docs.md](slice-4-projection-and-user-docs.md) | `scripts/project-main.sh` + leak guard, user README, LICENSE | 2, 3 |
| 5 | [slice-5-release-workflow.md](slice-5-release-workflow.md) | `release.yml`: tag on `dev` → verified projection → `main` → GitHub Release | 4 |
| 6 | [slice-6-go-public.md](slice-6-go-public.md) | Repo public, rulesets, stranger-clone verified | 5 |

## Working conventions

- Work branch: `chore/deploy-groundwork` (already checked out, off `dev`). Merge to `dev` via PR. One slice can be one PR, or group 0–2; keep each PR reviewable.
- Conventional commits, no AI attribution lines (owner's global rule).
- After each slice: one ledger line in the relevant `TASK.md` (not narrative), evergreen facts only into `CLAUDE.md` (e.g. "host auto-migrates when `Database:MigrateOnStartup=true`" — this **contradicts** the current api `CLAUDE.md` line "the host does not auto-migrate", so update it in Slice 1).
- Run commands with the repo's tooling notes: API tests via `dotnet test --solution PersonalFinance.sln` from `app/api/`; client tests need `CHROME_BIN=/usr/bin/brave`.
- Tool rule on this machine: use `rg`/`fd`/`bat`/`eza`/`sd` instead of `grep`/`find`/`cat`/`ls`/`sed` in interactive work. **Scripts that run in CI or in containers (`smoke.sh`, `project-main.sh`) must use only POSIX/coreutils + `git`, `curl`, `docker`** — those modern tools are not on GitHub runners.

## Acceptance checklist for the whole initiative

- [x] Fresh machine with only Git + Docker: `git clone https://github.com/InakiPoch/personal-finance-app && cd personal-finance-app && docker compose up` → app at `http://localhost:8080`, empty data.
- [x] `/`, `/reports` (deep route), `/health`, `GET /v1/instruments` (empty) all respond correctly.
- [x] `docker compose down` + `up` keeps data; `down -v` + `up` resets it.
- [ ] README backup → reset → restore round-trips data.
- [x] `git ls-files` on `main` has no `tests/`, `*.spec.ts`, `docs/`, `.claude/`, `CLAUDE.md`, `TASK.md`, `.github/`, `*.http`, `*.sln`, `*.db*`.
- [x] Each `main` commit names its source tag + SHA.
- [x] Port bound to `127.0.0.1` only.
- [ ] HTTPS opt-in works with the documented steps.
- [ ] A human push to `main` is rejected (verified); the release workflow can still push (deploy-key bypass proven on `release-test`; confirm on the next release, e.g. `v1.0.1`).
