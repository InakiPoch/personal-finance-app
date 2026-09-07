# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository shape

This is a monorepo with two independently-built sub-projects. Each has its own detailed `CLAUDE.md` — **read the relevant one before working in that sub-project**; this file only covers what spans both.

- `app/api/` — .NET 10 backend. Details: `app/api/.claude/CLAUDE.md`
- `app/client/` — Angular 20 frontend. Details: `app/client/.claude/CLAUDE.md`

There is no root `package.json`, no Docker/Compose setup, and no CONTRIBUTING.md — do not assume tooling that isn't there. The root `README.md` is currently empty.

## Subagents

- Only launch subagents that are defined under `.claude/agents/` (project or user scope), or ones the user explicitly names in the request. Never fall back to the built-in `general-purpose` / `claude` catch-all agent, and never invent an agent type.
- For parallelized codebase research and information gathering, use `codebase-researcher` (Haiku 4.5, read-only). Spawn one instance per independent question and run them concurrently.
- If no defined agent fits the task and the user has not named one, talk the user about this before making decisions.

## Commands

All commands are run from within the sub-project directory, not the repo root — there is no root-level build tool.

**API** (from `app/api/`):
```
dotnet build
dotnet run --project src/Bootstrap/PersonalFinance.Api
dotnet test --solution PersonalFinance.sln                          # all tests
dotnet test --project tests/PersonalFinance.Ledger.Tests            # one module's tests
dotnet test --project tests/PersonalFinance.Ledger.Tests --filter "FullyQualifiedName~SomeTestClass"
```
`global.json` opts into .NET 10's Microsoft.Testing.Platform test runner (required for xUnit v3) — always pass `--solution` or `--project`, never a bare path.

**Client** (from `app/client/`):
```
pnpm ng build
pnpm ng lint
CHROME_BIN=/usr/bin/brave pnpm ng test --watch=false --browsers=ChromeHeadless
```
`CHROME_BIN` must point at any installed Chromium-based browser — no Chrome is bundled with Karma.

## CI

`.github/workflows/ci.yml` runs two independent jobs on push/PR to `main`/`dev`:
- `build-test`: `dotnet restore --locked-mode` → `dotnet build -c Release` → `dotnet test` in `app/api`.
- `client-build-test`: `pnpm install --frozen-lockfile` → `pnpm ng lint` → `pnpm ng build --configuration production` → `pnpm ng test --watch=false --browsers=ChromeHeadless` in `app/client`.

Both jobs must pass; there is no cross-stack integration step in CI.

## Cross-cutting architecture

- **Contract boundary**: the client has no server-side counterpart in this repo beyond the API's OpenAPI document (`/openapi/v1.json`, served by the API host). The client hand-mirrors DTO types from it — there is no shared-types package and no codegen.
- **API surface**: all HTTP endpoints are versioned under `/v1` and live in the API host project (`src/Bootstrap/PersonalFinance.Api`), not inside the backend's module projects — see `app/api/.claude/CLAUDE.md` for the modular-monolith layering (`.Contracts` boundaries, CQRS, Outbox).
- **Design system**: the client's visual direction ("warm homebanking") is specified in `app/client/docs/SYSTEM.md` and is authoritative for all views — extend it rather than diverging.
- **Product/technical design docs**: each sub-project has its own `docs/PRD.md` and `docs/DESIGN.md` (API's are in Spanish); consult the relevant sub-project's docs for product scope and design decisions rather than this file.
