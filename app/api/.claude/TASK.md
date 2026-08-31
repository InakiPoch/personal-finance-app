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
- [ ] `OutboxHealthCheck` manually verified to go unhealthy if the Outbox Worker is paused while a `PaymentPlanCreated` message is pending (RNF-7). — **Deferred to Phase 8.** The check class is unchanged from Phase 1 (its WAL/backlog logic was hard-verified in the Phase 1 DoD) and has no HTTP route yet — `Program.cs` `/health` is still the Phase-0 stub and `MapHealthChecks` wiring is Phase 8 scope. On the card-split path the pending-backlog staleness was confirmed by direct `sqlite3` inspection of `financing_outbox_messages` instead.

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
- **Left for later phases:** `ExpenseSplitSettledIntegrationEvent` has no consumer (Ledger already posts the D4 entry synchronously in `SettleCurrentAccount`); negative `CurrentAccount` (holder owes the party after a settled slice is reversed) surfaces correctly as a signed balance but has no dedicated settlement flow; the `vw_current_account_timeline` `"Card installment"` label; `OutboxHealthCheck` HTTP route (Phase 8); `docs/DESIGN.md` D12 step-4 text should note "metadata-only, the storno corrects the ledger" (Phase 10 doc-sync).

---

## Phase 7 — Reporting Module

**Goal:** Read-only cross-module dashboards, touching nothing but `vw_*` views (D5/RNF-6).

**Depends on:** Phases 2, 3, 5, 6 (all views this phase queries must exist).

### Tasks
- [ ] Create `PersonalFinance.Reporting` — **no reference to any module's Contracts or impl project**, only a raw SQLite connection to the shared file.
- [ ] `ReadDbConnectionFactory.cs`.
- [ ] `Sql/monthly_expenses.sql`, `card_due_by_month.sql`, `current_account_timeline.sql`, `debt_by_party.sql`.
- [ ] `Dashboards/MonthlyExpensesQuery.cs` (RF-1/D9, queries `vw_ledger_monthly_expenses`).
- [ ] `Dashboards/CardDueByMonthQuery.cs` (RF-2/D11 — the one query that legitimately joins two modules' `vw_*` views: `vw_card_liability_accrued` + `vw_card_future_schedule`; still respects D5 since both are public view surfaces).
- [ ] `Reports/CurrentAccountTimelineQuery.cs` (RF-7), `Reports/DebtByPartyQuery.cs`.
- [ ] `ReportingModule.cs` — registers query handlers via the shared `QueryBus`.
- [ ] `Endpoints/ReportingEndpoints.cs`: `GET /reports/monthly-expenses`, `GET /reports/card-due-by-month`, `GET /reports/parties/{id}/timeline`, `GET /reports/parties/debt-summary`.

### Tests
- [ ] **Gap flag:** DESIGN.md names no Reporting test project. Recommend a small integration suite: seed data through the write-side command handlers, assert Reporting queries return matching numbers — closest thing to an RF-1/RF-2/RF-7 acceptance test.
- [ ] Extend `ModuleIsolationTests.cs`: `Reporting` has zero project references to any module's Contracts or impl assembly.

### Definition of done
- [ ] Reporting builds with zero compile-time dependency on any module (verify `<ProjectReference>` list directly).
- [ ] Manual smoke test: seed a debit expense, an accrued installment, a subscription renewal, a party split; hit all four Reporting endpoints and cross-check numbers.
- [ ] Architecture-fitness test passes with Reporting included.

---

## Phase 8 — Cross-Cutting: Host Polish for the Future Angular Client

**Goal:** Make the API genuinely consumable by the planned Angular client (D13) — CORS, consistent response/error envelope, OpenAPI, health check wiring. `POST /instruments` itself was already built in Phase 3; this phase is about the envelope/CORS/OpenAPI conventions wrapping every endpoint, including it.

**Depends on:** all module phases functionally complete.

### Tasks
- [ ] Define the API-wide error envelope (`ProblemDetails`-based or custom `ApiError { Code, Message, Metadata }` mapping from `SharedKernel.Error`), returned consistently for all 4xx/5xx via shared middleware/filter — not duplicated per endpoint file.
- [ ] Audit all `Endpoints/*.cs` files (including `InstrumentsEndpoints.cs`) to confirm they map `Result`/`Error` failures through the shared envelope; fix stragglers.
- [ ] Add CORS policy scoped to the Angular dev server origin (`http://localhost:4200`, confirm against `app/client`'s actual `ng serve` port), ideally `appsettings`-driven rather than hardcoded.
- [ ] Confirm `AddOpenApi()`/`MapOpenApi()` covers every module's endpoints once registered; add XML doc comments / `[EndpointSummary]` where the spec is too sparse for client codegen.
- [ ] Wire `OutboxHealthCheck` into `Program.cs` via `AddHealthChecks()`; expose `/health` (replacing Phase 0's placeholder).
- [ ] Confirm endpoint testability wasn't compromised — handlers should remain testable in isolation, as in each module's own test project.

### Tests
- [ ] Small `WebApplicationFactory`-based smoke test: `/health` reflects Outbox Worker state; CORS headers present for the configured origin; OpenAPI document generates without errors.

### Definition of done
- [ ] All module endpoints return the consistent error envelope on failure.
- [ ] `/health` reflects Outbox staleness correctly.
- [ ] OpenAPI spec is complete and importable (spot-check by generating a TS client or validating against the OpenAPI 3.x schema).
- [ ] CORS verified manually against a `fetch()` call from a browser console pointed at the Angular dev origin.

---

## Phase 9 — CI

**Goal:** Automated build+test on every push — regressions in the architecture-fitness test or double-entry invariants must be caught immediately.

**Depends on:** at least Phase 2 complete; ideally run once Phase 8 is done for full test-matrix coverage, but can be pulled forward earlier.

### Tasks
- [ ] `.github/workflows/ci.yml` running `dotnet restore` / `build` / `test` against the whole `.sln`.
- [ ] Pin the CI runner's SDK to match `global.json` explicitly.
- [ ] Ensure any file-based SQLite tests use a unique temp file per test run/worker (avoid CI concurrency collisions).

### Definition of done
- [ ] A CI run on a clean checkout passes end-to-end with no manual setup beyond `global.json`/the workflow file.

---

## Phase 10 — Fase-1 PRD Acceptance Pass

**Goal:** Explicit checkpoint against PRD §5's Fase 1 scope (US-1, US-2, US-3, US-4, US-6) before considering the build product-complete for that scope.

**Depends on:** Phases 2, 3, 4, 7, 8.

### Tasks
- [ ] Walk each Fase-1 user story's acceptance criteria (PRD §6) against the running API via `api.http`, recording request/response pairs as manual acceptance evidence.
- [ ] Confirm PRD §8's utility-metric candidates are checkable in principle against seeded test data (real bank reconciliation itself requires real usage over time, out of scope for this checkpoint).
- [ ] Revisit D12's netting behavior and D13's `POST /instruments` routing one more time now that the full Fase-1 flow works end-to-end, to catch any follow-on inconsistency integration surfaced that Phases 2–4 didn't.
- [ ] **Doc-sync from Phase 4:** correct `docs/DESIGN.md` D12's worked example — the compensating entry's credit leg is `Pasivo:Tarjeta` (`Cr CardLiability`), not `Gasto:Categoría`. Phase 4 posts the storno **always**, so crediting `Gasto` again would double-count the expense; the implementation (`ReverseTransactionHandler` + `ReversalCalculator`) uses `Dr CardCredit / Cr CardLiability`.

### Definition of done
- [ ] Every Fase-1 acceptance criterion in PRD §6 has a corresponding passing manual/automated check.
- [ ] D12 and D13's behavior in code matches what `docs/DESIGN.md` describes — no drift between doc and implementation.
