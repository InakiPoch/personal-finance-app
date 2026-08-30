# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Current state

**.NET 10 Minimal API. Phases 0–2 are complete.**

- **Phase 0** — multi-project layout from `docs/DESIGN.md` §6: `PersonalFinance.sln` + `global.json` + `Directory.Build.props` at `app/api/` (the solution root); host at `src/Bootstrap/PersonalFinance.Api/`.
- **Phase 1** — shared substrate: `PersonalFinance.SharedKernel` (`Money`/`Currency`/`Result`/`Error`/`Entity`/`AggregateRoot`/`PhantomPennyAllocator`), `PersonalFinance.Abstractions` (CQRS + `IModule` contracts), `PersonalFinance.Infrastructure` (command/query buses, Outbox/Inbox, `ModuleDbContextBase`, `SchedulerBase`, `SqliteConnectionFactory`, `AddSharedInfrastructure`).
- **Phase 2** — the **Ledger** module (`src/Modules/Ledger/`): double-entry, append-only `Transaction`/`Entry`, `Account` aggregate, storno reversal (plain storno only — the D12 cascade is a `// TODO(Phase 4)` seam), `ILedgerApi` facade over the buses, per-module `LedgerDbContext` + EF migrations + `vw_*` read views, wired into the host. HTTP surface is host-owned (`src/Bootstrap/PersonalFinance.Api/Endpoints/`), served under `/v1`: `POST /v1/ledger/transactions`, `.../transactions/{id}/reversal`, `GET /v1/ledger/accounts/{id}/balance`, dev-only `POST /v1/ledger/accounts`. Tests in `tests/PersonalFinance.Ledger.Tests` (xUnit v3, pure-domain). One shared, git-ignored SQLite file `personalfinance.db` at the solution root, WAL mode.

**Phase 3 (Financing — credit cards, installment plans, billing cycles) is next.** Before writing module code, check `.claude/TASK.md` for the phase in progress — checkbox tasks sequenced by real dependency order (Phase 0 → 10), each phase's "Definition of done" is the completion bar.

## Commands

Run from `app/api/` (the solution root — holds `PersonalFinance.sln`, `global.json`, `Directory.Build.props`):

```bash
dotnet build                                                  # build the whole solution
dotnet run --project src/Bootstrap/PersonalFinance.Api        # run the API (port in that project's Properties/launchSettings.json)
dotnet watch --project src/Bootstrap/PersonalFinance.Api run  # run with hot reload
```

`dotnet build` accepts `PersonalFinance.sln` directly; `dotnet run` needs `--project` because `app/api/` has no bare `.csproj` in it and `dotnet run` won't resolve one from a `.sln`.

### Tests

`global.json` opts into the .NET 10 **Microsoft.Testing.Platform** `dotnet test` mode (required for xUnit v3). That changes the CLI — pass `--solution` / `--project`, never a bare path (a bare path errors):

```bash
dotnet test --solution PersonalFinance.sln                # all tests
dotnet test --project tests/PersonalFinance.Ledger.Tests  # single test project
dotnet test --project tests/PersonalFinance.Ledger.Tests --filter "FullyQualifiedName~DoubleEntryInvariantTests"  # single test class
```

Per `docs/DESIGN.md` §6 the pattern is one xUnit project per module (`PersonalFinance.<Module>.Tests`) plus a future `PersonalFinance.Architecture.Tests` for module-isolation checks.

### EF Core migrations

Per-module `DbContext`, all sharing one SQLite file, each with its own `MigrationsHistoryTable` (`__EFMigrationsHistory_Ledger`, `_Financing`, …). Each module project carries its own `IDesignTimeDbContextFactory`, so it is its own `--startup-project`:

```bash
dotnet ef migrations add <Name> \
  --project src/Modules/<Module>/PersonalFinance.<Module> \
  --startup-project src/Modules/<Module>/PersonalFinance.<Module> \
  --context <Module>DbContext

dotnet ef database update \
  --project src/Modules/<Module>/PersonalFinance.<Module> \
  --startup-project src/Modules/<Module>/PersonalFinance.<Module> \
  --context <Module>DbContext
```

When `Sqlite:ConnectionString` is blank, the design-time factory and the host both resolve `personalfinance.db` at the solution root (via `SolutionRootLocatorHelper`), so `dotnet ef` and `dotnet run` hit the same file.

## Architecture

Full rationale lives in `docs/DESIGN.md` (decisions D1–D13) and product intent in `docs/PRD.md` — read both before making a design call that isn't already answered there. This is a deliberately over-engineered personal project: the explicit goal (`docs/PRD.md` §2) is practicing Modular Monolith + CQRS + event-driven design on a real domain, not minimizing effort. Don't simplify away the architecture patterns to make the domain "fit" — the domain is intentionally the excuse.

**Shape:** Monolito Modular — one process, one `.sln`, four bounded contexts each isolated behind a `.Contracts` assembly (public DTOs/interfaces) with an `internal` implementation assembly. Cross-module calls only ever go through the `.Contracts` interface (`ILedgerApi`, `IFinancingApi`, `ISubscriptionsApi`, `IPartiesApi`); referencing another module's implementation assembly must fail the build (enforced by `PersonalFinance.Architecture.Tests`, RNF-9). A read-only `Reporting` module sits alongside, querying `vw_*` views only — never another module's base tables (D5, RNF-6).

**The four modules:**
- **Ledger** — sole source of accounting truth (D1), double-entry, append-only (`Transaction`/`Entry`, RNF-4). Corrections are storno (reversal) entries, never edits/deletes (D3).
- **Financing** — credit cards, installment plans, billing-cycle calculation from each card's cutoff date (not calendar month).
- **Subscriptions** — recurring charges, renewal scheduling.
- **Parties** — third-party shared-expense tracking / running balances, layered as a management view over Ledger receivable accounts (D1) rather than a second ledger.

**Communication between modules** (`docs/DESIGN.md` §5.1):
- **Sync (DI-resolved `.Contracts` interface)** — default for request/response needs, e.g. Ledger→Financing and Ledger→Parties during reversal cascades (D12).
- **Async (integration events via Outbox)** — only for genuine transactional dual-writes triggered *within* a command's transaction. In this codebase that's essentially just `PaymentPlanCreatedIntegrationEvent` (D8). Producer writes to its own `<module>_outbox_messages` table in the same transaction; `OutboxWorker` (a `BackgroundService`) drains it; consumers dedupe via `<module>_inbox_consumed` (RNF-2).
- **Scheduler (clock-triggered)** — installment accrual and subscription renewal are *not* Outbox work, they're `BackgroundService`s that emit commands on a timer (D6). Don't conflate the two mechanisms — using Outbox for clock-triggered work or a scheduler for transactional dual-writes is the exact mistake D6 exists to prevent.

**Key invariants to preserve when touching Ledger/Financing/Parties code:**
- Every `Transaction` must balance (`Σdebits == Σcredits`) — `Domain/Rules/DoubleEntryMustBalance.cs`.
- Money is integral minor-units, never floating point; split/installment allocation goes through `PhantomPennyAllocator` (largest-remainder method) so `Σ(parts) == total` always, with no dropped or duplicated cents.
- Card liability has a hard temporal boundary (D11, Modello B): *un-accrued* installments are Financing's authority (future schedule); *accrued* liability is Ledger's authority (posted `CardLiability` account). Accrual is the handoff point — don't let both sides claim the same cuota.
- Reversal (D12, supersedes D10 — read D10's note before assuming the old "reject if already paid" behavior applies) always succeeds: plain storno always, plus a compensating card-credit entry if the installment was already paid (netted against the *next* statement, never against `Activo:Banco` directly — see RNF-5), plus a synchronous cascade to Parties if the transaction had a `SplitReference`.

**SQLite:** one file, `WAL` journal mode + `busy_timeout` (D7/RNF-1) — the API host, the Outbox worker, and the schedulers all write to it concurrently, so a connection that skips the `SqliteConnectionFactory` pragmas will eventually hit `SQLITE_BUSY`. Each module's `DbContext` needs its own `MigrationsHistoryTable` name (`__EFMigrationsHistory_Ledger`, `_Financing`, …) so the four migration histories coexist in one file without colliding.

**API host compatibility (D13):** designed for a future Angular client without compromising endpoint testability now — CORS is configurable (not hardcoded), error responses use one consistent envelope derived from `SharedKernel.Error`, and OpenAPI is kept complete. Payment-instrument registration (`POST /instruments`) is a single Bootstrap-level endpoint with no domain logic of its own — it just routes to `ILedgerApi` (debit/cash) or `IFinancingApi` (credit) based on the request's `type`.
