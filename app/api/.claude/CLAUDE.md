# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Status

.NET 10 Minimal API, modular monolith (4 modules + a read-only Reporting layer — see **Architecture** below). Phase-by-phase build history, what shipped in each phase, and open/in-progress work all live in `.claude/TASK.md` — **read it before starting new module work**, do not duplicate its content here. Latest: Phase 49 (Partial creditor payments, Slice 2 — pay a whole creditor purchase, full or partial) done pending user live E2E; Slice 3 (partial full-debt) and Slice 4 (party share) of `docs/partial-creditor-payments/` remain.

Fase-1 of `docs/PRD.md` §5 is acceptance-complete (no open code items). Fase-2 (US-5 Subscriptions) and Fase-3 (US-7 Parties) are built but have no dedicated acceptance-pass phase yet.

Feature work is planned as vertical-slice docs under the repo-root `docs/<feature-slice>/` (e.g. `docs/partial-creditor-payments/00-overview.md` + per-slice files) — check there for the design rationale behind any in-progress phase.

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

Per `docs/DESIGN.md` §6 the pattern is one xUnit project per module (`PersonalFinance.<Module>.Tests`) plus `PersonalFinance.Architecture.Tests` for module-isolation checks (RNF-9) — extended by every new module phase.

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

The connection string comes from `ConnectionStrings:PersonalFinanceDb` (override with the `ConnectionStrings__PersonalFinanceDb` env var or user-secrets). Resolution is centralized in `SqliteConnectionStringHelper` (shared project `PersonalFinance.Infrastructure.Persistence`): the host calls `Resolve(configuration.GetConnectionString(...))` from `AddSharedInfrastructure`, each module's design-time factory calls `ResolveForDesignTime()`. When nothing is configured, both fall back to `PF_SQLITE_CONNECTION` (legacy design-time knob) and then to `personalfinance.db` at the solution root (via `SolutionRootLocatorHelper`), so `dotnet ef` and `dotnet run` hit the same file. `Data Source` is normalized to an absolute path; WAL / `busy_timeout` / `foreign_keys` stay as pragmas issued by `SqliteConnectionFactory`.

## Architecture

Full rationale lives in `docs/DESIGN.md`'s decisions log and product intent in `docs/PRD.md` — read both before making a design call that isn't already answered there. This is a deliberately over-engineered personal project: the explicit goal (`docs/PRD.md` §2) is practicing Modular Monolith + CQRS + event-driven design on a real domain, not minimizing effort. Don't simplify away the architecture patterns to make the domain "fit" — the domain is intentionally the excuse.

**Shape:** Monolito Modular — one process, one `.sln`, four bounded contexts each isolated behind a `.Contracts` assembly (public DTOs/interfaces) with an `internal` implementation assembly. Cross-module calls only ever go through the `.Contracts` interface (`ILedgerApi`, `IFinancingApi`, `ISubscriptionsApi`, `IPartiesApi`); referencing another module's implementation assembly must fail the build (enforced by `PersonalFinance.Architecture.Tests`, RNF-9). A read-only `Reporting` module sits alongside, querying `vw_*` views only — never another module's base tables (D5, RNF-6).

**The four modules:**
- **Ledger** — sole source of accounting truth (D1), double-entry, append-only (`Transaction`/`Entry`, RNF-4). Corrections are storno (reversal) entries, never edits/deletes (D3).
- **Financing** — credit cards, installment plans, billing-cycle calculation from each card's cutoff date (not calendar month). Card closing = a usual day plus per-month overrides resolved via `CreditCard.ClosingDayOf`; edits re-bucket open plans, and months with a statement are locked. `GET /v1/financing/due-this-month` sums what is payable by the current *calendar* month (cards + creditors, per currency, overdue included) — takes an optional `month=yyyy-MM` plus the caller's local `today=yyyy-MM-dd` (the host clock is UTC, so "current month" must come from the client; falls back to UTC) (other months: due on or before M, minus amounts paid before the 1st of M), deliberately not the creditor cutoff-26 "due now" rule (`CreditorDueNowHelper`, used only by creditor payables, which are one row per creditor and currency).
- **Subscriptions** — recurring charges, renewal scheduling. Each template's category ledger account is named `<name> Subscription` (not `<name> Expense`). `GET /v1/subscriptions/by-month?month=yyyy-MM` lists active subscriptions with a month status; past-month paid status comes from the Ledger via `FindPaidSubscriptionIdsQuery` over `IQueryBus` (not `ILedgerApi`).
- **Reporting (read-only)** — owns the rich transaction feed: `GET /v1/reports/transactions[/{id}]` (the old Ledger feed is gone). `TransactionExplainer` is the single source of reversal/undo wording and must mirror `ReversalCalculator.Decide`. Each module contributes read views (`vw_ledger_transaction_legs`, `vw_installment_labels`, `vw_subscription_names`) via its own migration; the host auto-migrates only when `Database:MigrateOnStartup=true` (default `false`; the Docker image sets it) — see **Container hosting** — otherwise run per-module `dotnet ef database update`.
- **Parties** — third-party shared-expense tracking / running balances, layered as a management view over Ledger receivable accounts (D1) rather than a second ledger. Splits are created only through `RecordDebitExpense` (debit/cash) or a payment plan (card/creditor) — there is no standalone shared-expense endpoint.

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

**Container hosting (`docs/release-setup/`):** the root `Dockerfile` + `compose.yaml` build one image from source and serve the SPA and API from one origin. With `Database:MigrateOnStartup=true`, `DatabaseMigrationHelper` (host `Helpers/`) migrates every module context sequentially (Ledger → Financing → Subscriptions → Parties) between `Build()` and `Run()`, so hosted services only start on an existing schema; before each context it drops `__EFMigrationsLock` (single process — a run killed mid-migration leaves that row behind and EF would otherwise poll for it forever). `Program.cs` also does `UseStaticFiles` + `MapFallbackToFile("index.html")` (a `/v1/{**rest}` fallback keeps mistyped API paths a 404) and logs `Personal Finance is ready at {App:PublicUrl}` when that config is set. `SqliteConnectionStringHelper.Resolve` must keep the solution-root lookup lazy: the image has no `.sln`. HTTPS is opt-in: with `Https:Enabled=true` (default `false`; `Https:CertDirectory` default `/data/https`) `SelfSignedCertificateHelper` reuses or generates a localhost self-signed PFX + `.crt` in the data volume, Kestrel is configured in code (HTTP 8080 + HTTPS 8443) and `UseHttpsRedirection` runs; when disabled that call is absent and `ASPNETCORE_HTTP_PORTS` stays authoritative. No HSTS ever (localhost). Browser trust needs NSS `-t "P,,"` (peer), not `C` — see `docs/release-setup/slice-3-opt-in-https.md`.

**API host compatibility (D13, implemented in Phase 8):** designed for a future Angular client without compromising endpoint testability now — CORS is configurable (`Cors:AllowedOrigins`, not hardcoded), error responses use one consistent enriched RFC-9457 `ProblemDetails` envelope derived from `SharedKernel.Error` (HTTP status from `Endpoints/ErrorHttpStatusHelper.cs`, the source of truth for the 404/409/422/400 mapping; `Handlers/GlobalExceptionHandler` for unhandled/parse failures), and OpenAPI is kept complete and always served at `/openapi/v1.json`. Payment-instrument registration (`POST /instruments`) is a single Bootstrap-level endpoint with no domain logic of its own — it just routes to `ILedgerApi` (debit/cash) or `IFinancingApi` (credit) based on the request's `type`.
