# Slice 6 — Go public + protect `main`

Read [00-overview.md](00-overview.md) first. Requires [Slice 5](slice-5-release-workflow.md) (`v1.0.0` already on `main`).

## Goal

Strangers can clone and run the app; nobody (including the owner by accident) can push to, force-push or delete `main` — only the release workflow's deploy key can update it.

## Why last

- Rulesets are **not available on private repos on the free personal plan** (the API returned 403 during research). Protection can only be configured once the repo is public.
- Going public publishes `dev` and its full history (accepted, Q12). Slice 0's secrets/personal-data scan must still be valid — re-run it on the current history right before flipping.
- `main` should be correct *before* anyone can see it: `v1.0.0` landed in Slice 5.

## Steps

### 1. Pre-flight

- Re-run `gitleaks git --log-opts="--all"`; compare with Slice 0 findings. New findings block the flip.
- Repo description + topics (e.g. `personal-finance`, `dotnet`, `angular`, `sqlite`, `docker`) — `gh repo edit --description "…" --add-topic …`.
- Confirm default branch is `main` (`gh repo view --json defaultBranchRef`).

### 2. Flip visibility

```sh
gh repo edit InakiPoch/personal-finance-app --visibility public --accept-visibility-change-consequences
```

### 3. Rulesets (via `gh api` so they're reproducible — store the JSON bodies in this doc's Findings)

**Prove the bypass before relying on it.** Rulesets support **Deploy keys** as a bypass actor; that's the design from Slice 5. Verify on the scratch branch first:

1. Create ruleset `release-test-protect` targeting `refs/heads/release-test` with rules `update`, `deletion`, `non_fast_forward`, bypass actor = Deploy keys (`"actor_type": "DeployKey", "bypass_mode": "always"`).
2. Push an rc tag (`v1.0.1-rc.1` on `dev`) → the release workflow must still push to `release-test`.
3. From your machine: `git push origin HEAD:release-test` → must be **rejected**.
4. Clean up: delete ruleset, branch, rc tag.

If step 2 fails (deploy-key bypass not honoured), the fallback is a **GitHub App** owned by the account: install it on the repo, add it as bypass actor, and in `release.yml` mint a token with `actions/create-github-app-token` and push over HTTPS instead of SSH. Record which path was taken.

Then the real rulesets:

| Ruleset | Target | Rules | Bypass |
|---|---|---|---|
| `main-release-only` | `refs/heads/main` | `update`, `deletion`, `non_fast_forward` | Deploy keys |
| `dev-safety` | `refs/heads/dev` | `deletion`, `non_fast_forward` | — (owner still pushes/merges normally; add "require PR + status checks `build-test`, `client-build-test`, `docker-smoke`" only if wanted — not decided, default off) |
| `release-tags` | `refs/tags/v*` | `deletion`, `update` | — |

Note on `release-tags`: once on, a bad tag can't be deleted and re-created; the fix is always a new version. Turn it on only after `v1.0.0` is confirmed good.

### 4. Verify as a stranger

On a machine/user with no GitHub credentials (or a throwaway container):
```sh
GIT_TERMINAL_PROMPT=0 git -c credential.helper= clone https://github.com/InakiPoch/personal-finance-app.git
cd personal-finance-app
docker compose up -d
```
→ builds, `Personal Finance is ready at http://localhost:8080`, empty data. Run `smoke.sh` against it (copy the script from `dev`).

Also confirm on github.com: landing page shows `main` with the user README; `dev` is listed as a branch; the Releases sidebar shows `v1.0.0`.

## Done when

- [ ] Repo public; gitleaks re-run clean.
- [ ] Deploy-key (or GitHub App) bypass proven on `release-test`, then `main-release-only` active.
- [ ] `git push origin HEAD:main` from the owner's machine is rejected.
- [ ] `release-tags` and `dev-safety` active.
- [ ] Anonymous clone + `docker compose up` works end to end.
- [ ] Next release (whenever it happens — e.g. `v1.0.1`) passes through the protected `main` without workflow changes. Until then this box stays open.
- [ ] Overview acceptance checklist fully ticked; TASK.md ledger line; memory note updated.

## Findings

_(fill in: ruleset JSON bodies, bypass mechanism used, anything surprising about public visibility)_

## Out of scope

GHCR images, Dependabot, scheduled scans, release-please (all cut or deferred — see overview decisions Q1/Q5/Q10).
