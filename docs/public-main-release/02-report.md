# Report — A Public `main` for a Localhost App

Evidence IDs like **[E12]** refer to rows in [01-evidence.md](01-evidence.md). Items marked **(synthesis)** are engineering judgement built from verified pieces, not an established community standard.

## 1. Bottom line

1. **Make `dev` the only place humans commit; make `main` a generated, read-only release branch** that CI rewrites on every tagged release. *(synthesis)*
2. **What users run is a prebuilt image, not source.** `main` holds a `compose.yaml` pointing at a version-pinned public image on GHCR; `docker compose up -d` pulls and starts it. This is the pattern mature self-hosted projects use [E16]. Building from source stays possible (`docker compose up --build` via an override) but is not the happy path.
3. **One container, one volume, one port.** The API serves the built Angular app as static files. No nginx, no CORS, no second image, no health-gated startup order.
4. **"Empty database" is a property of a fresh volume plus startup migration**, not of anything committed. SQLite files are already git-ignored and there is no seed data, so there is nothing to scrub.
5. **CI/CD = the existing CI on `dev`, plus a tag-driven release pipeline** that tests → builds multi-arch image → attests → projects a stripped tree onto `main` → smoke-tests the exact thing a user will run.

## 2. Findings

### F1 — `export-ignore` alone cannot make a clean `main`
`export-ignore` affects only `git archive` [E1]. A normal branch always contains every tracked file. So a stripped `main` needs an explicit projection step: `git archive <tag> | tar -x` into a worktree of `main`, commit, push. Using `export-ignore` as the *declarative list* is still good: the rules are versioned on `dev`, reviewed in PRs, and GitHub's source archives for tags honour them too [E2], so "Download ZIP" matches `main`. *(synthesis)*

**Fail-open risk:** `export-ignore` is a denylist. A new dev-only file leaks onto `main` unless someone ignores it. Mitigation: a **guard step** in the release job that fails if the projected tree contains forbidden patterns (`**/tests/**`, `*.spec.ts`, `docs/`, `.claude/`, `CLAUDE.md`, `TASK.md`, `*.db*`). Allowlist projection is stricter but costs a maintained manifest; recommended only if leaks actually happen.

### F2 — History on `main` should be release commits, not merges
Today `main` is a strict ancestor of `dev` (0 ahead / 374 behind). Merging `dev` into `main` would bring the tests along, so that workflow must end. The release job instead creates **one commit per release on top of the current `main` tip** whose tree is the projection. No force-push is ever needed (so rulesets can forbid it [E9]), and the first projection commit works from today's state. Consequence: PRs target `dev`, never `main`; this goes in the contributing notes on `dev`.

### F3 — The solution file breaks on the stripped tree
`PersonalFinance.sln` references seven test projects. On `main` they won't exist, so `dotnet build PersonalFinance.sln` would fail. The Docker build restores the **host `.csproj`** instead [E18], so the image is unaffected; either ship no `.sln` on `main` or generate a filtered one during projection.

### F4 — Compose file semantics decide the user experience
- Name it `compose.yaml` [E3].
- With both `image:` and `build:` present, Compose **pulls by default** [E4]. That is what end users want. Contributors who want a local build use `compose.override.yaml` (on `dev` only) with `pull_policy: build`, or `docker compose up --build`.
- Pin the image to the release version (stamped by the release job during projection). Do not ship `:latest`: Compose re-pulls `latest` every time [E4], which makes upgrades implicit and uncontrolled for a financial app.

### F5 — GHCR is private by default
First publish creates a **private** package [E8]. A private image makes `docker compose up` fail with an auth error for every stranger. Set it public once after the first publish (a manual step; document it in the release checklist), and add the `org.opencontainers.image.source` label so the package links to the repo [E8]. The release smoke test (F9) pulls anonymously, so a regression is caught.

### F6 — Serve the SPA from the API (single container)
The Angular app needs a base URL; today dev uses an absolute `https://localhost:7095/v1`. If the API serves the compiled client from `wwwroot` (`UseStaticFiles` + `MapFallbackToFile("index.html")`) and the production `apiUrl` is relative (`/v1`), then:

- one image, one port (`http://localhost:8080`), no CORS (same origin);
- no `depends_on`, so no health check — which matters because chiseled images have no shell or `curl` [E13];
- no nginx config to maintain.

Tradeoff: SPA and API ship and version together (already true in this monorepo). The two-container nginx design [E14] remains valid if the client must someday deploy independently. *(An earlier council noticed this idea but did not pursue it — see brief.)*

### F7 — Chiseled runtime image: set data-dir ownership at build time
A distroless/chiseled final stage has no shell, so `RUN chown` cannot run there [E13]. The earlier internal note suggesting it [E18] is corrected: create `/data` in the SDK stage and `COPY --from=build --chown=1654:1654 /data /data`. A new named volume mounted over a path that exists in the image inherits that ownership on first use. Use the `-extra` variant (ICU/tzdata), since this app is money/date/culture-sensitive [E13, E18].

### F8 — Empty DB and schema upgrades
- **Fresh install:** new named volume → no file → startup migration creates the schema → empty app. Reset = `docker compose down -v` (destructive; must be documented as such).
- **Startup migration is acceptable here** [E12]: single process, single user, no replicas, localhost. EF Core 9+ locking covers concurrency. Keep the internal findings [E18]: run all four contexts **sequentially before** hosted services/Outbox start, guarded by a `RunMigrationsOnStartup` flag so test hosts are unaffected.
- **SQLite caveat [E12]:** the migration lock is a table (`__EFMigrationsLock`) that can be left behind if the container is killed mid-migration, blocking later boots. Document the recovery (delete that table) and keep the flag so it can be bypassed.
- **Upgrades are forward-only.** Reverting to an older image against a newer schema is unsupported. The README must say: back up the volume before upgrading (one `docker run --rm -v … tar` line).
- **CI guard on `dev`:** `dotnet ef migrations has-pending-model-changes` per context [E12] — otherwise `Migrate()` throws at startup for every user.
- Keep the SQLite file on a named volume **directory** (`/data`), never a single-file bind mount [E18].

### F9 — CI/CD that fits "no server, only an image"
Community norm for this shape [E6, E7, E15, E16]: no deploy step; the artifact *is* the deployment. Recommended pipeline:

| Stage | Trigger | What it does |
|---|---|---|
| **CI** (exists) | push / PR to `dev` | restore `--locked-mode`, build, test API; lint, build, test client |
| **CI additions** | same | `has-pending-model-changes`; Docker build (no push) + `compose up` smoke test on the PR; Dependabot for `nuget`, `npm`, `docker`, `github-actions` [E11] |
| **Release PR** | push to `dev` | release-please maintains a Release PR from Conventional Commits (version, changelog) [E15] |
| **Release** | Release PR merged / tag `vX.Y.Z` | (1) re-run tests; (2) build **multi-arch** (`amd64`, `arm64`) image and push to GHCR with semver tags + SHA tag via `metadata-action` and `build-push-action` [E6]; (3) attest provenance [E7]; (4) project stripped tree, stamp version into `compose.yaml`, run the leak guard, commit to `main`; (5) create GitHub Release |
| **Post-release smoke** | after step 4 | on a clean runner: check out `main`, run the *documented* command with no login, wait for `/health`, assert a fresh DB (e.g., list endpoints return empty) — **test what users actually run** |
| **Scheduled** | weekly | rebuild-check base images; Trivy (or equivalent) scan of the published image |

Hardening that applies because the repo is public [E10]: pin third-party actions to full commit SHAs, default `permissions: contents: read`, elevate only in the release job (`packages: write`, `id-token: write`, `attestations: write`, `contents: write`), never `pull_request_target` with untrusted checkout, no self-hosted runners.

Arm64 is not optional for a "runs on anyone's machine" claim (Apple Silicon). QEMU on one runner is simplest; switch to native `ubuntu-24.04-arm` runners if build time hurts [E6].

### F10 — Protect `main` with rulesets, not trust
Rulesets can protect branches **and tags** and name bypass actors [E9]:
- `main`: only the release identity may update; block force-push and deletion; require the leak-guard check.
- `v*` tags: restrict creation/deletion to the release identity.
- `dev`: require PR + CI.

**Unverified:** whether the built-in Actions identity can be a bypass actor, or whether a dedicated GitHub App token is needed. Verify in repo settings before relying on it; a GitHub App is the safe fallback.

### F11 — Security posture of a local finance app
- The app has no authentication layer documented. Publish the port on loopback only: `"127.0.0.1:8080:8080"`. Plain `"8080:8080"` exposes unauthenticated financial data to the whole LAN.
- Container runs non-root (UID 1654) [E13].
- Add a `LICENSE` (none exists) — a public repo without one is "all rights reserved" and not legally clonable for reuse.
- The README on `main` must not link to files that don't exist there.

## 3. Devil's-advocate pass (checkpoint)

| Challenge | Assessment |
|---|---|
| **"Most professional projects keep tests on the default branch; trunk-based wins [E17]."** | True, and the commonly recommended alternative is: single `main`, distribute via GitHub Releases + GHCR images, and let the compose file live in a `deploy/` folder. This request explicitly wants a stripped `main`, so the design honours it; the cost is the projection machinery. If that cost bites, option B below is the escape hatch. |
| **"The source on `main` is untested source."** | It is a deterministic projection of a commit that passed full CI, and the image is built from the *full* tree. The guard + post-release smoke test cover the gap. |
| **"Two sources of truth will drift."** | There is one: `dev`. `main` is write-protected and regenerated; nobody edits it. |
| **"You reversed four 'no Docker' councils."** | Their premise was onboarding friction and a missing client. The scope is now explicit public distribution, and the single-container design removes most of what they objected to (nginx, CORS, multi-service ordering, health checks). |
| **"Runtime migrations on financial data with no rollback."** | Accepted for single-user localhost [E12]; mitigated by backup instructions, forward-only upgrade policy, and the pending-changes CI check. |
| **"A user may `down -v` and lose everything."** | Real. README must put reset and backup side by side and name the volume. |

## 4. Options compared

| Option | `main` contains | Pros | Cons |
|---|---|---|---|
| **A (recommended)** projection branch, image + compose + stripped source | `compose.yaml`, Dockerfile, README, LICENSE, app source minus tests/docs | Meets every stated constraint; transparent source; one command | Needs release job + leak guard + ruleset bypass |
| **B** distribution-only `main` | `compose.yaml`, `.env.example`, README, LICENSE (no source) | Simplest; zero leak risk | Not "open source" on `main`; can't build from `main` |
| **C** single `main`, dev files kept, releases as assets | Everything | Industry-standard, least machinery | Violates the "no tests/dev files on `main`" requirement |

## 5. Open items for the owner (defaults chosen, flip if wrong)

1. Single container (F6) over nginx + API. *Default: single.*
2. Denylist (`export-ignore`) + guard over allowlist. *Default: denylist.*
3. Version-pinned image tag stamped into `compose.yaml` over `:latest`. *Default: pinned.*
4. Registry: GHCR (free for public, linked to repo). *Default: GHCR.*
5. License choice (MIT/Apache-2.0/other) — needs your decision; not assumed.
