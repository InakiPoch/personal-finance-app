# Council transcript — 2026-10-02

## Question
"Is the `docs/public-main-release/03-implementation-plan.md` proposed slices correct for the research, evidence and report?"

Framed for the advisors: are the five slices faithful, complete, correctly ordered and scoped relative to `00-research-brief.md`, `01-evidence.md`, `02-report.md`, and executable against the real repo.

Process notes: advisors and reviewers were project `codebase-researcher` agents (read-only), run in batches of 3 (global cap). Advisor texts below are the condensed versions given to reviewers (original hand-backs were longer). Anonymization map: **A = Executor, B = Contrarian, C = Expansionist, D = Outsider, E = First Principles**.

## Advisor responses (condensed)

**A — Executor.** Slices map to F1–F11 but underspecified. Slice 1 partially blocked: prod `environment.ts` already `/v1`; `Program.cs` lacks `UseStaticFiles`/`MapFallbackToFile`; Angular `outputPath` unverified. Slice 2 must precede Slice 4 (rulesets, `.gitattributes`). Slice 4: GHCR private after first publish vs anonymous `smoke-main`; bypass identity unverified; `.sln` "remove/replace" undecided (claims 8 test refs); version flow needs `outputs:`/`needs:`. Monday: add static middleware, run Slices 1–3, resolve bypass + GHCR before Slice 4.

**B — Contrarian.** ~75% faithful; scope/order sound. Gaps: (1) Slice 1 compose pins nonexistent `0.0.0` tag; (2) `.sln` ambiguity; (3) bypass identity not a pre-Slice-4 blocker; (4) "list endpoints empty" unnamed. Minor: Slice 1 verify assumes `compose.override.yaml` introduced only in Slice 2.

**C — Expansionist.** Correct, complete, conservative. Opportunities: Slice 0 local gate, 24h post-release dogfood, state Dependabot targets `dev`, buildx cache. Cites "E10" for bypass (should be E9).

**D — Outsider.** Directionally sound, five execution gaps: F3 not addressed in Slice 1, bypass identity unresolved, GHCR sequencing, `<dist>/browser` unverified, `<host.csproj>` placeholder.

**E — First Principles.** Correct, complete, ordered. Refinements: pick delete for `.sln`; note Slice 3 needs Slice 1's Dockerfile; GHCR manual step makes `smoke-main` fail silently → fail-fast check.

## Peer reviews (condensed)

**Reviewer 1 (general):** strongest D (concrete, correct 7 test projects); weakest A (said 8; `.sln` has 7). Missed: exact middleware location/order; `.NET 10` chiseled tag unverified; GHCR timing; `outputPath`.

**Reviewer 2 (ordering):** strongest C; weakest B (claims override file only in Slice 2) and A (overstates GHCR). Missed: Slice 2 ruleset "dev requires PR + CI" vs Slice 3 adding CI.

**Reviewer 3 (skeptic of consensus):** strongest A (GHCR first-release failure is guaranteed); weakest E (minimizes it). Missed: acceptance endpoints unnamed; committer identity in `publish-main`; `outputPath` not checked in `angular.json`; chiseled tag unverified.

## Orchestrator verification of disputed claims (repo, 2026-10-02)
- Prod `environment.ts`: `apiUrl: '/v1'` — already relative (A correct).
- `Program.cs`: no `UseStaticFiles`/`MapFallback` — but plan Slice 1 step 1 already schedules adding them, so this is a task, not a plan defect.
- `PersonalFinance.sln`: **7** test project references (A wrong).
- `compose.override.yaml`: Slice 1 step 5 **already includes it** ("plus dev-only `compose.override.yaml` with `pull_policy: build`"); B/D/Reviewer 2 claim it is only in Slice 2 is wrong.
- `angular.json`: no explicit `outputPath` (default location must be confirmed by building).
- Host project: `app/api/src/Bootstrap/PersonalFinance.Api/PersonalFinance.Api.csproj`.
- `ci.yml` exists with jobs `build-test`, `client-build-test` → Reviewer 2's "Slice 3 cannot merge under a PR+CI ruleset" is overstated; only a required check that doesn't exist yet would block.
- Existing `ci.yml` pins actions by tag (`@v4`, `@v5`), not SHA.

## Chairman verdict
See `council-report-2026-10-02.html` (same content).

### Where the council agrees
- Every slice traces to F1–F11 and the order (container → hygiene → CI → release → maintenance) is sound. No advisor found a missing finding.
- Pre-implementation unknowns need resolving before Slice 4: ruleset bypass identity, `.sln` rule, Angular `outputPath`, chiseled .NET 10 tag.
- `.sln` should be deleted on `main` (simplest) — A, B, D, E converge.

### Where the council clashes
- "Correct as written" (C, E) vs "blocked" (A, D). Both are right about different things: **faithful to the research — yes; executable without surprises — no.**
- Severity of the GHCR-private issue: E calls it a doc tweak; A/Reviewer 3 call it a guaranteed first-release failure. The chairman sides with A (see below).

### Blind spots caught
- **Ordering defect no advisor stated precisely:** the plan runs `publish-main` *before* `smoke-main`. If the image is private or broken, a bad release has already landed on `main`. The anonymous-pull check must gate `publish-main`.
- Version glue (release-please `outputs:` → image tag → compose stamping) and git committer identity in `publish-main` are unspecified.
- Acceptance "DB empty" names no endpoints.
- Dev ruleset should require only checks that already exist (`build-test`, `client-build-test`); add `docker-smoke` after Slice 3.
- Errors in the council itself: A's "8 test projects" (7), B/D/R2's override-file claim (already in Slice 1), C's "E10" (E9), A's static-files "blocker" (already scheduled in Slice 1 step 1).

### Recommendation
Keep the five-slice structure; do **not** execute yet. Apply a short revision pass to `03-implementation-plan.md`:
1. Add **Slice 0 (spike)**: prove the `main` ruleset bypass identity with a throwaway protected branch; decide Actions identity vs GitHub App.
2. Slice 1: name the host `.csproj` path; build Angular locally to confirm output path; confirm `aspnet:10.0-noble-chiseled-extra` exists; note prod `apiUrl` is already `/v1`; state that dev uses `compose.override.yaml` (`pull_policy: build`) because the pinned tag does not exist until Slice 4.
3. Slice 2: `dev` ruleset requires only existing checks; commit `.gitattributes` before any projection.
4. Slice 4: reorder to image → **anonymous pull check (gate)** → `publish-main` → `smoke-main`; first release is a two-step (publish image, flip GHCR public, then run the rest); add `outputs:`/`needs:` version flow and committer identity; `.sln` = delete.
5. Acceptance checklist: name the endpoints used for the empty-data assertion.

### The one thing to do first
Run the Slice 0 spike (ruleset bypass identity on a throwaway branch). It decides the design of `publish-main`, the riskiest job in the plan.
