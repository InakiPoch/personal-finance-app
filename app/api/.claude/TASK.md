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
- [ ] `IFinancingApi.cs` — include a query for installment payment state now (`GetInstallmentStatusAsync`), even though its only consumer (Ledger's reversal handler) isn't finished until Phase 4.
- [ ] `Commands/CreateCreditCardCommand.cs` — name, `CutoffDate`, used by D13's unified `POST /instruments` endpoint for credit registration.
- [ ] `Commands/CreatePaymentPlanCommand.cs` — amount, card id, installment count, purchase date, optional split-with-parties payload.
- [ ] `Commands/PayStatementCommand.cs`.
- [ ] `IntegrationEvents/InstallmentAccruedIntegrationEvent.cs` — direct/in-process dispatch.
- [ ] `IntegrationEvents/PaymentPlanCreatedIntegrationEvent.cs` — durable/Outbox dispatch (D8's one legitimate dual-write).

### Tasks — Domain (`PersonalFinance.Financing`)
- [ ] `Domain/CreditCard.cs` — `CutoffDate` field (day-of-month, per D13) plus a carried-forward credit-balance field (per D12, populated starting Phase 4 — add the field now so `MonthlyStatement`/`PayStatementHandler` have somewhere to read/write it later).
- [ ] `Domain/BillingCycleCalculator.cs` (per D13) — pure function `ResolveCycle(DateOnly purchaseDate, int cutoffDay)`: purchase on-or-before the cutoff day closes into the current cycle; purchase after opens the next cycle. Must be independently unit-testable — this is the exact mechanism behind PRD US-3 AC2.
- [ ] `Domain/PaymentPlan.cs`, `Installment.cs` — `Installment` carries its resolved billing cycle, `AccruedOnUtc` (nullable), and a paid/unpaid flag via `MonthlyStatement` linkage (needed for Phase 4's reversal check).
- [ ] `Domain/MonthlyStatement.cs` — aggregates installments accrued into a cycle for a card, tracks paid/unpaid.
- [ ] `Application/Commands/CreateCreditCard/CreateCreditCardHandler.cs`.
- [ ] `Application/Commands/CreatePaymentPlan/CreatePaymentPlanValidator.cs` + `Handler.cs` — uses `PhantomPennyAllocator` so `Σ(installments) == total` exactly; uses `BillingCycleCalculator` for the first installment's cycle; writes `PaymentPlanCreatedIntegrationEvent` to Financing's own Outbox in the same `SaveChangesAsync` transaction as the plan, only if a split payload is present (D8).
- [ ] `Application/Commands/PayStatement/PayStatementValidator.cs` + `Handler.cs` — calls `ILedgerApi` to post Dr Liability / Cr Bank (D2's second half); marks the statement paid. **Does not yet net any card credit** — that's added in Phase 4 once D12's compensating-entry flow exists.
- [ ] `Application/Scheduling/AccrueInstallments.cs` — `SchedulerBase`-derived; on cycle close, finds unaccrued installments whose cycle has closed, posts Dr Expense / Cr Liability via `ILedgerApi` (tagged with `InstallmentReference`), marks `AccruedOnUtc`, dispatches `InstallmentAccruedIntegrationEvent` (direct mode). **Idempotency guard mandatory**: re-running the tick must never double-accrue.
- [ ] `Infrastructure/Persistence/FinancingDbContext.cs` (history table `__EFMigrationsHistory_Financing`), `Configurations/` for all four entities.
- [ ] `Infrastructure/Persistence/Outbox/FinancingOutboxConfig.cs`, `Inbox/FinancingInboxConfig.cs` (Financing is a producer for `PaymentPlanCreated` only, no consumer role yet).
- [ ] `Infrastructure/Persistence/ReadViews/vw_card_future_schedule.sql` (RF-2's future-schedule half, D11 — not-yet-accrued installments only).
- [ ] `Infrastructure/PublicApi/FinancingApi.cs` — `internal`, implements `IFinancingApi`.
- [ ] `FinancingModule.cs`, migration, DI wiring, `Endpoints/FinancingEndpoints.cs` (`POST /financing/payment-plans`, `POST /financing/statements/{id}/pay`, `GET /financing/cards/{id}/future-schedule`). Card creation has no endpoint of its own here — it's reached via the unified endpoint below.
- [ ] Wire `AccrueInstallments` into host startup.
- [ ] **D13's unified instrument endpoint** — now that both `CreateAccountCommand` (Ledger, Phase 2) and `CreateCreditCardCommand` (Financing, this phase) exist: add `Bootstrap/PersonalFinance.Api/Endpoints/InstrumentsEndpoints.cs` with `POST /instruments`, a thin router with no domain logic that dispatches to `ILedgerApi` (type = debit/cash) or `IFinancingApi` (type = credit) based on the request's `type` field.

### Tests — `PersonalFinance.Financing.Tests`
- [ ] `InstallmentAllocationTests.cs` — property-based: `Σ(installments) == total` for random totals/counts, no negative/zero shares.
- [ ] `AccrualBoundaryTests.cs` — D11: unaccrued installment contributes $0 to `GetCardLiabilityQuery`, full amount to future-schedule view; reverse after accrual. Also add `BillingCycleCalculator` boundary tests here (purchase exactly on cutoff → current cycle; one day after → next cycle) — this is D13's mechanism and has no other home in DESIGN.md's named test list.

### Tests — `PersonalFinance.Architecture.Tests` (create now — first point with ≥2 modules)
- [ ] `ModuleIsolationTests.cs` — assert `Financing` doesn't reference `Ledger`'s impl assembly, only `Ledger.Contracts`. Pick and pin the inspection tool (reflection-based or a library like NetArchTest) — every later module phase extends this same test.

### Definition of done
- [ ] `Financing.Tests` and `Architecture.Tests` pass.
- [ ] Manual smoke test: register a debit account, a cash account, and a credit card (all via `POST /instruments`), then two card purchases straddling the cutoff boundary, confirm they land in different cycles via `api.http`.
- [ ] Confirm the plan-persisted/outbox-written same-transaction guarantee is real (test or manual check).

---

## Phase 4 — Reversal Semantics: Implementing D12

**Goal:** Implement D12 (already documented in `docs/DESIGN.md`) — reversal must succeed even on accrued/paid installments, with a compensating entry that nets against the card's next statement, cascading correctly to Financing (and to Parties once it exists in Phase 6).

**Depends on:** Phase 2 (`ReverseTransactionCommand` skeleton) + Phase 3 (installment/statement state to query and correct, `CreditCard`'s credit-balance field).

### Tasks — Ledger + Financing implementation (Parties cascade deferred, seam only)
- [ ] Extend `ReverseTransactionHandler` per D12: (a) always post the mirrored storno entry; (b) if `IFinancingApi` reports the installment was already paid via a statement, also post the compensating card-credit entry (`Dr Activo:CréditoTarjeta{Card} / Cr Gasto:Categoría` — never credit `Activo:Banco` directly, per RNF-5); (c) call `IFinancingApi` to mark the installment's Financing-side state as reversed so it stops appearing in future-schedule/liability views, and to record the credit against that card.
- [ ] Extend `IFinancingApi` with the mutation needed by (c) (e.g. `MarkInstallmentReversedAsync`, updates `CreditCard`'s carried-forward credit-balance field), implemented in `FinancingApi.cs`.
- [ ] Extend `PayStatementHandler` (built in Phase 3) to subtract any carried-forward card credit from the amount due before posting Dr Liability / Cr Bank — this is what makes D12's auto-netting real, and what US-2 AC2 depends on.
- [ ] Define `IPartiesApi`'s reversal-correction method signature now (Ledger's handler will call it once Parties.Contracts exists, per RNF-10), but stub the call site with `// TODO(Phase 6): wire IPartiesApi.CorrectExpenseSplitAsync` and a unit test asserting reversal succeeds *without* a party correction when no `SplitReference` is present (the common case).
- [ ] Update `Endpoints/LedgerEndpoints.cs` reversal response shape to communicate "a compensating entry was also posted" to the future Angular client (per D13).

### Tests
- [ ] `Ledger.Tests` — extend `ReverseTransactionTests.cs`: reversing an accrued-but-unpaid installment (plain storno only); reversing an accrued-and-paid installment (storno + compensating entry, verify amount/account); reversing a reversal is still rejected (D3 unaffected).
- [ ] `Financing.Tests` — a reversed installment no longer appears in `vw_card_future_schedule` nor contributes to `GetCardLiabilityQuery`, regardless of prior accrual/payment state; a `PayStatementHandler` test confirming the amount due is reduced by any carried-forward credit.

### Definition of done
- [ ] All `Ledger.Tests`/`Financing.Tests` pass, including new reversal and netting cases.
- [ ] Manual smoke test: create plan → accrue → pay statement → reverse; confirm the compensating entry appears; pay the *next* statement and confirm the amount due is reduced by the credit.
- [ ] The Parties-cascade TODO is tracked forward into Phase 6, not silently dropped.

---

## Phase 5 — Subscriptions Module

**Goal:** Recurring charges that self-renew on a clock (US-5), no manual action required.

**Depends on:** Phase 2 (`ILedgerApi`) only — no dependency on Financing or Parties.

### Tasks — Contracts (`PersonalFinance.Subscriptions.Contracts`)
- [ ] `ISubscriptionsApi.cs`.
- [ ] `Commands/CreateSubscriptionTemplateCommand.cs`, `RenewSubscriptionCommand.cs`.
- [ ] `IntegrationEvents/SubscriptionRenewedIntegrationEvent.cs` — direct/in-process dispatch (same reasoning as `InstallmentAccrued`).

### Tasks — Domain (`PersonalFinance.Subscriptions`)
- [ ] `Domain/SubscriptionTemplate.cs` — amount, payment-instrument reference, category.
- [ ] `Domain/RecurrenceRule.cs` — frequency, anchor date.
- [ ] `Domain/RenewalSchedule.cs` — next-due date, active/cancelled flag (US-5 AC3: cancelling stops future renewals, never touches past charges — a status flag checked by the scheduler, never a delete).
- [ ] `Application/Commands/CreateSubscriptionTemplate/Handler.cs`.
- [ ] `Application/Commands/RenewSubscription/Handler.cs` — posts the period charge via `ILedgerApi`, advances `NextDueDate`.
- [ ] `Application/Scheduling/RenewDueSubscriptions.cs` — finds active templates past due, invokes the renew handler, dispatches `SubscriptionRenewedIntegrationEvent` (direct mode). Idempotency guard: never renew twice for the same due date.
- [ ] `Infrastructure/Persistence/SubscriptionsDbContext.cs` (history table `__EFMigrationsHistory_Subscriptions`), configurations, Outbox/Inbox tables (unused, kept for folder-tree symmetry), migration.
- [ ] `Infrastructure/Persistence/ReadViews/vw_active_subscriptions.sql`.
- [ ] `Infrastructure/PublicApi/SubscriptionsApi.cs`, `SubscriptionsModule.cs`, DI wiring, `Endpoints/SubscriptionsEndpoints.cs` (`POST /subscriptions`, `DELETE /subscriptions/{id}`, `GET /subscriptions/active`).
- [ ] Wire `RenewDueSubscriptions` into host startup.

### Tests
- [ ] **Gap flag:** DESIGN.md §6 names no `PersonalFinance.Subscriptions.Tests` project. Recommend adding one anyway — `RecurrenceRuleTests` (monthly renewal on the 31st of a 30-day month, leap-year Feb 29 anchors) and `RenewDueSubscriptionsIdempotencyTests` — same class of boundary risk as `BillingCycleCalculator`. Confirm with the repo owner whether to add it or accept the gap.
- [ ] Extend `ModuleIsolationTests.cs` to cover Subscriptions.

### Definition of done
- [ ] Subscriptions builds, migrates, endpoints work via `api.http`.
- [ ] Architecture-fitness test still passes.
- [ ] The test-project decision above is either implemented or explicitly recorded as an accepted gap.

---

## Phase 6 — Parties Module (incl. RF-3 Outbox/Inbox flow + reversal cascade)

**Goal:** Third-party debt tracking as a management view over Ledger receivables (D1). Closes the two seams left open earlier: Financing's `PaymentPlanCreated` → Parties Outbox/Inbox flow (D8), and Phase 4's deferred reversal cascade.

**Depends on:** Phase 2 (Ledger), Phase 3 (Financing's Outbox writer), Phase 4 (the reversal seam to close).

### Tasks — Contracts (`PersonalFinance.Parties.Contracts`)
- [ ] `IPartiesApi.cs` — include the reversal-correction method anticipated in Phase 4 (e.g. `CorrectExpenseSplitAsync(originalTransactionId, correctionAmount)` — finalize the exact shape against what Ledger's handler needs).
- [ ] `Commands/CreatePartyCommand.cs`, `RegisterSharedExpenseCommand.cs`, `SettleCurrentAccountCommand.cs`.
- [ ] `Queries/GetCurrentAccountBalanceQuery.cs`.
- [ ] `IntegrationEvents/ExpenseSplitSettledIntegrationEvent.cs`.

### Tasks — Domain (`PersonalFinance.Parties`)
- [ ] `Domain/Party.cs` — reference data only (name), no auth/user account.
- [ ] `Domain/CurrentAccount.cs` — a management view, not a second ledger (D1): balance computed by querying Ledger's receivable account, never stored independently as source of truth.
- [ ] `Domain/ExpenseSplit.cs` — uses `PhantomPennyAllocator` for the N-way split (US-3 AC3/AC4).
- [ ] `Application/Commands/CreateParty/Handler.cs`.
- [ ] `Application/Commands/RegisterSharedExpense/Validator.cs` + `Handler.cs`.
- [ ] `Application/Commands/SettleCurrentAccount/Validator.cs` + `Handler.cs` — posts D4's receivable-cancellation entry (`Dr Banco / Cr PorCobrar`, explicitly not income) via `ILedgerApi`.
- [ ] `Application/Queries/GetCurrentAccountBalance/Handler.cs`.
- [ ] `Application/EventHandlers/OnPaymentPlanCreated.cs` — the D8 consumer: dedupes via inbox `(MessageId, Consumer)`, applies `PhantomPennyAllocator` to the raw split detail, creates `ExpenseSplit`, calls `ILedgerApi.PostReceivableCommand`.
- [ ] **Close the Phase 4 seam**: implement `IPartiesApi.CorrectExpenseSplitAsync` (sync call, per RNF-10/D12) to correct/reduce the `ExpenseSplit`/receivable when a reversed transaction had a `SplitReference` (PRD US-6 AC3). Remove the TODO placeholder left in Ledger's `ReverseTransactionHandler` once wired.
- [ ] `Infrastructure/Persistence/PartiesDbContext.cs` (history table `__EFMigrationsHistory_Parties`), configurations, `Outbox/PartiesOutboxConfig.cs` (producer for `ExpenseSplitSettledIntegrationEvent`), `Inbox/PartiesInboxConfig.cs` (consumer of `PaymentPlanCreatedIntegrationEvent`), migration.
- [ ] `Infrastructure/Persistence/ReadViews/vw_current_account_timeline.sql` (RF-7 running balance, D4).
- [ ] `Infrastructure/PublicApi/PartiesApi.cs`, `PartiesModule.cs`, DI wiring, `Endpoints/PartiesEndpoints.cs` (`POST /parties`, `POST /parties/expenses`, `POST /parties/{id}/settle`, `GET /parties/{id}/balance`, `GET /parties/{id}/timeline`).

### Tests — `PersonalFinance.Parties.Tests`
- [ ] `PhantomPennyAllocatorTests.cs` — property-based: `Σ(shares) == total` always, no negative shares, remainder-cent distribution doesn't always favor the same participant.
- [ ] `OnPaymentPlanCreatedTests.cs` — dedup: same event delivered twice results in exactly one `ExpenseSplit`/receivable posting (inbox-verified).
- [ ] **New (per D12/RNF-10, not in DESIGN.md's original test list):** reversal-cascade test — reversing a split expense's original transaction reduces the party's `CurrentAccount` balance exactly once via the synchronous `IPartiesApi.CorrectExpenseSplitAsync` call.

### Tests — Architecture
- [ ] Extend `ModuleIsolationTests.cs` for Parties.

### Definition of done
- [ ] All `Parties.Tests` pass.
- [ ] End-to-end smoke test via `api.http`: create a payment plan with a 2-way split, trigger the Outbox Worker, confirm the party's balance reflects the receivable; reverse the original transaction, confirm the party's balance auto-corrects (US-6 AC3).
- [ ] `OutboxHealthCheck` manually verified to go unhealthy if the Outbox Worker is paused while a `PaymentPlanCreated` message is pending (RNF-7).

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

### Definition of done
- [ ] Every Fase-1 acceptance criterion in PRD §6 has a corresponding passing manual/automated check.
- [ ] D12 and D13's behavior in code matches what `docs/DESIGN.md` describes — no drift between doc and implementation.
