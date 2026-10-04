# Public `main` for a localhost-only app — Research Brief

Date: 2026-10-02 · Mode: ARS `deep-research` / full (scoping → investigation → analysis → composition → devil's-advocate check, run inline) · Status: research only, no code changed.

Reading order: this brief → [01-evidence.md](01-evidence.md) → [02-report.md](02-report.md) → [03-implementation-plan.md](03-implementation-plan.md).

## Research question

How should a repository be structured and automated so that **anyone can clone `main` and run the whole app on localhost with one command**, where `main` carries no tests and no development-only files, ships an empty database, and is backed by CI/CD that the community and professionals consider sound for a *no-hosting* project?

Sub-questions:

1. **Branch model** — how do `dev` (everything) and `main` (public, stripped) coexist without drifting or leaking?
2. **One-command run** — what does `docker compose up` need for .NET 10 API + Angular 20 + SQLite?
3. **Empty database** — what makes a fresh clone start with a clean DB, and how are schema upgrades handled?
4. **CI/CD** — what is the right pipeline when the "deployment" is a published image plus a compose file, not a server?

## Fixed constraints (from the request)

- No hosting; everything runs on the user's machine.
- One command, in the style of `docker compose up`.
- `main` excludes tests, development docs and any dev-only file; `main` and `dev` intentionally differ.
- `main` ships with an empty database.
- CI/CD must follow community/professional practice for this scope.

## Repo facts established (2026-10-02)

| Fact | Evidence |
|---|---|
| `main` is 0 commits ahead / 374 behind `dev` (it is a strict ancestor) | `git rev-list --left-right --count main...dev` → `0 374` |
| No `Dockerfile`, compose file, `.dockerignore`, `.gitattributes`, Dependabot config, CODEOWNERS or LICENSE exists | `fd` over repo |
| Root `README.md` is empty | project `CLAUDE.md` |
| SQLite files are already git-ignored (`*.db`, `-wal`, `-shm`); no `.db` is tracked and there is no seed data | `.gitignore`, `rg Seed\|HasData` → none |
| The host does **not** migrate on startup; 4 module DbContexts need migrating | memory `sqlite-connection-string-convention` |
| API listens on 7095/5003 in dev, uses `UseHttpsRedirection` and CORS pinned to `http://localhost:4200` | `launchSettings.json`, `Program.cs`, `appsettings.json` |
| Client reads `environment.apiUrl` (dev value is absolute `https://localhost:7095/v1`) via `baseUrlInterceptor` | `base-url.interceptor.ts`, `environment.development.ts` |
| Existing CI (`ci.yml`) builds/tests API + lints/builds/tests client on push and on PRs to `main`/`dev` | `.github/workflows/ci.yml` |
| ~221 of 1012 tracked files match rough dev-only patterns (`tests/`, `*.spec.ts`, `docs/`, `CLAUDE.md`, `TASK.md`) | `git ls-files \| rg` |

## Prior project decisions that this research must respect or consciously reverse

Four earlier LLM-council/research sessions (2026-09-01) concluded **"no Docker"** for onboarding (`council-docker-clone-onboarding`, `council-docker-compose-pipeline`, `phase9-ci-no-docker`). Their reasoning was *"Docker removes 1 of 6 first-run steps and the client did not exist yet"*. The scope has since changed: the requirement is now an explicit public one-command distribution, and the client exists. This research therefore **reverses that verdict on purpose** but keeps the technical findings that still hold (SQLite on a named *directory* volume, migrate before hosted services, chiseled images lack a shell).

## Method and limits (honest account)

- Sources: vendor/primary documentation fetched this session (Docker, GitHub, Microsoft, git-scm) plus a handful of secondary blog posts. Fetched pages were summarised by a small model; quotes are as returned, not re-verified against the live page.
- ARS skill: the structure of `full` mode was followed (scoping, investigation, source grading, synthesis, devil's-advocate pass, composition). The plugin's 13-agent fan-out was **not** spawned: the question is narrow, the evidence set is small, and the project rule limits subagents to declared agents.
- **No primary source describes the "projection branch" pattern as an established standard.** That part of the recommendation is engineering synthesis built from verified primitives (`export-ignore`, rulesets, Actions). It is labelled as such in the report.
- Not verified this session (flagged in the report): whether GitHub's Actions app can be a ruleset bypass actor; whether release-please tags created with `GITHUB_TOKEN` trigger downstream workflows; Angular's production `outputPath` for this repo.
