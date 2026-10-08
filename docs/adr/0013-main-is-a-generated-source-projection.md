# 0013 — `main` is a generated source projection; users build from source

**Status**: accepted (2026-10-02, implemented through 2026-10-04, API Phases 57–62)

**Decision**: `dev` is the only human branch. Releases are manual `vX.Y.Z` tags on `dev` commits. A workflow projects the tree by **allowlist** (`git archive`), strips, runs a leak guard, smoke-tests the projected tree as a gate, then pushes one commit per release to `main` over a deploy key (never force-pushed), with provenance trailers; a second smoke runs from a fresh checkout of the published commit. `compose.yaml` uses `build: .` — no registry. The repo is public under MIT; the root README is user-facing and identical on both branches. Release tags are immutable (ruleset). Dry runs use `-rc.N` tags publishing to `release-test` (because `workflow_dispatch` only fires from the default branch, which never carries `.github/`).

**Why**: matches "the project fully builds" and removes GHCR, multi-arch and attestations; an allowlist fails safe (a forgotten file breaks the smoke test).

**Rejected**: GHCR image (can be added later without changing the user command); `export-ignore` denylist; release-please; Dependabot/scheduled workflows (they would force `.github/` onto `main`); a private-source + public-release repo pair.

**Reverses** the earlier "no Docker" councils (clone onboarding, compose pipeline, phase-9 CI): the requirement became public one-command distribution and the client now exists. **Kept**: SQLite on a named directory volume (never a single-file bind mount); non-root container.

See [`RELEASING.md`](../RELEASING.md).
