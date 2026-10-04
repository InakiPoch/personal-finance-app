# Slice 2 — Smoke script + CI job

Read [00-overview.md](00-overview.md) first. Requires [Slice 1](slice-1-runnable-container.md).

## Goal

Turn Slice 1's manual verification into one script, `scripts/smoke.sh`, and run it in CI on `dev`. The same script is reused later by the release workflow (Slice 5) against the projected `main` tree, so it is the single definition of "the release works".

## Why

- There is no cross-stack test today: CI tests API and client separately. The container is the first place they meet.
- The final review (§11) is explicit that `/health` alone is too weak — the release touches static hosting, SPA fallback, routing, SQLite creation, migrations and volume persistence at once. The script checks all of them.

## Design

`scripts/smoke.sh [compose-dir]` — runs in the directory holding `compose.yaml` (default: cwd). Uses only `sh`/`bash`, `curl`, `docker`, coreutils (it must run on a stock GitHub runner and on the owner's machine). Env `BASE_URL` (default `http://localhost:8080`) and `CURL_OPTS` (Slice 3 passes `-k` for HTTPS).

Sequence:
1. `docker compose up -d --build`; trap → on exit always `docker compose logs app` (on failure) and `docker compose down -v`.
2. Wait for `GET /health` to succeed — poll up to ~180 s (first build in CI is slow; the build happens in step 1 so the poll only covers startup + migrations, ~30 s is realistic).
3. `GET /` → 200 and body contains `<app-root`.
4. `GET /reports` → 200 and body contains `<app-root` (SPA fallback).
5. `GET /v1/does-not-exist` → 404.
6. `GET /v1/instruments` → 200 and empty (match the shape recorded in Slice 1 findings).
7. Create one instrument via `POST /v1/instruments` (body shape: copy from `app/api/src/Bootstrap/PersonalFinance.Api/PersonalFinance.Api.http`, a debit/cash instrument is the simplest).
8. `docker compose down` (keep volume) → `up -d` → wait healthy → `GET /v1/instruments` contains the created instrument (**persistence**).
9. `docker compose down -v` → `up -d` → wait healthy → `GET /v1/instruments` empty (**reset**).
10. Print `SMOKE OK`; exit non-zero with a clear message on the first failure.

Keep it one flat script with a tiny `check()` helper — no framework.

## CI

Add a job `docker-smoke` to `.github/workflows/ci.yml`:
- `runs-on: ubuntu-latest` (Docker + compose v2 preinstalled), `timeout-minutes: 25`.
- Steps: `actions/checkout` → `bash scripts/smoke.sh`.
- Same triggers as the existing jobs. Independent of `build-test` / `client-build-test` (no `needs:`), so the three run in parallel.
- Also add `workflow_call:` to `ci.yml`'s `on:` block now — Slice 5's release workflow reuses the whole CI file with `uses: ./.github/workflows/ci.yml`.

Action SHA pinning: not required here (CI is read-only, `permissions: contents: read`). The release workflow (Slice 5) pins SHAs because it holds write credentials.

## Done when

- [~] `bash scripts/smoke.sh` passes locally (working tree; clean-clone run pending commit).
- [x] Temporarily break something (e.g. comment out `MapFallbackToFile`) → script fails at step 4 with a readable message. Revert.
- [ ] `docker-smoke` green on the PR to `dev`; CI duration noted in Findings.
- [x] TASK.md ledger line.

## Findings

2026-10-03:
- Instrument POST body: `{"type":"debit","name":"Checking"}` → 201. Empty list matched with `"rows":[]`.
- Local run (cached layers): `SMOKE OK` in 38 s. Cold CI build is slower (Slice 1 measured ~60 s cold for the image alone).
- The script runs under its own compose project `pf-smoke` (`COMPOSE_PROJECT_NAME`, overrides the top-level `name`), so its `down -v` never touches the real `personal-finance_pf-data` volume. It still needs host port 8080 free.
- Break test (`MapFallbackToFile` commented out): fails at step 3 (`GET / serves the SPA`), not step 4 — `UseStaticFiles` alone does not serve `index.html` at `/`, so the fallback serves both. Message is readable; `SMOKE FAIL: …` is repeated as the last line because 200 log lines of EF SQL otherwise bury it.
- Environment gotcha (not a script bug): `failed to add the host <=> sandbox pair interfaces: operation not supported` means the running kernel has no matching `/lib/modules` (veth missing) — reboot into the installed kernel.
- CI duration / flakiness of `docker-smoke`: pending the PR to `dev`.

## Out of scope

HTTPS checks (Slice 3 extends the script via `BASE_URL`/`CURL_OPTS`), N→N+1 upgrade test (cut, Q10).
