# Slice 5 — Release workflow

Read [00-overview.md](00-overview.md) first. Requires [Slice 4](slice-4-projection-and-user-docs.md) (`project-main.sh`, `smoke.sh`, README, LICENSE all on `dev`).

## Goal

Pushing a `vX.Y.Z` tag on a `dev` commit runs `.github/workflows/release.yml`, which — only if every check passes — adds one commit to `main` with the projected tree, verifies a fresh checkout of `main` works, and creates a GitHub Release. Ends with `v1.0.0` on `main`.

## Why it's shaped this way

- **Workflows run from the triggering ref.** A tag on a `dev` commit runs the `release.yml` that exists *in that commit*. `main` never needs `.github/` (it's not on the allowlist).
- **Validate before publishing** (council's main ordering fix, review §7.2/§9): the projected tree is smoke-tested *before* `main` is touched; `main` is smoke-tested again *after*, from a fresh checkout, as a stranger would get it.
- **Deploy key for the push**, even though the repo is still private with no rulesets: Slice 6 adds a ruleset on `main` with the deploy key as bypass actor, so using it from day one means Slice 6 changes zero workflow code.
- **No GHCR / image push** (Q1): `main` builds from source.

## Design

Trigger — tag push only:
```yaml
on:
  push:
    tags: ['v*']
```
The tag itself picks the target:
- `vX.Y.Z` → target `main`, GitHub Release created.
- `vX.Y.Z-rc.N` → target `release-test` (dry run), no GitHub Release.

**Why not `workflow_dispatch` for dry runs:** GitHub only triggers `workflow_dispatch` when the workflow file exists on the **default branch**, and `main` (the default) never carries `.github/`. Same reason scheduled workflows were cut (Q10). Re-runs of a failed release use the "Re-run jobs" button on the tag's run, which works without dispatch.

`permissions: contents: read` at top level; `contents: write` only on the job that creates the GitHub Release. `concurrency: { group: release, cancel-in-progress: false }`. **All actions pinned to commit SHAs** (this workflow holds a write credential).

Jobs:

1. **`guard`** — checkout with `fetch-depth: 0`. Resolve `TAG` and its commit `SHA`. Fail unless: tag matches `^v[0-9]+\.[0-9]+\.[0-9]+(-rc\.[0-9]+)?$`; `git merge-base --is-ancestor $SHA origin/dev` (the tag sits on `dev` history). Output `tag`, `sha`, `target` (`release-test` if `-rc.`, else `main`).
2. **`verify`** — `uses: ./.github/workflows/ci.yml` (made reusable in Slice 2) → API tests, client lint/build/tests, `docker-smoke` on the tagged source. `needs: guard`.
3. **`publish`** — `needs: [guard, verify]`.
   - Checkout `SHA`.
   - `bash scripts/project-main.sh "$RUNNER_TEMP/out"` (includes the leak guard).
   - `cd "$RUNNER_TEMP/out" && bash $GITHUB_WORKSPACE/scripts/smoke.sh` — **gate**: the exact tree about to be published must build and pass.
   - Set up SSH with secret `RELEASE_DEPLOY_KEY`; `git clone --depth 1 --branch <target> git@github.com:InakiPoch/personal-finance-app.git "$RUNNER_TEMP/main"` (for `release-test` that doesn't exist yet: clone `main` and `git switch -c release-test`).
   - Replace the clone's contents with the projection: delete everything except `.git`, copy `out/` in (`rsync -a --delete --exclude .git` is on GitHub runners).
   - If `git status --porcelain` is empty → log "nothing to release" and stop successfully (idempotent re-runs).
   - Commit as `github-actions[bot] <41898282+github-actions[bot]@users.noreply.github.com>`, conventional message, no AI attribution:
     ```
     chore(release): v1.0.0

     Source-Tag: v1.0.0
     Source-Commit: <full SHA>
     Workflow-Run: <run URL>
     ```
   - `git push origin <target>` (plain push — never `--force`; a rejected push means `main` moved unexpectedly → fail and investigate).
   - Output the new `main` commit SHA.
4. **`smoke-main`** — `needs: publish`. Fresh `actions/checkout` of `<target>` at the published commit (default `GITHUB_TOKEN`, no deploy key). `scripts/` is not on `main`, so do a second `actions/checkout` of the tagged `SHA` into a sub-path (e.g. `path: src`) purely to get `scripts/smoke.sh`, then run it from the `main` checkout's root.
   - Also assert hygiene: `git ls-files` in the `main` checkout matches none of the leak-guard patterns (cheap double-check on what actually landed).
5. **`github-release`** — `needs: smoke-main`, `if: target == 'main'`, `permissions: contents: write`: `gh release create "$TAG" --verify-tag --generate-notes --title "$TAG"`. Notes come from `dev` commits between tags — conventional commit subjects make them readable.

Failure semantics:
- Fails in `guard`/`verify`/`publish` before push → `main` untouched. Fix on `dev`, delete and re-create the tag (tags aren't protected yet), re-run.
- Fails in `smoke-main` → `main` already has the commit. Fix on `dev`, release `vX.Y.Z+1`. Do **not** force-push `main` (that's exactly what Slice 6 will forbid). Acceptable because `publish` already smoke-tested the identical tree; a `smoke-main` failure would mean a checkout/environment difference — investigate, don't panic.

## One-time setup (owner, GitHub UI)

1. Generate a key pair locally: `ssh-keygen -t ed25519 -N '' -C personal-finance-release -f /tmp/pf-release-key`.
2. Repo → Settings → Deploy keys → add `/tmp/pf-release-key.pub`, **Allow write access**.
3. Repo → Settings → Secrets → Actions → `RELEASE_DEPLOY_KEY` = contents of `/tmp/pf-release-key`. Delete both local files.

## Steps

1. Write `release.yml` on `chore/deploy-groundwork`; merge slices 1–5 to `dev` via PR (CI green incl. `docker-smoke`).
2. **Dry run:** `git tag v1.0.0-rc.1 origin/dev && git push origin v1.0.0-rc.1` → run publishes to `release-test`.
   - Inspect `release-test`: file list, commit message, a local `git clone --branch release-test` + `docker compose up` by hand.
   - Negative test: tag a commit that is **not** on `dev` (e.g. a scratch-branch commit) as `v1.0.0-rc.2` → must fail in `guard`, no branch touched.
   - Clean up: delete `release-test` and the rc tags (`git push origin :release-test :v1.0.0-rc.1 :v1.0.0-rc.2`).
3. **Real release:** `git tag v1.0.0 origin/dev && git push origin v1.0.0` → watch the run → `main` has one new commit → GitHub Release `v1.0.0` exists.
4. Locally: `git clone --branch main https://github.com/InakiPoch/personal-finance-app /tmp/pf-v1 && cd /tmp/pf-v1 && docker compose up -d` → app works, empty.

## Done when

- [x] Dry run to `release-test` succeeded and was inspected; scratch branch + tag deleted.
- [x] The off-`dev` rc tag failed in `guard` without touching any branch.
- [x] `v1.0.0` released: `main` = init commit + one `chore(release): v1.0.0` commit with Source-Tag/Source-Commit trailers.
- [x] `git ls-files` on `main` passes the overview's hygiene checklist.
- [x] GitHub Release `v1.0.0` exists with generated notes.
- [x] TASK.md ledger line; overview acceptance items ticked.

## Findings

- Run durations: `v1.0.0-rc.1` ~5 min (guard 6 s, verify ~1.5 min with jobs in parallel, publish ~1.5 min, smoke-main ~1.5 min); `v1.0.0` run `37241951413` ~5 min 16 s, all jobs green.
- Action SHAs: only `actions/checkout`, pinned to `fbc6f3992d24b796d5a048ff273f7fcc4a7b6c09` (`v5`); everything else is plain shell. `ci.yml` actions stay on tags (out of scope).
- `ci.yml` push trigger narrowed to branches: a tag push would otherwise start a standalone CI run in the same `ci-${{ github.ref }}` concurrency group as the reusable one called by `verify`, and one would cancel the other.
- Dry run: `release-test` got one `chore(release): v1.0.0-rc.1` commit by `github-actions[bot]` (781 files, no forbidden paths, no Release). Off-`dev` `v1.0.0-rc.2` failed in `guard` ("is not on dev history"), later jobs skipped, nothing touched. `release-test` and the rc tags were deleted.
- Dry-run finding: the `docker compose up` log was ~3,400 lines because EF Core logged every SQL command at `Information`, burying the `Personal Finance is ready at …` line. Fixed in the `Dockerfile` (`Logging__LogLevel__Microsoft.EntityFrameworkCore=Warning`, container only) before `v1.0.0`.
- Real release: `main` = init commit `9b0d48a` + `d8f1dc0 chore(release): v1.0.0` (Source-Commit `061e5d7`, on `dev`); GitHub Release `v1.0.0` created with generated notes; `main` has the same 781 files and passes the hygiene checklist.

## Out of scope

Making the repo public, rulesets (Slice 6). release-please (Q5, later if wanted).
