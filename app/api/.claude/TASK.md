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

## Phase 20 — Debit/cash expenses with categories (Slice 3)

**Goal:** First way to record a debit or cash expense — one balanced Ledger transaction (money already gone, never an installment plan), with a required lightweight category that is get-or-created by name as an `Expense`-`Expense` Ledger account.

**Traces to:** `docs/expense-payment-modes/slice-3-debit-cash-categories.md` (final slice of the payment-modes initiative, decisions D3/D4/D6/D9/D11).

**Depends on:** Phase 18 (`PersonalFinance.Ledger` already references `PersonalFinance.Parties.Contracts` from the reversal cascade, so the split path reuses `RegisterSharedExpense` with no new module edge). No cross-module dependency outside existing `.Contracts` references.

### Tasks
- [x] `Ledger.Contracts/Queries/ListExpenseCategoriesQuery.cs` — `ListExpenseCategoriesQuery() : IQuery<ExpenseCategoriesResponse>`, `ExpenseCategoriesResponse(IReadOnlyList<ExpenseCategoryRow> Rows)`, `ExpenseCategoryRow(string Name)`. `Ledger.Contracts/Commands/GetOrCreateExpenseCategoryCommand.cs` — `GetOrCreateExpenseCategoryCommand(string Name) : ICommand<Guid>`.
- [x] `Application/ExpenseCategoryProvisioning.cs` (`internal static`) — the single normalization point: **trim + `OrdinalIgnoreCase` match** against `Type == Expense && Kind == Expense` accounts, first-writer casing wins, blank → `LedgerErrors.InvalidAccountName`; on miss mints `Account.Create(normalized, AccountType.Expense, AccountKind.Expense)` + `SaveChangesAsync`. `Application/Queries/ListExpenseCategories/ListExpenseCategoriesHandler.cs` — projects those account names, `.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase)` in memory. `Application/Commands/GetOrCreateExpenseCategory/GetOrCreateExpenseCategoryHandler.cs` — delegates to `ExpenseCategoryProvisioning`.
- [x] `Ledger.Contracts/Commands/RecordDebitExpenseCommand.cs` — `RecordDebitExpenseCommand(long AmountMinorUnits, Guid SourceAccountId, string CategoryName, DateOnly PurchaseDate, string Description, IReadOnlyList<RecordDebitExpenseParticipant>? Split = null) : ICommand<Guid>`, `RecordDebitExpenseParticipant(Guid PartyId, long Weight)`.
- [x] `Application/Commands/RecordDebitExpense/RecordDebitExpenseValidator.cs` — amount > 0 → `NonPositiveEntryAmount`; category non-blank → `InvalidExpenseCategory`; description non-blank → `InvalidExpenseDescription`; `SourceAccountId != Guid.Empty` → `AccountNotFound`; `Split` if present non-empty, each `PartyId != Guid.Empty` + `Weight > 0` + no dup party → `InvalidExpenseSplit`. `RecordDebitExpenseHandler.cs` (`LedgerDbContext`, `TransactionWriter`, `IPartiesApi`) — loads the source account (unknown → `AccountNotFound`), rejects `Kind` not in `{Bank, Cash}` → **`SourceAccountNotSpendable`**, resolves the category via `ExpenseCategoryProvisioning`, then **unsplit** → `Transaction.Post([Dr category(total), Cr source(total)], purchaseDate@midnight-UTC)` via `writer.PersistAsync`; **split (D9)** → `parties.RegisterSharedExpenseAsync(new RegisterSharedExpenseCommand(description.Trim(), amount, categoryAccountId, sourceAccountId, postedOnUtc, participants))`, returns that `ExpenseSplit` id. **`Transaction` has no description column** — an unsplit debit expense does not persist its description (detail lives in the category name); a split one keeps it on `ExpenseSplit`. Documented with a handler comment.
- [x] `Domain/LedgerErrors.cs` — `InvalidExpenseCategory`, `InvalidExpenseDescription`, `InvalidExpenseSplit`, `SourceAccountNotSpendable`; all added to the **422** bucket in `Endpoints/ErrorHttpStatusHelper.cs`.
- [x] `Ledger.Contracts/ILedgerApi.cs` — `GetOrCreateExpenseCategoryAsync` (→ `commandBus.SendAsync`) + `ListExpenseCategoriesAsync` (→ `queryBus.AskAsync`), impl in `Infrastructure/PublicApi/LedgerApi.cs`; all three handlers registered in `LedgerModule.Register`. Fake `ILedgerApi` impls in `PersonalFinance.Financing.Tests/FakeModuleApis.cs` + `PersonalFinance.Parties.Tests/OnPaymentPlanCreatedTests.cs` gained `NotSupportedException` stubs for both.
- [x] Host: `Endpoints/ExpenseCategories/GetExpenseCategories.cs` (→ `ILedgerApi.ListExpenseCategoriesAsync`), `Endpoints/DTOs/ExpenseCategoriesDTO.cs`, `Endpoints/Mapping/ExpenseCategoryMappingExtensions.cs`, `ApiRoutes.ExpenseCategories.Base = V1 + "/expense-categories"`, tag `"Expense categories"`, `.Produces<ExpenseCategoriesDto>(200)`; `Endpoints/Ledger/RecordDebitExpense.cs` (→ `ICommandBus.SendAsync<Guid>`), `Endpoints/DTOs/RecordDebitExpenseDTO.cs` (request `SourceInstrumentId` + string `PurchaseDate` parsed `DateOnly.Parse(..., CultureInfo.InvariantCulture)`), `LedgerMappingExtensions.ToRecordDebitExpenseCommand` / `ToRecordDebitExpenseResultDto`, `ApiRoutes.Ledger.Expenses = "/expenses"`, `201`/`404`/`422`, tag `"Ledger"`; both wired via `EndpointExtensions` + `ModuleRegistration.MapModuleEndpoints`.
- [x] `tests/PersonalFinance.Ledger.Tests/PersonalFinance.Ledger.Tests.csproj` gained a direct `Microsoft.EntityFrameworkCore.Sqlite` reference (+ regenerated `packages.lock.json`) for the in-memory SQLite harness — replicated as `private` members per test class (a shared base would expose the `internal` `LedgerDbContext` → CS0050).
- [x] Tests: `ListExpenseCategoriesHandlerTests.cs` (4) + `GetOrCreateExpenseCategoryHandlerTests.cs` (4) + `RecordDebitExpenseHandlerTests.cs` (9, with `RecordDebitExpenseTestDoubles.cs` — `FakePartiesApi` + `NoOpIntegrationEventDispatcher`) in `PersonalFinance.Ledger.Tests`; `ExpenseCategoriesTests.cs` (2) + `DebitExpenseTests.cs` (5 WAF — unsplit expense surfaces in `vw_ledger_monthly_expenses`, category reused case-insensitively, split posts per-party receivable + category debit, unknown source → 404, OpenAPI presence) in `PersonalFinance.Api.Tests`.

### Definition of done
- [x] `GET /v1/expense-categories` lists distinct `Expense`-`Expense` account names, case-insensitively sorted; `POST /v1/ledger/expenses` records an unsplit debit/cash expense as one balanced transaction and a split one as receivables + category debit via `RegisterSharedExpense`.
- [x] Category get-or-create is idempotent — same name (any casing/whitespace) never mints a second account.
- [x] New category accounts satisfy the `vw_ledger_monthly_expenses` filter (`Type='Expense' AND Kind NOT IN ('Receivable','CardPurchases')`) — proven by the `DebitExpenseTests` categorization fact.
- [x] No new EF migration (`AccountKind.Expense` already exists), no `IFinancingApi`/`IPartiesApi` change; `PersonalFinance.Architecture.Tests` green.
- [x] `dotnet test --solution` → **222 passed** (from 198 at Phase 19; Step 1 → 208, Step 2 → 222), 0 warnings; RNF-9 green; `dotnet restore --locked-mode` clean.
- [ ] Manual sanity walk (run the API, register a `debit` instrument, record a "Groceries" expense, confirm the balanced transaction + the category row in the monthly view, re-use "Groceries" and confirm no second account) — **handed to the user**, not run this session.

### Completion notes

All new code is Ledger-side. Endpoint-placement fork resolved to **Option A** (dedicated Ledger-side `POST /v1/ledger/expenses`, per the doc's own recommendation), and the test-project EF dependency fork resolved to **Option A** (add `Microsoft.EntityFrameworkCore.Sqlite` to `PersonalFinance.Ledger.Tests.csproj` + regenerate its lock file). Steps 1 and 2 were committed by the user (`e9b2d01`, `d4b5cd2`); step 3 (client) is `app/client/.claude/TASK.md` Phase 21. Built one green-lit step at a time (contracts + category get-or-create/list → debit-expense handler + host wiring → client → verification). Full verification: `dotnet test --solution` 222/222, 0 warnings; `PersonalFinance.Architecture.Tests` green; `dotnet restore --locked-mode` clean. Manual sanity walk handed to the user. Not committed by this session for the client step — the user commits their own.

## Phase 21 — Dashboard fixes: card name on future rows + expand hardening (Slice 2)

**Goal:** Fix two Dashboard "Card Debt by Cycle" symptoms with one root cause — future-installment cards showing a GUID instead of the card name, and expanding one card opening every row of the same physical card. This phase is the **API half (Bug A)**; the client `cycleByCard()` regroup (Bug B) and Slice 1 (drill-down wording) are `app/client/.claude/TASK.md` Phase 22.

**Traces to:** `docs/dashboard-fixes/slice-2-card-name-and-expand.md` (standalone planning doc; `docs/DESIGN.md` §D11 "Identidad de tarjeta compartida" gains one clause noting the Future half now labels with `CardName`).

**Depends on:** Phase 7 (Reporting `card_due_by_month.sql`), Phase 10 (`lower(CardId)` case-safe join key — kept untouched), Phase 18 (`vw_card_future_schedule` drop-and-recreate migration precedent). No cross-module dependency.

### Tasks
- [x] `src/Modules/Financing/PersonalFinance.Financing/Infrastructure/Persistence/ReadViews/vw_card_future_schedule.sql` — add `c.Name AS CardName` + inner `JOIN financing_credit_cards c ON c.Id = p.CardId`; the existing `WHERE p.CardId IS NOT NULL` guarantees the join drops no legitimate row.
- [x] `src/Reporting/PersonalFinance.Reporting/Sql/card_due_by_month.sql` — Future half: `CardId AS Card` → `CardName AS Card`; add `CardName` to the `GROUP BY`; keep `lower(CardId) AS CardId`. Embedded ADO string loaded by `ReportingSqlHelper` — **no migration for this file**.
- [x] Migration `20260906031333_UpdateCardFutureScheduleView` (FinancingDbContext) — empty scaffold (the view is keyless / not EF-mapped, consumed only by Reporting via raw ADO); `Up` = `DROP VIEW IF EXISTS vw_card_future_schedule;` + `ReadViewSqlHelper.Load("vw_card_future_schedule.sql")`; `Down` = drop + inline previous `CREATE VIEW` (no `CardName`, no card join). Single-migration form — no table rebuild, so the Phase-18 deferred-rebuild caveat does not apply. Applied to the dev `personalfinance.db`, verified via `sqlite_master`.
- [x] Tests: `tests/PersonalFinance.Reporting.Tests/ReportingQueryTests.cs` +1 fact `CardDueByMonth_labels_future_rows_with_the_card_name_not_its_id` — the seeded "Visa Reporting" future rows' `Card` equals the card name, not `ReportingCardId.ToString()`, not the `CardId` column.

### Definition of done
- [x] `GET /v1/reports/card-due-by-month` Future rows carry the card **name**; the Accrued/Future shared `lower(CardId)` key is unchanged.
- [x] No Ledger migration (`vw_card_liability_accrued` already correct); no new module edge — `PersonalFinance.Architecture.Tests` (RNF-9) green.
- [x] `dotnet test --solution` → **223 passed** (from 222 at Phase 20), 0 warnings.
- [ ] Manual live browser E2E walk (run the API + client, open a Dashboard with a card that has future installments, confirm the card **name** shows and expanding it opens **only** its own purchases) — **handed to the user**, not run this session.

### Completion notes

Two-symptom / one-cause fix built one green-lit step at a time (view → Reporting query → migration → client regroup → tests → verification). The client half (`dashboard-page.ts` `cycleByCard()` keyed on `cardId` instead of the label string; `@for` `track` → `card.cardId ?? card.card`) is `app/client/.claude/TASK.md` Phase 22, as is Slice 1. `docs/DESIGN.md` §D11 reconciled with one clause. Full verification: `dotnet test --solution` 223/223, 0 warnings; `PersonalFinance.Architecture.Tests` green; migration applied and verified against `sqlite_master`. Manual browser walk handed to the user. Committed by the user as `979eb54`.

## Phase 22 — Dashboard fixes: Parties list endpoint (Slice 3)

**Goal:** Make a created party visible everywhere — add a plain `GET /v1/parties` list endpoint in the Parties module so the client no longer depends on `GET /v1/reports/parties/debt-summary` (whose INNER JOIN drops any party with zero ledger movements) just to enumerate parties.

**Traces to:** `docs/dashboard-fixes/slice-3-parties-list-endpoint.md` (standalone planning doc; final slice of the dashboard-fixes initiative — fixes bug #2). No `docs/PRD.md` / `docs/DESIGN.md` change.

**Depends on:** Phase 6 (Parties module — `PartiesDbContext.Parties`, `IPartiesApi`), Phase 13 (Creditors list vertical — the pattern mirrored one module over). No cross-module dependency.

### Tasks
- [x] `src/Modules/Parties/PersonalFinance.Parties.Contracts/Queries/ListPartiesQuery.cs` — `PartyRow(Guid Id, string Name)`, `ListPartiesResponse(IReadOnlyList<PartyRow> Rows)`, `ListPartiesQuery() : IQuery<ListPartiesResponse>`.
- [x] `src/Modules/Parties/PersonalFinance.Parties/Application/Queries/ListParties/ListPartiesHandler.cs` — `context.Parties.AsNoTracking().Select(p => new { p.Id, p.Name }).ToListAsync()` then in-memory `OrderBy(Name, StringComparer.OrdinalIgnoreCase)` → `PartyRow`. No ledger join → every party appears.
- [x] `IPartiesApi.ListPartiesAsync` (+ `Infrastructure/PublicApi/PartiesApi.cs` impl `queryBus.AskAsync`); registered in `PartiesModule.Register`. `.Contracts`-only → RNF-9 unaffected. `ListPartiesAsync` `NotSupportedException` stub added to `FakePartiesApi` in `tests/PersonalFinance.Financing.Tests/FakeModuleApis.cs` + `tests/PersonalFinance.Ledger.Tests/RecordDebitExpenseTestDoubles.cs`.
- [x] Host: `ApiRoutes.Parties.List = "/"`; `Endpoints/Parties/GetParties.cs` (via `IPartiesApi` facade, like `GetCreditors`); `Endpoints/DTOs/PartiesListDTO.cs` — `PartyRowDto(Guid Id, string Name)` + `PartiesListDto(IReadOnlyList<PartyRowDto> Rows)` (`Guid Id`, matching the real `CreditorRowDto`); `PartyMappingExtensions.ToPartiesListDto`; `EndpointExtensions.MapPartiesEndpoints` `MapGet` with `.WithSummary`/`.WithDescription`/`.Produces<PartiesListDto>(200)`.
- [x] Tests: `tests/PersonalFinance.Parties.Tests/ListPartiesHandlerTests.cs` (2, in-memory SQLite — name-ordered case-insensitively; zero-movement party still listed) + `tests/PersonalFinance.Api.Tests/PartiesListTests.cs` (2 WAF — POST party then GET list includes it by id/name; OpenAPI `/v1/parties` GET under `Parties` tag with 200).

### Definition of done
- [x] `GET /v1/parties` returns every registered party ordered by name, including parties with zero ledger movements.
- [x] No EF migration (no schema change); no new module edge — `PersonalFinance.Architecture.Tests` (RNF-9) green.
- [x] `dotnet test --solution` → **227 passed** (from 223 at Phase 21), 0 warnings.
- [ ] Manual live E2E walk (`POST /v1/parties` a fresh party with no shared expense, then `GET /v1/parties` returns it; it shows on the Parties page as "settled" and is selectable in the load-expense split) — **handed to the user**, not run this session.

### Completion notes

Pure additive CQRS read query mirroring the Creditors list vertical one module over, built one green-lit step at a time (contract → handler → facade → register → host wiring → client → tests → verification). The client half (`parties-service.list()`, the Parties page merging `list()` + `debtSummary()` so movement-less parties render as "settled" / $0, the load-expense split switching from `debtSummary()` to `list()`) is `app/client/.claude/TASK.md` Phase 23. `PartyRowDto` uses `Guid Id` — the planning doc's `string Id` + `.ToString()` was based on a misreading of `CreditorRowDto`, which uses `Guid`; JSON serializes it as a string regardless. Full verification: `dotnet test --solution` 227/227, 0 warnings; `PersonalFinance.Architecture.Tests` green. Manual E2E walk handed to the user. Not committed by this session — the user commits their own.

## Phase 23 — Dashboard fixes follow-up: creditor-financed split posts the co-borrower receivable up front

**Goal:** Fix a bug surfaced by Phase 22 — a *recent* expense recorded with the "owed to a creditor" payment mode and split with a party shows that party as **"Settled up" / $0.00** on the Parties page. The co-borrower's share of a creditor-financed purchase must be owed from day one, exactly as it already is for a debit/cash split.

**Traces to:** the reported bug (root cause: creditor-financed split co-borrower receivables were posted only by the month-gated `AccrueCreditorSplitInstallments` scheduler, so nothing posted until an installment's owed month arrived — for a this-month purchase, never within the current month; the party then has zero movements and `vw_current_account_timeline`'s INNER JOIN drops it from `debt-summary`). Planning doc: `~/.claude/plans/staged-mapping-meerkat.md`. No `docs/PRD.md` / `docs/DESIGN.md` change (`DESIGN.md` never documented the retired scheduler).

**Depends on:** Phase 6 (Parties — `ExpenseSplit`, `CorrectExpenseSplit`), Phase 18 (creditor-financed plans — `CreditorPayableAccountId`, `PaymentPlanSplitParticipant`, the scheduler now retired). No new cross-module dependency — `ILedgerApi` and `IPartiesApi` were already referenced from Financing.

### Tasks
- [x] `Application/Scheduling/CreditorSplitAccrualCalculator` → `Application/Commands/LinkPaymentPlanSplit/CreditorSplitReceivableCalculator` (relocated beside the handler; pure phantom-penny / holder-weight-0-dropped allocation unchanged; input is now `plan.Total`, not one `installment.Amount`). Test `CreditorSplitAccrualCalculatorTests` → `CreditorSplitReceivableCalculatorTests` (retargeted, 5 facts unchanged).
- [x] `LinkPaymentPlanSplitHandler` — after `plan.AssignCreditorPayableAccount(...)` and before `SaveChangesAsync`, for `plan.CardId is null && plan.SplitParticipants.Count > 0`: build the co-borrower lines via `CreditorSplitReceivableCalculator.BuildLines(plan.Total, plan.SplitParticipants, plan.CreditorPayableAccountId.Value)` and `ledger.PostTransactionAsync(new PostTransactionCommand(lines, plan.PurchaseDate midnight-UTC, SplitReferenceId: command.SplitReferenceId, Description: "Creditor-financed split"))`; `posting.IsFailure` returns the error (load-bearing).
- [x] `OnPaymentPlanCreated` (Parties) — `ExpenseSplit.Create(..., accruedReceivable:)` is `Σ participant shares` when `integrationEvent.CardId is null`, else `Money.Zero` (unchanged for card plans).
- [x] Retire `AccrueCreditorSplitInstallments` + its `FinancingModule` registration + `Installment.MarkCreditorAccrued` + `AccrueCreditorSplitInstallmentsTests` (3 facts). Trim the now-dead `FakePartiesApi` from `tests/PersonalFinance.Financing.Tests/FakeModuleApis.cs`. `PaymentPlan.cs` comment pointer updated.
- [x] Reversal cascade — verified by trace: the up-front transaction reduces to the tested `RegisterSharedExpense` debit-split reversal shape (`SplitReference` set, `InstallmentReference` null, `Receivable` debit legs) → `ReversalCalculator.Decide(false, null, true)` → `CorrectParty: true`, `PostCompensating: false` → `CorrectExpenseSplitHandler` advances `ReversedReceivable` to equal `AccruedReceivable`. No code change.
- [x] Tests: `LinkPaymentPlanSplitHandlerTests` +1 (`Handle_books_the_co_borrower_receivable_in_full_up_front_for_a_card_less_split`; card-backed split asserts `Assert.Empty(PostedTransactions)`); `OnPaymentPlanCreatedTests` +2 (`Card_split_starts_with_a_zero_accrued_receivable`, `Creditor_financed_split_starts_with_the_full_co_borrower_receivable_accrued`).

### Definition of done
- [x] A creditor-financed expense split with a party posts the co-borrowers' full shares to the Ledger at link time — the party's `debt-summary` row (and Parties-page balance) is non-zero immediately, no scheduler wait.
- [x] Card-backed splits are unchanged (still accrue per cycle via `AccrueInstallments`).
- [x] Reversing the up-front transaction returns the party to settled and nets `ExpenseSplit.ReversedReceivable` to `AccruedReceivable`.
- [x] No EF migration (no schema change); no new module edge — `PersonalFinance.Architecture.Tests` (RNF-9) green.
- [x] `dotnet test --solution` → **227 passed** (net-flat from Phase 22 — +3 new facts, −3 deleted), 0 warnings.
- [ ] Manual live E2E walk (register a creditor + account; `POST /v1/financing/payment-plans` with `creditorId`, `creditorAccountId`, `installmentCount: 3`, a `split` participant; without waiting, `GET /v1/reports/parties/debt-summary` shows the party's share; `sqlite3` — one `ledger_transactions` row tagged with the split reference, `Σdebits == Σcredits`; then reverse it and re-check) — **handed to the user**, not run this session (no browser / long-lived host in this environment).

### Completion notes

Approach A of the planning doc (mirror the synchronous debit/cash split), built one green-lit step at a time (relocate the pure calculator → post the receivable up front → set the metadata at creation → retire the scheduler → reversal check + docs). The old `AccrueCreditorSplitInstallments` existed only to trickle-post these same shares month by month; booking the whole co-borrower receivable at link time makes it redundant and removes the month-gate that caused the bug. Creditor-financed `Installment` rows survive as a pure repayment schedule (feeds `GetCreditorPayables` `NextDueDate`) — never accrued. `dotnet test --solution` 227/227, 0 warnings; `PersonalFinance.Architecture.Tests` green. Not committed by this session — the user commits their own.

---

## Phase 24 — Parties card-split: a party's future monthly shares (Slice 2b)

**Goal:** Surface, on the Parties view, what a co-borrower *will* owe on a card-backed split — one projected row per not-yet-accrued installment share — so a freshly-split party no longer reads "$0 / settled" until the first billing cycle closes. Byte-exact with what `AccrueInstallments` will post.

**Traces to:** `docs/parties-card-split/slice-2b-party-future-shares.md` (+ `README.md` for shared context; final slice of the parties-card-split initiative — Slice 1, the client-only reconcile-loop fix, is `app/client/.claude/TASK.md` Phase 24 and shipped as `959a3a2`). `docs/PRD.md` §9 gains decision 8.

**Depends on:** Phase 3 (Financing `Installment` / `PaymentPlan` / `BillingCycle`), Phase 6 (Parties split flow — `PaymentPlanSplitParticipant`, `AccrueInstallments.buildSplitLines`, the phantom-penny holder-at-index-0 split). No cross-module dependency outside `Financing.Contracts`.

### Tasks
- [x] `Financing.Contracts/Queries/GetFuturePartySharesQuery.cs` — `FuturePartyShareRow(int CycleYear, int CycleMonth, long ShareMinorUnits, string CurrencyCode, string SourceLabel)`, `GetFuturePartySharesResponse(IReadOnlyList<FuturePartyShareRow> Rows)`, `GetFuturePartySharesQuery(Guid PartyId) : IQuery<GetFuturePartySharesResponse>` — mirrors `ListCreditorsQuery`.
- [x] `Application/Queries/GetFuturePartyShares/GetFuturePartySharesHandler.cs` — EF query: `context.Set<Installment>()` where `AccruedOnUtc == null && IsReversed == false`, join `PaymentPlans` where `CardId != null && SplitParticipants.Any(p => p.PartyId == query.PartyId)`, inner-join `CreditCards` for the name; then in memory per plan: load `PaymentPlanSplitParticipant` ordered `OrderBy(PartyId)`, `long[] weights = [1L, .. participants.Select(p => p.Weight)]`, `new PhantomPennyAllocator().Allocate(installment.Amount, weights)`, party slice = `shares[index + 1]`, skip `<= 0`; emit `FuturePartyShareRow(installment.CycleYear, installment.CycleMonth, share.MinorUnits, share.Currency.Code, $"{CardName} — {plan.Description}")`; order by `CycleYear` then `CycleMonth`. Identical split shape to `AccrueInstallments.buildSplitLines` → projection == accrual. Registered in `FinancingModule.Register`.
- [x] `IFinancingApi.GetFuturePartySharesAsync` (+ `Infrastructure/PublicApi/FinancingApi.cs` impl `queryBus.AskAsync`); `.Contracts`-only → RNF-9 unaffected. `GetFuturePartySharesAsync` `NotSupportedException` stub added to `FakeFinancingApi` in `tests/PersonalFinance.Parties.Tests/OnPaymentPlanCreatedTests.cs`.
- [x] Host: `ApiRoutes.Parties.FutureShares = "/{id:guid}/future-shares"`; `Endpoints/Parties/GetPartyFutureShares.cs` (`Ok<FuturePartySharesDto>`, via the `IFinancingApi` facade — allowed from a Parties-grouped endpoint, host is the composition root); `Endpoints/DTOs/FuturePartySharesDTO.cs` (`FuturePartyShareDto` + `FuturePartySharesDto`); `PartyMappingExtensions.ToFuturePartySharesDto`; `EndpointExtensions.MapPartiesEndpoints` `MapGet` with `.WithSummary`/`.WithDescription`/`.Produces<FuturePartySharesDto>(200)`, tag `"Parties"`.
- [x] Tests: `tests/PersonalFinance.Financing.Tests/GetFuturePartySharesHandlerTests.cs` (6, in-memory SQLite — one row per unaccrued installment with the exact allocator share + label; odd-cent share matches `PhantomPennyAllocator`; accrued + reversed excluded; card-less creditor plan excluded; no-split card plan excluded; multi-party split returns only the queried party's slice) + `tests/PersonalFinance.Api.Tests/PartyFutureSharesTests.cs` (2 WAF — real split pipeline → `GET /v1/parties/{id}/future-shares` returns the scheduled rows; OpenAPI presence under `Parties` with `200`).

### Definition of done
- [x] `GET /v1/parties/{id}/future-shares` returns one row per not-yet-accrued card-split installment share for that party, ordered by billing cycle, each share byte-exact with `PhantomPennyAllocator`.
- [x] Accrued / reversed installments, card-less creditor plans, and no-split card plans are all excluded; a multi-party split returns only the requested party's slice.
- [x] No EF migration, no Ledger write, no Reporting change; `IFinancingApi` widened `.Contracts`-only — `PersonalFinance.Architecture.Tests` (RNF-9) green.
- [ ] `dotnet test --solution` → **235** expected (227 at Phase 23 + 6 handler + 2 WAF). The six handler facts and two WAF facts pass in isolation; the full-solution re-run + full `pnpm ng test`, and the manual live walk (Sept card-split → party-detail scheduled rows → a cycle closes → the row moves to the posted timeline), are the slice's outstanding Verify steps.

### Completion notes

Pure additive read-only CQRS query on the `GetCardPurchases` / `GetCreditorPayables` precedent, built one green-lit step at a time (contract → handler → facade → register → host wiring → client → tests). Reuses `PhantomPennyAllocator` in C# rather than a Reporting SQL view precisely because SQL cannot reproduce the phantom-penny tie-break (holder at weight-index 0) — a view-based projection would drift a minor unit from real accrual on odd splits. **Known limitation — billing-cycle anchor:** the rows carry each installment's stored `(CycleYear, CycleMonth)` = the **statement-close** cycle, identical to every other card read path and to `AccrueInstallments`. For a purchase on or before the card's cutoff day that is the **purchase-month** cycle (`BillingCycleCalculator.ResolveCycle` — Sept 6 purchase, cutoff 15 → Sept/Oct/Nov), i.e. the month each statement closes, not the month it is paid. `docs/parties-card-split/README.md` / `slice-2b-party-future-shares.md` describe the first owed month as *purchase month + 1* (Oct/Nov/Dec) — that holds for creditor-financed plans (`PaymentPlan.Create` schedules `purchaseMonth + k` there) but not card plans. Whether the "Scheduled" block should shift to the payment month (cycle + 1) is an **open product decision** recorded as `docs/PRD.md` §9 decision 8; the slice ships showing the statement-close cycle, consistent with the rest of the app, and `BillingCycleCalculator` / accrual are untouched. Client half (party-detail "Scheduled" block) is `app/client/.claude/TASK.md` Phase 24. Committed by the user as `74bf11a` (query + endpoints), `e281437` (client), `09357cb` (tests).

## Phase 25 — Card due-month (Slice 1)

**Goal:** Every *payment-facing* card surface shows and behaves by the **due month** (`DueCycle = statement-close cycle + 1`, "when the money moves") — a Sept-6/cutoff-15 purchase reads Oct/Nov/Dec, a post-cutoff purchase reads two months out — while *statement-facing* surfaces keep the raw stored close cycle. The split co-borrower receivable moves from statement-close to due-month arrival, so the schedule and the posted balance never contradict each other. Nothing stored changes.

**Traces to:** `docs/cycle-due-month/slice-1-card-due-month.md` (+ `00-overview.md` for the shared model; initiative "Billing cycle 'due month' reframe + creditor-split parity"). **Resolves Phase 24's open question** and `docs/PRD.md` §9 decision 8's *cuestión abierta*; adds §9 decision 9. Slices 2 (creditor-split parity) and 3 (schedule-aware summary) are not started.

**Depends on:** Phase 3 (`BillingCycle` / `Installment` / `MonthlyStatement` / `AccrueInstallments`), Phase 6 (Parties split flow, phantom-penny holder-at-index-0), Phase 24 (`GetFuturePartyShares`), Phase 16 (`GetCardPurchases` "outstanding" predicate, reused by `GetCardFutureSchedule`). No cross-module dependency outside `Financing.Contracts` / `Ledger.Contracts` / `Parties.Contracts`.

### Tasks
- [x] `Domain/BillingCycle.cs` — `public BillingCycle DueCycle => AddMonths(1);`. `Domain/Installment.cs` — `public BillingCycle DueCycle => Cycle.DueCycle;` + `DateTimeOffset? SplitAccruedOnUtc` (private setter) + `IsSplitAccrued` + `Result MarkSplitAccrued(DateTimeOffset)` (double-set guard → `FinancingErrors.InstallmentSplitAlreadyAccrued`). `Domain/BillingCycleCalculator.cs` **untouched**.
- [x] `Infrastructure/Persistence/Configurations/InstallmentConfiguration.cs` — `Property(i => i.SplitAccruedOnUtc)`, `Ignore(i => i.IsSplitAccrued)`, `Ignore(i => i.DueCycle)`.
- [x] `Application/Scheduling/AccrueInstallments.cs` — constructor gains `TimeProvider timeProvider`; `TickAsync` reads `now = timeProvider.GetUtcNow()` and calls two named gates. **Gate 1** (`accrueClosedCyclesAsync`, `IsClosedAsOf` — timing unchanged): posts `Dr CardExpense(full) / Cr CardLiability(full)` for every installment (split or not), rolls onto the `MonthlyStatement`, dispatches `InstallmentAccruedIntegrationEvent`; **no party legs, no `RecordSplitAccrualAsync`**. **Gate 2** (`accrueDueSplitReceivablesAsync`): accrued + `SplitAccruedOnUtc == null` + non-reversed + card + split, once `installment.DueCycle` has begun by month ordinal → posts `Dr Receivable_k(share_k) / Cr CardExpense(Σ)` tagged **`SplitReferenceId` only** + `Description: "Split receivable — due month"`, then `MarkSplitAccrued(now)` + `IPartiesApi.RecordSplitAccrualAsync` (best-effort). `buildSplitReceivableLines` rebuilds the split with `PhantomPennyAllocator.Allocate(amount, [1L, ..weights])`, holder slice dropped.
- [x] `Application/Queries/GetFuturePartyShares/GetFuturePartySharesHandler.cs` — predicate `AccruedOnUtc == null` → `SplitAccruedOnUtc == null`; rows carry `installment.DueCycle.(Year, Month)`.
- [x] `Application/Queries/GetCardFutureSchedule/GetCardFutureScheduleHandler.cs` — project `new BillingCycle(CycleYear, CycleMonth).DueCycle`; predicate `AccruedOnUtc == null` → `!IsReversed && (AccruedOnUtc == null || statement not paid)` (pull the card's paid-statement ids, filter in memory — `GetCardPurchasesHandler` precedent). `Financing.Contracts/Queries/GetCardFutureScheduleQuery.cs` XML doc comments updated.
- [x] `Infrastructure/Persistence/ReadViews/vw_card_future_schedule.sql` — `CASE WHEN i.CycleMonth = 12 THEN i.CycleYear + 1 ELSE i.CycleYear` / `THEN 1 ELSE i.CycleMonth + 1` on both cycle columns. `Reporting/Sql/card_due_by_month.sql` **no change** (inherits the projection). Statement-facing (`GetCardStatements`, `GetMonthlyStatement`, `GetCardPurchases`) **left on the raw close cycle**.
- [x] Migrations (FinancingDbContext): `20260906205213_ProjectDueCycleOnCardFutureSchedule` (hand-authored view rebuild — `Up` = `DROP VIEW IF EXISTS` + `ReadViewSqlHelper.Load(...)`, `Down` = raw-cycle `CREATE VIEW`) then `20260906212513_AddInstallmentSplitAccruedOnUtc` (`AddColumn` nullable TEXT + backfill `UPDATE` for already-accrued split installments).
- [x] Tests: new `AccrueInstallmentsTests.cs` (in-memory SQLite + DI + fakes + `FixedTimeProvider`, `TickAsync` via reflection — Gate 1 holds / Gate 2 reclassifies at due cycle / Gate 2 idempotent / Gate 1 no stranding), new `GetCardFutureScheduleHandlerTests.cs` (5 — literal Oct/Nov/Dec + Nov/Dec/Jan year roll, accrued-unpaid kept, dropped once paid, reversed excluded), new `GetCardStatementsHandlerTests.cs` (1 — statement stays the close month), `GetFuturePartySharesHandlerTests.cs` +2 literal-month + reworked exclusion test; `FakeModuleApis.cs` gains `FakePartiesApi` + `NoOpIntegrationEventDispatcher`.

### Definition of done
- [x] Payment-facing card surfaces (`GetFuturePartyShares`, `GetCardFutureSchedule`, `vw_card_future_schedule` → dashboard `card-due-by-month`) show the due month; statement surfaces (`GetCardStatements`, `GetMonthlyStatement`) still show the close month.
- [x] A split co-borrower owes $0 until the due month, with the schedule visible meanwhile (Gate 2); an accrued-but-unpaid solo installment stays on the card's forward schedule under its due month until the statement is paid.
- [x] `AccrualBoundaryTests` unchanged and green — write side / storage / `ResolveCycle` untouched. Financing suite 89 (from 81), Api 41, Architecture 15, Reporting 7 all green (direct test binary).
- [x] Client: no production change; `pnpm ng lint` clean, `pnpm ng build --configuration production` clean, `pnpm ng test` 214/214 (2 stale `load-expense-page` reconcile assertions re-pointed to debit mode).
- [ ] Live smoke on the Sept-6 repro (Oct/Nov/Dec) — handed to the user; no browser in this environment. **`dotnet ef database update` for `FinancingDbContext` is required before the live check** — the dev DB drifted (`no such column: SplitAccruedOnUtc` on a live `GET /v1/parties/{id}/future-shares`) because the suites build schema via `EnsureCreated()` and never exercise the migrations; applied this session.

### Completion notes

The heart of the slice is the `AccrueInstallments` two-gate split. Gate 1's timing is unchanged (`IsClosedAsOf`), so a split installment always lands on its statement at close before any payment — the plain-A "catch-up on `StatementAlreadyPaid`" concern the LLM Council raised (`docs/cycle-due-month/council-*-slice1-step5.*`) is moot here. The Gate-2 reclassification is tagged **`SplitReferenceId` only** (no `InstallmentReferenceId`) so `FindAccrualTransactionIdsHandler` (`OriginalTransactionId == null && InstallmentReference != null`, group-by-ref, `.First()`) stays unambiguous for the reversal cascade. **Residual gap:** reversing a split installment *after* its due month leaves the Gate-2 receivable leg un-stornoed — consistent with the codebase's "no saga, log-and-continue" cascade stance; a candidate for a later slice or explicit handling in `ReverseTransactionHandler` (Phase 26 did not pick it up). The `SplitAccruedOnUtc` column (an approved middle path — plain-A had no idempotency key, the council's column-free variant needed a heuristic) makes Gate 2 idempotent on a simple query predicate. `GetCardFutureScheduleHandler` keeping accrued-but-unpaid rows was a user decision over tests-only; it now returns a superset of `vw_card_future_schedule` (still un-accrued only) — fine for its own consumer, but do not point the Reporting `card_due_by_month.sql` `UNION` at it (double-count vs the accrued bucket). Client needed no code change — every cycle render is verbatim except `party-detail-page.ts`'s `MONTH_LABELS[cycleMonth - 1]` array index. API steps 1–6 committed by the user as `83d7809` + `22f88b1`; client steps 7–8 and `GetCardStatementsHandlerTests.cs` uncommitted at time of writing.

---

## Phase 26 — Creditor-split parity (Slice 2)

**Goal:** A creditor-financed ("owed to a creditor") expense split behaves **exactly like a card split** — **$0 owed now**, the co-borrower receivable **accrues at its due month** (`DueCycle`), and it shows in the party "Scheduled" block dated the payment month. Undoes Phase 23's up-front booking (safe now — Phase 25 made the future schedule visible, so a $0-now party reads "N scheduled", not "settled").

**Traces to:** `docs/cycle-due-month/slice-2-creditor-split-parity.md` (+ `00-overview.md`; second slice of "Billing cycle 'due month' reframe + creditor-split parity"). **Depends on** Phase 25 (`DueCycle`, `SplitAccruedOnUtc`, `MarkSplitAccrued`, the `AccrueInstallments` two-gate scheduler), Phase 18/23 (card-less creditor `PaymentPlan`, `CreditorPayableAccountId`, `CreditorSplitReceivableCalculator`), Phase 24 (`GetFuturePartyShares`). `docs/PRD.md` §9 gains **decision 10**. No cross-module dependency outside the existing `.Contracts` edges. Slice 3 (schedule-aware Parties **list** summary) is not started.

### Tasks
- [x] `Domain/PaymentPlan.cs` — `Create` creditor branch: `new BillingCycle(purchaseDate.Year, purchaseDate.Month)` (**dropped `.AddMonths(1)`**), so the uniform `DueCycle = +1` projection yields first payment = month after purchase, no card-vs-creditor branching in any reader. Card branch untouched. **No migration** — dev `personalfinance.db` data-wiped (all dummy; no FK `financing_payment_plans` → `financing_creditors`).
- [x] `Application/Queries/GetCreditorPayables/GetCreditorPayablesHandler.cs` — `NextDueDate` projects `new BillingCycle(earliest.CycleYear, earliest.CycleMonth).DueCycle` before `buildDueDate(...)` (pulled into step 1 so no interim commit shows the purchase month as "next due"). `buildDueDate` signature unchanged.
- [x] `Modules/Parties/.../Application/EventHandlers/OnPaymentPlanCreated.cs` — `accruedReceivable` ternary collapses to `Money.Zero(currency)` for both branches (restores D8 / Option-C "record the split intent only"; Phase 23 had it book the full co-borrower sum for creditor plans).
- [x] `Application/Commands/LinkPaymentPlanSplit/LinkPaymentPlanSplitHandler.cs` — deleted the `if(plan.CardId is null && plan.SplitParticipants.Count > 0)` up-front `"Creditor-financed split"` ledger post (`Dr Receivable_k / Cr CreditorPayable` on `PurchaseDate`); **kept** the `CreditorPayableAccountId` provisioning block above it.
- [x] `Application/Scheduling/AccrueInstallments.cs` — new third gate `accrueDueCreditorSplitReceivablesAsync` after Gate 2, one tick. Filters `installment.SplitAccruedOnUtc == null && !IsReversed`, join `PaymentPlans` where `CardId == null && SplitReferenceId != null && CreditorPayableAccountId != null` (no `CreditCards` join, no `AccruedOnUtc` precondition). In-memory due gate on `installment.DueCycle` month ordinal `<= today`. Legs via `CreditorSplitReceivableCalculator.BuildLines(installment.Amount, splitParticipants, plan.CreditorPayableAccountId.Value)` → `Dr Receivable_k / Cr CreditorPayable`, `PostTransactionCommand` tagged **`SplitReferenceId` only** (not `InstallmentReferenceId` — matches Gate 2, diverges from the retired `AccrueCreditorSplitInstallments`; keeps reversal on the tested `Decide(hasInstallment:false, _, true)` path), `Description: "Creditor-financed split accrual"`, then `installment.MarkSplitAccrued(now)` + `IPartiesApi.RecordSplitAccrualAsync(plan.SplitReferenceId, partyPortionMinorUnits)` (best-effort). `partyPortion <= 0` → `MarkSplitAccrued` + save, no post. New `using PersonalFinance.Financing.Application.Commands.LinkPaymentPlanSplit;` (same assembly — RNF-9 unchanged); `CreditorSplitReceivableCalculator` left in the `LinkPaymentPlanSplit/` folder.
- [x] `Application/Queries/GetFuturePartyShares/GetFuturePartySharesHandler.cs` — removed `where plan.CardId != null`; `CreditCards` join → `.DefaultIfEmpty()` (LEFT); added `Creditors` LEFT join on `plan.CreditorId`; projection `CardName = card == null ? null : card.Name` / `CreditorName = creditor == null ? null : creditor.Name`; `sourceLabel = CardName ?? CreditorName ?? "Financed"` + description. XML doc widened. Ordering unchanged (raw cycle order is monotonic with `DueCycle`).
- [x] Tests: `CreatePaymentPlanHandlerTests.…from_the_purchase_month_so_the_due_cycle_is_the_following_month` (stored `[(2026,1),(2026,2),(2026,3)]` **and** `DueCycle` `[(2026,2),(2026,3),(2026,4)]`); `OnPaymentPlanCreatedTests.…also_starts_with_a_zero_accrued_receivable` (asserts `0`); `LinkPaymentPlanSplitHandlerTests.Handle_posts_no_receivable_on_link_for_a_card_less_split`; `AccrueInstallmentsTests` **+4** (+ `SeedCreditorSplitPlanAsync` helper) — nothing before the due cycle / `Dr receivable 1500 / Cr payable 1500` on arrival with the right description + null `InstallmentReferenceId` + `RecordSplitAccrual 1500` + `AccruedOnUtc` still null / idempotent / per-installment Feb-Mar-Apr summing 4500; `GetFuturePartySharesHandlerTests.Handle_includes_a_card_less_creditor_financed_split_dated_by_the_due_month` (real seeded `Creditor`, rows `(2026,2)/(2026,3)/(2026,4)`, `SourceLabel "MercadoPago — Creditor purchase"`, share `1500`) + `SeedCreditorAsync` helper. `GetCreditorPayablesHandlerTests.…next_due_date` unchanged and still green.

### Definition of done
- [x] Loading a creditor-financed split produces the same on-screen story as a card split: $0 now, schedule ahead (`GET /v1/parties/{id}/future-shares` returns creditor rows dated `DueCycle`), accrues at the due month.
- [x] No "Settled up" regression in the party **detail** schedule (list summary → Slice 3).
- [x] No schema change, no EF migration, no new module edge; `PersonalFinance.Architecture.Tests` (RNF-9) green.
- [x] `dotnet build -c Release` 0W/0E; `dotnet test --solution` → **Financing 93** (from 89), Api 41, Ledger 31, Architecture 15, Reporting 7, Subscriptions 32, Parties 32 = 251, 0 failed. `AccrualBoundaryTests` 7/7 (the creditor `PaymentPlan.Create` branch moved, but that suite only exercises the card `ResolveCycle` path).
- [x] Client: `load-expense-page.ts` `reconcile()` creditor mode joins the card `'scheduled'` short-circuit; `party-detail-page.html` "Scheduled" copy widened; `pnpm ng lint` clean, `pnpm ng test` **215/215**, `pnpm ng build --configuration production` clean.
- [ ] Live smoke (no browser here) — handed to the user: creditor-financed Sept-6 split, 3 installments → $0 now, "Scheduled" reads Oct/Nov/Dec, no "Settled up" in the party detail view.

### Completion notes

Built one green-lit step at a time (write path → $0 intent → remove up-front post → due-month gate → read path → full gate → client → client verify); the user committed each API step (`d4dd08f` steps 1–4, `bf4477b` step 5), client steps 7–8 (3 files) uncommitted at time of writing. **Two divergences from the slice doc, both flagged and kept:** (1) the client `reconcile()` was **not** "mostly free" — removing the synchronous up-front post means a creditor split's co-borrower balance no longer moves at submit, so the `pollUntil` loop would only ever stall; the fix is the same `'scheduled'` short-circuit `mode === 'card'` already had. (2) `GetCreditorPayables.NextDueDate` was pulled into step 1 (not step 5) so no interim commit reports the purchase month as "next due". **Tagging decision:** the creditor accrual tx carries `SplitReferenceId` only — matches Gate 2, diverges from the retired `AccrueCreditorSplitInstallments` (which tagged both) — so every reversal reduces to the tested `Decide(hasInstallment:false, _, true)` path; creditor installments have no `MonthlyStatement` rows, so `FindAccrualTransactionIdsHandler` is never queried for them. `CreditorSplitReceivableCalculator` is reused verbatim (fed `installment.Amount` instead of `plan.Total`) — not reinvented — and left in `Application/Commands/LinkPaymentPlanSplit/` though its only consumer is now the scheduler gate + its own test; relocation is low-value churn. The "one scheduler, one due-month mechanism" from the slice doc is satisfied — same tick, same `DueCycle` gate, same `SplitAccruedOnUtc` marker, same `RecordSplitAccrualAsync` — just a creditor-specific leg builder (`Cr CreditorPayable` vs Gate 2's `Cr CardExpense`). Slice 3 (`docs/cycle-due-month/slice-3-schedule-aware-summary.md`) makes the Parties **list** summary schedule-aware so a $0-now scheduled party stops reading "Settled up" there too — Phase 27 below.

## Phase 27 — Schedule-aware summary (Slice 3)

**Goal:** The Parties **list** stops labelling a party "Settled up" when it owes $0 **now** but has not-yet-accrued split installments (card or creditor) scheduled ahead. Distinguish *truly settled* ($0 posted, nothing scheduled) from *$0 now, N scheduled*. The party **detail** schedule is already correct after Phase 26.

**Traces to:** `docs/cycle-due-month/slice-3-schedule-aware-summary.md` (+ `00-overview.md`; final slice of "Billing cycle 'due month' reframe + creditor-split parity"). **Depends on** Phase 25 (`SplitAccruedOnUtc`, `DueCycle`), Phase 24/26 (`GetFuturePartyShares` covering card + creditor). `docs/PRD.md` §9 gains **decision 11**; `docs/DESIGN.md` §"Mes de pago vs. mes de cierre" gains a trailing clause. No cross-module dependency outside the existing `.Contracts` edges. **This closes the `docs/cycle-due-month/` initiative.**

**Doc deviation (flagged, user-approved):** the slice doc says "extend `debt_by_party.sql` / the debt-summary read" — architecturally blocked (`Reporting` is a leaf module over `vw_*` views via raw ADO, no `DbContext` edge to `Financing`; phantom-penny allocation can't be done in SQL). Instead: a new Financing bulk query + a third client `forkJoin` source.

### Tasks
- [x] `Financing.Contracts/Queries/GetPendingSharesByPartyQuery.cs` — `GetPendingSharesByPartyQuery()` / `GetPendingSharesByPartyResponse(IReadOnlyList<PendingSharesByPartyRow> Rows)` / `PendingSharesByPartyRow(Guid PartyId, int ScheduledCount, long ScheduledTotalMinorUnits, string CurrencyCode)`.
- [x] `Application/Queries/GetPendingSharesByParty/GetPendingSharesByPartyHandler.cs` — reuses `GetFuturePartySharesHandler`'s predicate (`installment.SplitAccruedOnUtc == null && !installment.IsReversed`, join `PaymentPlans` where `plan.SplitParticipants.Any()`) + the same `PhantomPennyAllocator` rebuild (`weights = [1L, .. participant weights]`, holder = `shares[0]`), **drops the single-party filter**, accumulates count + summed minor-units per party (nested `PartyPendingAccumulator`), one row per party with a positive pending share, ordered by `PartyId`. Card and creditor both, no branch.
- [x] `FinancingModule.Register` — registered beside `GetFuturePartySharesHandler`. `IFinancingApi.GetPendingSharesByPartyAsync` + `FinancingApi` impl (`queryBus.AskAsync`) — `.Contracts`-only, RNF-9 unaffected; `FakeFinancingApi` in `tests/PersonalFinance.Parties.Tests/OnPaymentPlanCreatedTests.cs` got a `NotSupportedException` stub.
- [x] Host `GET /v1/parties/pending-shares` — `ApiRoutes.Parties.PendingShares = "/pending-shares"` (literal; no clash with `/{id:guid}/future-shares`), `Endpoints/Parties/GetPendingSharesByParty.cs` (fans through `IFinancingApi`, no `{id}`), `Endpoints/DTOs/PendingSharesByPartyDTO.cs`, `PartyMappingExtensions.ToPendingSharesByPartyDto`, `EndpointExtensions.MapPartiesEndpoints` wiring with `.WithSummary`/`.WithDescription`/`.Produces<PendingSharesByPartyDto>(200)`, tag `"Parties"`.
- [x] Tests: `GetPendingSharesByPartyHandlerTests.cs` (6 — card split → one row count 3 / total 4500 / ARS; card-less creditor split included; split-accrued + reversed excluded from the count; multi-party split → one row per party with exact allocator shares; one party across two plans → one summed row count 5 / total 7500; card plan with no split participants omitted) + `PartyPendingSharesTests.cs` (2 WAF — happy path returns `scheduledCount 3` / `scheduledTotalMinorUnits 4500` / `currencyCode "ARS"`; OpenAPI presence under `Parties` with `200`).

### Definition of done
- [x] The Parties list distinguishes truly-settled from $0-now-with-a-schedule, for both card and creditor.
- [x] No regression to "They owe you" / "You owe them" / "Settled up" when there is a real posted balance.
- [x] No schema change, no EF migration, no new module edge; `PersonalFinance.Architecture.Tests` (RNF-9) green.
- [x] `dotnet build -c Release` 0W/0E; test binaries run directly → **Financing 99** (from 93), Api 43 (from 41), Ledger 31, Subscriptions 32, Parties 32, Reporting 7, Architecture 15 = **259**. `AccrualBoundaryTests` 7/7.
- [x] Client: `PartiesService.pendingShares()` + `pending-shares-by-party-row.ts`; `parties-page.ts` third `forkJoin` source + `scheduledCount` VM field + `balanceHint()` "Nothing owed yet · N scheduled" branch; `parties-page.html` `[class.text-ledger]` guard widened. `pnpm ng lint` clean, `pnpm ng test` **218/218**, `pnpm ng build --configuration production` clean.
- [ ] Live browser walk (no browser here) — handed to the user: a $0-now card/creditor split party reads "Nothing owed yet · N scheduled" on the Parties list; a truly-settled party still reads "Settled up".

### Completion notes

Built one green-lit step at a time; the user committed API steps 1–2 as `ff478ca`, the rest (API tests + all client) uncommitted at time of writing. **One divergence from the slice doc, flagged and kept:** its literal instruction to "extend the debt-summary read" is not viable — `Reporting` cannot reach `Financing`'s `DbContext` and the largest-remainder split allocation has no SQL form — so the pending-schedule figure comes from a new Financing bulk query (`GetPendingSharesByParty`, reusing `GetFuturePartySharesHandler`'s shape verbatim) that the client merges as a third `forkJoin` source alongside `list()` + `debtSummary()`. **One copy call:** the list hint reads **"Nothing owed yet · N scheduled"** rather than the doc's literal "$0 now · N scheduled" — the amount cell beside it already shows the zero, and "$0" assumes a currency glyph `formatArs` may not use. The roster from `GET /v1/parties` (Phase 22, no ledger join) already contains every party, so a schedule-only party needs no extra merge — the count is just mapped on. This is the last slice of `docs/cycle-due-month/`; the initiative is complete.

---

## Phase 28 — Foundation: paid-state + payable-from-installments (Slice 1)

**Goal:** Install the plumbing for paying a statement's cuotas individually and isolate the one risky change — `PayStatement` charging the **installment-derived payable** (Σ accrued, non-reversed, unpaid) instead of the stored `MonthlyStatement.AmountDue` — **before any new payment path exists**, so it is provably behavior-preserving for reversal-free statements. **No new user-facing payment path** (that is Slice 2).

**Traces to:** `docs/individual-installment-payments/slice-1-foundation-payable-from-installments.md` (+ `00-overview.md`; first slice of the "pay a statement's cuotas individually" initiative). `docs/DESIGN.md` D2 (`PayStatementCommand` + `GetMonthlyStatementQuery`) updated; `docs/PRD.md` §9 gains **decision 12**. No cross-module dependency — the change is entirely inside Financing + host + the Contracts DTO. **Slices 2 (`POST /v1/financing/installments/{id}/pay` + per-row Pay UI) and 3 ("N/M paid · next month" on Recent Purchases) remain — not started.**

### The correction, stated plainly

`MonthlyStatement.AmountDue` is only ever incremented (`AmountDue += installment.Amount`; no `-=` in the module). `Installment.MarkReversed()` only flips a flag — it never decrements `AmountDue`. But reversing an *accrued* cuota **does** reduce the ledger `CardLiability` (storno `Dr CardLiability / Cr CardPurchases`, plus a compensating `Dr CardCredit / Cr CardLiability` when the statement was already paid). So charging the stored `AmountDue` on a statement that holds a reversed cuota **over-debits the liability and overpays from the bank** — a latent bug today. Charging Σ (accrued, non-reversed, unpaid) installment amounts matches what the ledger liability already reflects: it **fixes the overpay** and is **byte-identical for reversal-free statements**. Precedent: `GetCreditorPayablesHandler` already filters `IsReversed == false` when summing what is owed.

### Tasks
- [x] `Domain/Installment.cs` — `PaidOnUtc` (nullable `DateTimeOffset`, private setter), `IsPaid => PaidOnUtc is not null`, `MarkPaid(paidOnUtc) : Result` (guard: `IsPaid` → `FinancingErrors.InstallmentAlreadyPaid`, mirroring `MarkReversed` / `MarkSplitAccrued`). `FinancingErrors.InstallmentAlreadyPaid` = `("Financing.InstallmentAlreadyPaid", "The installment has already been paid.")`.
- [x] `Domain/MonthlyStatement.cs` — pure predicate `IsFullyPaidBy(IEnumerable<Installment> statementInstallments)` = every accrued, non-reversed installment `IsPaid`. The aggregate has **no installment navigation** (`HasOne<MonthlyStatement>().WithMany()`, no collection), so the caller passes the installments joined on the statement id. `AmountDue` left as the historical accrual total — **not** kept in sync with reversals.
- [x] `Application/Commands/PayStatement/PayStatementHandler.cs` — after loading statement + card, loads `context.Set<Installment>().Where(i => i.StatementId == command.StatementId)` (tracked); `settleable = those where IsAccrued && !IsReversed && !IsPaid`; `payable = settleable.Aggregate(Money.Zero(Currency.Reference), (r, i) => r + i.Amount)`; `payable.MinorUnits == 0` → `FinancingErrors.StatementAlreadyPaid`; feeds **`payable`** (not `statement.AmountDue`) into `StatementPaymentCalculator.Build`; after the ledger post + `ConsumeCredit`, `installment.MarkPaid(command.PaidOnUtc)` per settleable cuota (checked), then `statement.MarkPaid(command.PaidOnUtc)`. The up-front `statement.IsPaid` guard is kept.
- [x] `Application/Commands/PayStatement/StatementPaymentCalculator.cs` — param `amountDue` renamed → `payable` (rename only; all callers positional). Carried-credit netting unchanged, now nets against `payable`.
- [x] Persistence — `InstallmentConfiguration` maps `PaidOnUtc` (nullable) + `Ignore(i => i.IsPaid)`. Migration `20260907152207_AddInstallmentPaidOnUtc` (FinancingDbContext) — single `AddColumn<DateTimeOffset>("PaidOnUtc", "financing_installments", "TEXT", nullable: true)`, **no backfill**. Applied to the dev `personalfinance.db`; verified clean on a fresh DB. **Pull → `dotnet ef database update`** (the `EnsureCreated()` test suites never catch migration drift).
- [x] Query wiring — `MonthlyStatementInstallmentRow` (`Financing.Contracts/Queries/GetMonthlyStatementQuery.cs`) gains trailing `bool IsPaid, DateTimeOffset? PaidOnUtc`; `GetMonthlyStatementHandler` projects `installment.PaidOnUtc`; host `MonthlyStatementInstallmentRowDto` + `FinancingMappingExtensions.ToMonthlyStatementDetailDto` mirror both (the doc named only the Contracts row — the host DTO must carry it too). No behavior reads these yet.
- [x] Tests — new `tests/PersonalFinance.Financing.Tests/PayStatementHandlerTests.cs` (4 facts, in-memory SQLite + `FakeLedgerApi`, shared `SeedStatementAsync` helper): reversal-free parity (`Dr liability 6000 / Cr bank 6000`, `PostedOnUtc == paidOnUtc`); reversed cuota → charges 6000 not the stored `AmountDue` 9000, `AmountDue` untouched, only non-reversed cuotas stamped `PaidOnUtc`; paid-stamping on every settled installment + the statement; carried credit 2000 nets the payable 6000 not `AmountDue` 9000 (`Dr liability 6000 / Cr bank 4000 / Cr cardCredit 2000`, `CarriedCreditBalance → 0`).

### Definition of done
- [x] `dotnet build -c Release` 0W/0E. Test binaries run directly → **Financing 103** (from 99), Ledger 31, Subscriptions 32, Parties 32, Reporting 7, Architecture 15, Api 43 = **263**. `AccrualBoundaryTests` untouched.
- [x] Existing `PayStatement` behavior unchanged for reversal-free statements; the new reversal test proves the corrected charge; the migration applies cleanly on a fresh DB.
- [x] Client: `MonthlyStatementInstallment` gains `isPaid: boolean` + `paidOnUtc: IsoInstant | null` (type addition only, no template change). `pnpm ng lint` clean, `pnpm ng test` **218/218**, `pnpm ng build --configuration production` clean.
- [ ] Live E2E (no browser here) — handed to the user.

### Completion notes

Built one green-lit step at a time (8 steps); the user committed the API work as `f528422` (steps 1–5) + `8b27758` (step 6 tests), leaving the client type addition uncommitted at time of writing. `MonthlyStatement.IsFullyPaidBy` is dead on Slice 1's write path — the handler settles every unpaid cuota and stamps the statement unconditionally — but the slice doc calls for it now because Slice 2's individual-pay handler needs it to decide whether the last cuota just closed the statement. Client `paidOnUtc` typed as the branded `IsoInstant` (matching the sibling `MonthlyStatement.paidOnUtc`) over the slice doc's literal `string | null`. Test-runner note carried from Phase 27: `dotnet test --solution` reports "Zero tests ran" (exit 5) in this shell — run `./tests/<Proj>/bin/<Config>/net10.0/<Proj>`, and filter with `-class "FullName"` (not `--filter-class`).

---

## Phase 29 — Pay a single installment (Slice 2)

**Goal:** The headline feature — a dedicated `POST /v1/financing/installments/{id}/pay` that settles **one** accrued credit-card cuota in full, leaving the rest of the statement owed, plus the client per-row **Pay** button and the **"Pay full statement"** relabel. Because Phase 28 already made `PayStatement` charge Σ (accrued, non-reversed, unpaid) installments, there is **no double-pay window** — after an individual pay, "Pay full statement" charges only the remaining cuotas.

**Traces to:** `docs/individual-installment-payments/slice-2-pay-single-installment.md` (+ `00-overview.md`; second slice of the "pay a statement's cuotas individually" initiative). `docs/DESIGN.md` D2 gains a `PayInstallmentCommand` bullet; `docs/PRD.md` §9 decision 12 marks Slice 2 done. **No cross-module dependency, no schema change, no EF migration** — the change is entirely inside Financing + host + one Contracts command. **Slice 3 ("N/M paid · next month" on Recent Purchases) remains — not started.**

### Ledger shape — Option A (plain, confirmed)

An individual cuota payment posts `Dr CardLiability(installment.Amount) / Cr Bank(installment.Amount)` — **no carried-credit netting** (that is a card-level pool and stays on the full-statement path; `StatementPaymentCalculator` is untouched). The `PostTransactionCommand` carries **no `InstallmentReferenceId`** — matches the `PayStatement` call and the Phase-25 Gate-2 precedent; tagging it would make `FindAccrualTransactionIdsHandler`'s accrual→reversal map ambiguous.

### Tasks
- [x] `Financing.Contracts/Commands/PayInstallmentCommand.cs` — `(Guid InstallmentId, Guid BankAccountId, DateTimeOffset PaidOnUtc) : ICommand<Guid>`.
- [x] `Domain/FinancingErrors.cs` — new `InstallmentNotAccrued` = `("Financing.InstallmentNotAccrued", "The installment has not been accrued to a statement yet and cannot be paid individually.")`. `Endpoints/ErrorHttpStatusHelper.cs` — explicit 409 entries for `InstallmentNotAccrued` (matches no suffix rule → would have fallen to 400) **and** `InstallmentAlreadyPaid` (already 409 via the `AlreadyPaid` suffix rule — made explicit for consistency with the other two `Installment*` conflict entries).
- [x] `Application/Commands/PayInstallment/PayInstallmentValidator.cs` — empty `InstallmentId` → `InstallmentNotFound`, empty `BankAccountId` → `InvalidBankAccount` (mirrors `PayStatementValidator`).
- [x] `Application/Commands/PayInstallment/PayInstallmentHandler.cs` — `internal sealed`, `(FinancingDbContext context, ILedgerApi ledger)`, `ICommandHandler<PayInstallmentCommand, Guid>`. Load installment (tracked) → guards (`IsReversed` → `InstallmentAlreadyReversed`; `IsPaid` → `InstallmentAlreadyPaid`; `!IsAccrued || StatementId is null` → `InstallmentNotAccrued` — the null-`StatementId` check also rejects creditor-financed cuotas) → load `MonthlyStatement` via `installment.StatementId.Value` (null → `StatementNotFound`) → load `CreditCard` via `statement.CardId` (null → `CardNotFound`) → build 2 plain lines inline `Dr card.LiabilityAccountId(Amount) / Cr command.BankAccountId(Amount)`, `ledger.PostTransactionAsync(new PostTransactionCommand(lines, command.PaidOnUtc))` (failure → return) → `installment.MarkPaid(command.PaidOnUtc)` → re-query `context.Set<Installment>().Where(i => i.StatementId == statement.Id)` (change-tracker returns the now-paid instance), `if(!statement.IsPaid && statement.IsFullyPaidBy(list)) statement.MarkPaid(command.PaidOnUtc)` → `SaveChangesAsync`; return `installment.Id`.
- [x] `FinancingModule.Register` — `services.AddScoped<ICommandHandler<PayInstallmentCommand, Guid>, PayInstallmentHandler>()` next to `PayStatementCommand`.
- [x] Host — `ApiRoutes.Financing.InstallmentPayment = "/installments/{id:guid}/pay"`; `Endpoints/Financing/PayInstallment.cs` (mirrors `PayStatement.cs`); `Endpoints/DTOs/PayInstallmentDTO.cs` — `PayInstallmentDto(Guid BankAccountId, DateTimeOffset PaidOnUtc)` + `PayInstallmentResultDto(Guid InstallmentId)` (the slice doc said `Endpoints/Financing/DTOs/`; the repo keeps endpoint DTOs in `Endpoints/DTOs/`); `FinancingMappingExtensions.ToPayInstallmentCommand` / `ToPayInstallmentResultDto` (individual `this`-form — `Guid installmentId` is a distinct extension receiver from `Guid statementId`); `EndpointExtensions.MapFinancingEndpoints()` `MapPost` after `StatementPayment` with `.Produces<PayInstallmentResultDto>(201)` + `.ProducesProblem` 404/409/422.
- [x] Tests — new `tests/PersonalFinance.Financing.Tests/PayInstallmentHandlerTests.cs` (7 facts, in-memory SQLite + `FakeLedgerApi`, seed helper returns ordered installment ids): pay one cuota → 2-line `Dr liability / Cr bank` (no credit leg, `PostedOnUtc == paidOnUtc`), statement stays unpaid, only that cuota stamped; last unpaid cuota → statement `PaidOnUtc` set, all stamped; after an individual pay `PayStatement` charges only Σ still-unpaid (no-double-pay); already-paid / reversed (`reverseIndexes:[0]`) / not-yet-accrued (`accrueCount:1`, pay cuota[2]) → 409; unknown id → 404 — each asserts no stray ledger post. **No `PersonalFinance.Api.Tests` fact** — the happy path needs a real accrued statement and the WAF strips the accrual scheduler (Phases 11/12/25 precedent); the slice did not ask for an OpenAPI-presence fact.

### Definition of done
- [x] `dotnet build -c Release` 0W/0E. Test binaries run directly → **Financing 110** (from 103), Ledger 31, Subscriptions 32, Parties 32, Reporting 7, Architecture 15, Api 43 = **270**. `AccrualBoundaryTests` untouched. `PersonalFinance.Architecture.Tests` (RNF-9) green — no new module edge.
- [x] Individual pay + full-statement pay coexist; after paying one cuota individually, `PayStatement` charges only the remaining cuotas (asserted).
- [x] Guards: paying an already-paid / reversed / not-yet-accrued cuota → 409; unknown id → 404.
- [x] Client: `pay-installment.ts` / `pay-installment-result.ts` + `FinancingService.payInstallment(id, body)` (`POST financing/installments/${id}/pay`); `installments-table` per-row **Pay** button (disabled on `isPaid` / `isReversed` / in-flight) + "Paid" chip + `payClick` output + `paying` input; `statement-page` reuses the shared bank/date form for `onPayInstallment`, full-statement button relabelled "Pay full statement"; `payErrorMessages` gains 4 `Financing.Installment*` codes. `pnpm ng lint` clean, `pnpm ng test` **226/226** (from 218), `pnpm ng build --configuration production` clean.
- [ ] Live E2E (no browser here) — handed to the user: multi-cuota card purchase → let a cuota accrue → open the statement → pay that one cuota → it shows Paid and the owed total drops by that amount; the other cuotas remain owed; "Pay full statement" then settles only the rest.

### Completion notes

Built one green-lit step at a time (steps 1–9; step 10 = this doc sync). Nothing committed by this session — the user commits their own. **Two deviations from the slice doc, both kept:** (1) endpoint DTOs live in `Endpoints/DTOs/` (repo convention), not `Endpoints/Financing/DTOs/` as the doc wrote; (2) the "not-accrued" guard needed a brand-new `FinancingErrors.InstallmentNotAccrued` — nothing existing covered it, and without an explicit `ErrorHttpStatusHelper` line it would have mapped to 400 rather than 409. The ledger legs are built inline in the handler (two `PostTransactionLine`s) rather than via a pure calculator — the slice doc only forbids touching `StatementPaymentCalculator`, and a 2-line plain posting does not earn its own calculator. Client per-row Pay is **emit-up**: the table emits `payClick(installmentId)` and the page validates + submits the existing reactive form, so there is no second bank/date selector. Client spec gotcha: `fixture.nativeElement.querySelectorAll<T>(...)` fails `TS2347` (nativeElement is `any`) — annotate the receiving const `: NodeListOf<HTMLButtonElement>` instead of passing the type argument.

---

## Phase 30 — "Next payment for this purchase" visibility (Slice 3)

**Goal:** Make each purchase legible at a glance on the **Recent Purchases** list — "3/12 paid · next: Nov 2026", or "Fully paid" when nothing remains. "Next payment" is **derived** (the earliest un-paid, un-reversed installment's `DueCycle`); nothing is rescheduled.

**Traces to:** `docs/individual-installment-payments/slice-3-next-payment-visibility.md` (+ `00-overview.md`; third and final slice of the "pay a statement's cuotas individually" initiative). `docs/DESIGN.md` D2 gains a `ListRecentPurchasesQuery` read-model bullet; `docs/PRD.md` §9 decision 12 marks Slice 3 done and records the initiative closed. **Pure additive read — no cross-module dependency, no schema change, no EF migration, no Ledger touch.** **This closes `docs/individual-installment-payments/`.**

### Tasks
- [x] `Financing.Contracts/Queries/ListRecentPurchasesQuery.cs` — `RecentPurchaseRow` gains trailing `int PaidInstallmentCount, int? NextDueYear, int? NextDueMonth`.
- [x] `Application/Queries/ListRecentPurchases/ListRecentPurchasesHandler.cs` — order + `Take(query.Limit)` the newest plans in memory first; one extra query loads their installments (`context.Set<Installment>().Where(i => planIds.Contains(i.PaymentPlanId))` — no root `DbSet`, `GetCreditorPayables` / `GetCardPurchases` precedent), projecting `PaidOnUtc` (the computed `IsPaid` is `builder.Ignore`d — cannot `.Select` it), `IsReversed`, `Sequence`, `CycleYear`, `CycleMonth`. Per plan: `paidInstallmentCount = Count(PaidOnUtc is not null)`; `nextDue = earliest by Sequence where PaidOnUtc is null && IsReversed == false → new BillingCycle(CycleYear, CycleMonth).DueCycle` (reuse the domain `+1`, don't hand-roll); `FirstOrDefault()` → null → both `NextDue*` null. Reuses the `IsReversed == false` predicate shape from `GetCreditorPayablesHandler`.
- [x] Host — `RecentPurchaseRowDto` (`Endpoints/DTOs/RecentPurchasesDTO.cs`) + `FinancingMappingExtensions.ToRecentPurchasesDto` mirror the three fields (the slice doc named only the Contracts row).
- [x] Tests — `tests/PersonalFinance.Financing.Tests/ListRecentPurchasesHandlerTests.cs` +3 facts + `CreateMultiInstallmentPlan` helper (the pre-existing `CreatePlan` is single-installment): mixed paid/reversed → correct `PaidInstallmentCount` + `nextDue` = earliest unpaid-non-reversed `DueCycle`; reversed earliest cuota skipped; fully-paid plan → null `DueCycle`.

### Definition of done
- [x] `dotnet build PersonalFinance.sln -c Release` 0W/0E. Test binaries run directly → **Financing 113** (from 110), Ledger 31, Subscriptions 32, Parties 32, Reporting 7, Architecture 15, Api 43 = **273**. `AccrualBoundaryTests` + `PersonalFinance.Architecture.Tests` (RNF-9) untouched.
- [x] Client — `features/financing/types/recent-purchase-row.ts` gains `paidInstallmentCount` / `nextDueYear` / `nextDueMonth`; `recent-purchases-table.ts` (`MONTH_LABELS`, `paidLabel`, `nextPaymentLabel`) + `.html` sub-line; `recent-purchases-table.spec.ts` +2. `pnpm ng lint` clean, `pnpm ng test` **228/228** (from 226), `pnpm ng build --configuration production` clean.
- [ ] Live E2E (no browser here) — handed to the user: a purchase with some cuotas paid shows an accurate paid count and the correct next-payment month; paying its last cuota flips it to "Fully paid".

### Completion notes

Built one green-lit step at a time (8 self-defined steps — the slice doc has no numbered steps). Nothing committed by this session — the user commits their own. No deviations from the slice doc. The client month label reuses the `party-detail-page.ts` `MONTH_LABELS` array pattern (no new date library); the table's `installmentLabel` helper is replaced (the total `M` survives as the denominator in "N/M paid"). Doc-sync is this phase's step 8-equivalent, done here.

---

## Phase 31 — Back-dated card expenses (Slice 1)

**Goal:** Loading a **credit-card** purchase with a past `PurchaseDate` should read like a purchase you have been paying for months — its elapsed installments already settled in the ledger, funded from a bank the user picks — instead of "0/N paid" starting this cycle. Settle **synchronously at creation**, not on a scheduler tick.

**Traces to:** `docs/backdated-expenses/slice-1-card-backdating.md` (+ `00-overview.md`; first of three slices — Slice 2 creditor cutoff + display-only `PaidOnUtc`, Slice 3 optional pending-$). Card mode only. `docs/DESIGN.md` D2 gains a back-dated-`CreatePaymentPlan` bullet; `docs/PRD.md` §9 records decision 13. **No schema change, no EF migration, no new module edge** (`ILedgerApi` is an existing `.Contracts` dependency — `PayInstallmentHandler` precedent — so `PersonalFinance.Architecture.Tests` / RNF-9 is unaffected). The cycle math already keys off `purchaseDate` (`BillingCycleCalculator.ResolveCycle`) — that part is not rebuilt.

### Tasks
- [x] **Contract + DTO.** `CreatePaymentPlanCommand` (`Financing.Contracts`), `Endpoints/DTOs/CreatePaymentPlanDTO.cs`, and `Endpoints/Mapping/FinancingMappingExtensions.cs` gain a trailing optional `Guid? BankAccountId` — funds the retroactive payments (the pay path has no default bank).
- [x] **Errors.** `FinancingErrors.FuturePurchaseDate` + `BackdatedCardBankAccountRequired`; both added as explicit **422** rows in `Endpoints/ErrorHttpStatusHelper.cs` (neither matches the `NotFound` / `AlreadyPaid` / `.Invalid` / `NonPositive` suffix heuristics).
- [x] **Validator.** `CreatePaymentPlanValidator.Validate` now takes `DateOnly today`; rejects `PurchaseDate > today` for **every** mode. `CreatePaymentPlanHandler` derives `today` from an injected `TimeProvider` (`DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)` — the clock `AccrueInstallments` uses) and passes it in.
- [x] **Handler — accrue + settle pass.** `CreatePaymentPlanHandler` ctor gains `TimeProvider` + `ILedgerApi`. The bank-required check (card + any installment whose `DueCycle` ordinal `< currentMonth` + null `BankAccountId` → `BackdatedCardBankAccountRequired`) runs in the handler after `PaymentPlan.Create` — it needs `card.CutoffDay`, which the pure validator never sees. New private `accrueAndSettleBackdatedInstallmentsAsync`: installments ascending, `break` at the first cycle not `IsClosedAsOf(today, card.CutoffDay)`; **(1) accrue** mirrors `AccrueInstallments` Gate 1 — `Dr card.ExpenseAccountId / Cr card.LiabilityAccountId` via `ILedgerApi.PostTransactionAsync`, `PostedOnUtc` = the clamped-cutoff instant of the close cycle, `InstallmentReferenceId` + `SplitReferenceId` carried; then `statement.Accrue` (find-or-`MonthlyStatement.Open`) + `installment.MarkAccrued(closeInstant, statement)`; **(2) pay** only when `DueCycle` ordinal `< currentMonth` ordinal (`Year*12 + Month` — `BillingCycle` has no `<`): `Dr card.LiabilityAccountId / Cr command.BankAccountId` dated at the due cycle's clamped cutoff, `installment.MarkPaid(dueInstant)`, then `statement.MarkPaid` when `IsFullyPaidBy` (evaluated against the DB rows **plus** this plan's still-unsaved installments). Ledger posts are cross-module, no saga (the scheduler / `PayInstallmentHandler` contract); a mid-loop failure returns the error with earlier posts standing. `SplitAccruedOnUtc` left null — Gate 2 keeps handling the co-borrower receivable at `DueCycle` unchanged.
- [x] **API tests.** `CreatePaymentPlanValidatorTests.cs` +2 (future date rejected, today accepted; every `Validate` call site gained a `today` arg). `CreatePaymentPlanHandlerTests.cs` +7 (a nested `FixedTimeProvider` + the shared `FakeLedgerApi` from `FakeModuleApis.cs`; existing card/creditor tests pinned to a `2026-01-15` clock): Jun-15 / 3-cuota / today Sep-7 → 3 accrued, cuotas 1–2 paid, cuota 3 accrued-unpaid; the five ledger posts are `Dr Expense/Cr Liability` at 6-15/7-15/8-15 + `Dr Liability/Cr Bank` at 7-15/8-15, historically dated; fully-elapsed → every installment + statement paid; a today-dated card plan posts nothing; future `PurchaseDate` → `FuturePurchaseDate`; back-dated + no bank → `BackdatedCardBankAccountRequired`; running the real `AccrueInstallments.TickAsync` (reflection, `AccrueInstallmentsTests` pattern) after creation adds no ledger posts.
- [x] **Collateral test fixes.** `PartyFutureSharesTests` / `PartyPendingSharesTests` and `ReportingIntegrationFixture` seed plans switched from a hardcoded past `purchaseDate` to **today** — their assertions need every installment un-accrued / in the "Future" bucket, which a back-dated plan no longer leaves.

### Definition of done
- [x] `dotnet build PersonalFinance.sln` 0W/0E. Test binaries run directly (`dotnet test --solution` still "Zero tests ran" exit 5 in this shell — use `cd tests/<Proj> && dotnet run -c Debug`) → **Financing 122** (from 113), Ledger 31, Subscriptions 32, Parties 32, Reporting 7, Api 43, Architecture 15 = **282** (273 + 2 validator + 7 handler). `PersonalFinance.Architecture.Tests` (RNF-9) untouched.
- [x] Client — `create-payment-plan.ts` `bankAccountId?: string`; `load-expense-page` back-dated "Paid from" selector (reusing `bankAndCashInstruments()`) + `merge(mode, purchaseDate)` watcher that toggles `Validators.required`; `notFuture` `ValidatorFn` on `purchaseDate`; `load-expense-page.spec.ts` fixtures made time-independent + 6 new cases. `pnpm ng lint` clean, `pnpm ng test` **234/234** (from 228), `pnpm ng build --configuration production` clean.
- [ ] Live E2E (no browser here) — handed to the user: on a card with a known cutoff, load an expense dated ~2 months back with 3 installments and pick a bank → Recent Purchases reads "2/3 paid · next: <current month>", the bank's balance is down by two cuotas dated in the two historical months, and the past-month statements appear in the card's statement list.

### Completion notes

Built one green-lit step at a time (steps 1–8). Nothing committed by this session — the user commits their own. **Deviations from the slice doc, both kept:** (1) the "require `BankAccountId` when back-dated with an elapsed cuota" rule lives in the **handler**, not `CreatePaymentPlanValidator` as the doc wrote — the check needs `card.CutoffDay` and the validator is a pure static function with no DB / card access; the plain `PurchaseDate > today` guard is in the validator (with a `today` parameter). (2) The client's back-dated trigger is the coarser "`purchaseDate` before today" (it cannot compute cuota 1's due cycle without the card's cutoff), so it may show the "Paid from" selector in a few cases where the API would not strictly require a bank — the command doc says `BankAccountId` is ignored when not needed, so this is harmless. Historical dates use the **clamped cutoff day** of each cycle (`min(cutoff, DaysInMonth)`) at midnight UTC, for both the close instant and the due instant. **Known edge, not handled:** a back-dated close cycle that lands on an already-**paid** pre-existing `MonthlyStatement` makes `statement.Accrue` fail → creation returns 409 (the scheduler would just skip and retry). Doc-sync is this phase's step 8-equivalent, done here.

---

## Phase 32 — Back-dated creditor expenses (Slice 2)

**Goal:** A creditor-financed purchase honors a uniform **26th** closing day (like a card), and loading one with a past `PurchaseDate` shows its already-elapsed cuotas as **paid** — a display-only `PaidOnUtc` stamp with **no ledger movement**, matching the ledger-free creditor model.

**Traces to:** `docs/backdated-expenses/slice-2-creditor-cutoff.md` (+ `00-overview.md`; middle of three slices — Slice 1 was card mode, Slice 3 optional pending-$). **Creditor (card-less) mode only. No schema change, no EF migration, no new module edge, no ledger post** — two small Financing edits + tests. `docs/DESIGN.md` D2 back-dated bullet gains the creditor case; `docs/PRD.md` §9 decision 13 marks Slice 2 done.

### Tasks
- [x] **Cutoff constant + branch.** `Domain/PaymentPlan.cs` — new `internal const int CreditorCutoffDay = 26` (the slice doc wrote `private const`; widened to `internal` so `CreatePaymentPlanHandler` reads the same constant instead of duplicating the `26`). The creditor branch of `PaymentPlan.Create`'s first-cycle selection changes from `new BillingCycle(purchaseDate.Year, purchaseDate.Month)` to `BillingCycleCalculator.ResolveCycle(purchaseDate, CreditorCutoffDay)` — applied to **every** creditor purchase, not just back-dated ones (a fresh creditor buy after the 26th now rolls to the next cycle — intended, documented in the slice doc and PRD).
- [x] **Handler stamp.** `Application/Commands/CreatePaymentPlan/CreatePaymentPlanHandler.cs` — new `else` (creditor) branch after `context.PaymentPlans.Add(plan.Value)` → `stampBackdatedCreditorInstallments(plan.Value, today)`. New `private static Result stampBackdatedCreditorInstallments(PaymentPlan plan, DateOnly today)`: installments ascending by `Sequence`, `break` at the first whose `DueCycle` ordinal `>= currentMonth` ordinal, `installment.MarkPaid(clampedCutoffInstant(installment.DueCycle, PaymentPlan.CreditorCutoffDay))` on the strictly-past ones. **`PaidOnUtc` only — no `ILedgerApi` call, no `AccruedOnUtc` / `StatementId`, no `MonthlyStatement`, no `SplitAccruedOnUtc`.** Reuses the handler's existing `ordinalOf` + `clampedCutoffInstant` helpers. Class summary comment extended with the creditor case.
- [x] **API tests.** `CreatePaymentPlanHandlerTests.cs` +4 facts (no helper changes — reuse `SeedCreditorAsync` / `HandlerAsOf` / `LoadPlanAsync` / `RunAccrueInstallmentsAsync`): (1) `Handle_applies_the_26th_cutoff_uniformly_to_creditor_purchases` — clock Mar-28, purchase on the 27th → first close cycle April `[(2026,4),(2026,5),(2026,6)]`, on the 26th → March `[(2026,3),(2026,4),(2026,5)]`; (2) `Handle_backdated_creditor_plan_stamps_the_elapsed_installments_paid_and_posts_nothing_to_the_ledger` — Jun-15 / 3-cuota / today Sep-7 → cuotas 1–2 `IsPaid`, cuota 3 not, none `IsAccrued`, none `SplitAccruedOnUtc`, `ledger.PostedTransactions` empty, no `MonthlyStatement`; (3) `Handle_fully_elapsed_backdated_creditor_plan_marks_every_installment_paid` — Jan-10 → all 3 paid, zero ledger; (4) `Handle_backdated_creditor_split_stamps_the_holder_cuotas_and_leaves_gate_three_to_accrue_the_co_borrower` — holder cuotas 1–2 stamped at creation with nothing posted, then after simulating the outbox-consumer link (`plan.LinkSplit` + `AssignCreditorPayableAccount` on a reloaded `.Include(SplitParticipants)` instance) and running `AccrueInstallments.TickAsync`, Gate 3 posts `"Creditor-financed split accrual"` `Dr receivable / Cr payable` and every installment gets `SplitAccruedOnUtc` while the holder's 1–2 stay `IsPaid`.

### Definition of done
- [x] `dotnet build PersonalFinance.sln -c Release` 0W/0E. Test binaries run directly (`dotnet test --solution` still "Zero tests ran" exit 5 in this shell — run `./tests/<Proj>/bin/Release/net10.0/<Proj>`) → **Financing 126** (from 122), Ledger 31, Subscriptions 32, Parties 32, Reporting 7, Api 43, Architecture 15 = **286** (was 282). `PersonalFinance.Architecture.Tests` (RNF-9) untouched; `AccrualBoundaryTests` untouched.
- [x] Client — no production change (`isBackdatedCardPurchase()` was already `mode === 'card'`-gated; Recent Purchases already renders `PaidOnUtc`, Phase 30). `load-expense-page.spec.ts` +1 fact (selector stays hidden for a back-dated creditor purchase). `pnpm ng lint` clean, `pnpm ng test` **235/235** (from 234), `pnpm ng build --configuration production` clean.
- [ ] Manual smoke (no browser here) — handed to the user: load a back-dated creditor purchase → Recent Purchases shows the right paid count and next month, and the ledger shows **no** new transactions for it (only co-borrower receivables if it was a split); a fresh creditor purchase dated after the 26th now shows its first payment one month later than it used to.

### Completion notes

Built one green-lit step at a time (steps 1–5 of the slice). Nothing committed by this session — the user commits their own. **Deviation from the slice doc:** `CreditorCutoffDay` is `internal const`, not the `private const` the doc specified — the handler's paid-at date computation needs the same value, and a widened accessibility modifier on an already-`internal` class beats duplicating a magic number across the domain and application layers. The paid-at instant uses the **clamped 26th** of each due cycle at midnight UTC (`clampedCutoffInstant`), consistent with Slice 1's card path; the value is cosmetic (`PaidOnUtc` is display-only and never surfaced as a date). Gate 3 coverage for creditor splits already exists in `AccrueInstallmentsTests` — Slice 2's split fact only proves the holder stamp and Gate 3 coexist. Doc-sync is this phase's step 5-equivalent, done here.
