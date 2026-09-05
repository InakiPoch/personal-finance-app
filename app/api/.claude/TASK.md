# TASK.md — Implementation Guide

Phased build order for the PersonalFinance API. Sequenced by actual code dependency, not by module-list order. Each phase lists its goal, dependencies, concrete tasks, owning test project, and definition of done.

Source docs: `docs/PRD.md` (product), `docs/DESIGN.md` (technical, D1–D13 + folder scaffold in §6). Both are reconciled and internally consistent as of the D12/D13 revision — this file just sequences building what they already specify.

## Decisions this file builds against (already resolved in `docs/DESIGN.md`)

- **D12** (supersedes D10): reversal always succeeds, including on paid installments — plain storno, plus a card-credit compensating entry if already paid, auto-netted against the card's next statement, cascading to Parties via a sync `IPartiesApi` call when a `SplitReference` is present. Implemented in Phase 4.
- **D13**: payment instruments (`debit | credit | cash`, `cutoffDate` for credit) register through a single Bootstrap-level `POST /instruments` endpoint that routes to `ILedgerApi` (debit/cash) or `IFinancingApi` (credit); the API is designed CORS/envelope/OpenAPI-ready for the future Angular client. Instrument creation lands in Phase 2/3; host polish (envelope/CORS/OpenAPI) in Phase 8.
- **AccountKind** (Ledger) and **CreditCard.CutoffDate** + **BillingCycleCalculator** (Financing) are the concrete entities carrying D9/D12/D13 into code — built in Phase 2 and Phase 3 respectively.

---

## Phase 0 — Repository & Solution Scaffolding

**Goal:** Replace the flat `dotnet new webapi` scaffold with the multi-project structure from `docs/DESIGN.md` §6, with nothing but an empty, running host.

**Depends on:** nothing.

### Tasks
- [x] Remove the `/weatherforecast` sample endpoint and `WeatherForecast` record from `Program.cs`.
- [x] Create `PersonalFinance.sln` at repo root, add projects to it as they're created.
- [x] Create `global.json` pinning the .NET 10 SDK (`rollForward: latestFeature`).
- [x] Create `Directory.Build.props` at repo root: `TargetFramework=net10.0`, `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`.
- [x] Create directory skeleton: `src/Bootstrap/`, `src/Shared/`, `src/Modules/`, `src/Reporting/`, `tests/`.
- [x] Move `api.csproj`, `Program.cs`, `appsettings*.json`, `Properties/launchSettings.json`, `api.http` into `src/Bootstrap/PersonalFinance.Api/`, rename project `PersonalFinance.Api.csproj`.
- [x] Confirm `dotnet build` and `dotnet run` succeed with an empty pipeline (a temporary `GET /health` returning 200 is fine — replaced by the real health check in Phase 8).
- [x] Update `.gitignore`: `bin/`, `obj/`, `*.db`, `*.db-wal`, `*.db-shm` (SQLite file and its WAL/SHM siblings must never be committed).
- [x] Update `api.http` to the new project's port; keep it as the running manual smoke-test file for every later phase.

### Definition of done
- [x] `dotnet build` succeeds against `PersonalFinance.sln` from repo root.
- [x] `dotnet run` starts the host and answers on the declared port.
- [x] No leftover weather-forecast code anywhere in the tree.

### Completion notes (2026-08-28)

- **Solution root is `app/api/`** (not the git repo root) — matches `CLAUDE.md`'s "run from `app/api/`". `PersonalFinance.sln` is classic format (`dotnet new sln --format sln`; the .NET 10 default is now `.slnx`).
- The pre-existing git-root `PersonalFinanceApp.sln` held a stale reference to the old flat `app/api/api.csproj` and was **deleted** — `app/api/PersonalFinance.sln` is the repo's only solution.
- `PersonalFinance.Api.csproj` sets `RootNamespace`/`AssemblyName` = `PersonalFinance.Api`; `TargetFramework`/`Nullable`/`ImplicitUsings`/`LangVersion` are inherited from `Directory.Build.props`.
- `global.json` pins SDK `10.0.111`.
- `.gitignore` already covered `bin/`/`obj/` repo-wide (unanchored). Added `*.db`, `*.db-wal`, `*.db-shm`, `*.db-journal`. Also collapsed the JetBrains block to `.idea/` + `**/.idea/` and untracked the previously committed `app/api/.idea/` files.
- Empty skeleton dirs (`src/Shared`, `src/Modules`, `src/Reporting`, `tests`) carry a `.gitkeep` until real projects land.
- `dotnet run` from `app/api/` needs `--project src/Bootstrap/PersonalFinance.Api` (no bare `.csproj` in cwd; `dotnet run` doesn't resolve from a `.sln`). `dotnet build`/`test` take the `.sln` directly. **`CLAUDE.md` → Commands still shows the pre-Phase-0 paths and needs updating.**
- Verified: `dotnet build PersonalFinance.sln` → 0/0; host answers `200` on `GET /health`; `rg -i weatherforecast` clean repo-wide.

---

## Phase 1 — Shared: Abstractions, SharedKernel, Infrastructure

**Goal:** Build the substrate every module depends on: messaging contracts, value objects, the SQLite connection strategy, Outbox/Inbox, and the scheduler base class.

**Depends on:** Phase 0.

### Tasks — `PersonalFinance.Abstractions`
- [x] `Messaging/ICommand.cs` (`ICommand` + `ICommand<TResult>`), `Messaging/IQuery.cs` (`IQuery<TResult>`).
- [x] `Messaging/ICommandHandler.cs`, `Messaging/IQueryHandler.cs` — `IQueryHandler<in TQuery, TResult>` → `Task<TResult>`; `ICommandHandler<in TCommand>` → `Task<Result>` and `ICommandHandler<in TCommand, TResult>` → `Task<Result<TResult>>`. `ICommandHandler` landed at the end of Step 2 with the `Abstractions → SharedKernel` project reference.
- [x] `Messaging/IIntegrationEvent.cs` — `Guid MessageId`, `DateTimeOffset OccurredOnUtc`.
- [x] `Messaging/IIntegrationEventHandler<TEvent>.cs`.
- [x] `Modularity/IModule.cs` — `Name` + `Register(IServiceCollection, IConfiguration)` + `MapEndpoints(IEndpointRouteBuilder)`; csproj carries `FrameworkReference Microsoft.AspNetCore.App`.

### Tasks — `PersonalFinance.SharedKernel`
- [x] `Currency.cs` — `sealed record Currency(string Code, byte DecimalPlaces)` + `static readonly Currency Reference` (`ARS`/2). Modeled explicitly so `Money` isn't unitless (multi-currency stays out of scope per PRD §7, but the seam exists).
- [x] `Money.cs` — `readonly record struct Money(long MinorUnits, Currency Currency)`; `Zero`/`FromMinorUnits`; `+ - unary- *(int|long)` and `< <= > >=`; throws on cross-currency arithmetic/ordering; `ToString()` formats via `Currency.DecimalPlaces`. Pure `long` arithmetic, no float/decimal path. `PhantomPennyAllocator` depends on this being integral.
- [x] `Result.cs` / `ResultOfT.cs`, `Error.cs` (`Code`, `Message`, `Metadata` + `static Error None`) — `Result` (`IsSuccess`/`IsFailure`/`Error`, `Success()`/`Failure(Error)`); `Result<T>` adds `T Value` (throws on failure) + implicit conversions from `T` and from `Error`. `Error`'s shape is designed now to match the API error envelope built in Phase 8.
- [x] `IDomainEvent.cs` — in-process marker interface (NOT `IIntegrationEvent`; a handler maps a domain event to an integration event when another module must learn of it).
- [x] `Entity.cs` (`abstract class Entity<TId> where TId : notnull`, identity equality by concrete type + `Id`, `==`/`!=`/`GetHashCode`), `AggregateRoot.cs` (`abstract class AggregateRoot<TId> : Entity<TId>` — `IReadOnlyCollection<IDomainEvent> DomainEvents`, `protected RaiseDomainEvent`, `ClearDomainEvents`), `ValueObject.cs` (`abstract class ValueObject`, structural equality over `protected abstract IEnumerable<object?> GetEqualityComponents()`).
- [x] `Allocation/IAllocationStrategy.cs` (`IReadOnlyList<long> Allocate(long total, IReadOnlyList<long> weights)`), `Allocation/PhantomPennyAllocator.cs` — largest-remainder (Hamilton): share toward zero to everyone, hand leftover minor units one-by-one to the largest remainders (ties → lowest index, deterministic), so `Σ(parts) == total` always; `checked` multiplication; `Money` convenience overload. Property-tested later (Phases 3/6).

### Tasks — `PersonalFinance.Infrastructure`
- [x] `Persistence/SqliteOptions.cs` — binds config section `"Sqlite"`: `ConnectionString`, `BusyTimeoutMs` (5000), `JournalMode` ("WAL"), `ForeignKeys` (true).
- [x] `Persistence/SqliteConnectionFactory.cs` — `ISqliteConnectionFactory` + `sealed` impl; `CreateOpenConnection()` opens a `SqliteConnection` and issues `PRAGMA journal_mode`, `PRAGMA busy_timeout`, `PRAGMA foreign_keys` from `SqliteOptions` on every connection (D7/RNF-1).
- [x] `Persistence/ModuleDbContextBase.cs` — `abstract class ModuleDbContextBase(DbContextOptions, ISqliteConnectionFactory) : DbContext`; `protected abstract string ModuleName`; `OnConfiguring` (when not already configured) calls `UseSqlite(factory connection, contextOwnsConnection: true, o => o.MigrationsHistoryTable($"__EFMigrationsHistory_{ModuleName}"))` so the four migration histories coexist in one SQLite file (DESIGN.md §7 risk #2). Per-module design-time `IDesignTimeDbContextFactory` deferred to Phase 2.
- [x] `Messaging/CommandBus.cs` (`ICommandBus` + `sealed` impl over `IServiceProvider`: `Task<Result> SendAsync(ICommand)` and `Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult>)`, resolves the closed `ICommandHandler<>` / `ICommandHandler<,>` by the command's runtime type via reflection), `Messaging/QueryBus.cs` (`IQueryBus` + impl: `Task<TResult> AskAsync<TResult>(IQuery<TResult>)`, resolves `IQueryHandler<,>`). In-process only. (Registered scoped in Step 7 so scoped handlers resolve.)
- [x] `Messaging/IntegrationEventDispatcher.cs` — `IIntegrationEventDispatcher` + impl; `DispatchAsync(IIntegrationEvent)` resolves every `IIntegrationEventHandler<>` for the runtime type and awaits them sequentially, exceptions propagate. Two modes documented in the class XML-doc + `docs/notes/integration-event-dispatch.md` (Step 8):
  - **Durable** (via Outbox): only `PaymentPlanCreatedIntegrationEvent` (D8) — the one genuine transactional dual-write.
  - **Direct/in-process**: `InstallmentAccrued`, `SubscriptionRenewed` — dispatched synchronously right after the scheduler's own transaction commits; no persistence, no delivery guarantee; correctness relies on the scheduler's own idempotency guard, not on Outbox durability.
- [x] `Outbox/OutboxMessage.cs` (`long Id`, `Guid MessageId`, `string Type`, `string Payload`, `DateTimeOffset OccurredOnUtc`, `DateTimeOffset? ProcessedOnUtc`, `int Attempts`, `string? Error`), `Outbox/OutboxOptions.cs` (`PollingInterval` 5s, `StalenessThreshold` 5m, `BatchSize` 100), `Outbox/IOutboxWriter.cs` (`IOutboxWriter.Add(IIntegrationEvent)` — callable inside an existing `SaveChangesAsync` UoW, RNF-3 — + `OutboxWriterBase` that serializes to `OutboxMessage` and defers `AddToUnitOfWork` to the per-module subclass), `Outbox/IOutboxStore.cs` (deferred per-module seam: `GetUnprocessedBatchAsync`/`MarkProcessedAsync`/`MarkFailedAsync`/`GetBacklogAsync` + `OutboxBacklog` record struct), `Outbox/OutboxWorker.cs` (`BackgroundService` + `PeriodicTimer`; per tick opens a scope, fans out over every `IOutboxStore`, rebuilds the event via `Type.GetType` + `JsonSerializer`, calls `IIntegrationEventDispatcher.DispatchAsync`, `MarkProcessedAsync`; a throwing message → `MarkFailedAsync` + retry next tick), `Outbox/OutboxHealthCheck.cs` (`IHealthCheck`; aggregates `IOutboxStore` backlogs — Unhealthy past `StalenessThreshold` per RNF-7, Degraded if pending within it, Healthy when clear; uses `TimeProvider`). Concrete per-module `IOutboxStore`/`IOutboxWriter` wiring lands in Phase 2/3.
- [x] `Idempotency/InboxConsumedMessage.cs` (`MessageId`, `Consumer`, `ConsumedOnUtc`, composite key `(MessageId, Consumer)`), `Idempotency/IInboxStore.cs` (`IsConsumedAsync`, `MarkConsumedAsync` + `InboxStoreBase` over a `DbContext` — `IsConsumedAsync` = `AnyAsync`; `MarkConsumedAsync` only enlists the row so the consumer's own `SaveChangesAsync` commits it in the same transaction as the guarded side effects, RNF-2).
- [x] `Scheduling/SchedulerBase.cs` — abstract `BackgroundService`; `abstract TimeSpan Interval`, `virtual bool RunOnStartup => false`, `abstract Task TickAsync(ct)`; `ExecuteAsync` runs the tick (optionally on startup) then loops `Task.Delay(Interval, ct)` → tick; a throwing tick is logged and swallowed, `OperationCanceledException` unwinds cleanly on shutdown. Clock-triggered, not Outbox (D6). Used later by `AccrueInstallments` / `RenewDueSubscriptions`.
- [x] `DependencyInjection/InfrastructureServiceCollectionExtensions.cs` — `AddSharedInfrastructure(IServiceCollection, IConfiguration)` binds `SqliteOptions`/`OutboxOptions`, registers `TimeProvider.System` + `ISqliteConnectionFactory` (singleton) and `ICommandBus`/`IQueryBus`/`IIntegrationEventDispatcher` (**scoped**, so scoped handlers/`DbContext` resolve). Opt-in `AddOutboxProcessing()` adds `OutboxWorker` (hosted service) + `OutboxHealthCheck` (`"outbox"`). Host does not call these in Phase 1 — wired per-module / in Phase 8.

### Definition of done
- [x] All three Shared projects build and are referenced into the `.sln`.
- [x] `SqliteConnectionFactory` verified to actually set WAL mode and busy_timeout (`PRAGMA journal_mode;` returns `wal`).
- [x] `IntegrationEventDispatcher`'s two-mode design is written down so later phases don't reinvent it differently.

### Completion notes (2026-08-29)

- **Central Package Management introduced.** New `app/api/Directory.Packages.props` (`ManagePackageVersionsCentrally=true`) owns every version: `Microsoft.AspNetCore.OpenApi` `10.0.11` (host) and `Microsoft.EntityFrameworkCore.Sqlite` `10.0.11` (Infrastructure). Both `.csproj` `PackageReference`s dropped their `Version=`. No `Version=` remains in any `.csproj` under `src/`.
- **`Microsoft.AspNetCore.App` framework reference** carries the ASP.NET shared-framework types on `Abstractions` (`IServiceCollection`/`IConfiguration`/`IEndpointRouteBuilder` for `IModule`) and `Infrastructure` (hosting, health checks, DI, options, logging) — avoids listing ~8 `Microsoft.Extensions.*` packages. `SharedKernel` stays dependency-free (no framework ref, no packages, no project refs).
- **Dispatcher two-mode design** written down at `docs/notes/integration-event-dispatch.md`: one mode-agnostic `IIntegrationEventDispatcher`; durability lives at the call site (Outbox-backed for `PaymentPlanCreated` per D8, direct/in-process for scheduler-emitted events per D6). Phases 3/5 must not add a second dispatcher.
- **SQLite config** (`SqliteOptions`, section `"Sqlite"`): `ConnectionString` (empty default — host supplies), `BusyTimeoutMs` `5000`, `JournalMode` `"WAL"`, `ForeignKeys` `true`. `SqliteConnectionFactory` re-issues all three pragmas on every `CreateOpenConnection()`.
- **WAL verification** (DoD hard check, D7/RNF-1): a throwaway console in the job scratch dir referenced `PersonalFinance.Infrastructure`, opened a connection via `SqliteConnectionFactory` against a temp `.db`, and asserted `PRAGMA journal_mode;` → `wal` and `PRAGMA busy_timeout;` → `5000` (both **PASS**). Scratch project + temp db deleted afterward — no permanent Infra test project (DESIGN.md §6 lists none).
- **Buses are scoped.** `AddSharedInfrastructure` registers `ICommandBus`/`IQueryBus`/`IIntegrationEventDispatcher` scoped (they resolve handlers from the ambient scope), plus `TimeProvider.System` + `ISqliteConnectionFactory` as singletons. `AddOutboxProcessing()` is opt-in (worker + `"outbox"` health check). The host wires none of this in Phase 1.
- **Deferred to later phases:** per-module `IDesignTimeDbContextFactory` and EF entity configs (`OutboxMessage`/`InboxConsumedMessage` mappings, composite keys, `__EFMigrationsHistory_<Module>`), concrete `IOutboxStore`/`IOutboxWriter`/`IInboxStore` implementations, and host integration of `AddSharedInfrastructure`/`AddOutboxProcessing`.
- Verified: `dotnet build PersonalFinance.sln` → **0 warnings / 0 errors**, 4 projects (host + 3 shared).

---

## Phase 2 — Ledger Module (Core Accounting)

**Goal:** The sole source of accounting truth (D1) — post/reverse balanced double-entry transactions, expose balance queries. Everything else depends on this module.

**Depends on:** Phase 1.

### Tasks — Contracts (`PersonalFinance.Ledger.Contracts`)
- [x] `ILedgerApi.cs` — only way other modules touch Ledger.
- [x] `Commands/PostTransactionCommand.cs` — list of `(AccountId, DebitOrCredit, Money)` lines, optional `SplitReference`/`InstallmentReference` linkage.
- [x] `Commands/PostReceivableCommand.cs` — D1's receivable entry, used by Parties.
- [x] `Commands/CreateAccountCommand.cs` — `AccountType` + `AccountKind` (Bank/Cash), used by D13's unified `POST /instruments` endpoint for debit/cash registration.
- [x] `Commands/ReverseTransactionCommand.cs` — takes `OriginalTransactionId`. Behavior is D12 (not D10 — D10 is superseded in `docs/DESIGN.md`). *Phase 2 ships plain storno only; D12 cascade is a `// TODO(Phase 4)` seam.*
- [x] `Queries/GetAccountBalanceQuery.cs`, `Queries/GetCardLiabilityQuery.cs` (accrued liability only, D11).
- [x] `IntegrationEvents/TransactionPostedIntegrationEvent.cs` — informational only, no subscriber (Reporting reads views, D5).

### Tasks — Domain (`PersonalFinance.Ledger`)
- [x] ~~`Domain/AccountType.cs`~~ → `PersonalFinance.Ledger.Contracts/AccountType.cs` — Asset, Liability, Expense, Income, Equity. *(Deviation: enums live in Contracts, defined once, no mapping layer — locked plan decision 1.)*
- [x] ~~`Domain/AccountKind.cs`~~ → `PersonalFinance.Ledger.Contracts/AccountKind.cs` (per D9/D12) — `Bank`, `Cash`, `Receivable`, `CardLiability`, `CardCredit`, `Expense`, `Income`, `Equity`. Lets `vw_ledger_monthly_expenses` group Bank+Cash as "already spent" (PRD US-1 AC2) without naming conventions. `DebitOrCredit` also moved to Contracts.
- [x] `Domain/Account.cs` — carries `AccountType` + `AccountKind`; no stored balance, always derived from `Entry` rows (consistent with D4's no-materialized-view philosophy).
- [x] `Application/Commands/CreateAccount/CreateAccountHandler.cs`.
- [x] `Domain/Transaction.cs` — append-only aggregate root: `Id`, `PostedOnUtc`, `OriginalTransactionId` (nullable, for reversals), `Entry` collection.
- [x] `Domain/Entry.cs` — `AccountId`, `DebitOrCredit`, `Money`.
- [x] `Domain/SplitReference.cs`, `Domain/InstallmentReference.cs` — plain external ids linking to `ExpenseSplit`/`Installment` (those modules don't exist yet — no FK/navigation, respects module isolation).
- [x] `Domain/Rules/DoubleEntryMustBalance.cs` — `Σ(debits) == Σ(credits)` per currency, validated before a `Transaction` can be constructed.
- [x] `Domain/Events/TransactionPosted.cs` — in-aggregate domain event, mapped to the integration event by the handler.
- [x] `Application/Commands/PostTransaction/PostTransactionValidator.cs` + `PostTransactionHandler.cs`.
- [x] `Application/Commands/PostReceivable/PostReceivableHandler.cs` — builds D1's Dr Gasto + Dr PorCobrar / Cr Banco pattern, delegates to the transaction-posting logic (don't duplicate the balance check).
- [x] `Application/Queries/GetAccountBalance/GetAccountBalanceHandler.cs`, `Application/Queries/GetCardLiability/GetCardLiabilityHandler.cs` (sums `CardLiability`-kind accounts only, D11).
- [x] `Infrastructure/Persistence/LedgerDbContext.cs` (history table `__EFMigrationsHistory_Ledger`), `Configurations/` for `Transaction`, `Entry`, `Account`.
- [x] `Infrastructure/Persistence/Outbox/LedgerOutboxConfig.cs`, `Inbox/LedgerInboxConfig.cs` (tables exist for folder-tree symmetry even if unused for now — Ledger has no producer/consumer role yet per D6). *Also `LedgerOutboxStore`/`LedgerOutboxWriter`/`LedgerInboxStore` registered so `OutboxHealthCheck` enumeration is complete.*
- [x] `Infrastructure/Persistence/ReadViews/vw_ledger_balances.sql`, `vw_ledger_monthly_expenses.sql` (RF-1/D9 — excludes `Receivable` accounts from "own expense"), `vw_card_liability_accrued.sql` (RF-2 accrued half, D11). Embedded resources, created via a dedicated `LedgerReadViews` migration; each view emits literal `'ARS'` (Money mapping is single-currency, see notes).
- [x] `Infrastructure/PublicApi/LedgerApi.cs` — `internal`, implements `ILedgerApi`, thin delegation to `ICommandBus`/`IQueryBus`.
- [x] `LedgerModule.cs` — implements `IModule`. `Register` wires the DbContext + closed-generic handlers; `MapEndpoints` is a documented no-op (HTTP is host-owned).
- [x] Generate the initial EF Core migration for `LedgerDbContext` (`InitialLedgerSchema` + `LedgerReadViews`).
- [x] Wire `LedgerModule` into the Bootstrap host, add `Endpoints/` (host-owned: `EndpointExtensions.MapLedgerEndpoints`, `ApiRoutes`, `POST /v1/ledger/transactions`, `POST /v1/ledger/transactions/{id}/reversal`, `GET /v1/ledger/accounts/{id}/balance`, dev-only `POST /v1/ledger/accounts`). All routes under a `/v1` segment (`ApiRoutes.V1`). `CreateAccountCommand` has no production endpoint — dev-only seam until D13's `POST /instruments` in Phase 3.

### Tests — `PersonalFinance.Ledger.Tests`
- [x] `DoubleEntryInvariantTests.cs` — balanced succeeds; unbalanced → `Ledger.Unbalanced`; mixed-currency → `Ledger.MixedCurrency`; single leg → `Ledger.DegenerateTransaction`.
- [x] `ReceivableReconciliationTests.cs` — RNF-5: 3-leg `Dr Expense 500 + Dr Receivable 500 / Cr Bank 1000` balances; per-account fold Bank −1000, Expense +500, Receivable +500; own + receivable == bank outflow.
- [x] `ReverseTransactionTests.cs` — **basic case only**: mirrored counter-transaction, every direction flipped, `OriginalTransactionId == original.Id`, original aggregate untouched (RNF-4); reversing a reversal → `Ledger.CannotReverseAReversal`. Paid-installment/compensating-entry case is D12 — Phase 4.

### Definition of done
- [x] All `Ledger.Tests` pass. *(7 tests green — xUnit v3 / Microsoft.Testing.Platform.)*
- [x] `dotnet ef migrations` applies cleanly against the shared SQLite file. *(`InitialLedgerSchema` + `LedgerReadViews` on a fresh `personalfinance.db`; 5 tables + 3 views, `PRAGMA journal_mode` → `wal`.)*
- [x] Manual smoke test via `api.http`: post a balanced transaction, query balance, reverse it, confirm original untouched. *(curl: bank −1000 / "−10.00 ARS", groceries +1000, reversal → `compensatingEntryPosted:false`, bank → 0; unbalanced post → HTTP 400; `ledger_transactions` = 3 rows, original keeps NULL `OriginalTransactionId` and 2 entries.)*
- [x] `vw_ledger_balances`/`vw_ledger_monthly_expenses` spot-checked directly against the SQLite file. *(After a receivable-shaped txn: `vw_ledger_monthly_expenses` = 500 own-share for the month, Receivable leg excluded; `vw_ledger_balances` sign-normalized — Bank −1000, Groceries +500, Roommate +500; `vw_card_liability_accrued` empty.)*

### Completion notes (2026-08-29)
- **`Money` → SQLite = Plan B (scalar).** EF 10 can't constructor-bind a `ComplexProperty` whose `Currency` is a reference type, so `Money` maps through a `ValueConverter<Money, long>` to a single `AmountMinorUnits` column; currency is not stored, always reconstructed as `Currency.Reference` (ARS/2). Read views emit literal `'ARS'`. Phases 3/6 reuse this.
- **Enums in `Contracts`, not `Domain`** — `AccountType` / `AccountKind` / `DebitOrCredit` are public enums defined once in `PersonalFinance.Ledger.Contracts`; `Domain` references them directly, no mapping layer (deviates from the DESIGN §6 `Domain/AccountType.cs` path).
- **Dev connection-string resolution** — when `Sqlite:ConnectionString` is blank, both `LedgerDbContextFactory` (design-time) and a host `PostConfigure<SqliteOptions>` (runtime) resolve `Data Source={ascend for PersonalFinance.sln}/personalfinance.db` via `SolutionRootLocatorHelper` (now `public` in shared `PersonalFinance.Infrastructure`, moved out of the Ledger module).
- **Temporary dev endpoint** — `POST /v1/ledger/accounts` is mapped only in `Development`, marked `// TODO(Phase 3): remove` — replaced by D13's `POST /instruments`.
- **`/v1` route prefix** — every HTTP route sits under `ApiRoutes.V1` (`/health` stays unversioned). Versioning convention recorded in `.claude/rules/csharp-style.md` → "API Route Versioning".
- **`EF.Design` `PrivateAssets="all"`** on the Ledger impl `csproj` so the design-time package doesn't flow to the host.
- **xUnit v3 + .NET 10 MTP mode** — `global.json` carries `"test": { "runner": "Microsoft.Testing.Platform" }`; `dotnet test` VSTest mode no longer runs MTP projects on the .NET 10 SDK. Invocation is now `dotnet test --solution PersonalFinance.sln` / `dotnet test --project <csproj>` — bare-path forms error. **`.claude/CLAUDE.md` "Commands" still shows the old `dotnet test <path>` form — fix when convenient.**
- **Reversal scope** — Phase 2 reversal is plain storno. `ReverseTransactionHandler` has a `// TODO(Phase 4): D12` seam for the paid-installment compensating entry + Financing/Parties cascade.

---

## Phase 3 — Financing Module (Cards & Installments)

**Goal:** Credit purchases, installment schedules, automatic billing-cycle assignment, accrual (scheduler), statement payment. Accrual hands liability ownership to Ledger (D11).

**Depends on:** Phase 2 (`ILedgerApi`, `PostTransactionCommand`).

### Tasks — Contracts (`PersonalFinance.Financing.Contracts`)
- [x] `IFinancingApi.cs` — include a query for installment payment state now (`GetInstallmentStatusAsync`), even though its only consumer (Ledger's reversal handler) isn't finished until Phase 4.
- [x] `Commands/CreateCreditCardCommand.cs` — name, `CutoffDate`, used by D13's unified `POST /instruments` endpoint for credit registration.
- [x] `Commands/CreatePaymentPlanCommand.cs` — amount, card id, installment count, purchase date, optional split-with-parties payload.
- [x] `Commands/PayStatementCommand.cs`.
- [x] `IntegrationEvents/InstallmentAccruedIntegrationEvent.cs` — direct/in-process dispatch.
- [x] `IntegrationEvents/PaymentPlanCreatedIntegrationEvent.cs` — durable/Outbox dispatch (D8's one legitimate dual-write).

### Tasks — Domain (`PersonalFinance.Financing`)
- [x] `Domain/CreditCard.cs` — `CutoffDate` field (day-of-month, per D13) plus a carried-forward credit-balance field (per D12, populated starting Phase 4 — add the field now so `MonthlyStatement`/`PayStatementHandler` have somewhere to read/write it later).
- [x] `Domain/BillingCycleCalculator.cs` (per D13) — pure function `ResolveCycle(DateOnly purchaseDate, int cutoffDay)`: purchase on-or-before the cutoff day closes into the current cycle; purchase after opens the next cycle. Must be independently unit-testable — this is the exact mechanism behind PRD US-3 AC2.
- [x] `Domain/PaymentPlan.cs`, `Installment.cs` — `Installment` carries its resolved billing cycle, `AccruedOnUtc` (nullable), and a paid/unpaid flag via `MonthlyStatement` linkage (needed for Phase 4's reversal check).
- [x] `Domain/MonthlyStatement.cs` — aggregates installments accrued into a cycle for a card, tracks paid/unpaid.
- [x] `Application/Commands/CreateCreditCard/CreateCreditCardHandler.cs`.
- [x] `Application/Commands/CreatePaymentPlan/CreatePaymentPlanValidator.cs` + `Handler.cs` — uses `PhantomPennyAllocator` so `Σ(installments) == total` exactly; uses `BillingCycleCalculator` for the first installment's cycle; writes `PaymentPlanCreatedIntegrationEvent` to Financing's own Outbox in the same `SaveChangesAsync` transaction as the plan, only if a split payload is present (D8).
- [x] `Application/Commands/PayStatement/PayStatementValidator.cs` + `Handler.cs` — calls `ILedgerApi` to post Dr Liability / Cr Bank (D2's second half); marks the statement paid. **Does not yet net any card credit** — that's added in Phase 4 once D12's compensating-entry flow exists.
- [x] `Application/Scheduling/AccrueInstallments.cs` — `SchedulerBase`-derived; on cycle close, finds unaccrued installments whose cycle has closed, posts Dr Expense / Cr Liability via `ILedgerApi` (tagged with `InstallmentReference`), marks `AccruedOnUtc`, dispatches `InstallmentAccruedIntegrationEvent` (direct mode). **Idempotency guard mandatory**: re-running the tick must never double-accrue.
- [x] `Infrastructure/Persistence/FinancingDbContext.cs` (history table `__EFMigrationsHistory_Financing`), `Configurations/` for all four entities.
- [x] `Infrastructure/Persistence/Outbox/FinancingOutboxConfig.cs`, `Inbox/FinancingInboxConfig.cs` (Financing is a producer for `PaymentPlanCreated` only, no consumer role yet).
- [x] `Infrastructure/Persistence/ReadViews/vw_card_future_schedule.sql` (RF-2's future-schedule half, D11 — not-yet-accrued installments only).
- [x] `Infrastructure/PublicApi/FinancingApi.cs` — `internal`, implements `IFinancingApi`.
- [x] `FinancingModule.cs`, migration, DI wiring, `Endpoints/FinancingEndpoints.cs` (`POST /financing/payment-plans`, `POST /financing/statements/{id}/pay`, `GET /financing/cards/{id}/future-schedule`). Card creation has no endpoint of its own here — it's reached via the unified endpoint below.
- [x] Wire `AccrueInstallments` into host startup.
- [x] **D13's unified instrument endpoint** — now that both `CreateAccountCommand` (Ledger, Phase 2) and `CreateCreditCardCommand` (Financing, this phase) exist: add `Bootstrap/PersonalFinance.Api/Endpoints/InstrumentsEndpoints.cs` with `POST /instruments`, a thin router with no domain logic that dispatches to `ILedgerApi` (type = debit/cash) or `IFinancingApi` (type = credit) based on the request's `type` field.

### Tests — `PersonalFinance.Financing.Tests`
- [x] `InstallmentAllocationTests.cs` — property-based: `Σ(installments) == total` for random totals/counts, no negative/zero shares.
- [x] `AccrualBoundaryTests.cs` — D11: unaccrued installment contributes $0 to `GetCardLiabilityQuery`, full amount to future-schedule view; reverse after accrual. Also add `BillingCycleCalculator` boundary tests here (purchase exactly on cutoff → current cycle; one day after → next cycle) — this is D13's mechanism and has no other home in DESIGN.md's named test list.

### Tests — `PersonalFinance.Architecture.Tests` (create now — first point with ≥2 modules)
- [x] `ModuleIsolationTests.cs` — assert `Financing` doesn't reference `Ledger`'s impl assembly, only `Ledger.Contracts`. Pick and pin the inspection tool (reflection-based or a library like NetArchTest) — every later module phase extends this same test.

### Definition of done
- [x] `Financing.Tests` and `Architecture.Tests` pass.
- [x] Manual smoke test: register a debit account, a cash account, and a credit card (all via `POST /instruments`), then two card purchases straddling the cutoff boundary, confirm they land in different cycles via `api.http`.
- [x] Confirm the plan-persisted/outbox-written same-transaction guarantee is real (test or manual check).

### Completion notes

Completed 2026-08-30 (18-step plan `today-we-will-implement-playful-mitten.md`, one green-lightable step at a time).

- **Deviations from the task list above:**
  - **One expense account per card** (not per-purchase category): `CreateCreditCardHandler` provisions two Ledger accounts — `"{name} Liability"` (`Liability`/`CardLiability`) and `"{name} Purchases"` (`Expense`/`Expense`) — via `ILedgerApi.CreateAccountAsync`. `CreatePaymentPlanCommand` still carries no account ids; accrual/payment read them off `CreditCard`. Phase 3 has no category concept — revisit when categories land.
  - **`AddOutboxProcessing()` pulled forward from Phase 8** into `Program.cs` (right after `AddModules`). Financing is the first real Outbox producer and the DoD needs the `PaymentPlanCreated` same-transaction guarantee observable. `/health` is still the bare `MapGet` stub — Phase 8 still owns `MapHealthChecks`.
  - **`BillingCycle` persistence**: two plain `int` columns `CycleYear`/`CycleMonth` (backing fields on `Installment`, direct props on `MonthlyStatement`) — no `ValueConverter`, no `OwnsOne`. `Cycle` reconstructed in code.
  - **Architecture-test tool**: dependency-free reflection over `Assembly.GetReferencedAssemblies()` (not NetArchTest). Less expressive (no namespace/transitive rules); revisit if deeper rules are wanted.
  - **`GetInstallmentStatusAsync` / `GetCardFutureScheduleAsync` handlers** read base tables via LINQ through `IQueryBus`; `vw_card_future_schedule` exists for a future Reporting module (mirrors Phase 2).
- **Bug fixed in passing**: `LedgerOutboxStore` / `FinancingOutboxStore` ordered the drain batch by `OccurredOnUtc` (a `DateTimeOffset`) — SQLite cannot `ORDER BY` / `MIN` / `MAX` that type. Both now order by the autoincrement `Id` (correct FIFO order anyway). Latent since Phase 2; surfaced once `OutboxWorker` actually started.
- **DoD evidence (live smoke, 2026-08-30):**
  - `POST /v1/instruments` — `debit`/`cash`/`credit` all → 200 + id; `credit` without `cutoffDate` → 400 `Instruments.CutoffRequired`; unknown type → 400 `Instruments.UnknownType`.
  - Straddling the cutoff: purchase `2026-03-15` (cutoff day 15) → first installment cycle **2026-03**; purchase `2026-03-16` → first installment cycle **2026-04**. Different `(CycleYear, CycleMonth)`, confirmed via `future-schedule` and `SELECT ... FROM financing_installments`.
  - Same-transaction guarantee: two no-split plans → `financing_outbox_messages` count `0`; one split plan → count `1`. Host SQL log shows plan C's `SaveChangesAsync` batching the 3 installment `INSERT`s + the 1 `financing_outbox_messages` `INSERT` together.
  - `dotnet test --solution PersonalFinance.sln` → 20 passed / 0 failed / 0 skipped (Ledger 7 + Financing 11 + Architecture 2).
- **Left for later phases:** the `// TODO(Phase 4)` D12 seam in `PayStatementHandler` (net `CarriedCreditBalance` before posting); `PostDevAccount` (`POST /v1/ledger/accounts`, dev-only) still mapped — its `// TODO(Phase 3): remove` is now stale, safe to drop once nothing else depends on it; the `PaymentPlanCreated` Outbox row has no consumer yet (Parties, Phase 6).

---

## Phase 4 — Reversal Semantics: Implementing D12

**Goal:** Implement D12 (already documented in `docs/DESIGN.md`) — reversal must succeed even on accrued/paid installments, with a compensating entry that nets against the card's next statement, cascading correctly to Financing (and to Parties once it exists in Phase 6).

**Depends on:** Phase 2 (`ReverseTransactionCommand` skeleton) + Phase 3 (installment/statement state to query and correct, `CreditCard`'s credit-balance field).

### Tasks — Ledger + Financing implementation (Parties cascade deferred, seam only)
- [x] Extend `ReverseTransactionHandler` per D12: (a) always post the mirrored storno entry; (b) if `IFinancingApi` reports the installment was already paid via a statement, also post the compensating card-credit entry — **`Dr CardCredit{Card} / Cr CardLiability{Card}`**, never credit `Activo:Banco` directly (RNF-5); (c) call `IFinancingApi` to mark the installment's Financing-side state as reversed so it stops appearing in future-schedule/liability views, and to record the credit against that card. *Deviation from D12's worked example: the compensating credit leg is `Pasivo:Tarjeta`, not `Gasto:Categoría` — since the storno posts **always**, crediting Expense again would double-count it. See Phase 10 doc-sync.*
- [x] Extend `IFinancingApi` with the mutation needed by (c) — `MarkInstallmentReversedAsync(MarkInstallmentReversedCommand)` (`InstallmentId`, `CompensatingCreditPosted`, `CreditAmountMinorUnits`); handler flags `Installment.IsReversed` and, when `CompensatingCreditPosted`, `CreditCard.ApplyCredit(...)`. Implemented in `FinancingApi.cs`, registered in `FinancingModule`.
- [x] Extend `PayStatementHandler` (built in Phase 3) to subtract any carried-forward card credit from the amount due before posting — 3-leg `Dr CardLiability (due) / Cr Bank (remainder) / Cr CardCredit (credit applied)`, then `CreditCard.ConsumeCredit(...)`. Split-out pure helper `StatementPaymentCalculator`. This is what makes D12's auto-netting real (US-2 AC2).
- [x] Define `IPartiesApi`'s reversal-correction call site now — `// TODO(Phase 6): wire IPartiesApi.CorrectExpenseSplitAsync(...)` in `ReverseTransactionHandler` (fires only when the reversed transaction carried a `SplitReference`), plus a unit test asserting reversal succeeds *without* a party correction when no `SplitReference` is present.
- [x] Update the host reversal response shape — `ReverseTransactionResult(Guid ReversalTransactionId, bool CompensatingEntryPosted)` in `Ledger.Contracts`; `ReverseTransactionCommand : ICommand<ReverseTransactionResult>`; `ILedgerApi.ReverseTransactionAsync` + `Endpoints/Ledger/ReverseTransaction.cs` + `LedgerMappingExtensions` follow (D13).

### Tests
- [x] `Ledger.Tests` — new pure `ReversalCalculatorTests.cs` (7 cases): no installment ref → plain storno; split-only → party cascade flagged; unknown installment → plain storno; accrued-unpaid → `MarkReversed` without a compensating entry; accrued-**paid** → compensating `Dr CardCredit / Cr CardLiability` with amount + accounts; already-reversed → untouched; paid split → compensating entry **and** party cascade. `ReverseTransactionTests.cs` left as-is (pure-domain, never touches the new result type). Reversing a reversal still rejected (`Ledger.CannotReverseAReversal`, unchanged).
- [x] `Financing.Tests` — `InstallmentReversalTests.cs` (`MarkReversed` idempotency, `ApplyCredit`/`ConsumeCredit` arithmetic + zero-clamp) and `PayStatementNettingTests.cs` (no credit → 2-leg; partial → 3-leg netted; credit ≥ due → no bank leg, capped). The "reversed installment absent from `vw_card_future_schedule`/`GetCardLiabilityQuery`" assertion is covered by the E2E smoke below — the pure-domain test projects have no DB harness.
- [x] `Architecture.Tests` — `Ledger_module_sees_Financing_only_through_its_contracts_assembly` + `Ledger_contracts_assembly_references_no_Financing_assembly` (the new Ledger→`Financing.Contracts` edge is impl-only).

### Definition of done
- [x] `dotnet test --solution PersonalFinance.sln` fully green — **40 passed** (Ledger 14 + Financing 22 + Architecture 4).
- [x] E2E smoke via `PersonalFinance.Api.http` (live, 2026-08-30): plan → accrue → pay → reverse → `compensatingEntryPosted: true`, compensating `Dr CardCredit 120000 / Cr CardLiability 120000`, net `CardLiability` for the card = 0, `CarriedCreditBalance` = 120000, installment gone from `vw_card_future_schedule` and never re-accrues. Fresh plan on the same card (due 200000) → pay → `Dr CardLiability 200000 / Cr Bank 80000 / Cr CardCredit 120000`, `CarriedCreditBalance` → 0. Accrued-**unpaid** reversal → `compensatingEntryPosted: false`, plain storno, installment still `IsReversed = 1`.
- [x] The Parties-cascade TODO is tracked forward into Phase 6 (`ReverseTransactionHandler`), not silently dropped.

### Completion notes (2026-08-30)

Completed via the 10-step plan `today-we-will-implement-drifting-nautilus.md`, one green-lightable step at a time.

- **Compensating-entry shape — Option A (user-confirmed).** The storno is posted **always**; when the reversed accrual's installment was already **paid**, a second transaction `Dr CardCredit / Cr CardLiability` is posted. Storno + compensating net to D12's intended end state (Expense undone, no phantom `CardLiability`, no cash returned per RNF-5, a `CardCredit` **asset** carrying the credit forward). `docs/DESIGN.md` D12's worked example credits `Gasto:Categoría` on the compensating leg — that only works if the storno is *not* posted, which contradicts D12's own "storno always". **Recorded as a Phase 10 doc-sync item** (`Gasto:Categoría` → `Pasivo:Tarjeta`).
- **Cross-module consistency — no saga.** Ledger posts the storno (+ compensating), then calls `IFinancingApi.MarkInstallmentReversedAsync`. If that call fails, the handler **logs a warning and still returns success** (D12: "reversal always succeeds"). A failed step 3 leaves a logged inconsistency (storno posted, installment still active) — accepted limitation for this project, same class as D1/D10's existing cross-context calls.
- **New per-card `CardCredit` Ledger account.** `CreateCreditCardHandler` now provisions a **third** account (`"{name} Credit"`, `Asset`/`CardCredit`) alongside Liability + Purchases. `CreditCard.CreditAccountId` + migration `CreditCardCreditAccount` (`defaultValue: Guid.Empty` — the two pre-existing Phase 3 demo cards keep an empty credit account, harmless as long as they're never reversed-while-paid).
- **`Installment.IsReversed`** (migration `InstallmentReversalFlag`, default `0`) + `MarkReversed()` idempotency guard. `where !IsReversed` added to `GetCardFutureScheduleHandler`, `AccrueInstallments` (belt-and-suspenders), and `vw_card_future_schedule.sql` (migration `FinancingReadViewsReversal`, hand-authored `Up`/`Down`).
- **`InstallmentStatusResponse`** gained `Reversed`, `CardId`, `CardCreditAccountId`, `CardLiabilityAccountId`; `GetInstallmentStatusHandler` now joins `Installment → PaymentPlan → CreditCard`.
- **Ledger → Financing.Contracts** project reference added (impl `.csproj` only — no cycle: `Financing.Contracts` references only `Abstractions` + `SharedKernel`; `Ledger.Contracts` stays a leaf, pinned by `Architecture.Tests`).
- **Step 8 folded into Step 5** — changing `ReverseTransactionCommand` to `ICommand<ReverseTransactionResult>` breaks the host build until the endpoint + mapper compile against the new type, so the host response change couldn't be a separate step.
- **Deferred / follow-ups:** the Parties cascade (`ReverseTransactionHandler` `// TODO(Phase 6)`); reversing an entire multi-installment plan in one action (no phase owns it — Phase 10 / follow-up candidate); DESIGN.md D12 example doc-sync (Phase 10).

---

## Phase 5 — Subscriptions Module

**Goal:** Recurring charges that self-renew on a clock (US-5), no manual action required.

**Depends on:** Phase 2 (`ILedgerApi`) only — no dependency on Financing or Parties.

### Tasks — Contracts (`PersonalFinance.Subscriptions.Contracts`)
- [x] `ISubscriptionsApi.cs` — `CreateSubscriptionTemplateAsync` / `RenewSubscriptionAsync` (`Result<Guid>`), `CancelSubscriptionAsync` (`Result`), `GetActiveSubscriptionsAsync` (`ActiveSubscriptionsResponse`). No cross-module consumer yet; host endpoints use the buses directly.
- [x] `Commands/CreateSubscriptionTemplateCommand.cs` (`Name`, `AmountMinorUnits`, `Category`, `FundingAccountId`, `Frequency`, `AnchorDay`), `RenewSubscriptionCommand.cs` (`SubscriptionId`, `RenewedOnUtc`; result = posted txn id), `CancelSubscriptionCommand.cs`.
- [x] `Queries/GetActiveSubscriptionsQuery.cs` + `ActiveSubscriptionsResponse` / `ActiveSubscriptionRow`.
- [x] `RecurrenceFrequency` enum — `Monthly = 1` only; explicit seam for Weekly/Annually.
- [x] `IntegrationEvents/SubscriptionRenewedIntegrationEvent.cs` — direct/in-process dispatch (same reasoning as `InstallmentAccrued`). No handler registered in Phase 5 (dispatcher fans out to zero handlers).

### Tasks — Domain (`PersonalFinance.Subscriptions`)
- [x] `Domain/SubscriptionTemplate.cs` — `AggregateRoot<Guid>`: amount (`Money`), funding + expense account ids, category, `Frequency`/`AnchorDay`, `NextDueDate`, `IsActive`, `LastRenewalOnUtc`. `Create` validation table; `Renew` guards `IsActive` then advances via `Recurrence.Next`; `Cancel` idempotent, never touches past state (AC3).
- [x] `Domain/RecurrenceRule.cs` — `(Frequency, AnchorDay)` value object; `Create` bounds `anchorDay` 1–31; `Next(after)` = earliest anchor-day occurrence strictly after `after`, clamped to month length, rolls month/year.
- [x] `Domain/RenewalSchedule.cs` — `(NextDueDate, IsActive)`; `Advance(rule)` / `Deactivate()`. A code-side projection of the aggregate's columns — the scheduler checks the flag, never a delete (US-5 AC3).
- [x] `Domain/SubscriptionErrors.cs` — `InvalidName` / `InvalidCategory` / `NonPositiveAmount` / `InvalidAnchorDay` / `InvalidFundingAccount` / `SubscriptionNotFound` / `SubscriptionNotActive`.
- [x] `Application/SubscriptionChargeCalculator.cs` — pure; `Build(...)` → `Dr Expense / Cr Funding` tagged `SubscriptionReferenceId`. Shared by create + renew.
- [x] `Application/Commands/CreateSubscriptionTemplate/Handler.cs` + `Validator` — provisions the `{Name} Expense` Ledger account via `ILedgerApi`, posts the **first period charge synchronously** (charge is the commit point), then `SaveChanges`.
- [x] `Application/Commands/RenewSubscription/Handler.cs` + `Validator` — load → `SubscriptionNotFound` / `SubscriptionNotActive` → post the period charge via `ILedgerApi` → `template.Renew()` → `SaveChanges`; returns the posted txn id.
- [x] `Application/Commands/CancelSubscription/Handler.cs` + `Validator` — load → `SubscriptionNotFound` → `template.Cancel()` → `SaveChanges`.
- [x] `Application/Queries/GetActiveSubscriptions/Handler.cs` — `Where(IsActive).OrderBy(NextDueDate)` → `ActiveSubscriptionRow` projection.
- [x] `Application/Scheduling/RenewDueSubscriptions.cs` — `SchedulerBase`, interval 1 min, `RunOnStartup`; finds `IsActive && NextDueDate <= today`, sends `RenewSubscriptionCommand` **through `ICommandBus`** (not `ILedgerApi` — no new project ref), dispatches `SubscriptionRenewedIntegrationEvent` (direct mode) after each success. Idempotency: the date filter + the handler advancing `NextDueDate` in Subscriptions' own transaction; residual crash-window is the same accepted risk as `AccrueInstallments`.
- [x] `Infrastructure/Persistence/SubscriptionsDbContext.cs` (history table `__EFMigrationsHistory_Subscriptions`), `SubscriptionTemplateConfiguration`, `SubscriptionsDbContextFactory` + `DesignTimeSqliteConnectionFactory` + `ReadViewSqlHelper` copies, Outbox/Inbox config+store+writer (unused — kept for folder-tree symmetry and to keep `IOutboxStore` enumeration complete), migrations `InitialSubscriptionsSchema` + `SubscriptionsReadViews`.
- [x] `Infrastructure/Persistence/ReadViews/vw_active_subscriptions.sql` — embedded resource, created by the hand-authored `SubscriptionsReadViews` migration.
- [x] `Infrastructure/PublicApi/SubscriptionsApi.cs`, `SubscriptionsModule.cs`, DI wiring, host-owned `Endpoints/Subscriptions/{PostSubscription,DeleteSubscription,GetActiveSubscriptions}.cs` (`POST /v1/subscriptions`, `DELETE /v1/subscriptions/{id}`, `GET /v1/subscriptions/active`) + `ApiRoutes.Subscriptions` + DTOs + `SubscriptionMappingExtensions`.
- [x] Wire `RenewDueSubscriptions` into host startup — `AddHostedService<RenewDueSubscriptions>()` in `SubscriptionsModule.Register`, module added to `ModuleRegistration`.

### Tests
- [x] **Gap flag resolved — test project added** (repo owner approved). `tests/PersonalFinance.Subscriptions.Tests` (xUnit v3, pure-domain, mirrors `PersonalFinance.Financing.Tests`): `RecurrenceRuleTests` (anchor 15 before/on/after; 31st into non-leap Feb → 28, leap Feb → 29, 30-day month → 30; 29th leap vs non-leap; roll past a clamped Feb anchor; December → January year roll; `Create` rejects 0 / 32 / -1) and `SubscriptionTemplateTests` (`Create` validation table + name/category trim; `Renew` advances `NextDueDate` one period + stamps `LastRenewalOnUtc`; `Renew` after `Cancel` → `SubscriptionNotActive`, due date untouched; `Cancel` idempotent; cancelled reports `IsActive == false` — AC3). `RenewDueSubscriptions` idempotency is covered by the `api.http` E2E smoke (the pure-domain project has no DB harness, per the Phase 4 note).
- [x] Extend `ModuleIsolationTests.cs` to cover Subscriptions — `Subscriptions_module_sees_Ledger_only_through_its_contracts_assembly`, `Subscriptions_contracts_assembly_references_no_module_implementation`, `Subscriptions_module_does_not_reference_Financing_or_Parties`.

### Definition of done
- [x] Subscriptions builds, migrates, endpoints work via `api.http` — `dotnet build` 0W/0E; both migrations applied to `personalfinance.db` (`subscriptions_templates` / `subscriptions_outbox_messages` / `subscriptions_inbox_consumed` + `vw_active_subscriptions`); live smoke on `:5003` walked US-5 AC1/AC2/AC3 (see Completion notes).
- [x] Architecture-fitness test still passes — `dotnet test --solution` green, **75 passed** / 0 failed / 0 skipped (was 40 pre-Phase-5).
- [x] The test-project decision is implemented (see above), not recorded as a gap.

### Completion notes

**US-5 acceptance criteria walked (live smoke, host on `:5003`, `ASPNETCORE_ENVIRONMENT=Development`):**
- **AC1 — define once, charged on subscribe.** `POST /v1/instruments {type:"debit"}` → funding account; `POST /v1/subscriptions {Netflix, 1500, Streaming, Monthly, AnchorDay 15}` → the auto-provisioned `Netflix Expense` Ledger account reads **1500** immediately (first period posted synchronously inside the create handler). The Ledger transaction carries `SubscriptionReferenceId`; entries are `Dr Netflix Expense 1500 / Cr Checking 1500` (balanced). `GET /v1/subscriptions/active` returns the row with `NextDueDate = 2026-09-15` (next anchor occurrence strictly after today).
- **AC2 — every period the charge appears automatically.** A second subscription with `NextDueDate` hand-set into the past was renewed by `RenewDueSubscriptions` within one ~60 s tick: `NextDueDate` advanced one period, `LastRenewalOnUtc` stamped, the expense balance rose by a second charge, and a second Ledger transaction was tagged with the subscription reference. One period per tick — converges, same documented behaviour as `AccrueInstallments`.
- **AC3 — cancelling stops future renewals, never touches past charges.** `DELETE /v1/subscriptions/{id}` → `204`; `GET /active` → `{"rows":[]}`; `vw_active_subscriptions` → 0 rows; `subscriptions_templates.IsActive = 0`; the past charge's balance is unchanged. A second `DELETE` also returns `204` (idempotent `Cancel`).

**Deviations from `docs/DESIGN.md` §6 / plan predictions:**
- **Typed `SubscriptionReferenceId` on Ledger's `PostTransactionCommand`** (Phase-2 touch) — threaded into `Transaction` exactly like `InstallmentReference`; Ledger defines its own opaque `internal sealed record SubscriptionReference(Guid Value)`, **no** cross-module assembly edge. Migration `TransactionSubscriptionReference` adds one nullable `TEXT` column to `ledger_transactions`.
- **Charge-on-subscribe** — the create handler posts the first period synchronously (`PostedOnUtc = now`); the scheduler owns every subsequent period. Both paths post through `SubscriptionChargeCalculator.Build`.
- **`RecurrenceFrequency` enum has only `Monthly = 1`** — Weekly/Annually are a deliberate seam, not implemented.
- **Per-module file copies** — `DesignTimeSqliteConnectionFactory`, `ReadViewSqlHelper`, the Outbox/Inbox config+store+writer trio/pair are verbatim copies under the Subscriptions namespace (the established per-module duplication).
- **Scheduler routes through `ICommandBus`**, not `ILedgerApi` directly (unlike `AccrueInstallments`) — `RenewSubscriptionCommand` already exists on `ISubscriptionsApi`, so reusing it keeps one write path and adds no project reference.
- **`SubscriptionTemplate` private ctor takes `DateTimeOffset? lastRenewalOnUtc`** (nullable) so EF Core can bind it to the nullable `LastRenewalOnUtc` property — EF will not bind a non-nullable ctor param to a nullable property. Financing sidesteps this by keeping all nullable properties out of constructors.
- **Migrations live under `Infrastructure/Persistence/Migrations/`** (namespace `...Infrastructure.Persistence.Migrations`) — `dotnet ef migrations add` defaults to `<ProjectRoot>/Migrations/`, so both were regenerated with `--output-dir` to match the Ledger/Financing convention. `SubscriptionsReadViews` `Up`/`Down` hand-authored (`ReadViewSqlHelper.Load` / `DROP VIEW IF EXISTS`).
- **Scheduler residual crash-window accepted** — if the Ledger post commits but the template `SaveChanges` does not, the next tick re-charges. Documented, not engineered away (identical to `AccrueInstallments`).
- **`Frequency` parsed from the request string** at the host mapping layer via `Enum.Parse<RecurrenceFrequency>(..., ignoreCase: true)` — mirrors Financing's `DateOnly.Parse` (throws → consistent error envelope from the host exception handler).

**Left for later phases:** `SubscriptionRenewedIntegrationEvent` has no consumer yet; the Subscriptions Outbox/Inbox tables have no producer/consumer role (symmetry only). No Parties interaction (Subscriptions has no split concept).

---

## Phase 6 — Parties Module (incl. RF-3 Outbox/Inbox flow + reversal cascade)

**Goal:** Third-party debt tracking as a management view over Ledger receivables (D1). Closes the two seams left open earlier: Financing's `PaymentPlanCreated` → Parties Outbox/Inbox flow (D8), and Phase 4's deferred reversal cascade.

**Depends on:** Phase 2 (Ledger), Phase 3 (Financing's Outbox writer), Phase 4 (the reversal seam to close).

### Tasks — Contracts (`PersonalFinance.Parties.Contracts`)
- [x] `IPartiesApi.cs` — 7 methods: `CreatePartyAsync` / `RegisterSharedExpenseAsync` / `SettleCurrentAccountAsync` (`Result<Guid>`), `CorrectExpenseSplitAsync` / `RecordSplitAccrualAsync` (`Result`), `GetCurrentAccountBalanceAsync` / `GetCurrentAccountTimelineAsync` (typed responses). `CorrectExpenseSplitCommand` shape settled against what `ReverseTransactionHandler` needs: `SplitReferenceId`, `InstallmentReferenceId` (`Guid?`), `ReversedReceivableMinorUnits`, `CorrectedOnUtc`.
- [x] `Commands/CreatePartyCommand.cs`, `RegisterSharedExpenseCommand.cs`, `SettleCurrentAccountCommand.cs` — plus `CorrectExpenseSplitCommand.cs` (Phase 4 seam) and `RecordSplitAccrualCommand.cs` (split-aware accrual progress).
- [x] `Queries/GetCurrentAccountBalanceQuery.cs` — plus `GetCurrentAccountTimelineQuery.cs` (RF-7 timeline).
- [x] `IntegrationEvents/ExpenseSplitSettledIntegrationEvent.cs` — producer-only, no consumer wired in Phase 6 (symmetry + audit, same as Subscriptions).

### Tasks — Domain (`PersonalFinance.Parties`)
- [x] `Domain/Party.cs` — reference data only (`Name` + `ReceivableAccountId`), no auth/user account. `Party.Placeholder(id, receivableAccountId)` for the auto-created party on the async path.
- [x] `Domain/CurrentAccount.cs` — a management view, not a second ledger (D1): balance is a live read of the party's Ledger `Receivable` account, never stored.
- [x] `Domain/ExpenseSplit.cs` — allocation metadata (`Total`, `HolderShare`, `AccruedReceivable`, `ReversedReceivable`, participant shares), never a balance. N-way split via `SplitAllocationCalculator` (wraps `PhantomPennyAllocator`, holder = weight index 0). Also `ExpenseSplitParticipant`, `ExpenseSplitSource` (`Debit`/`CardPlan`), `PartyShare`.
- [x] `Application/Commands/CreateParty/Handler.cs` + `Validator` — provisions the party's Ledger `Receivable` account via `ILedgerApi.CreateAccountAsync`.
- [x] `Application/Commands/RegisterSharedExpense/Handler.cs` + `Validator` — synchronous debit/cash path; one multi-leg `Dr Gasto(ownShare) + Dr PorCobrar_k(share_k) … / Cr Banco(total)` via `ILedgerApi.PostTransactionAsync`, tagged `SplitReferenceId`. The end-to-end reversal-cascade proof.
- [x] `Application/Commands/SettleCurrentAccount/Handler.cs` + `Validator` — D4 `Dr Banco / Cr PorCobrar` (explicitly not income) via `ILedgerApi`; enqueues `ExpenseSplitSettledIntegrationEvent` on `PartiesOutboxWriter` in the same `SaveChangesAsync`.
- [x] `Application/Queries/GetCurrentAccountBalance/Handler.cs` — live `ILedgerApi.GetAccountBalanceAsync`; plus `GetCurrentAccountTimeline/Handler.cs` over `vw_current_account_timeline`.
- [x] `Application/EventHandlers/OnPaymentPlanCreated.cs` — the D8 consumer (first real Outbox consumer): dedupes via inbox `(MessageId, "Parties")`, auto-creates a placeholder `Party` + Ledger `Receivable` account per unknown participant, records the split **intent** as an `ExpenseSplit` (`Source=CardPlan`, `AccruedReceivable=0`), calls `IFinancingApi.LinkSplitAsync`. **No Ledger post** — the receivable becomes real cycle by cycle in `AccrueInstallments` (Option C, D11).
- [x] **Phase 4 seam closed**: `IPartiesApi.CorrectExpenseSplitAsync` → `CorrectExpenseSplitHandler` records `ExpenseSplit.RecordReversed(...)` + `SaveChanges`, **no Ledger post** (the storno's mirrored `Cr Receivable` already corrected the account — RNF-5). `// TODO(Phase 6)` removed from `ReverseTransactionHandler`; it now calls `parties.CorrectExpenseSplitAsync` when `decision.CorrectParty` (failure → `LogWarning`, reversal still succeeds).
- [x] `Infrastructure/Persistence/PartiesDbContext.cs` (`__EFMigrationsHistory_Parties`), configs for `Party` / `ExpenseSplit` / `ExpenseSplitParticipant` / keyless `CurrentAccountTimelineEntry`; `Outbox/PartiesOutboxConfig.cs` + store + writer (real producer — `ExpenseSplitSettled`); `Inbox/PartiesInboxConfig.cs` + store (real consumer — `PaymentPlanCreated`); migrations `20260831030734_InitialPartiesSchema` + `20260831030746_PartiesReadViews`.
- [x] `Infrastructure/Persistence/ReadViews/vw_current_account_timeline.sql` (RF-7 running balance, D4) — joins `parties_parties` to the Ledger-published `vw_receivable_account_movements` (D5-safe; Ledger migration `20260831030717_LedgerReceivableMovementsView`, added this phase).
- [x] `Infrastructure/PublicApi/PartiesApi.cs`, `PartiesModule.cs`, host DI (`ModuleRegistration`), host-owned `Endpoints/Parties/{PostParty,PostSharedExpense,PostSettlement,GetBalance,GetTimeline}.cs` — `POST /v1/parties`, `POST /v1/parties/shared-expenses`, `POST /v1/parties/{id}/settlements`, `GET /v1/parties/{id}/balance`, `GET /v1/parties/{id}/timeline` + `ApiRoutes.Parties` + DTOs + `PartyMappingExtensions`.

### Tests — `PersonalFinance.Parties.Tests`
- [x] `SplitAllocationCalculatorTests.cs` (the `PhantomPennyAllocatorTests` slot) — `AllocateWhole` + `AllocatePerInstallment`: `Σ(shares) == total` for random totals/weights, no negative shares, per-installment reconciliation exact, the ≤1-minor-unit grand-total drift is bounded and toward the holder (index 0), guard rejections.
- [x] `ExpenseSplitTests.cs` — `Create` / `RecordAccrued` / `RecordReversed` arithmetic + guards; the accrued-then-fully-reversed edge keeps both counters coherent.
- [x] `OnPaymentPlanCreatedTests.cs` — dedup via an **in-memory SQLite harness** (repo owner approved — `Parties.Tests` is the first EF-touching test project, a deliberate divergence from the Phases 2–5 pure-domain convention): same event delivered twice → exactly one `ExpenseSplit`, two `Party` rows, one inbox row, one `LinkSplitAsync` (fakes for `ILedgerApi` / `IFinancingApi`).
- [x] **Reversal-cascade (per D12/RNF-10)** — covered by the Step 18 live E2E smoke (debit-path reversal auto-correct + card-split paid-cycle reversal), not an in-process test: the cross-module cascade has no DB harness, same convention as Phases 4–5.

### Tests — Architecture
- [x] `ModuleIsolationTests.cs` extended — 8 Parties facts: module sees Ledger/Financing only via `.Contracts`; `Parties.Contracts` references no module impl; Ledger/Financing see Parties only via `Parties.Contracts`; Ledger/Financing `.Contracts` reference no Parties assembly; the Subscriptions negative assertions now also exclude `PersonalFinance.Parties*`.

### Definition of done
- [x] All `Parties.Tests` pass — `dotnet test --solution PersonalFinance.sln` → **110 passed** / 0 failed / 0 skipped (was 75 pre-Phase-6).
- [x] End-to-end smoke via `PersonalFinance.Api.http` (live, 2026-08-31, host on `:5003`) — both cascade paths proven; see Completion notes.
- [x] `OutboxHealthCheck` manually verified to go unhealthy if the Outbox Worker is paused while a `PaymentPlanCreated` message is pending (RNF-7). — **Closed in Phase 8 Step 8.** `MapHealthChecks("/health")` is now wired; with a pending `financing_outbox_messages` row `GET /health` returns `Degraded`/200 within `OutboxOptions.StalenessThreshold` (5 min) and `Unhealthy`/503 past it, then back to `Healthy`/200 once the row is drained. Full transcript in `src/Bootstrap/PersonalFinance.Api/PersonalFinance.Api.http` (`### RNF-7 health degradation`).

### Completion notes (2026-08-31)

Completed via the 19-step plan `today-we-will-implement-velvety-quilt.md`, one green-lightable step at a time. Build 0W/0E and `dotnet test --solution` green (75 → 110) after every step.

- **Option C (LLM-council-decided).** At `OnPaymentPlanCreated` time nothing is posted to the Ledger — the card-split receivable is **not** owed yet (D11: an un-accrued installment is not a liability). The consumer records the split *intent* (`ExpenseSplit`, `Source=CardPlan`, `AccruedReceivable=0`) and proves the Outbox→Inbox path; the receivable becomes real **cycle by cycle** in a split-aware `AccrueInstallments`, each accrual transaction tagged `SplitReferenceId` — exactly where Phase 4's locked reversal-cascade test expects the tag. Options A (reclassify now, on the wrong txn) and B (full accrual now + pre-mark installments, violates D11) were rejected.
- **Two split paths.** (1) **Synchronous debit/cash** — `RegisterSharedExpense` posts one multi-leg `Dr Gasto(ownShare) + Dr PorCobrar_k(share_k) … / Cr Banco(total)` via `ILedgerApi.PostTransactionAsync` (not N× `PostReceivableCommand` — that command is a fixed 3-leg shape), `SplitReferenceId` = `ExpenseSplit.Id`, `AccruedReceivable` set to the full party portion immediately. This is the end-to-end reversal-cascade proof. (2) **Async card-split** — `OnPaymentPlanCreated` → `LinkSplitAsync` → split-aware `AccrueInstallments` posts `Dr CardPurchases(holderShare) + Dr Receivable_k(partyShare_k) … / Cr CardLiability(installmentAmount)` per closed cycle, tagged `InstallmentReferenceId` **and** `SplitReferenceId`, then calls `IPartiesApi.RecordSplitAccrualAsync` so `ExpenseSplit.AccruedReceivable` tracks progress for the timeline.
- **Per-installment allocation** via `SplitAllocationCalculator` (wraps `PhantomPennyAllocator`, holder = weight index 0). `holderShare[i] + Σ partyShare[i] == installmentAmount[i]` **exactly** per installment; grand totals drift ≤1 minor unit per odd installment **toward the holder** (e.g. 3 × 100000 split 3-way → holder 33334 × 3, each party 33333 × 3). Documented, accepted.
- **`CorrectExpenseSplitAsync` never posts to the Ledger.** The storno's mirrored `Cr Receivable` leg already corrects the receivable account in every case (paid / unpaid / partial cycle). The handler only advances `ExpenseSplit.ReversedReceivable` metadata. A ledger post here would double-correct (RNF-5, same class as the D12 `Gasto:Categoría` drift). A failed `CorrectExpenseSplitAsync` from `ReverseTransactionHandler` logs a warning and the reversal still returns success (D12 "reversal always succeeds", no saga — same tolerance as the Phase 4 Financing cascade).
- **Cross-module coupling stays narrow** — one callback `IFinancingApi.LinkSplitAsync(planId, splitReferenceId, [(partyId, receivableAccountId)])`. Only opaque Ledger account guids + an opaque `SplitReferenceId` cross the boundary — same shape as Ledger's opaque `SubscriptionReference`. `PaymentPlan` gained `SplitReferenceId` (`Guid?`) + a `PaymentPlanSplitParticipant` child collection (migration `20260831040212_PaymentPlanSplitLink`); the participant weights are persisted at `CreatePaymentPlan` time now, not only emitted.
- **Ledger → `Parties.Contracts`** project reference added (impl `.csproj` only — mirrors the Phase 4 `Ledger → Financing.Contracts` edge; `Ledger.Contracts` stays a leaf, pinned by `Architecture.Tests`).
- **Auto-created placeholder `Party` on the async path** (DESIGN.md §6 deviation) — `OnPaymentPlanCreated` provisions a `Party.Placeholder(id, receivableAccountId)` + a Ledger `Receivable` account for any participant id it doesn't recognise; the split event is authoritative for "this party is involved". The synchronous path still requires the party to exist first.
- **Multi-leg `PostTransactionAsync` instead of N× `PostReceivableCommand`** (DESIGN.md §6 deviation) — `PostReceivableCommand` is a fixed 3-leg `Dr Expense + Dr Receivable / Cr Funding`; an N-party split needs explicit lines, so both split paths build the transaction leg-by-leg and pass `SplitReferenceId` directly.
- **`Parties.Tests` is the first EF-touching test project** (repo owner approved) — `OnPaymentPlanCreatedTests` uses an in-memory SQLite connection + real `PartiesDbContext` + fakes for `ILedgerApi` / `IFinancingApi`, a deliberate divergence from the Phases 2–5 pure-domain convention. `SplitAllocationCalculatorTests` / `ExpenseSplitTests` stay pure.
- **Bug fixed in passing (Step 18 live smoke, latent since Step 10).** `GetCurrentAccountTimelineHandler` did `.OrderBy(entry => entry.MovementOnUtc)` server-side → SQLite `NotSupportedException` ("no `ORDER BY` on `DateTimeOffset`"). Now the rows are projected + `ToListAsync`'d, then ordered client-side (LINQ stable sort; the view's window function already computes the running balance in `PostedOnUtc, e.Id` order, so the client sort is display-only). `GET /v1/parties/{id}/timeline` was dead on arrival before this.
- **`vw_current_account_timeline` label imprecision (accepted).** The view's `CASE` labels a card-plan accrual receivable leg `"Shared expense"` — it can only see the movement sign + `IsReversal`, not whether the leg came from a debit-path expense or an installment accrual. A precise `"Card installment"` label needs `InstallmentReferenceId` plumbed through the Ledger-owned `vw_receivable_account_movements` + `vw_current_account_timeline` + a 2-view migration. The money is 100% correct; label-only. Left for a follow-up.
- **DoD evidence (live smoke, 2026-08-31, host on `:5003`, `ASPNETCORE_ENVIRONMENT=Development`):**
  - **Debit path (US-6 AC3):** `POST /v1/parties` ×2 → `POST /v1/parties/shared-expenses` (total 9000, participant weight 1) → `GET .../balance` = **4500**, timeline has the `Shared expense` row. `POST /v1/ledger/transactions/{sharedExpenseTxn}/reversal` → `compensatingEntryPosted: false`, balance **auto-corrects 4500 → 0** (the storno's mirrored `Cr Receivable`), timeline gains a `Reversal` row (running 0). `parties_expense_splits.ReversedReceivableMinorUnits` 0 → 4500 while `AccruedReceivableMinorUnits` stays 4500 — metadata only, no Parties-side Ledger post.
  - **Card-split path:** `POST /v1/instruments {credit}` → `POST /v1/financing/payment-plans` (300000, 3 installments, Alice + Bob weight 1) → OutboxWorker drains → `OnPaymentPlanCreated` writes the `ExpenseSplit` + `LinkSplitAsync` populates `financing_payment_plans.SplitReferenceId` + 2 `financing_payment_plan_split_participants` rows. Balance stays **0** at plan creation (D11). `AccrueInstallments` accrues the 3 closed cycles → Alice & Bob balances **99999** each (per installment 100000 split 3-way = holder 33334 / each party 33333, ×3). `POST /v1/financing/statements/{id}/pay` → reverse a paid cycle's accrual txn → `compensatingEntryPosted: true`, Alice & Bob balances **99999 → 66666** (−33333 each), `ReversedReceivableMinorUnits` 0 → 66666. Timeline: 6 rows, correctly ordered.
  - **Dedup:** re-null `financing_outbox_messages.ProcessedOnUtc` + restart the host → still exactly one `parties_expense_splits` row and one `parties_inbox_consumed` row (`Consumer = "Parties"`).
  - `dotnet test --solution PersonalFinance.sln` → **110 passed** / 0 failed / 0 skipped.
- **Left for later phases:** `ExpenseSplitSettledIntegrationEvent` has no consumer (Ledger already posts the D4 entry synchronously in `SettleCurrentAccount`); negative `CurrentAccount` (holder owes the party after a settled slice is reversed) surfaces correctly as a signed balance but has no dedicated settlement flow; the `vw_current_account_timeline` `"Card installment"` label; ~~`OutboxHealthCheck` HTTP route (Phase 8)~~ **— done in Phase 8 Step 4/8**; `docs/DESIGN.md` D12 step-4 text should note "metadata-only, the storno corrects the ledger" (Phase 10 doc-sync).

---

## Phase 7 — Reporting Module

**Goal:** Read-only cross-module dashboards, touching nothing but `vw_*` views (D5/RNF-6).

**Depends on:** Phases 2, 3, 5, 6 (all views this phase queries must exist).

### Tasks
- [x] Create `PersonalFinance.Reporting` (`src/Reporting/PersonalFinance.Reporting/`) — **no reference to any module's Contracts or impl project**, only a raw SQLite connection to the shared file. csproj references `PersonalFinance.Abstractions` + `PersonalFinance.Infrastructure` only, plus `Microsoft.Data.Sqlite` direct.
- [x] `ReadDbConnectionFactory.cs` — `IReadDbConnectionFactory` + `internal sealed` impl; rewrites `SqliteOptions.ConnectionString` to `Mode=ReadOnly` via `SqliteConnectionStringBuilder`, applies `PRAGMA busy_timeout`. Plus `ReportingSqlHelper.Load(...)` for the embedded `Sql/*.sql`.
- [x] `Sql/monthly_expenses.sql`, `card_due_by_month.sql`, `current_account_timeline.sql`, `debt_by_party.sql` — embedded resources (`<EmbeddedResource Include="Sql/*.sql" />`).
- [x] `Dashboards/MonthlyExpensesQuery.cs` (RF-1/D9, queries `vw_ledger_monthly_expenses`; optional `$month` filter).
- [x] `Dashboards/CardDueByMonthQuery.cs` (RF-2/D11 — `UNION ALL` of `vw_card_liability_accrued` (`Accrued` bucket, `CycleYear/CycleMonth = NULL`) + `vw_card_future_schedule` (`Future` bucket); tagged buckets, **not** a row-level join — no shared key exists across the two view surfaces; still respects D5 since both are public view surfaces).
- [x] `Reports/CurrentAccountTimelineQuery.cs` (RF-7 — `GetPartyTimelineQuery`), `Reports/DebtByPartyQuery.cs` (`GetDebtByPartyQuery` — nets each party's movements to one row).
- [x] `ReportingModule.cs` — `IModule`, `Name => "Reporting"`; `Register` registers `IReadDbConnectionFactory` (singleton) + the four closed-generic `IQueryHandler<,>` (scoped); `MapEndpoints` is a documented no-op (HTTP is host-owned). Endpoints call the handlers through the shared `IQueryBus`.
- [x] Host endpoints (host-owned, not a module `ReportingEndpoints.cs`): `Endpoints/Reporting/{GetMonthlyExpenses,GetCardDueByMonth,GetPartyTimeline,GetDebtSummary}.cs` + `Endpoints/DTOs/ReportingDTOs.cs` + `Endpoints/Mapping/ReportingMappingExtensions.cs`; `ApiRoutes.Reporting` (`Base = V1 + "/reports"`); wired via `EndpointExtensions.MapReportingEndpoints()` + `ModuleRegistration.MapModuleEndpoints()`. Served under `/v1`: `GET /v1/reports/monthly-expenses`, `.../card-due-by-month`, `.../parties/{id:guid}/timeline`, `.../parties/debt-summary`.

### Tests
- [x] **Gap flag resolved — test project added** (repo owner approved). `tests/PersonalFinance.Reporting.Tests` — the repo's first true multi-context DI + migration integration harness: `ReportingIntegrationFixture` boots every module's `Register` + `AddSharedInfrastructure` against a throwaway SQLite file, migrates all four `DbContext`s (Ledger→Financing→Subscriptions→Parties), seeds through the write-side `.Contracts` (`ILedgerApi`/`IFinancingApi`/`IPartiesApi`), then asserts each Reporting query through `IQueryBus` (`MonthlyExpenses`, `CardDueByMonth`, `PartyTimeline`, `DebtByParty` — 4 facts). A deliberate divergence from the Phases 2–5 pure-domain convention, same class as Phase 6's EF-touching `OnPaymentPlanCreatedTests`.
- [x] Extend `ModuleIsolationTests.cs`: `Reporting_references_no_module_contracts_or_impl_assembly` — `ReferencedAssemblyNames(typeof(ReportingModule).Assembly)` contains none of the 8 module assemblies (`PersonalFinance.{Ledger,Financing,Subscriptions,Parties}` × {impl, `.Contracts`}). `Architecture.Tests` project gains a `<ProjectReference>` to `PersonalFinance.Reporting`.

### Definition of done
- [x] Reporting builds with zero compile-time dependency on any module — `rg "ProjectReference" src/Reporting/PersonalFinance.Reporting/PersonalFinance.Reporting.csproj` shows only `Abstractions` + `Infrastructure`; the new `ModuleIsolationTests` fact pins it.
- [x] Manual smoke test (live host, `PersonalFinance.Api.http` Phase-7 block, 2026-08-31): seeded a debit expense, a cash expense, a manual card accrual, a card future-schedule plan, and two party splits (one settled) — all four `/v1/reports/*` endpoints return HTTP 200 with cross-checked numbers. Subscription renewals need no dedicated seed: a subscription charge is a plain `Dr Expense` Ledger posting, already covered by `monthly-expenses`.
- [x] Architecture-fitness test passes with Reporting included — `dotnet test --solution PersonalFinance.sln` → **115 passed** / 0 failed / 0 skipped (was 110 pre-Phase-7: +1 architecture fact, +4 integration facts).

### Completion notes (2026-08-31)

Completed via the 11-step plan `today-we-will-implement-quizzical-gem.md`, one green-lightable step at a time. Build 0W/0E and `dotnet test --solution` green (110 → 115) after every step. No EF migration this phase — Reporting has no `DbContext` by design.

- **Reporting's shape deliberately differs from every prior module** (`docs/DESIGN.md` §6): single impl project, **no `.Contracts`, no `DbContext`/migration, no Outbox/Inbox**. Query records + handlers live in the impl project; the Bootstrap host (which already references every module impl to `new XModule()`) constructs the query records in its own endpoint handlers. This is the documented trade-off of a lean read-only leaf, not an omission.
- **Read path is raw ADO** (`SqliteConnection`/`SqliteCommand`/`DbDataReader`, no Dapper — none in the repo), against a connection opened in `Mode=ReadOnly`. Every `.sql` body references only `vw_*` names — verified by inspection (D5/RNF-6).
- **`ReadDbConnectionFactory` is Reporting-owned** (`docs/DESIGN.md` §6 names the file). A read-only handle is a real safety property for a read-only module — a write attempt fails at the driver.
- **`CardDueByMonthQuery` does not fake a join.** `vw_card_liability_accrued` is keyed by the Ledger liability account **name**; `vw_card_future_schedule` by the Financing `CardId` **GUID**. The query `UNION ALL`s them as tagged `Accrued` / `Future` buckets — no row-level correlation, because no shared key exists across the two public view surfaces.
- **`ReportingIntegrationFixture` design notes** (first multi-context harness): `Host.CreateApplicationBuilder()` + `FrameworkReference Microsoft.AspNetCore.App` (no `Directory.Packages.props` edits); discovers `DbContext` types by scanning `builder.Services` descriptors (all four are `internal` — resolved via non-generic `GetRequiredService(Type)` cast to the public `DbContext` base, no `InternalsVisibleTo`); migrates Ledger **before** Parties (`vw_current_account_timeline` joins the Ledger-published `vw_receivable_account_movements`); `host.Build()` but never `StartAsync()` so the registered schedulers never tick → deterministic; test config uses `JournalMode=DELETE` so no WAL sidecars fight the read-only connection in the single-threaded test.
- **Flagged for Phase 10 doc-sync (consumed as-is, NOT fixed here — fixing would mean Reporting knowing a module's internals):**
  - **RF-1 view leak:** `vw_ledger_monthly_expenses` filters `Type='Expense' AND Kind<>'Receivable'`, so per-card `*Purchases*` buckets (`Kind='Expense'`) appear in the "debit & cash" monthly summary that US-1 AC2 says should exclude credit. Confirmed live in the Phase-7 smoke (`P7 Card Purchases` 12000 shows up in `monthly-expenses`).
  - **RF-2 card-identity gap:** no shared join key between the Accrued bucket (Ledger account name) and the Future bucket (Financing `CardId` GUID). Sharper edge found in Step 10: the Future bucket's `CardId` surfaces **UPPERCASE** (raw EF/SQLite `TEXT`) while any id a client got from a POST response is lowercase — there isn't even a case-safe string key. A future Ledger view could surface the Financing `CardId` to close this.
  - **DebtByParty** omits zero-movement parties — `vw_current_account_timeline` inner-joins movements. Acceptable; noted.

---

## Phase 8 — Cross-Cutting: Host Polish for the Future Angular Client

**Goal:** Make the API genuinely consumable by the planned Angular client (D13) — CORS, consistent response/error envelope, OpenAPI, health check wiring. `POST /instruments` itself was already built in Phase 3; this phase is about the envelope/CORS/OpenAPI conventions wrapping every endpoint, including it.

**Depends on:** all module phases functionally complete.

### Tasks
- [x] API-wide error envelope — enriched RFC-9457 `ProblemDetails` (not a custom body): `Error.Code` → `extensions["code"]`, `Error.Message` → `detail`, `Error.Metadata` merged into extensions, HTTP status derived from the code by the pure `Endpoints/ErrorHttpStatusHelper.cs` (4 buckets: `*NotFound` → 404, state-conflict → 409, domain-validation → 422, else 400). One `Endpoints/ProblemResultsHelper.From(Error)`; `Handlers/GlobalExceptionHandler : IExceptionHandler` for the rest (`FormatException`/`ArgumentException` from mapping-layer `DateOnly.Parse`/`Enum.Parse` → 400 `Request.Malformed`, everything else → 500 `Server.Unhandled`, generic detail outside Development). `AddProblemDetails()` so framework 400/404/405/415 are enveloped too.
- [x] Audited every `Endpoints/**` file + `InstrumentsEndpoints.cs` — all command/DELETE handlers return `Results<T, ProblemHttpResult>` through `ProblemResultsHelper.From`; Reporting query handlers throw and are enveloped by `GlobalExceptionHandler` (no `Result` retrofit into the query pipeline).
- [x] `appsettings`-driven CORS — `Cors:AllowedOrigins` bound to `ClientCorsOptions`, named policy `"client"` (`http://localhost:4200`), empty list = no-op. `app.UseCors` between `UseHttpsRedirection` and endpoint mapping.
- [x] OpenAPI completeness — `OpenApi/ApiDocumentInfoTransformer` sets title/version/description; per-feature `.WithTags`; per-route `.WithSummary`/`.WithDescription`/`.Produces<T>`/`.ProducesProblem` (status codes derived from each handler's real `*Errors.*` usage, not guessed). `MapOpenApi()` un-gated from Development so CI / client codegen can pull `/openapi/v1.json`; Scalar UI stays dev-only.
- [x] `MapHealthChecks("/health")` + JSON `Endpoints/HealthCheckResponseWriterHelper` over the already-registered `OutboxHealthCheck` (replaces Phase 0's `MapGet` placeholder); default status→HTTP mapping kept (Healthy/Degraded → 200, Unhealthy → 503).
- [x] Endpoint testability intact — handlers unchanged; each module's own pure-domain test project still passes unchanged (115 → 126 with the new host project).

### Tests
- [x] `tests/PersonalFinance.Api.Tests` (`WebApplicationFactory<Program>` against a throwaway SQLite file, app hosted services stripped for determinism): `ErrorHttpStatusHelperTests` (pure `[Theory]`), `ErrorEnvelopeTests`, `HealthEndpointTests`, `CorsTests`, `OpenApiDocumentTests` — 11 cases, all green. `Program.cs` gained a trailing `public partial class Program { }`; `PersonalFinance.Api.csproj` an `InternalsVisibleTo` for the test project.

### Definition of done
- [x] All module endpoints return the consistent error envelope on failure — verified live per status class (422/404/409/400) in `PersonalFinance.Api.http` (`# Phase 8 — Host envelope`).
- [x] `/health` reflects Outbox staleness correctly — see the Phase 6 DoD line above (RNF-7 proof: `Degraded`/200 → `Unhealthy`/503 → `Healthy`/200).
- [x] OpenAPI spec is complete and importable — `/openapi/v1.json` validates; 19 paths in Production (20 in Development with the dev-only account route), per-route tags + summaries + response types present, `info.title` "PersonalFinance API".
- [x] CORS verified — preflight from `http://localhost:4200` echoes `Access-Control-Allow-Origin`; an unlisted origin gets no such header (recorded in the `.http` Phase 8 block; `curl -i` stands in for the browser `fetch()`).

### Completion notes (2026-09-01)

Completed via the 9-step plan `today-we-will-implement-lively-lightning.md`, one green-lightable step at a time. Build 0W/0E and `dotnet test --solution` green (115 → 126) after every step. No EF migration this phase — host-only.

- **Envelope = enriched RFC-9457 `ProblemDetails`, not a custom `ApiError` body** (the task's open choice). `ProblemResultsHelper.From(Error)` builds it; the HTTP status is a pure function of `Error.Code` in `Endpoints/ErrorHttpStatusHelper.cs` — an explicit `switch` over the ~48 catalogued codes with suffix-based fallbacks (`NotFound` → 404; `AlreadyPaid`/`AlreadyAccrued`/`AlreadyReversed`/`NotActive`/`CannotReverseAReversal`/`SettlementExceedsBalance` → 409; `Invalid*`/`NonPositive*`/known validation set → 422; else 400). This file is the source of truth for the mapping.
- **`GlobalExceptionHandler : IExceptionHandler`** — mapping-layer `FormatException`/`ArgumentException` (`DateOnly.Parse`, `Enum.Parse<RecurrenceFrequency>`) → 400 `Request.Malformed` with the real message (it is about input shape); anything else → 500 `Server.Unhandled`, generic detail outside Development, logged at `Error`. Writes through `TypedResults.Problem(...)` so the body matches the envelope exactly.
- **Every resource-creating `POST` returns `201 Created`** with `TypedResults.Created((string?)null, dto)` — **no `Location`** header (no canonical `GET /v1/<x>/{id}` routes exist yet; a dangling `Location` is worse than none — follow-up if item-GET routes land). `DELETE` stays 204, all `GET` stay 200. Dev-only `POST /v1/ledger/accounts` deliberately left at 200 (`TODO(Phase 3): remove`).
- **PUT audit (mid-phase, verification-only):** no endpoint misuses POST for a full-resource update. Every POST either mints a new aggregate/transaction or is a verb-suffixed action (`/statements/{id}/pay`, `/{id}/reversal`, `/{id}/settlements`) — RPC-style actions are conventionally POST, not PUT. Ledger being append-only (D3) means no PUT candidate exists there at all.
- **OpenAPI:** `.ProducesProblem(...)` status codes per route were derived by grepping which `*Errors.*` constant each command handler / validator / domain rule actually references — e.g. `DELETE /v1/subscriptions/{id}` documents only 404, because `SubscriptionNotActive` (409) is exclusive to the background `RenewSubscriptionCommand`, never reachable from `CancelSubscriptionCommand`.
- **`.NET 10 `Microsoft.OpenApi` v2 breaking change:** the `Microsoft.OpenApi.Models` namespace is gone — `OpenApiDocument` et al. moved to the `Microsoft.OpenApi` root namespace.
- **`dotnet run` launch-profile gotcha:** `Properties/launchSettings.json`'s `ASPNETCORE_ENVIRONMENT=Development` overrides an already-exported shell var of the same name unless `--no-launch-profile` is passed — matters for any host smoke that needs a genuinely non-Development environment (the Production OpenAPI path-count check).
- **`tests/PersonalFinance.Api.Tests`** (`WebApplicationFactory<Program>`): factory overrides `Sqlite:ConnectionString` to a throwaway temp file (also defeats the host's blank-string fallback to the shared `personalfinance.db`), migrates the four module contexts, and **removes the app's own `IHostedService`s** (accrual / renewal / `OutboxWorker`, matched by `PersonalFinance.*` impl namespace) so an HTTP test can't race a background tick. `InternalsVisibleTo` on the host project mirrors every module impl project's convention (needed for the pure `ErrorHttpStatusHelper` theory).
- **RNF-7 closed** (deferred Phase 6 DoD): `OutboxHealthCheck` has one knob — `OutboxOptions.StalenessThreshold`, default 5 min. Pending row within it → `Degraded`/200; past it → `Unhealthy`/503; drained → `Healthy`/200. Proven live and restored; transcript in `PersonalFinance.Api.http` (`### RNF-7 health degradation`).
- **Envelope JSON shape:** `code` / `status` serialize at the **root** of the `ProblemDetails` body (not under a nested `extensions` object) — `TypedResults.Problem(extensions:)` flattens extension members to the root.

---

## Phase 9 — CI

**Goal:** Automated build+test on every push — regressions in the architecture-fitness test or double-entry invariants must be caught immediately.

**Depends on:** at least Phase 2 complete; ideally run once Phase 8 is done for full test-matrix coverage, but can be pulled forward earlier.

### Tasks
- [x] `.github/workflows/ci.yml` running `dotnet restore` / `build` / `test` against the whole `.sln`.
- [x] Pin the CI runner's SDK to match `global.json` explicitly.
- [x] Ensure any file-based SQLite tests use a unique temp file per test run/worker (avoid CI concurrency collisions).

### Definition of done
- [x] A CI run on a clean checkout passes end-to-end with no manual setup beyond `global.json`/the workflow file.

### Completion notes (2026-09-01)

Completed via the 6-step plan `sprightly-foraging-duckling.md`, one green-lightable step at a time. Scope (user-chosen): minimal CI **+ reproducibility hardening**. No Docker in the per-push loop (LLM Council verdict 2026-09-01, `app/api/council/`).

- **`.github/workflows/ci.yml` lives at the git root** (`/home/ipoch/Documents/PersonalFinanceApp/.github/`), one level above the solution at `app/api/`. One `build-test` job on `ubuntu-latest`, `defaults.run.working-directory: app/api`, `timeout-minutes: 15`, `permissions: contents: read`, `concurrency` group per ref with `cancel-in-progress`. Steps: checkout → `actions/setup-dotnet@v4` (`global-json-file: app/api/global.json`, `cache: true`, `cache-dependency-path: app/api/**/packages.lock.json`) → `dotnet --info` → `dotnet restore PersonalFinance.sln --locked-mode` → `dotnet build PersonalFinance.sln --no-restore -c Release` → `dotnet test --solution PersonalFinance.sln --no-build -c Release`. `global-json-file` / `cache-dependency-path` are repo-root-relative because `defaults.run.working-directory` only affects `run:` steps, not `uses:` steps.
- **Task 2 (SDK pin):** `setup-dotnet` + `global-json-file` installs SDK `10.0.111` exactly; `dotnet --info` prints the resolved SDK into the run log for auditability.
- **Task 3 (unique temp SQLite per worker): already satisfied, no code change.** Only `PersonalFinance.Api.Tests`, `PersonalFinance.Reporting.Tests`, `PersonalFinance.Parties.Tests` touch SQLite; each uses a `Guid.CreateVersion7()`-named temp file (`pf-api-{guid}.db` / `pf-reporting-{guid}.db`) or `Filename=:memory:`, fresh per run. No test opens the shared `personalfinance.db`; no `xunit.runner.json`, no parallelism override.
- **Reproducibility hardening (scope add):** 20 `packages.lock.json` committed (13 src + 7 tests), generated with `dotnet restore PersonalFinance.sln --use-lock-file`. **Do NOT** add `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` to `Directory.Build.props` — it applies to the `PersonalFinance.sln` metaproject (no `TargetFramework`) and breaks `dotnet restore`/`build` on the solution (`Invalid framework identifier ''` / `NETSDK1013`). `Directory.Build.props` is unchanged. Once the lock files are committed, plain `dotnet restore` auto-honors them; CI enforces freshness with `--locked-mode`. Caveat: `setup-dotnet` `cache: true` fails the job if no `packages.lock.json` matches `cache-dependency-path` — the lock files and `ci.yml` must land in the same push.
- **Local dry-run (DoD proof):** `git archive HEAD` into a non-git dir, empty `NUGET_PACKAGES`, ran the workflow's exact sequence → SDK `10.0.111` resolved from `global.json`, `restore --locked-mode` exit 0 from a cold package cache, `build -c Release` 0W/0E, `test --solution … -c Release` → **126 passed / 0 failed / 0 skipped** (Architecture.Tests / RNF-9 among them).
- **Not exercised locally (CI-only, low risk):** `actions/setup-dotnet@v4` SDK install + its NuGet cache action on `ubuntu-latest`; Linux-x64 runner vs local arch — pure managed .NET 10 + SQLitePCLRaw bundled `linux-x64` native, no arch-specific code paths. First live validation is the first push to `chore/ci-pipeline`.
- **Optional deferred hedge (LLM Council, not started):** a separate, non-blocking `dotnet publish /t:PublishContainer` job (tag- or `workflow_dispatch`-triggered, no Dockerfile / registry / volume) to catch packaging regressions `dotnet test` can't — trimming/linker warnings, missing runtime assets, ICU/globalization, timezone data. Kept out of the per-push loop; full containerization is a separate later track judged on learning value, not CI value.
- **Not committed by this session** — the user commits all Phase 9 work (`c283521` = the 20 lock files + a 2-line `Program.cs` trim; `81c3328` = `ci.yml`, were the user's own commits made during the build).

---

## Phase 10 — Fase-1 PRD Acceptance Pass

**Goal:** Explicit checkpoint against PRD §5's Fase 1 scope (US-1, US-2, US-3, US-4, US-6) before considering the build product-complete for that scope.

**Depends on:** Phases 2, 3, 4, 7, 8.

### Tasks
- [x] Walk each Fase-1 user story's acceptance criteria (PRD §6) against the running API via `api.http`, recording request/response pairs as manual acceptance evidence.
- [x] Confirm PRD §8's utility-metric candidates are checkable in principle against seeded test data (real bank reconciliation itself requires real usage over time, out of scope for this checkpoint).
- [x] Revisit D12's netting behavior and D13's `POST /instruments` routing one more time now that the full Fase-1 flow works end-to-end, to catch any follow-on inconsistency integration surfaced that Phases 2–4 didn't.
- [x] **Doc-sync from Phase 4:** correct `docs/DESIGN.md` D12's worked example — the compensating entry's credit leg is `Pasivo:Tarjeta` (`Cr CardLiability`), not `Gasto:Categoría`. Phase 4 posts the storno **always**, so crediting `Gasto` again would double-count the expense; the implementation (`ReverseTransactionHandler` + `ReversalCalculator`) uses `Dr CardCredit / Cr CardLiability`.
- [x] **Doc-sync from Phase 7 (RF-1 view leak):** `vw_ledger_monthly_expenses` (`Type='Expense' AND Kind<>'Receivable'`) leaks per-card `*Purchases*` buckets (`Kind='Expense'`) into RF-1's "debit & cash" monthly summary, which US-1 AC2 says should exclude credit. **Fixed in code** — dedicated `AccountKind.CardPurchases`, view excludes it; doc reconciled.
- [x] **Doc-sync from Phase 7 (RF-2 card-identity gap):** `vw_card_liability_accrued` is keyed by the Ledger liability account **name**, `vw_card_future_schedule` by the Financing `CardId` **GUID** (which surfaces UPPERCASE from raw EF/SQLite `TEXT` while a client's POST-response id is lowercase). No shared, case-safe join key exists, so `card-due-by-month` can only present tagged `Accrued`/`Future` buckets, never a per-card row join. **Fixed in code** — `OwnerReferenceId` seam carries the Financing `CardId` onto the Ledger account; `vw_card_liability_accrued` surfaces it `lower()`-cased; `card_due_by_month.sql` carries it on both `UNION` halves.

### Definition of done
- [x] Every Fase-1 acceptance criterion in PRD §6 has a corresponding passing manual/automated check.
- [x] D12 and D13's behavior in code matches what `docs/DESIGN.md` describes — no drift between doc and implementation.

### Completion notes (2026-09-02)

Completed via the 8-step plan `today-we-will-implement-zazzy-prism.md`, one green-lightable step at a time. Per the user's decision the two Phase-7 carry-forwards were **fixed in code**, not just documented. `dotnet build PersonalFinance.sln` 0W/0E; `dotnet test --solution` → **127 passed / 0 failed / 0 skipped** (126 baseline + 1 new Reporting assertion; `PersonalFinance.Architecture.Tests` / RNF-9 green). No commits — the user commits.

- **RF-1 fix — `AccountKind.CardPurchases`.** New enum member (in the existing `Ledger.Contracts` assembly — no new module edge); `Account.kindMatchesType` maps it to `AccountType.Expense`; `CreateCreditCardHandler` provisions each card's `"{name} Purchases"` account as `CardPurchases` instead of `Expense`. `vw_ledger_monthly_expenses` now filters `Type = 'Expense' AND Kind NOT IN ('Receivable', 'CardPurchases')`. Hand-authored view-rebuild migration `20260902011354_LedgerMonthlyExpensesExcludeCardPurchases` (EF emits an empty model diff for a view-only change). Subscriptions' `"{name} Expense"` account stays `Kind='Expense'` — a real own-expense, correctly still counted.
- **RF-2 fix — `OwnerReferenceId` opaque-id seam.** `Guid? OwnerReferenceId` added to `CreateAccountCommand` (trailing optional param — every existing call site compiles untouched) and the `Account` aggregate (set via object initializer, never a ctor param — sidesteps the Phase-5 nullable-ctor-binding gotcha). `CreateCreditCardHandler` mints one `cardId = Guid.CreateVersion7()`, passes `OwnerReferenceId: cardId` on all three provisioned accounts and `CreditCard.Create(cardId, …)` (the `Create` factory gained an explicit leading `Guid id`). `vw_card_liability_accrued` surfaces `lower(a.OwnerReferenceId) AS CardId`; `card_due_by_month.sql` carries `lower(CardId) AS CardId` on both `UNION` halves. Migration `20260902012215_AccountOwnerReference` (`AddColumn` first, then the view rebuild). `GET /v1/reports/card-due-by-month` response gains a nullable `cardId` field — technically breaking, acceptable (no released client). No new assembly edge (bare `Guid?` in `Ledger.Contracts`, already referenced by Financing).
- **Doc-sync — `docs/DESIGN.md`** (5 edits): D12 paso 2 worked example `Cr Gasto:Categoría` → `Cr Pasivo:Tarjeta (Visa)` + prose (the storno always posts, so re-crediting `Gasto` would double-count); D12 paso 4 — the Parties correction is metadata-only (`ExpenseSplit.ReversedReceivable`, RNF-5, no ledger post — the storno's mirrored `Cr PorCobrar` already corrected the balance); D9/RF-1 new paragraph (card spend isolated by `AccountKind.CardPurchases`, not naming convention); D11/RF-2 new paragraph (`OwnerReferenceId` shared per-card key across the Accrued/Future buckets); §3 RF table RF-1/RF-2 `Estado` cells annotated. D10 row left untouched (already marked superseded).
- **Acceptance evidence — `PersonalFinance.Api.http`** (two new `# Phase 10` blocks, ~700 lines, real captured `curl -sk` responses + inline `sqlite3` checks against a fresh DB):
  - **Step 5 — D12/D13 re-verification.** `POST /instruments` routing (debit/cash/credit 201; credit-no-cutoff → 422 `Instruments.CutoffRequired`; unknown → 400 `Instruments.UnknownType`; provisioned accounts carry `OwnerReferenceId`). D12 cascade on a paid split installment: `compensatingEntryPosted: true`, compensating `Dr CardCredit / Cr CardLiability` (not `Cr Gasto` — the doc-sync proof, live), carried-credit netting in both the 2-leg (credit == due) and 3-leg (credit < due) forms.
  - **Step 6 — Fase-1 acceptance walk (PRD §6).** US-1 (monthly-expenses: current + prior month, débito + efectivo in one summary, third-party receivable excluded, per-category rows, RF-1 leak gone); US-2 (card-due-by-month: per-card accrued total, only closed cycles, cutoff-driven cycle assignment for the same purchase date on two cards, Future bucket separate, RF-2 shared `cardId`); US-3 (payment-plans + N-way split: body fields, cutoff resolution, phantom-penny split sums exactly — 33334+33333+33333 = 100000, per-participant share queryable); US-4 (statement pay: liability drops to 0, paid vs pending distinguishable); US-6 (reversal of a *paid* split installment: not blocked, storno not delete, party debt corrected same call, balances + carried credit reflect it, storno visible with timestamp).
- **~~KNOWN FASE-1 GAP — US-4 AC1~~ → CLOSED (2026-09-02, Phase 11).** "Ver, antes de pagar, el detalle de qué cuotas componen el total a pagar de una tarjeta ese mes." Closed by `GET /v1/financing/statements/{id}` + one query — see Phase 11 below. No remaining Fase-1 acceptance gap.
- **PRD §8.** The card-balance metric (system's card balance matches the bank's statement) and the party-balance metric (matches what the person acknowledges) are structurally checkable now — `GET /accounts/{cardLiability}/balance` + the Accrued bucket equal exactly the closed cycles the bank will bill (D11 temporal boundary); `GET /parties/{id}/balance` + timeline is a live read of the Receivable account. The "replaces the prior method for 4+ weeks" metric needs real longitudinal use — out of scope for the checkpoint. The learning metrics (§8) are met by the suite.
- **Not committed by this session** — the user commits all Phase 10 work. Branch `task/api-acceptance`.

---

## Phase 11 — US-4 AC1 Statement Detail

**Goal:** Close the one open Fase-1 acceptance gap from Phase 10 — US-4 AC1: "Puedo ver, antes de pagar, el detalle de qué cuotas componen el total a pagar de una tarjeta ese mes." Give a read endpoint that itemizes the accrued installments composing a `MonthlyStatement`'s `AmountDue`.

**Depends on:** Phase 3 (Financing: `MonthlyStatement` / `Installment` / `PaymentPlan`), Phase 8 (host error envelope).

### Tasks
- [x] `GetMonthlyStatementQuery` contract (row + response with a leading `bool Found` + query) in `PersonalFinance.Financing.Contracts`.
- [x] `GetMonthlyStatementHandler` (query `Installment.StatementId == id` joined to `PaymentPlans`; client-side sort; card name from `CreditCards`) + DI registration in `FinancingModule`.
- [x] Host route `GET /v1/financing/statements/{id}` — endpoint, DTO, mapping extension, `ApiRoutes.Financing.Statement`, wiring + OpenAPI metadata (`200` + `404`).
- [x] WAF regression test (`StatementDetailTests` — 404 contract + OpenAPI presence).
- [x] Live E2E walk + `PersonalFinance.Api.http` (real captured run; `KNOWN FASE-1 GAP` note replaced with `GAP CLOSED`).

### Definition of done
- [x] `GET /v1/financing/statements/{id}` returns the statement (cycle, amount due, paid state) plus one row per accrued installment (`plan`, cuota N de M, purchase date, cycle, amount, `isReversed`); `Σ` non-reversed rows `== amountDueMinorUnits`.
- [x] Unknown id → RFC-9457 `404` with `code` `Financing.StatementNotFound`.
- [x] `dotnet build PersonalFinance.sln` 0W/0E; `dotnet test --solution` green; `PersonalFinance.Architecture.Tests` (RNF-9) green — no new cross-module edge.

### Completion notes (2026-09-02)

Completed via the 7-step plan `effervescent-imagining-noodle.md`, one green-lightable step at a time. Pure additive CQRS query following the `GetCardFutureSchedule` precedent — no schema change, no EF migration, no `IFinancingApi` change (host GET calls `IQueryBus` directly). `dotnet test --solution` → **129 passed / 0 failed / 0 skipped** (127 baseline + 2 new `PersonalFinance.Api.Tests`); Architecture.Tests green.

- **Contract — `GetMonthlyStatementQuery.cs`** (`…Financing.Contracts/Queries/`). `MonthlyStatementInstallmentRow(PlanId, InstallmentId, Sequence, InstallmentCount, PurchaseDate, CycleYear, CycleMonth, AmountMinorUnits, IsReversed)`; `MonthlyStatementDetailResponse(bool Found, StatementId, CardId, CardName, CycleYear, CycleMonth, AmountDueMinorUnits, IsPaid, PaidOnUtc, IReadOnlyList<…Row>)` — leading `Found` bool is the not-found transport (precedent: `InstallmentStatusResponse`); `GetMonthlyStatementQuery(Guid StatementId) : IQuery<…Response>`.
- **Handler — `GetMonthlyStatementHandler.cs`** (`Application/Queries/GetMonthlyStatement/`). Projects the statement scalars by id → `FirstOrDefaultAsync`; null → `Found: false` empty response. Card name via a scalar `CreditCards` projection (`?? ""`). Installments: `where Installment.StatementId == query.StatementId` joined to `PaymentPlans` (for `PlanId` / `InstallmentCount` / `PurchaseDate`), `ToListAsync` then **client-side** `OrderBy(PurchaseDate).ThenBy(Sequence)` (SQLite `DateTimeOffset`-orderby gotcha avoidance / Parties-timeline precedent). Reversed installments are included, flagged `IsReversed`. `StatementId` is set only at accrual (`Installment.MarkAccrued`), so `where StatementId == id` is inherently exactly the accrued cuotas behind `AmountDue` — the D11 temporal boundary needs no special filtering. Registered in `FinancingModule.Register` next to the other two query handlers.
- **Host — `Endpoints/Financing/GetStatement.cs`.** `Results<Ok<MonthlyStatementDetailDto>, ProblemHttpResult>`; `!Found` → `ProblemResultsHelper.From(new Error("Financing.StatementNotFound", "The referenced monthly statement was not found."))` — the code string is already mapped → 404 in `ErrorHttpStatusHelper` (`FinancingErrors` is `internal` to the module, so the host builds the `Error` inline). First host GET that 404s; matches `POST .../statements/{id}/pay` on the same resource. `MonthlyStatementDetailDTO.cs` (`PurchaseDate` serialized as `string` ISO `yyyy-MM-dd`) — the client-facing DTO deliberately omits `Found` (always `true` on a 200; a dead field). `FinancingMappingExtensions.ToMonthlyStatementDetailDto` (individual `this`-param form — new receiver type). `ApiRoutes.Financing.Statement = "/statements/{id:guid}"`. Wired in `EndpointExtensions.MapFinancingEndpoints()` with `.Produces<T>(200)` + `.ProducesProblem(404)`.
- **Tests — `tests/PersonalFinance.Api.Tests/StatementDetailTests.cs`** (2 facts): unknown id → `404` + `code` `Financing.StatementNotFound`; `/openapi/v1.json` advertises `/v1/financing/statements/{id}` under the `Financing` tag with `200` + `404`. The happy-path (accrue → assert `Σ rows == AmountDue`) is covered by the live `.http` walk instead — `AccrueInstallments` is an `internal sealed` scheduler stripped from the test host with no command-bus hook, so a WAF happy-path would need scheduler-timing polling (the flaky-integration style this repo avoids; Financing tests are pure-domain).
- **Acceptance evidence — `PersonalFinance.Api.http`.** US-4 block: the old AC1 "closest view" workaround (`GET /ledger/accounts/{cardLiability}/balance`) replaced with the real endpoint walk — captured `curl -sk` JSON for a two-plan / one-cycle statement (`Σ` 30000 + 20000 == `amountDueMinorUnits` 50000), the before/after-pay pair (`isPaid` false → true, `paidOnUtc` populated), and the `404`. The `KNOWN FASE-1 GAP — US-4 AC1` block is now `FASE-1 GAP CLOSED (2026-09-02, Phase 11)`. Walked live against the real host on an isolated DB (the shared `personalfinance.db` untouched).
- **Not committed by this session** — the user commits all Phase 11 work. Branch `task/api-acceptance`.

---

## Phase 12 — Client-Driven Read Endpoints (client gaps 4.2–4.4 + D20 enabler)

**Goal:** Add the read-side endpoints the Angular client needs to stop faking data. `app/client/.claude/TASK.md` Phase 4 flags four API gaps; three are real (4.2 no instrument list, 4.3 no per-card statement list, 4.4 no transaction feed), and 4.4 also blocks client D20 (per-row Reverse buttons). Auth (4.5) is out of scope — no design exists.

**Depends on:** Phase 2 (Ledger `Account`), Phase 3 (Financing `CreditCard` / `MonthlyStatement`), Phase 6 (Parties timeline view), Phase 8 (host error envelope + OpenAPI metadata).

**Shape:** each endpoint is a pure additive CQRS query on the `GetCardFutureSchedule` / `GetMonthlyStatement` precedent — no schema change, no EF migration, `.Contracts`-only facade additions so `Architecture.Tests` (RNF-9) stays green. Built one green-lightable step at a time alongside the matching Angular wiring (client-side steps live in the client repo).

### Tasks
- [x] **Step 1 — `GET /v1/instruments`.** Ledger `ListInstrumentAccountsQuery` (rows for `Account.Kind IN (Bank, Cash)`, name-ordered) + handler + `ILedgerApi.ListInstrumentAccountsAsync` + `LedgerModule` reg. Financing `ListCreditCardsQuery` (every `CreditCard`) + handler + `IFinancingApi.ListCreditCardsAsync` + `FinancingModule` reg. Host `Endpoints/GetInstruments.cs` fans to both, `InstrumentMappingExtensions.ToInstrumentsListDto` merges (`Bank→debit` / `Cash→cash`; credit rows carry `cutoffDate`). `ApiRoutes.Instruments.List = "/"`; wired with `.Produces<InstrumentsListDto>(200)`. Test `InstrumentsListTests.cs` (2 facts: POST debit+credit → GET asserts type/name/cutoffDate; OpenAPI presence under `Instruments` + `200`).
- [x] **Step 3 — `GET /v1/financing/cards/{id}/statements`.** Summary rows (no installments); unknown card → `200` empty (sibling consistency with `/cards/{id}/future-schedule`).
- [x] **Step 5 — `GET /v1/ledger/transactions`.** Ledger-owned feed (`Transaction` has no `Description` column — synthesize a label); optional `accountId` / `from` / `to` filters; newest-first, client-side ordered.
- [x] **Step 7 — reversible transaction id into the timeline + statement-installment rows (D20 enabler).** `vw_current_account_timeline` gains `m.TransactionId` (hand-authored Parties view-rebuild migration); a batched `ILedgerApi` accrual-tx lookup feeds a nullable `reversalTransactionId` onto each statement-installment row.

### Definition of done
- [ ] Each endpoint returns the documented shape; unknown-id behavior matches its sibling (`200` empty, not `404`, for the list endpoints).
- [ ] `dotnet build PersonalFinance.sln` 0W/0E; `dotnet test --solution` green; `PersonalFinance.Architecture.Tests` (RNF-9) green — no new cross-module impl edge (facade additions are `.Contracts`-only).
- [ ] Happy paths that need the accrual scheduler (stripped from the WAF host) are captured in the `PersonalFinance.Api.http` walk instead of a flaky WAF test (Phase 11 precedent).

### Completion notes (2026-09-03 —, in progress)

Built via the 8-step plan `today-we-will-implement-dynamic-pizza.md` (4 API steps here, 4 client steps in the client repo), one green light per step. The user commits.

- **Step 1 — `GET /v1/instruments` (done).** `dotnet test --solution` → **131 passed / 0 failed** (129 baseline + 2 new `PersonalFinance.Api.Tests`); Architecture.Tests green.
  - **Ledger** — `PersonalFinance.Ledger.Contracts/Queries/ListInstrumentAccountsQuery.cs` (`InstrumentAccountRow(Guid AccountId, string Name, string Kind)`, `InstrumentAccountsResponse(rows)`, `ListInstrumentAccountsQuery() : IQuery<…Response>`). Handler `Application/Queries/ListInstrumentAccounts/ListInstrumentAccountsHandler.cs` — `context.Accounts.Where(Kind == Bank || Kind == Cash)` → project → `ToListAsync` → `OrderBy(Name, OrdinalIgnoreCase)` client-side → `Kind.ToString()`. `ILedgerApi.ListInstrumentAccountsAsync` + `LedgerApi` impl (query-bus passthrough) + `LedgerModule.Register`.
  - **Financing** — `PersonalFinance.Financing.Contracts/Queries/ListCreditCardsQuery.cs` (`CreditCardRow(Guid CardId, string Name, int CutoffDay)`, `ListCreditCardsResponse(rows)`, `ListCreditCardsQuery()`). Handler `Application/Queries/ListCreditCards/ListCreditCardsHandler.cs` — `context.CreditCards` project → `ToListAsync` → name-ordered. `IFinancingApi.ListCreditCardsAsync` + `FinancingApi` impl + `FinancingModule.Register`.
  - **Host** — `Endpoints/GetInstruments.cs` (`static class GetInstruments`, `Handle(ILedgerApi, IFinancingApi, CancellationToken)` → `Ok<InstrumentsListDto>`) awaits both facade calls, merges via `accounts.ToInstrumentsListDto(cards)`. `Endpoints/DTOs/InstrumentsListDTO.cs` — `InstrumentRowDto(Guid Id, string Type, string Name, int? CutoffDate)`, `InstrumentsListDto(IReadOnlyList<…> Rows)`. `Endpoints/Mapping/InstrumentMappingExtensions.cs` — `ToInstrumentsListDto` (new receiver → individual `this`-form, sits beside `ToInstrumentCreatedDto` on `Guid`), private `toInstrumentType` (`Bank→debit`, `Cash→cash`, else `ToLowerInvariant()`); credit rows `("credit", …, card.CutoffDay)`, debit/cash rows `CutoffDate = null`. `ApiRoutes.Instruments.List = "/"` (same value as `Create` — GET vs POST on `Base`). Wired in `EndpointExtensions.MapInstrumentsEndpoints()` with `.WithSummary`/`.WithDescription`/`.Produces<InstrumentsListDto>(200)`.
  - **Cross-module** — the two new facade members are `.Contracts`-only (no impl-assembly reference added), so RNF-9 is untouched. `OnPaymentPlanCreatedTests` fakes (`FakeLedgerApi` / `FakeFinancingApi`) gained the new members as `throw new NotSupportedException()`.
  - **Tests — `tests/PersonalFinance.Api.Tests/InstrumentsListTests.cs`** (2 facts): POST `debit` + `credit` (cutoff 15) → `GET /v1/instruments` asserts the debit row (`type == "debit"`, `cutoffDate` null) and the credit row (`type == "credit"`, `cutoffDate == 15`); `/openapi/v1.json` advertises `/v1/instruments` `get` under the `Instruments` tag with `200`. Happy path is WAF-testable here (no scheduler needed — both writes are synchronous).
  - **Doc note** — the plan's "DESIGN §9/§11" refers to the *client* `docs/DESIGN.md` section layout; `app/api/docs/DESIGN.md` has no §9/§11, so (per the Phase 11 precedent for a pure additive read endpoint) the API-side doc update is this `TASK.md` entry + the `CLAUDE.md` "Current state" Phase 12 bullet only.
  - **Drive-by cleanup** — `IModule.MapEndpoints(IEndpointRouteBuilder)` and its five empty module overrides were dead since Phase 8 (all HTTP is host-owned). Removed the interface member, the five `MapEndpoints` bodies, the now-orphaned `using Microsoft.AspNetCore.Routing;` in each module, and the `foreach(... module.MapEndpoints(endpoints))` loop in `ModuleRegistration.MapModuleEndpoints` (the explicit `endpoints.Map{Feature}Endpoints()` calls stay). `IModule` is now `Name` + `Register` only.
- **Step 3 — `GET /v1/financing/cards/{id}/statements` (done).** `dotnet test --solution` → **133 passed / 0 failed** (131 + 2 new `PersonalFinance.Api.Tests`); `dotnet build` 0W/0E; Architecture.Tests green.
  - **Financing** — `PersonalFinance.Financing.Contracts/Queries/GetCardStatementsQuery.cs` (`CardStatementRow(Guid StatementId, Guid CardId, string CardName, int CycleYear, int CycleMonth, long AmountDueMinorUnits, bool IsPaid, DateTimeOffset? PaidOnUtc)`, `CardStatementsResponse(rows)`, `GetCardStatementsQuery(Guid CardId) : IQuery<…Response>`). Handler `Application/Queries/GetCardStatements/GetCardStatementsHandler.cs` — one `CreditCards` name lookup (`?? ""` when the card is unknown), `context.MonthlyStatements.Where(CardId == query.CardId)` projected to an anonymous type (incl. the `HasConversion`-mapped `AmountDue` `Money`) → `ToListAsync` → `OrderBy(CycleYear).ThenBy(CycleMonth)` client-side (idiom carried from the siblings; ordering is on ints, so the `DateTimeOffset` gotcha doesn't bite here). Unknown card → empty `rows` (no `404`). Registered in `FinancingModule.Register` (`GetCardStatements` using + `AddScoped<IQueryHandler<GetCardStatementsQuery, CardStatementsResponse>, GetCardStatementsHandler>()`).
  - **Host** — `Endpoints/Financing/GetCardStatements.cs` (`static class GetCardStatements`, `Handle(Guid id, IQueryBus, CancellationToken)` → `Ok<CardStatementsDto>`) dispatches `GetCardStatementsQuery` via `IQueryBus.AskAsync` and maps with `statements.ToCardStatementsDto(id)` — same shape as the sibling `GetCardFutureSchedule`, **no `IFinancingApi` edge** (query + handler live inside Financing + host). `Endpoints/DTOs/CardStatementsDTO.cs` — `CardStatementRowDto(...)`, `CardStatementsDto(Guid CardId, IReadOnlyList<CardStatementRowDto> Rows)`. `Endpoints/Mapping/FinancingMappingExtensions.cs` — `ToCardStatementsDto(this CardStatementsResponse, Guid cardId)` (new receiver → individual `this`-form, beside `ToCardFutureScheduleDto`). `ApiRoutes.Financing.CardStatements = "/cards/{id:guid}/statements"`; wired in `EndpointExtensions.MapFinancingEndpoints()` after `FutureSchedule` with `.WithSummary`/`.WithDescription`/`.Produces<CardStatementsDto>(200)`.
  - **Cross-module** — nothing added to any facade; RNF-9 untouched (only a new `Financing.Contracts` query record).
  - **Tests — `tests/PersonalFinance.Api.Tests/CardStatementsTests.cs`** (2 facts): `GET /v1/financing/cards/{unknown}/statements` → `200`, body `cardId` echoes the route id, `rows` empty; `/openapi/v1.json` advertises `/v1/financing/cards/{id}/statements` `get` under the `Financing` tag with `200`. Happy path (needs the `AccrueInstallments` scheduler the WAF strips) → `PersonalFinance.Api.http` walk, per the Phase 11 precedent.
- **Step 5 — `GET /v1/ledger/transactions` (done).** `dotnet test --solution` → **137 passed / 0 failed** (133 + 4 new `PersonalFinance.Api.Tests`); `dotnet build` 0W/0E; Architecture.Tests green.
  - **Ledger** — `PersonalFinance.Ledger.Contracts/Queries/GetTransactionsQuery.cs` (`TransactionFeedRow(Guid TransactionId, DateTimeOffset PostedOnUtc, string Description, long AmountMinorUnits, bool IsReversal, bool IsReversed, Guid? InstallmentReferenceId, Guid? SplitReferenceId)`, `TransactionFeedResponse(rows)`, `GetTransactionsQuery(Guid? AccountId, DateOnly? FromUtc, DateOnly? ToUtc) : IQuery<…Response>`). Handler `Application/Queries/GetTransactions/GetTransactionsHandler.cs` — `context.Transactions.Include(t => t.Entries)`; the `AccountId` filter is server-side (`t.Entries.Any(e => e.AccountId == id)`, a Guid comparison), then `.ToListAsync()` and **both** the `From`/`To` date-range filter (inclusive lower, exclusive `+1 day` upper, compared on `PostedOnUtc.UtcDateTime`) and the newest-first `OrderByDescending(PostedOnUtc)` run in memory — SQLite cannot filter or order a `DateTimeOffset` column server-side (the Phase-10 `GetCurrentAccountTimelineHandler` gotcha, extended here to `WHERE` as well as `ORDER BY`). `AmountMinorUnits` = Σ of the debit-side entry amounts; `IsReversed` = another loaded transaction has `OriginalTransactionId == t.Id` (id set built once from the loaded list); `Description` synthesized: `"Reversal"` when `IsReversal`, else `"Installment accrual"` / `"Shared expense / split"` / `"Subscription charge"` by which `*Reference` tag is set, else `"Manual entry"`. Registered in `LedgerModule.Register`. `ILedgerApi.GetTransactionsAsync` + `LedgerApi` impl (query-bus passthrough) — `.Contracts`-only, RNF-9 untouched; the host does not use the facade method (see below), it is added for parity with the other read facade members and for Step 7's cross-module reuse.
  - **Host** — `Endpoints/Ledger/GetTransactions.cs` (`static class GetTransactions`, `Handle(Guid? accountId, string? from, string? to, IQueryBus, CancellationToken)` → `Ok<TransactionFeedDto>`) dispatches via `IQueryBus.AskAsync` like its sibling `GetAccountBalance`. `Endpoints/DTOs/TransactionFeedDTO.cs` — `TransactionFeedRowDto(...)`, `TransactionFeedDto(IReadOnlyList<TransactionFeedRowDto> Rows)`. `Endpoints/Mapping/LedgerMappingExtensions.cs` — `ToGetTransactionsQuery(this Guid? accountId, string? from, string? to)` (new receiver → individual `this`-form) parsing `from`/`to` via a private `parseDateOnly` (`DateOnly.Parse(value, CultureInfo.InvariantCulture)` on non-blank input; bad input throws `FormatException` → `GlobalExceptionHandler` → 400 `Request.Malformed`, already wired) + `ToTransactionFeedDto(this TransactionFeedResponse)`. `MapGet` on the existing `ApiRoutes.Ledger.Transactions` (`"/transactions"`) alongside the `MapPost`, with `.WithSummary`/`.WithDescription`/`.Produces<TransactionFeedDto>(200)`/`.ProducesProblem(400)`.
  - **Cross-module** — one new `ILedgerApi` member, `.Contracts`-only; RNF-9 untouched. `OnPaymentPlanCreatedTests.FakeLedgerApi` gained `GetTransactionsAsync` as `throw new NotSupportedException()`.
  - **Tests — `tests/PersonalFinance.Api.Tests/LedgerTransactionsTests.cs`** (4 facts, each isolating itself to its own freshly-registered debit accounts because `ApiWebApplicationFactory` is a shared `IClassFixture`): (1) two `POST /v1/instruments` debit accounts → two `POST /v1/ledger/transactions` transfers → one `POST …/{id}/reversal` → `GET …?accountId={A}` returns the 3 rows touching A, with the reversal row `isReversal == true` / `description == "Reversal"`, the original row `isReversed == true` / `description == "Manual entry"` / `amountMinorUnits == 500000`; (2) `?accountId=` narrows to the single transfer touching a third account; (3) `?accountId=&from=2026-09-01&to=2026-09-05` drops a transfer posted 2026-09-20; (4) `/openapi/v1.json` advertises `/v1/ledger/transactions` `get` under the `Ledger` tag with `200`. Happy path is fully WAF-testable — a plain manual transaction reversed with no `InstallmentReference`/`SplitReference` needs no scheduler and no cross-module cascade.
- **Step 7 — reversible transaction id into the timeline + statement-installment rows (D20 enabler) (done).** `dotnet test --solution` → **140 passed / 0 failed** (137 + 3 new); `dotnet build` 0W/0E; Architecture.Tests green. Two independent threads, both `.Contracts`-only (no impl-assembly edge → RNF-9 untouched).
  - **Timeline thread.** `vw_current_account_timeline.sql` gains `m.TransactionId` in the SELECT (the source `vw_receivable_account_movements` already surfaces `e.TransactionId`). Migration `20260904000641_TimelineTransactionId` — `dotnet ef migrations add` scaffolded an **empty** `Up`/`Down` (a keyless `ToView` property change emits no relational DDL) and regenerated `PartiesDbContextModelSnapshot` with the new `CurrentAccountTimelineEntry.TransactionId`; the `Up` was then hand-filled with `DROP VIEW IF EXISTS` + `ReadViewSqlHelper.Load("vw_current_account_timeline.sql")` and `Down` with `DROP` + the pre-Step-7 `CREATE VIEW` inline (Phase-10 view-rebuild pattern). `Guid TransactionId` threaded through: `CurrentAccountTimelineEntry` read model; `CurrentAccountTimelineRow` (Parties.Contracts — **leads** the record, mirroring `TransactionFeedRow`) + the Parties `GetCurrentAccountTimelineHandler` projection; Reporting `Sql/current_account_timeline.sql` (new **leading** column → `map(reader)` indices shift +1, `Guid.Parse(reader.GetString(0))`) + `PartyTimelineRow`; host `CurrentAccountTimelineRowDto` (Parties) + `PartyTimelineRowDto` (Reporting) + `PartyMappingExtensions.ToCurrentAccountTimelineDto` + `ReportingMappingExtensions.ToPartyTimelineDto`. `CurrentAccountTimelineEntryConfiguration` unchanged — EF binds the keyless view entity by property name.
  - **Statement thread.** New Ledger `.Contracts` query `FindAccrualTransactionIdsQuery(IReadOnlyList<Guid> InstallmentReferenceIds) : IQuery<AccrualTransactionIdsResponse>`; `AccrualTransactionIdsResponse(IReadOnlyDictionary<Guid, Guid> ByInstallmentReferenceId)`. `FindAccrualTransactionIdsHandler` — empty request → empty map; else `context.Transactions.Where(t => t.OriginalTransactionId == null && t.InstallmentReference != null)` (both null-checks on the value-converted `InstallmentReference` translate to SQL fine), project `{ Id, Reference }`, `.ToListAsync()`, then in memory keep the wanted ids and `GroupBy(Reference.Value).ToDictionary(.., g => g.First().Id)`. A storno carries the reversed transaction's `InstallmentReference` too, so the `OriginalTransactionId == null` filter is what keeps the map pointing at the **original** accrual. Registered in `LedgerModule.Register`; `ILedgerApi.FindAccrualTransactionIdsAsync` + `LedgerApi` passthrough; `OnPaymentPlanCreatedTests.FakeLedgerApi` gained it as `throw new NotSupportedException()`. `GetMonthlyStatementHandler` (Financing — already has the `Ledger.Contracts` edge) now takes `ILedgerApi ledger`, calls `FindAccrualTransactionIdsAsync` once with `accrued.Select(r => r.Id)`, and each `MonthlyStatementInstallmentRow` / host `MonthlyStatementInstallmentRowDto` gains a trailing `Guid? ReversalTransactionId` (`ByInstallmentReferenceId.TryGetValue(row.Id, out var tx) ? tx : null`) — the accrual tx a client posts to `/v1/ledger/transactions/{id}/reversal`; null when no accrual matches (e.g. the row is not yet accrued, or is itself already reversed with no live accrual). `FinancingMappingExtensions.ToMonthlyStatementDetailDto` carries the field.
  - **Naming note.** The contract/DTO field is `ReversalTransactionId` (client-side `reversalTransactionId` in Step 8) per the approved plan, though the value is the *accrual/original* transaction id, i.e. the id you submit to the Reverse action — documented on the `CurrentAccountTimelineRow` / `FindAccrualTransactionIdsQuery` summaries.
  - **Tests.** `tests/PersonalFinance.Api.Tests/AccrualTransactionIdsTests.cs` (2 facts, resolving `ILedgerApi` from `factory.Services` — no HTTP surface for this facade method): post a manual balanced tx carrying an `InstallmentReferenceId`, assert `FindAccrualTransactionIdsAsync` maps it to that tx and **still** maps it to the same tx after `ReverseTransactionAsync`; empty request → empty map. `ReportingQueryTests.PartyTimeline_rows_carry_the_ledger_transaction_behind_each_movement` — every `GetPartyTimelineQuery` row has a non-`Guid.Empty` `TransactionId` (exercises the rebuilt view end-to-end via the migrate-all fixture). The statement `reversalTransactionId` happy path needs the `AccrueInstallments` scheduler (stripped from the WAF) → deferred to the `PersonalFinance.Api.http` walk, Phase 11 precedent.
- **Not committed by this session** — the user commits all Phase 12 work.

---

## Phase 13 — Creditors CRUD (Slice 1)

**Goal:** Deliver a self-contained CRUD vertical for a new `Creditor` reference entity (who the user pays) with nested `CreditorAccount` destination accounts — a prerequisite for a later Load-expense enhancement (Slice 2, `docs/creditor-expense-fields/slice-2-load-expense-integration.md`) that lets a user record who was paid and to which account, separately from splitting a shared expense with Parties.

**Depends on:** Phase 3 (Financing module scaffolding — mirrors `CreditCard`'s domain/CQRS/host shape).

### Tasks
- [x] `Domain/Creditor.cs` (aggregate root, `Name` + `IReadOnlyList<CreditorAccount> Accounts`), `Domain/CreditorAccount.cs` (entity, `Label` + optional `Identifier`) — mirrors `CreditCard`/`Installment`'s private-ctor + static `Create` pattern; `FinancingErrors.InvalidCreditorName`.
- [x] Contracts: `CreateCreditorCommand`/`CreditorAccountPayload`, `ListCreditorsQuery`/`CreditorRow`/`CreditorAccountRow`/`ListCreditorsResponse`; `IFinancingApi.CreateCreditorAsync`/`ListCreditorsAsync`.
- [x] `CreateCreditorHandler`, `ListCreditorsHandler` (Application layer); `FinancingModule` registrations.
- [x] `CreditorConfiguration`/`CreditorAccountConfiguration` (EF), `FinancingDbContext.Creditors`; migration `AddCreditors` (`financing_creditors`, `financing_creditor_accounts`, FK cascade).
- [x] Host: `ApiRoutes.Creditors`, `EndpointExtensions.MapCreditorEndpoints`, `Endpoints/Creditors/PostCreditor.cs` + `GetCreditors.cs`, `CreditorsDTO.cs`, `CreditorMappingExtensions.cs` — `POST /v1/creditors` (201 + `{ creditorId }`), `GET /v1/creditors` (`{ rows: [...] }`).
- [x] Tests: `CreditorTests.cs` (domain), `CreditorHandlersTests.cs` (in-memory SQLite) — `PersonalFinance.Financing.Tests.csproj` gained a direct `Microsoft.EntityFrameworkCore.Sqlite` reference + regenerated `packages.lock.json`.
- [x] **Follow-up: make `CreditorAccount.Identifier` genuinely optional.** Domain normalizes blank/whitespace → `null`, trims otherwise; property moved to a private setter (EF can't reliably constructor-bind a nullable reference type — same fix as `Installment.AccruedOnUtc`). New migration `MakeCreditorAccountIdentifierNullable` (real `AlterColumn` — a prior hand-edit of the already-applied `AddCreditors` migration file had no effect on the live schema). `CreditorAccountConfiguration` dropped `.IsRequired()` on `Identifier`; contracts/DTOs widened to `string?`.

### Definition of done
- [x] `dotnet build PersonalFinance.sln` 0W/0E; `dotnet test --solution` → **149 passed** (140 baseline + 9 new); `PersonalFinance.Architecture.Tests` (RNF-9) green — both new `IFinancingApi` members are `.Contracts`-only.
- [x] Migration applies cleanly against the shared SQLite file; `Identifier` column verified nullable via `PRAGMA table_info`.

### Completion notes (2026-09-04)

Built one green-lit step at a time per `docs/creditor-expense-fields/slice-1-creditors-crud.md`. No domain validation exists for a blank `Label` — only `Name` (creditor) is guarded (`FinancingErrors.InvalidCreditorName`); `Label` presence is enforced client-side plus the EF `IsRequired()`/`NOT NULL` column — `Identifier` is the only truly optional field. Slice 2 (wiring a creditor + destination account into the Load-expense form) is **not started** — this phase is CRUD-only. Not committed by this session for most of the work — the user commits their own.

---

## Phase 14 — Load-expense integration (Slice 2)

**Goal:** Extend `CreatePaymentPlan` with two optional metadata fields (`CreditorId`, `CreditorAccountId`) to record who was paid and to which account, separate from Parties expense-splitting. No ledger impact, no cross-validation — pure metadata per the spec.

**Depends on:** Phase 3 (`CreatePaymentPlan` command exists, `PaymentPlan` aggregate) + Phase 13 (`Creditor` entities exist and are reference data).

### Tasks
- [x] Extend `CreatePaymentPlanCommand` with two new nullable Guid fields: `Guid? CreditorId = null, Guid? CreditorAccountId = null` (object-initializer set, no new params to constructor).
- [x] Extend `PaymentPlan` domain aggregate: add `public Guid? CreditorId { get; private set; }` and `public Guid? CreditorAccountId { get; private set; }`; thread both as optional trailing params into `PaymentPlan.Create(...)` (precedent: `SplitReferenceId`).
- [x] `PaymentPlanConfiguration`: map both as plain nullable columns via `builder.Property(plan => plan.CreditorId);` / `CreditorAccountId` (mirrors `SplitReferenceId`).
- [x] `CreatePaymentPlanHandler`: pass `command.CreditorId`/`command.CreditorAccountId` into `PaymentPlan.Create(...)`. No validation beyond lenient nullable-through.
- [x] `CreatePaymentPlanDTO`: add `Guid? CreditorId = null, Guid? CreditorAccountId = null`.
- [x] `FinancingMappingExtensions.ToCreatePaymentPlanCommand`: thread the two new fields through. `Endpoints/Financing/PostPaymentPlan.cs` needs no change (it just calls `body.ToCreatePaymentPlanCommand()`).
- [x] EF migration `AddCreditorToPaymentPlan`: ALTER adding two nullable TEXT columns to `financing_payment_plans` table (`CreditorId`, `CreditorAccountId`). Hand-authored migration file, no schema change, no new table.
- [x] Tests: `tests/PersonalFinance.Financing.Tests/CreatePaymentPlanHandlerTests.cs` (2 facts: CreditorId/CreditorAccountId persist when supplied, stay null when absent; in-memory SQLite harness mirrors `CreditorHandlersTests.cs` pattern) + `tests/PersonalFinance.Api.Tests/FinancingMappingExtensionsTests.cs` (2 facts: fields thread through `ToCreatePaymentPlanCommand` / stay null). No new module edge — `.Contracts`-only so `Architecture.Tests` (RNF-9) stays green.

### Definition of done
- [x] `dotnet build PersonalFinance.sln` 0W/0E; `dotnet test --solution` → **153 passed** (149 baseline + 4 new); `PersonalFinance.Architecture.Tests` (RNF-9) green.
- [x] Migration applies cleanly against the shared SQLite file; both columns verified nullable via `PRAGMA table_info`.
- [x] End-to-end verified against running API + live SQLite: POST with creditorId+creditorAccountId persists both; POST without them leaves both NULL. No cross-validation (lenient design).

### Completion notes (2026-09-04)

Built per `docs/creditor-expense-fields/slice-2-load-expense-integration.md`. Design is intentionally lenient — no validation that `CreditorId` exists as a `Creditor`, no validation that `CreditorAccountId` belongs to that creditor; cross-cutting validation is out of scope (deferred to a future phase if needed). No ledger posting/balances/settlement for creditors, no surfacing of creditor/account on read views (statements, timelines), no editing/deleting creditors. Slice 3 (integrating the creditor selector into the client's Load-expense form) is out of scope — this phase is API metadata integration only. Not committed by this session for most of the work — the user commits their own.

---

## Phase 15 — Expense description field (Slice 1)

**Goal:** Add a required, human-readable `Description` to the `PaymentPlan` expense aggregate and thread it through the whole create-expense write path, closing the loop by echoing it back on the client confirmation panel instead of a bare GUID.

**Traces to:** `docs/expense-description/slice-1-description-field.md` (a standalone planning doc, not `docs/PRD.md`/`docs/DESIGN.md`).

**Depends on:** Phase 3 (`CreatePaymentPlan` vertical exists).

### Tasks
- [x] `Domain/PaymentPlan.cs` — add `public string Description { get; private set; }`, set (trimmed) inside `Create(...)`; new required `string description` param placed with the required params, not trailing-optional.
- [x] `Financing.Contracts/Commands/CreatePaymentPlanCommand.cs` — add required `string Description`, positioned after `PurchaseDate` and before the optional trailing params (`Split`, `CreditorId`, `CreditorAccountId`).
- [x] `CreatePaymentPlanValidator.cs` — trim, reject blank/whitespace-only, reject `> 120` chars, reject any `\n`/`\r`; three new `FinancingErrors` codes: `BlankDescription`, `DescriptionTooLong`, `DescriptionMustBeSingleLine`.
- [x] `CreatePaymentPlanHandler.cs` — pass `command.Description` into `PaymentPlan.Create(...)`.
- [x] `PaymentPlanConfiguration.cs` — `builder.Property(plan => plan.Description).IsRequired();`.
- [x] Full multi-project data-only wipe (SQLite can't add `NOT NULL` without a default over existing rows), then migration `AddPaymentPlanDescription` (`AddColumn<string>("Description", "financing_payment_plans", nullable: false)`, no default).
- [x] Host: `CreatePaymentPlanDTO` + `FinancingMappingExtensions.ToCreatePaymentPlanCommand` carry `Description` through; `POST /v1/financing/payment-plans` and its route need no change (already delegates to the mapping extension).
- [x] Tests: `CreatePaymentPlanValidatorTests.cs` (blank/whitespace-only, `>120` chars, a string with `\n`, valid 1–120 single-line — each asserting the specific `FinancingErrors` code) + `CreatePaymentPlanHandlerTests.cs` extended (a plan created with a description persists the trimmed value).

### Definition of done
- [x] `financing_payment_plans` has a `Description TEXT NOT NULL` column, migration applied against the shared SQLite file, data wiped first.
- [x] `POST /v1/financing/payment-plans` rejects a missing/blank/`>120`/multiline description with `422` + a Financing description error code, and persists the trimmed value on a valid one.
- [x] `dotnet test --solution` green with a higher count; `PersonalFinance.Architecture.Tests` (RNF-9) unaffected — no new module edge.

### Completion notes

Slice 1 was already complete when this documentation pass ran — built and verified in a prior session on `feat/expense-description`. No Ledger involvement by design: the description lives only on Financing's `PaymentPlan`, never threaded into `AccrueInstallments` or `PostTransactionCommand`. Client-side counterpart (required form field + confirmation-panel echo) is documented in `app/client/.claude/TASK.md`. Not committed by this session — the user commits their own.

---

## Phase 16 — Card-debt drill-down (Slice 2)

**Goal:** Make the Dashboard's "Card Debt by Cycle" block expandable per card into the individual outstanding purchases behind its total, each carrying its Phase-15 `Description`.

**Traces to:** `docs/expense-description/slice-2-card-debt-drilldown.md` (continued planning doc).

**Depends on:** Phase 15 (`PaymentPlan.Description` must exist and be populated) + Phase 3 (Financing installment/statement/billing-cycle model).

### Tasks
- [x] `Financing.Contracts/Queries/GetCardPurchasesQuery.cs` — `GetCardPurchasesQuery(Guid CardId) : IQuery<CardPurchasesResponse>`, `CardPurchasesResponse(Guid CardId, IReadOnlyList<CardPurchaseRow> Rows)`, `CardPurchaseRow(Guid PlanId, string Description, long TotalMinorUnits, int InstallmentCount, int OutstandingCount, DateOnly PurchaseDate, bool IsCreditorPayment)` — mirrors `GetCardStatementsQuery` exactly.
- [x] `Application/Queries/GetCardPurchases/GetCardPurchasesHandler.cs` — joins `Installment` (`context.Set<Installment>()`, no root `DbSet`) to `PaymentPlan` filtered by `CardId`, excludes `IsReversed`; outstanding = not-yet-accrued OR accrued-with-unpaid-statement; groups by `PlanId`; orders current-cycle first (resolved via `BillingCycleCalculator.ResolveCycle(today, card.CutoffDay)`, compared by `(CycleYear, CycleMonth)` equality — **not** `IsClosedAsOf`, which is also true of every future cycle) then `PurchaseDate` descending; unknown card → empty rows, no exception. Registered in `FinancingModule.Register`.
- [x] Host: `Endpoints/Financing/GetCardPurchases.cs` (`IQueryBus.AskAsync` + `FinancingMappingExtensions.ToCardPurchasesDto`), `Endpoints/DTOs/CardPurchasesDTO.cs` (`CardPurchasesDto`/`CardPurchaseRowDto`), `ApiRoutes.Financing.CardPurchases = "/cards/{id:guid}/purchases"`, wired in `EndpointExtensions.MapFinancingEndpoints` with `.Produces<CardPurchasesDto>(200)` under tag `"Financing"`. Unknown card → `200` empty (sibling consistency with `/cards/{id}/future-schedule` and `/cards/{id}/statements`, never `404`).
- [x] Tests: `tests/PersonalFinance.Financing.Tests/GetCardPurchasesHandlerTests.cs` (7 facts, in-memory SQLite: paid-excluded, unaccrued-included, accrued-unpaid-included, grouping/`OutstandingCount`, creditor flag, current-cycle-first ordering, unknown-card-empty) + `tests/PersonalFinance.Api.Tests/CardPurchasesTests.cs` (2 facts: unknown-card `200` empty, OpenAPI presence under `Financing` with `200`).

### Definition of done
- [x] `GET /v1/financing/cards/{id}/purchases` returns one row per outstanding purchase with `description`, unknown card → `200` empty.
- [x] Paid-off purchases excluded; creditor purchases flagged; current-cycle purchases sort first.
- [x] No new EF migration, no `IFinancingApi` change; `PersonalFinance.Architecture.Tests` green.
- [x] `dotnet test --solution` → **170 passed** (153 baseline at Phase 14 + Phase 15's own additions + these 9 new facts).
- [ ] Manual live E2E walk (run the API, let a card accrue real installments, expand it on a running Dashboard, confirm purchases + descriptions against the DB) — **not yet performed**, offered to the user and left pending.

### Completion notes (2026-09-04)

Built one green-lit step at a time (contract → handler → host wiring → API tests → client type → client service → dashboard state → dashboard template → client tests → full verification). **Bug caught and fixed during this session's own review, before it shipped**: the first draft of the ordering logic tested `!installment.Cycle.IsClosedAsOf(today, cutoffDay)` to mean "current cycle" — that predicate is also true of every *future* cycle (`AccrueInstallments` uses it only to mean "not due to accrue yet"), so nearly every outstanding plan would have been wrongly flagged current-cycle. Fixed to resolve the actual current cycle via `BillingCycleCalculator.ResolveCycle` and compare by `(CycleYear, CycleMonth)` equality; the regression test for this (`GetCardPurchasesHandlerTests`'s ordering fact) derives "current" the same way rather than hardcoding a cycle, so it stays valid regardless of what day it runs. Client-side counterpart (dashboard accordion) is documented in `app/client/.claude/TASK.md`. Full verification: `dotnet test --solution` 170/170, `pnpm ng test` 176/176, `pnpm ng lint` clean, `pnpm ng build --configuration production` clean. Not committed by this session — the user commits their own.

---

## Phase 17 — Recent purchases view (Slice 3)

**Goal:** Add the final expense-description slice — a standalone, newest-first chronological list of every payment plan across every card, independent of card grouping or debt state.

**Traces to:** `docs/expense-description/slice-3-recent-purchases-view.md` (continued planning doc, final slice).

**Depends on:** Phase 15 (`PaymentPlan.Description` must exist and be populated) + Phase 3 (Financing `PaymentPlan`/`CreditCard` model). Independent of Phase 16's endpoint, though it reuses the same patterns.

### Tasks
- [x] `Financing.Contracts/Queries/ListRecentPurchasesQuery.cs` — `RecentPurchaseRow(Guid PlanId, string Description, string CardName, DateOnly PurchaseDate, long TotalMinorUnits, int InstallmentCount, bool IsCreditorPayment)`, `RecentPurchasesResponse(IReadOnlyList<RecentPurchaseRow> Rows)`, `ListRecentPurchasesQuery(int Limit = 100) : IQuery<RecentPurchasesResponse>` — mirrors `GetCardStatementsQuery` exactly.
- [x] `Application/Queries/ListRecentPurchases/ListRecentPurchasesHandler.cs` — builds a `CreditCard.Id → Name` dictionary, projects every `PaymentPlan` (no card filter), orders in memory by `PurchaseDate` descending, `Take(Limit)`. Registered in `FinancingModule.Register`.
- [x] Host: `Endpoints/Financing/GetRecentPurchases.cs` (`IQueryBus.AskAsync` + `FinancingMappingExtensions.ToRecentPurchasesDto`), `Endpoints/DTOs/RecentPurchasesDTO.cs` (`RecentPurchasesDto`/`RecentPurchaseRowDto`), `ApiRoutes.Financing.RecentPurchases = "/purchases/recent"`, wired in `EndpointExtensions.MapFinancingEndpoints` with `.Produces<RecentPurchasesDto>(200)` under tag `"Financing"`.
- [x] Tests: `tests/PersonalFinance.Financing.Tests/ListRecentPurchasesHandlerTests.cs` (4 facts: newest-first, description/cardName, creditor flag, `Limit` cap) + `tests/PersonalFinance.Api.Tests/RecentPurchasesTests.cs` (1 fact: OpenAPI presence under `Financing` with `200`).

### Definition of done
- [x] `GET /v1/financing/purchases/recent` returns every purchase newest-first with `description` + `cardName`, capped by `Limit`.
- [x] No new EF migration, no `IFinancingApi` change; `PersonalFinance.Architecture.Tests` green.
- [x] `dotnet test --solution` → **175 passed** (170 baseline at Phase 16 + these 5 new facts).
- [x] Manual live E2E walk (run the API, load a couple of expenses with distinct descriptions, hit the endpoint, confirm newest-first with the right text) — **performed this session** via `curl` against a running host.

### Completion notes (2026-09-05)

Built one green-lit step at a time (contract → handler → host wiring → API tests → client type → client service → client page → client route/nav → client tests → full verification), each step confirmed before the next started. **Ordering choice, worth flagging:** the planning doc offered ordering by `Id` descending (`Guid.CreateVersion7()` is time-ordered) as a safe proxy to avoid the SQLite `DateTimeOffset`-ordering trap; this handler orders by `PurchaseDate` descending in memory instead, since `PurchaseDate` is a `DateOnly` — not the affected type — so ordering by it directly is both simpler and semantically exact. Route: added `ApiRoutes.Financing.RecentPurchases = "/purchases/recent"` as a new top-level segment rather than overloading `PaymentPlans` (`POST`-only, differently-shaped response). Client-side counterpart (new standalone page + route + nav entry) is documented in `app/client/.claude/TASK.md`. Full verification: `dotnet test --solution` 175/175, `pnpm ng test` 183/183, `pnpm ng lint` clean, `pnpm ng build --configuration production` clean. Manual E2E walk performed live against a running API instance (no browser available in this environment — substituted a `curl` walk). Not committed by this session — the user commits their own.

---

## Phase 18 — Creditor-financed expenses (Slice 1)

**Goal:** Make a payment plan's card optional so a store-financed or person-financed purchase is recorded as a card-less installment schedule — no billing cycle, no statement, no card Ledger posting — while split-with-parties keeps working against a new per-plan `CreditorPayable` liability.

**Traces to:** `docs/expense-payment-modes/slice-1-creditor-financed.md` (parent plan `~/.claude/plans/today-we-will-expand-hashed-crown.md`, decisions D1–D11).

**Depends on:** Phase 3 (Financing `PaymentPlan`/`Installment`), Phase 6 (Parties split flow + `LinkPaymentPlanSplit`), Phase 13–14 (Creditors CRUD + the `CreditorId`/`CreditorAccountId` fields on `CreatePaymentPlan`).

### Tasks
- [x] `Financing.Contracts` — `CreatePaymentPlanCommand.CardId` and `PaymentPlanCreatedIntegrationEvent.CardId` → `Guid?`; drop `bool IsCreditorPayment` from `GetCardPurchasesQuery`'s `CardPurchaseRow`.
- [x] Host — `CreatePaymentPlanDTO.CardId` → `Guid?`; `FinancingMappingExtensions` passes it through unchanged; drop `IsCreditorPayment` from `CardPurchaseRowDto` + `ToCardPurchasesDto`.
- [x] `PaymentPlan` — `CardId` → `Guid?` (ctor param nullable so EF binds); `Create(...)` takes `Guid? cardId` / `int? cutoffDay`; guards `PlanNeedsCardOrCreditor` / `PlanCannotMixCardAndCreditor` / `CreditorAccountRequired`; creditor branch schedules `Installment.Schedule(..., new BillingCycle(purchaseDate.Year, purchaseDate.Month).AddMonths(i + 1))`. New `CreditorPayableAccountId` + `AssignCreditorPayableAccount`. `Installment.MarkCreditorAccrued(now)`.
- [x] `FinancingErrors` — `PlanNeedsCardOrCreditor`, `PlanCannotMixCardAndCreditor`, `CreditorAccountRequired`, `CreditorNotFound`, `CreditorAccountMismatch`.
- [x] `CreatePaymentPlanValidator` + `CreatePaymentPlanHandler` — card XOR creditor; handler resolves the `Creditor` (`Include(Accounts)`) and checks the account belongs to it; `cutoffDay = null` in creditor mode; the outbox event carries `command.CardId` (nullable).
- [x] `AccrueInstallments` — inner join kept, `where plan.CardId != null` guard added (card path otherwise untouched, D11). `GetInstallmentStatusHandler` + `MarkInstallmentReversedHandler` — left join on `plan.CardId` + null-card guards.
- [x] `ListRecentPurchasesHandler` — `plan.CardId is { } id ? cardNames.GetValueOrDefault(id, "") : ""` compile fix; its own `IsCreditorPayment` kept. `GetCardPurchasesHandler` — `IsCreditorPayment` projection dropped.
- [x] `Ledger.Contracts` — `AccountKind.CreditorPayable`; `Account.kindMatchesType` → `AccountType.Liability` alongside `CardLiability`.
- [x] `LinkPaymentPlanSplitHandler` — inject `ILedgerApi`; after `LinkSplit`, for a card-less plan with no payable yet, `CreateAccountAsync(CreateAccountCommand("Payable to creditor — {Description}", Liability, CreditorPayable, OwnerReferenceId: plan.Id))` + `AssignCreditorPayableAccount` (idempotent).
- [x] `CreditorSplitAccrualCalculator` (new, pure) — weights `[1L, ..participant weights]` → `PhantomPennyAllocator`; drop index 0; one `Dr Receivable_k` per participant with share > 0, one `Cr CreditorPayable` for the sum; `([], 0)` when no party portion.
- [x] `AccrueCreditorSplitInstallments` (new, `: SchedulerBase`, 1-min, run-on-startup) — un-accrued/un-reversed installments of card-less split plans (`CardId == null && SplitReferenceId != null && CreditorPayableAccountId != null`) whose owed month ordinal has arrived; `PostTransactionAsync` (tagged `SplitReferenceId` + `InstallmentReferenceId`, log-warn on failure), `MarkCreditorAccrued`, `SaveChanges`, `RecordSplitAccrualAsync` (log-warn on failure); no `MonthlyStatement`, no integration event. Registered in `FinancingModule`.
- [x] `PaymentPlanConfiguration` — drop `CardId` `.IsRequired()`; map `CreditorPayableAccountId`. Migrations `20260905170609_AllowCardlessCreditorFinancedPlan` + `20260905170957_RestoreCardFutureScheduleView` (two, because the `AlterColumn` table rebuild is deferred past a trailing `Sql()` and `vw_card_future_schedule` depends on the rebuilt table). No Ledger migration.
- [x] Tests — see below; 3 stale Phase-14 tests that built a card+creditor plan rewritten (card-less) or deleted (`GetCardPurchases` creditor-flag).

### Definition of done
- [x] A card-less creditor-financed plan round-trips: valid persisted shape (`CardId` NULL, `CreditorId`/`CreditorAccountId` set), monthly `(CycleYear, CycleMonth)` stepping from the month after purchase, **zero** Ledger rows for a non-split plan.
- [x] Card XOR creditor enforced in the validator and the domain; unknown creditor → `CreditorNotFound` (404), foreign account → `CreditorAccountMismatch` (422).
- [x] A creditor split posts exactly the co-borrower legs (`Dr Receivable_k / Cr CreditorPayable`), the holder's share never posts, the accrual is idempotent, `RecordSplitAccrualAsync` gets the party portion; a plain creditor plan and a not-yet-due installment post nothing.
- [x] Card accrual + statement + reversal paths behave exactly as before (D11); a card-less installment reverses with no card-lookup crash.
- [x] `dotnet ef database update` applies both migrations to the dev DB; `vw_card_future_schedule` restored (returns rows).
- [x] `dotnet test --solution` → **192 passed**, 0 warnings; `PersonalFinance.Architecture.Tests` green (no new module edge).
- [ ] Manual live E2E walk (creditor plan no split / creditor plan + split + scheduler tick / card plan unchanged / reverse the split accrual) — **handed to the user**, not run this session.

### Completion notes (2026-09-05)

Built one green-lit step at a time (nullable `CardId` + validation + migrations → `CreditorPayable` account + calculator + scheduler → API tests → client mode selector → client tests → E2E). **Two deviations folded into step 1:** `PaymentPlanConfiguration`'s `.IsRequired()` removal was needed for the in-memory-SQLite tests (`EnsureCreated` reads the live model), and `GetCardPurchases.IsCreditorPayment` was retired outright (user decision) since the new card-XOR-creditor guard makes it permanently false — three Phase-14 tests that asserted the old mixed state were rewritten or deleted. **The migration is two migrations**, not one: EF defers the SQLite table rebuild from `AlterColumn(CardId nullable)` past any trailing `Sql()` operation, so `vw_card_future_schedule` (which depends on `financing_payment_plans`) has to be dropped in the first migration and recreated in a follow-up — this is now the repo's pattern for `AlterColumn` under a dependent view. No new D-numbers — this implements D1–D11 of the parent plan. `AccrueCreditorSplitInstallmentsTests` drives the `SchedulerBase` tick via reflection (`TickAsync` is `protected` on a `sealed` class). Client-side counterpart (two-way payment-mode selector replacing the "Different creditor" checkbox) is `app/client/.claude/TASK.md` Phase 10. Full verification: `dotnet test --solution` 192/192, 0 warnings; `PersonalFinance.Architecture.Tests` green; client `pnpm ng lint` + `pnpm ng build --configuration production` clean, `pnpm ng test` 187/187. Manual E2E walk handed to the user. Not committed by this session — the user commits their own.

---

## Phase 19 — Owed to creditors list (Slice 2)

**Goal:** Add a read-only list of every creditor and their outstanding balance across all card-less plans, grouped by creditor with per-account breakdown.

**Traces to:** `docs/expense-payment-modes/slice-2-owed-to-creditors-list.md` (continued planning doc).

**Depends on:** Phase 18 (card-less `PaymentPlan` + `CreditorPayableAccountId` + `Installment.IsReversed`). No cross-module dependency outside `Financing.Contracts`.

### Tasks
- [x] `Financing.Contracts/Queries/GetCreditorPayablesQuery.cs` — `GetCreditorPayablesQuery() : IQuery<CreditorPayablesResponse>`, `CreditorPayablesResponse(IReadOnlyList<CreditorPayableRow> Rows)`, `CreditorPayableRow(Guid CreditorId, string CreditorName, long OutstandingMinorUnits, DateOnly? NextDueDate, IReadOnlyList<CreditorPayableAccountBreakdown> Accounts)`, `CreditorPayableAccountBreakdown(Guid AccountId, string Label, long OutstandingMinorUnits)` — mirrors `GetCardPurchasesQuery` and `ListRecentPurchasesQuery` pattern.
- [x] `Application/Queries/GetCreditorPayables/GetCreditorPayablesHandler.cs` — joins `Installment` (`context.Set<Installment>()`, no root `DbSet`) to `PaymentPlan` filtered by `plan.CreditorId != null && !installment.IsReversed`, `ToListAsync`, then in memory: loads `Creditors.Include(c => c.Accounts)`, groups by creditor → outstanding sum + earliest `(CycleYear, CycleMonth)` as `NextDueDate` (month boundaries, purchase day clamped to month length), per-account breakdown ordered by label, rows ordered by creditor name. **Limitation:** no per-installment paid/settled flag exists, so "outstanding" means every non-reversed installment of the plan; `NextDueDate` is earliest *scheduled* month, does not advance as time passes. Registered in `FinancingModule.Register`.
- [x] Host: `Endpoints/Financing/GetCreditorPayables.cs` (`IQueryBus.AskAsync` + `FinancingMappingExtensions.ToCreditorPayablesDto`), `Endpoints/DTOs/CreditorPayablesDTO.cs` (`CreditorPayablesDto`/`CreditorPayableRowDto`/`CreditorPayableAccountDto`), `ApiRoutes.Financing.CreditorPayables = "/creditor-payables"`, wired in `EndpointExtensions.MapFinancingEndpoints` with `.Produces<CreditorPayablesDto>(200)` under tag `"Financing"` and `.WithDescription` documenting the limitation.
- [x] Tests: `tests/PersonalFinance.Financing.Tests/GetCreditorPayablesHandlerTests.cs` (5 facts, in-memory SQLite: two-creditor grouping + sums; no-creditor plans excluded; card-backed plans never appear; `NextDueDate` = earliest scheduled date; per-account breakdown ordered) + `tests/PersonalFinance.Api.Tests/CreditorPayablesTests.cs` (1 fact: OpenAPI presence under `Financing` with `200`).

### Definition of done
- [x] `GET /v1/financing/creditor-payables` returns grouped creditors with outstanding totals + next-due dates + account breakdowns; no card-backed or non-creditor plans; list ordered by creditor name.
- [x] No new EF migration, no `IFinancingApi` change; `PersonalFinance.Architecture.Tests` green.
- [x] `dotnet test --solution` → **198 passed** (from 192 at Phase 18), 0 warnings; RNF-9 green.
- [ ] Manual live E2E walk (run the API, load creditor-financed plans with and without splits, hit the endpoint, confirm grouping + sums + next-due + account breakdown against the DB) — **handed to the user**, not run this session.

### Completion notes

Pure additive CQRS read-only query on the `GetCardPurchases` / `GetCardStatements` precedent. No schema change, no EF migration, no `IFinancingApi` edge. Built one green-lit step at a time (contract → handler → host wiring → API tests → verification). Client-side counterpart (new standalone page + route + nav entry) is `app/client/.claude/TASK.md` Phase 20. The limitation (no per-installment settlement tracking) is accepted and documented in the OpenAPI description — a future session with a payment concept will refine it. Full verification: `dotnet test --solution` 198/198, 0 warnings; `PersonalFinance.Architecture.Tests` green. Manual E2E walk handed to the user. Not committed by this session — the user commits their own.
