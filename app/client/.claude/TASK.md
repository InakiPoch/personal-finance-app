# TASK.md — Client Implementation Guide

Phased build order for the PersonalFinance **Angular 20 client**. Sequenced by actual code dependency, not by document order. Each phase lists its goal, dependencies, concrete tasks, and definition of done.

Source docs: `docs/PRD.md` (product — 7 views, 3 Fases, cross-cutting rules, 4 API gaps) and `docs/DESIGN.md` (technical — folder tree §2, hand-written type model §3, services §4, HTTP + error model §5, state/forms §6, eventual consistency §7, local registry §8, endpoint traceability §9, config §10, open questions §11). The two are internally consistent; this file only sequences building what they already specify.

Phases 0–3 trace step-by-step to PRD and DESIGN statements. Phase 4 collects the departures both documents already flag (plus two scaffold-state gaps) and is worked only when each trigger lands.

## Conventions every step inherits

From `.claude/CLAUDE.md`, `.claude/rules/typescript-frontend-style.md`, and `docs/DESIGN.md` §1/§4/§6:

- Standalone components (never write `standalone: true`), `ChangeDetectionStrategy.OnPush`, `inject()` in field initializers, signals-first state, no `effect()` for data flow, no async pipe, native control flow (`@if` / `@for` / `@switch`).
- One `type` per file under a feature `types/` folder; no `I-` prefix; `Money` is a branded `number`, never floated.
- Services: `@Injectable({ providedIn: 'root' })`, `inject(HttpClient)`, return `Observable<T>`, explicit HTTP generics, unwrap `{ rows: [...] }` list envelopes to the array via `map`.
- Reactive forms only: built in an `initXForm(): void` method via `FormBuilder`; validators as pure functions; error copy via an `ErrorConfig` map; money entered in **major** units and converted with `toMinorUnits` at submit.
- Subscription cleanup: `private destroy$ = new Subject<void>()` + `takeUntil(this.destroy$)` + `ngOnDestroy`.
- Every service gets an `HttpTestingController` spec asserting URL, verb, body, envelope unwrap, and `AppError` mapping. The `money` utilities and `problemDetailsInterceptor` get focused unit specs.
- User-facing error messages key off the error `code`, not the `detail` text (`docs/DESIGN.md` §5).

## Deviations baked into Phases 0–3 (intentional, not drift)

1. **Phase 0 exists** though `docs/PRD.md` §5 names only three Fases — it is entirely `docs/DESIGN.md` §2–§10 enabling work. No PRD view can ship without it.
2. **Step 1.4** pulls `PartiesService.getBalance` and `ReportsService.debtSummary` into Fase 1 even though Parties is Fase 3 — `docs/DESIGN.md` §7 and §8 make the Fase-1 Load-expense split flow depend on them (balance reconciliation polling; party choices for the split). Only those two methods move up; the rest of Parties stays in Phase 3.
3. **`LedgerService.postTransaction`** (`docs/DESIGN.md` §9 row 2, marked "low-level; internal") is implemented in Step 1.7 for service-list parity, with no dedicated view.
4. **`POST /v1/ledger/accounts`** is intentionally **not** wired — `docs/DESIGN.md` §9 states it is removed outside Development.
5. **Tailwind, client CI, ESLint** are Phase 4 only — `docs/DESIGN.md` §10 defers Tailwind to the separate UI task; CI and ESLint have no PRD/DESIGN statement and are scaffold-state gaps.

---

## Phase 0 — Core scaffolding & HTTP wiring

**Goal:** Stand up everything under `src/app/core/`, the environment config, the interceptor chain, and the lazy routing shell — so the first feature slice has types, an HTTP boundary, and a route to land on.

**Traces to:** `docs/DESIGN.md` §2 (folder tree), §3 (value types), §5 (HTTP + error model), §6 (forms/cleanup rules), §7 (`pollUntil`), §8 (local registry), §10 (environments, routing).

**Depends on:** nothing.

### Tasks — configuration

- [x] `src/app/environments/environment.ts` — prod placeholder (`apiUrl` empty or prod URL). (`docs/DESIGN.md` §10)
- [x] `src/app/environments/environment.development.ts` — `apiUrl: 'http://localhost:5000/v1'`. (`docs/DESIGN.md` §10)
- [x] `angular.json` — add a `fileReplacements` entry swapping `environment.ts` → `environment.development.ts` for the `development` build config; production keeps `environment.ts`. No proxy (CORS already allows `http://localhost:4200`, API D13). (`docs/DESIGN.md` §10)

### Tasks — `core/types/` (one type per file)

- [x] `money.ts` — `Money = number & { readonly __brand: 'Money' }` (minor units; never floated). (`docs/DESIGN.md` §3)
- [x] `currency-code.ts` — `CurrencyCode = 'ARS'`. (`docs/DESIGN.md` §3)
- [x] `iso-instant.ts` — `IsoInstant = string` (ISO-8601 UTC). (`docs/DESIGN.md` §3)
- [x] `iso-date.ts` — `IsoDate = string` (`YYYY-MM-DD`). (`docs/DESIGN.md` §3)
- [x] `problem-details.ts` — `ProblemDetails = { type?: string; title?: string; status?: number; detail?: string; code?: string; [key: string]: unknown }`. (`docs/DESIGN.md` §5)
- [x] `app-error.ts` — `AppError = { code: string; title: string; detail: string; status: number; metadata: Record<string, unknown> }`. (`docs/DESIGN.md` §5)

### Tasks — `core/money/`

- [x] `money.ts` — pure functions `fromMinorUnits(n: number): Money`, `toMinorUnits(major: number): Money`, `formatArs(value: Money): string`. No floating-point arithmetic on money. (`docs/DESIGN.md` §3, §6; `docs/PRD.md` §4)
- [x] `money.spec.ts` — focused unit spec (round-trip, formatting, no float drift). (`docs/DESIGN.md` §10)

### Tasks — `core/http/`

- [x] `base-url.interceptor.ts` — `baseUrlInterceptor` (functional `HttpInterceptorFn`): prefix relative request URLs with `environment.apiUrl`. Keeps services free of the host. (`docs/DESIGN.md` §5, §10)
- [x] `problem-details.interceptor.ts` — `problemDetailsInterceptor`: catch `HttpErrorResponse`, read root-level `error.error.code`, fall back to `status`-derived defaults on missing body (network / 5xx), preserve extra root keys as `metadata`, rethrow a typed `AppError`. Status meaning per `docs/DESIGN.md` §5 (`*.NotFound` → 404; `AlreadyPaid`/`AlreadyAccrued`/`AlreadyReversed`/`NotActive`/`SettlementExceedsBalance` → 409; `Invalid*`/`NonPositive*` → 422; `Instruments.UnknownType`/`Request.Malformed` → 400).
- [x] `problem-details.interceptor.spec.ts` — focused unit spec (each status branch, missing body, metadata passthrough). (`docs/DESIGN.md` §10)
- [x] `poll-until.ts` — `pollUntil` helper (RxJS `timer` + `switchMap` + `take` / `retry`), bounded retry count/interval; used by the Load-expense split reconciliation. (`docs/DESIGN.md` §7)
- [x] Wire both interceptors in `app.config.ts`: `provideHttpClient(withFetch(), withInterceptors([baseUrlInterceptor, problemDetailsInterceptor]))`. (`docs/DESIGN.md` §5)

### Tasks — `core/registry/` and `core/health/`

- [x] `instrument-registry-service.ts` — `InstrumentRegistryService`: a `WritableSignal` of created instruments (`id`, `type`, `name`, `cutoff`) persisted to `localStorage`, try/catch-guarded, empty-safe. Documented stopgap for API gap 1. (`docs/DESIGN.md` §8; `docs/PRD.md` §3.2, §7.1)
- [x] `health-service.ts` — `HealthService.check(): Observable<...>` → `GET /health`, degradation-aware. (`docs/DESIGN.md` §4, §9 row 21)

### Tasks — routing shell

- [x] `app.routes.ts` — `loadChildren` lazy entries for the Fase-1 features only (`reports`, `instruments`, `financing`, `ledger`); default redirect to the dashboard route. Fase-2/3 entries added in their phases. (`docs/DESIGN.md` §10) — **Phase 0 keeps `routes = []`** (documented comment block only); each lazy entry is wired in its own Phase 1 step (1.1 makes `reports` the default). See D2.

### Definition of done

- [x] `pnpm ng build` succeeds (0 errors, within the 500 kB / 1 MB initial-JS budgets). — prod initial JS **234.89 kB** (58.05 kB transfer).
- [x] `pnpm ng test --watch=false --browsers=ChromeHeadless` green (`money`, `problemDetailsInterceptor` specs pass). — **37/37** across `money`, `base-url.interceptor`, `problem-details.interceptor`, `poll-until`, `instrument-registry-service`, `health-service`, `app`. Needs `CHROME_BIN=/usr/bin/brave` on this machine (no Chrome/Chromium installed).
- [x] Both interceptors run on every `HttpClient` request; a forced 4xx surfaces as an `AppError`. — covered by specs (409/422/400/network/500 → `AppError`; both interceptors wired in `app.config.ts`). Live-API browser smoke **not run** — API was not running this session; run it per the checklist below once it is up.
- [x] No feature code yet — `src/app/features/` is empty.

### Completion notes

Done 2026-09-02. Built as 7 gated steps (environments → `core/types/` → `core/money/` → `core/http/` → `core/registry/` + `core/health/` → routing shell → verification).

**Structure delta vs. `docs/DESIGN.md` §2:** all hand-written types live under `core/types/` — the
`health/` and `registry/` folders hold only their services. So `core/types/` also carries
`health-status.ts`, `health-check-entry.ts`, `health-report.ts`, and `registered-instrument.ts`
(the last is the Phase-0 local instrument shape; `InstrumentType` proper arrives in Step 1.2).

**Intentional deviations (D1–D8):**

- **D1** — Dev API URL is `https://localhost:7095/v1`, not `http://localhost:5000/v1`. The API binds
  7095/5003 (`launchSettings.json`); `:5000` does not exist. User chose HTTPS. Requires
  `dotnet dev-certs https --trust`. `docs/DESIGN.md` §10 to be reconciled.
- **D2** — `app.routes.ts` stays `Routes = []` in Phase 0 (documented comment block only). The four
  `loadChildren` entries are added per feature in Phase 1; honors the "`src/app/features/` is empty"
  DoD (a `loadChildren` to a missing file breaks the build).
- **D3** — `core/types/registered-instrument.ts` is a self-contained shape with an inline
  `'debit' | 'credit' | 'cash'` union. Phase 1 Step 1.2 defines `InstrumentType` and reconciles.
- **D4** — `problemDetailsInterceptor` trusts `HttpErrorResponse.status` as authoritative instead of
  re-deriving it from a client-side `code`→status table. The API always sets the status from the
  code (verified in `ErrorHttpStatusHelper.cs` / `ProblemResultsHelper.cs`); §5's table is
  documentation, exercised by representative spec branches, not reimplemented.
- **D5** — `core/http/skip-error-mapping.ts` adds `SKIP_ERROR_MAPPING: HttpContextToken<boolean>`.
  `HealthService` sets it so it can read the raw `GET /health` **503** body (an `Unhealthy` report)
  while the interceptor stays globally registered.
- **D6** — `pollUntil` completes via `first(done)` with an inner `retry(1)`; operator set
  `timer / take / switchMap / first`. `first(done)` errors with `EmptyError` when attempts run out —
  the caller surfaces "still reconciling". `poll` is re-invoked each tick, so `retry(1)` genuinely
  re-issues a cold HTTP call.
- **D7** — interceptor/helper files use the dotted `*.interceptor.ts` form (per `docs/DESIGN.md` §2
  and this file), overriding the style guide's hyphen form.
- **D8** — `poll-until.spec.ts` uses RxJS `TestScheduler` marbles, not `fakeAsync`/`tick`: the
  `@angular/build:karma` setup does not load `zone.js/testing`.

**Environment note (not a design deviation):** this machine has no Chrome/Chromium, only Brave.
`pnpm ng test` needs `CHROME_BIN=/usr/bin/brave` prefixed (Brave runs headless fine). Options for a
permanent fix: install `chromium`, or add a `karma.conf.js` custom launcher. Left stock for now.

**Pending manual verification (needs the API up + trusted dev cert):**

1. `dotnet dev-certs https --trust`, run the API on `https://localhost:7095`, `pnpm ng serve`.
2. From the browser console, force a 409/422 and confirm an `AppError` keyed off `code` is thrown.
3. Hit `environment.healthUrl` and confirm a `HealthReport` (try it with the API stopped too →
   synthetic `Unhealthy`).

---

## Phase 1 — Fase 1: standalone-valuable core

**Goal:** Ship the five Fase-1 views: Dashboard, Instruments setup, Load expense, Statement detail & pay, Reverse movement.

**Traces to:** `docs/PRD.md` §5 "Fase 1" — US-1/US-2 (§3.1), prerequisite (§3.2), US-3 (§3.3), US-4 (§3.4), US-6 (§3.5). Types and services per `docs/DESIGN.md` §3, §4, §9.

**Depends on:** Phase 0.

### 1.1 — Reports feature + Dashboard (US-1, US-2 / `docs/PRD.md` §3.1)

- [x] `features/reports/types/monthly-expense-row.ts` — `MonthlyExpenseRow = { month: string; category: string; amountMinorUnits: Money; currencyCode: CurrencyCode }`.
- [x] `features/reports/types/card-due-row.ts` — `CardDueRow = { bucket: 'Accrued' | 'Future'; card: string; cycleYear: number | null; cycleMonth: number | null; amountMinorUnits: Money; currencyCode: CurrencyCode; cardId: string | null }`.
- [x] `features/reports/reports-service.ts` — `monthlyExpenses(month?: string): Observable<MonthlyExpenseRow[]>` → `GET /v1/reports/monthly-expenses` (unwrap `{ rows }`); `cardDueByMonth(): Observable<CardDueRow[]>` → `GET /v1/reports/card-due-by-month` (unwrap `{ rows }`). Issues **relative** URLs — `baseUrlInterceptor` prefixes `environment.apiUrl` (D2 mechanism), so the service carries no host.
- [x] `features/reports/reports-service.spec.ts` — `HttpTestingController` (URL, verb, `?month=` present/absent, `{ rows }` unwrap, `AppError` on a flushed 422).
- [x] `features/reports/pages/dashboard-page/` — container `DashboardPage`: `WritableSignal` state for both datasets + selected month (defaults to the current `'YYYY-MM'`); `computed()` groupings (expenses summed by category; card-due split Accrued vs Future, grouped by card); native month selector re-fetches `monthlyExpenses` only; per-feed loading / empty / error states via a `LoadStatus` signal + `@if` (`docs/PRD.md` §3.1). Monthly expenses shown as-is (API already excludes card purchases & receivables — API D9).
- [x] `features/reports/reports.routes.ts` (`{ path: '', component: DashboardPage }`) + lazy-wired into `app.routes.ts` with `'' → reports` as the default route.

### 1.2 — Instruments feature + setup page (`docs/PRD.md` §3.2)

- [x] `core/types/instrument-type.ts` — `InstrumentType = 'debit' | 'credit' | 'cash'`. **D9:** placed in `core/types/` (next to `CurrencyCode`), not `features/instruments/types/` — `core`'s `RegisteredInstrument` needs it, and `core` must not import from a feature.
- [x] `features/instruments/types/create-instrument.ts` — `CreateInstrument = { type: InstrumentType; name: string; cutoffDate?: number }` (required when `type === 'credit'`, 1–31).
- [x] `features/instruments/types/instrument-created.ts` — `InstrumentCreated = { id: string; type: InstrumentType }`.
- [x] ~~`features/instruments/types/instrument.ts` — local registry shape~~ — not needed; `core/types/registered-instrument.ts` reconciled to import `InstrumentType` (closes **D3**).
- [x] `features/instruments/instruments-service.ts` — `create(body: CreateInstrument): Observable<InstrumentCreated>` → `POST /v1/instruments` (relative URL; `baseUrlInterceptor` prefixes the host).
- [x] `features/instruments/instruments-service.spec.ts` — `HttpTestingController` (URL, verb, body, `AppError` on a flushed 400 `Instruments.UnknownType`).
- [x] `features/instruments/validation-helpers.ts` — `creditRequiresCutoff: ValidatorFn` (group-level): sets/clears `creditCutoff` on the `cutoffDate` control when `type === 'credit'` and the value is not an integer 1–31.
- [x] `features/instruments/pages/instruments-page/` — `InstrumentsPage`: OnPush container, reactive form built in `initInstrumentForm()` from `ngOnInit` (`type` default `debit`, `name` required, `cutoffDate` gated by the group validator); list rendered from `InstrumentRegistryService`; on create success `registry.add(...)` (trimmed name, `cutoffDate` only for credit) then `form.reset()`; submit errors keyed off `AppError.code` via a message map (`docs/DESIGN.md` §8).
- [x] `features/instruments/instruments.routes.ts` (`{ path: '', component: InstrumentsPage }`) + lazy-wired into `app.routes.ts` (`instruments` entry).

### 1.3 — Financing types + service (`docs/PRD.md` §3.3, §3.4)

- [x] `features/financing/types/` — nine files, each one type, mirroring `docs/DESIGN.md` §3: `split-participant.ts` (`SplitParticipant = { partyId: string; weight: number }`), `create-payment-plan.ts` (`CreatePaymentPlan = { amountMinorUnits: Money; cardId: string; installmentCount: number; purchaseDate: IsoDate; split?: SplitParticipant[] }`), `create-payment-plan-result.ts` (`{ paymentPlanId: string }`), `monthly-statement-installment.ts` (`{ planId; installmentId; sequence; installmentCount; purchaseDate: IsoDate; cycleYear; cycleMonth; amountMinorUnits: Money; isReversed: boolean }`), `monthly-statement.ts` (`{ statementId; cardId; cardName; cycleYear; cycleMonth; amountDueMinorUnits: Money; isPaid: boolean; paidOnUtc: IsoInstant | null; installments: MonthlyStatementInstallment[] }`), `pay-statement.ts` (`{ bankAccountId: string; paidOnUtc: IsoInstant }`), `pay-statement-result.ts` (`{ statementId: string }`), `card-future-schedule-row.ts` (`{ planId; installmentId; sequence; cycleYear; cycleMonth; amountMinorUnits: Money }`), `card-future-schedule.ts` (`{ cardId: string; rows: CardFutureScheduleRow[] }`).
- [x] `features/financing/financing-service.ts` — `createPaymentPlan(body): Observable<CreatePaymentPlanResult>` → `POST /v1/financing/payment-plans`; `getStatement(id): Observable<MonthlyStatement>` → `GET /v1/financing/statements/{id}`; `payStatement(id, body): Observable<PayStatementResult>` → `POST /v1/financing/statements/{id}/pay`; `getFutureSchedule(cardId): Observable<CardFutureSchedule>` → `GET /v1/financing/cards/{id}/future-schedule`. Relative URLs; `getStatement` / `getFutureSchedule` return the object as-is (`installments` / `rows` are DTO fields, not a `{ rows }` envelope).
- [x] `features/financing/financing-service.spec.ts` — `HttpTestingController`: all four methods, `split` present/absent, `installments` / `rows` returned intact, `AppError` on 409 `Financing.AlreadyPaid` and 404 `Financing.StatementNotFound`. No view wired yet (routes land in Step 1.5).

### 1.4 — Split-flow dependencies (traced deviation — see "Deviations baked in" #2)

- [x] `features/parties/types/current-account-balance.ts` — `CurrentAccountBalance = { partyId: string; name: string; balanceMinorUnits: Money }`.
- [x] `features/parties/parties-service.ts` — **only** `getBalance(partyId: string): Observable<CurrentAccountBalance>` → `GET /v1/parties/{id}/balance` (relative URL; the `pollUntil` reconciliation target, `docs/DESIGN.md` §7). Remaining methods land in Phase 3.
- [x] `features/reports/types/party-debt-row.ts` — `PartyDebtRow = { partyId: string; partyName: string; netBalanceMinorUnits: Money; currencyCode: CurrencyCode }`.
- [x] `features/reports/reports-service.ts` — added `debtSummary(): Observable<PartyDebtRow[]>` → `GET /v1/reports/parties/debt-summary` (unwrap `{ rows }`); the only way to enumerate parties for the split form (`docs/PRD.md` §3.7, `docs/DESIGN.md` §8).
- [x] `parties-service.spec.ts` (new — URL/verb, `AppError` on 404 `Parties.NotFound`) and `reports-service.spec.ts` (extended — `debtSummary` URL/verb/unwrap). No route wiring (parties routes are Phase 3; `reports` was already wired in 1.1).

### 1.5 — Load-expense page (US-3 / `docs/PRD.md` §3.3)

- [ ] `features/financing/pages/load-expense-page/` — `LoadExpensePage`: reactive form — amount (major units → `toMinorUnits` at submit), card (from `InstrumentRegistryService`), installment count, purchase date (`YYYY-MM-DD`), optional split rows (`partyId` from `ReportsService.debtSummary` + integer `weight`). On submit: call `createPaymentPlan`, confirm the returned `paymentPlanId` immediately, then — when a split was included — `pollUntil(() => partiesService.getBalance(partyId))` with a short bounded retry before showing the reconciled party balance (`docs/DESIGN.md` §7; API D8 eventual consistency). Client never computes cycle or split cents.
- [ ] Route in `financing.routes.ts` + lazy-wire.

### 1.6 — Statement detail & pay page (US-4 / `docs/PRD.md` §3.4)

- [ ] `features/financing/pages/statement-page/` — `StatementPage`: statement id from a route param (no list endpoint — gap 2). `getStatement(id)` → render cycle (`cycleYear`/`cycleMonth`), `amountDueMinorUnits`, `isPaid`/`paidOnUtc`, and the itemized `installments` (sequence rendered "N of M" from `sequence`/`installmentCount`, `purchaseDate`, `amountMinorUnits`, `isReversed`). Optional future schedule via `getFutureSchedule(cardId)`. Reactive Pay form: `bankAccountId` (from `InstrumentRegistryService`), `paidOnUtc`; calls `payStatement(id, body)`. Plain-language note: a reversed **paid** installment yields a card credit netted on the **next** statement, not cash back (API D12, `docs/PRD.md` §3.4).
- [ ] Route in `financing.routes.ts` (id-driven) + lazy-wire.

### 1.7 — Ledger feature + Reverse-movement page (US-6 / `docs/PRD.md` §3.5)

- [ ] `features/ledger/types/` — `direction.ts` (`Direction = 'Debit' | 'Credit'`), `transaction-line.ts` (`{ accountId: string; direction: Direction; amountMinorUnits: Money }`), `post-transaction.ts` (`{ lines: TransactionLine[]; postedOnUtc: IsoInstant; splitReferenceId?: string; installmentReferenceId?: string; description?: string }`), `post-transaction-result.ts` (`{ transactionId: string }`), `reverse-transaction-result.ts` (`{ reversalTransactionId: string; originalTransactionId: string; compensatingEntryPosted: boolean }`), `account-balance.ts` (`{ accountId: string; balanceMinorUnits: Money; currencyCode: CurrencyCode; formatted: string }`).
- [ ] `features/ledger/ledger-service.ts` — `reverse(transactionId: string): Observable<ReverseTransactionResult>` → `POST /v1/ledger/transactions/{id}/reversal`; `getAccountBalance(accountId: string): Observable<AccountBalance>` → `GET /v1/ledger/accounts/{id}/balance`; `postTransaction(body: PostTransaction): Observable<PostTransactionResult>` → `POST /v1/ledger/transactions` (internal, no view — deviation #3).
- [ ] `features/ledger/ledger-service.spec.ts` — `HttpTestingController` (all three methods).
- [ ] `features/ledger/pages/reverse-movement-page/` — `ReverseMovementPage`: id-driven confirm + result view (transaction id surfaced from another view — no feed, gap 3). Shows `compensatingEntryPosted` / card-credit notation for an already-paid installment; append-only messaging (reversal never deletes — `docs/PRD.md` §3.5, §4). May poll `getAccountBalance` after the reversal.
- [ ] `features/ledger/ledger.routes.ts` + lazy-wire.

### 1.8 — Fase-1 integration pass

- [ ] All Fase-1 feature routes lazy-wired in `app.routes.ts`; default route resolves to the dashboard.
- [ ] `pnpm ng build` + `pnpm ng test --watch=false --browsers=ChromeHeadless` green.
- [ ] Manual smoke against a local API (`http://localhost:5000/v1`): each view walked through loading / empty / error; a forced 409/422 renders an `AppError`.

### Definition of done

- [ ] Dashboard, Instruments setup, Load expense, Statement detail & pay, Reverse movement are all reachable via lazy routes.
- [ ] `ReportsService`, `InstrumentsService`, `FinancingService`, `LedgerService`, and `PartiesService` (partial) each have an `HttpTestingController` spec covering URL, verb, body, `{ rows }` unwrap, and `AppError` mapping.
- [ ] Money is entered in major units and submitted as minor units everywhere; no float arithmetic on `Money`.
- [ ] Reversal-credit wording (API D12) is present on the Statement page.

### Completion notes

_(filled by the implementer)_

---

## Phase 2 — Fase 2: Subscriptions

**Goal:** Ship the Subscriptions view — list active, create, cancel.

**Traces to:** `docs/PRD.md` §5 "Fase 2" — US-5 (§3.6). Types and service per `docs/DESIGN.md` §3, §4.

**Depends on:** Phase 0 core wiring. Independent of the financing/ledger views (may run alongside Phase 1).

### Tasks

- [ ] `features/subscriptions/types/frequency.ts` — `Frequency = 'monthly' | 'weekly' | 'daily' | 'annually'`.
- [ ] `features/subscriptions/types/create-subscription.ts` — `CreateSubscription = { name: string; amountMinorUnits: Money; category: string; fundingAccountId: string; frequency: Frequency; anchorDay: number }`.
- [ ] `features/subscriptions/types/subscription-result.ts` — `SubscriptionResult = { id: string }`.
- [ ] `features/subscriptions/types/active-subscription.ts` — `ActiveSubscription = { subscriptionId: string; name: string; amountMinorUnits: Money; category: string; frequency: Frequency; anchorDay: number; nextDueDate: IsoDate }`.
- [ ] `features/subscriptions/subscriptions-service.ts` — `listActive(): Observable<ActiveSubscription[]>` → `GET /v1/subscriptions/active` (unwrap `{ rows }`); `create(body): Observable<SubscriptionResult>` → `POST /v1/subscriptions`; `cancel(id): Observable<void>` → `DELETE /v1/subscriptions/{id}`.
- [ ] `features/subscriptions/subscriptions-service.spec.ts` — `HttpTestingController` (all three methods).
- [ ] `features/subscriptions/pages/subscriptions-page/` — `SubscriptionsPage`: list (name, amount, category, frequency, anchor day, next due date); create form (note: first period is charged immediately — informational); cancel action (stops future renewals; past charges remain). No optimistic UI for scheduled effects — reflect latest state on load (API D6, `docs/DESIGN.md` §7).
- [ ] `features/subscriptions/subscriptions.routes.ts` + lazy-wire into `app.routes.ts`.

### Definition of done

- [ ] Subscriptions view reachable via a lazy route.
- [ ] `SubscriptionsService` spec covers URL, verb, body, `{ rows }` unwrap, and `AppError` mapping.
- [ ] `pnpm ng build` + `pnpm ng test --watch=false --browsers=ChromeHeadless` green.

### Completion notes

_(filled by the implementer)_

---

## Phase 3 — Fase 3: Parties

**Goal:** Ship the Parties list, party detail, and shared-expense views; complete `PartiesService` and the reporting party-timeline method.

**Traces to:** `docs/PRD.md` §5 "Fase 3" — US-7 (§3.7), plus reversal audit visibility (§3.5). Types and services per `docs/DESIGN.md` §3, §4, §9.

**Depends on:** Phase 1 (extends the Phase-1 `PartiesService.getBalance` and `ReportsService.debtSummary`).

### Tasks — types (`features/parties/types/`; `current-account-balance.ts` already exists from Step 1.4)

- [ ] `create-party.ts` — `CreateParty = { name: string }`.
- [ ] `party-result.ts` — `PartyResult = { id: string }`.
- [ ] `current-account-timeline-row.ts` — `CurrentAccountTimelineRow = { movementOnUtc: IsoInstant; description: string; deltaMinorUnits: Money; runningBalanceMinorUnits: Money }`.
- [ ] `shared-expense-participant.ts` — `SharedExpenseParticipant = { partyId: string; weight: number }`.
- [ ] `register-shared-expense.ts` — `RegisterSharedExpense = { description: string; totalMinorUnits: Money; expenseAccountId: string; fundingAccountId: string; incurredOnUtc: IsoInstant; participants: SharedExpenseParticipant[] }`.
- [ ] `shared-expense-result.ts` — `SharedExpenseResult = { splitReferenceId: string }`.
- [ ] `settle-current-account.ts` — `SettleCurrentAccount = { amountMinorUnits: Money; bankAccountId: string; settledOnUtc: IsoInstant }`.
- [ ] `settlement-result.ts` — `SettlementResult = { ledgerTransactionId: string }`.

### Tasks — services

- [ ] `features/parties/parties-service.ts` — add `create(body): Observable<PartyResult>` → `POST /v1/parties`; `registerSharedExpense(body): Observable<SharedExpenseResult>` → `POST /v1/parties/shared-expenses`; `settle(partyId, body): Observable<SettlementResult>` → `POST /v1/parties/{id}/settlements`; `getTimeline(partyId): Observable<CurrentAccountTimelineRow[]>` → `GET /v1/parties/{id}/timeline`.
- [ ] `features/reports/types/party-timeline-row.ts` — `PartyTimelineRow = { movementOnUtc: IsoInstant; description: string; deltaMinorUnits: Money; runningBalanceMinorUnits: Money; currencyCode: CurrencyCode }`.
- [ ] `features/reports/reports-service.ts` — add `partyTimeline(partyId): Observable<PartyTimelineRow[]>` → `GET /v1/reports/parties/{id}/timeline` (unwrap `{ rows }`; reporting equivalent of `getTimeline`).
- [ ] Extend `parties-service.spec.ts` and `reports-service.spec.ts` for the new methods.

### Tasks — pages

- [ ] `features/parties/pages/parties-page/` — `PartiesPage`: list every party with net balance from `ReportsService.debtSummary` (positive = they owe you); create-party form.
- [ ] `features/parties/pages/party-detail-page/` — `PartyDetailPage`: party id from route param; current balance (`PartiesService.getBalance`) + movement timeline (chronological, running balance) from `getTimeline` or `ReportsService.partyTimeline`; actions to register a shared expense and to register a settlement; surface reversal state where the timeline exposes it (`docs/PRD.md` §3.5 audit visibility, §3.7). Cross-debts net server-side — show the resulting net only.
- [ ] `features/parties/pages/shared-expense-page/` — `SharedExpensePage`: reactive form — description, total (major → minor at submit), `expenseAccountId`, `fundingAccountId`, `incurredOnUtc`, participant rows (`partyId` + integer `weight`); calls `registerSharedExpense`.
- [ ] `features/parties/parties.routes.ts` + lazy-wire into `app.routes.ts`.

### Definition of done

- [ ] Parties list, party detail, and shared-expense views reachable via lazy routes.
- [ ] `PartiesService` (full) and `ReportsService.partyTimeline` have `HttpTestingController` coverage.
- [ ] Net balances and timelines render the API's values as-is (no client-side accounting).
- [ ] `pnpm ng build` + `pnpm ng test --watch=false --browsers=ChromeHeadless` green; manual smoke against a local API.

### Completion notes

_(filled by the implementer)_

---

## Phase 4 — Deviations & deferred items

**Not traced to any PRD view.** These are the gaps the documents already flag (`docs/PRD.md` §7, `docs/DESIGN.md` §11) plus two scaffold-state gaps. Work each only when its trigger lands — do not schedule them into Phases 0–3.

### Tasks

- [ ] **4.1 Tailwind + UI-task groundwork** — install Tailwind and wire the build; tokens, theme, and components belong to the separate UI task. Trigger: UI task kickoff. (`docs/DESIGN.md` §10; `docs/PRD.md` §6)
- [ ] **4.2 API gap 1 — `GET /v1/instruments`** — when the endpoint ships, add the list method and retire the `InstrumentRegistryService` `localStorage` stopgap; forms then read cards/accounts from the API. (`docs/DESIGN.md` §11.1; `docs/PRD.md` §7.1)
- [ ] **4.3 API gap 2 — `GET /v1/financing/cards/{id}/statements`** — make the Statement pay flow self-navigable; drop the id-only route entry. (`docs/DESIGN.md` §11.2; `docs/PRD.md` §7.2)
- [ ] **4.4 API gap 3 — transactions feed (`GET /v1/ledger/transactions`)** — build a real history / reverse-picker view, replacing the id-driven `reverse-movement-page`. (`docs/DESIGN.md` §11.3; `docs/PRD.md` §7.3)
- [ ] **4.5 API gap 4 — app-level auth surface** — when a backing endpoint exists, add login as a new cross-cutting feature (route guard, session). Not part of any view above. (`docs/DESIGN.md` §11.4; `docs/PRD.md` §2, §7.4)
- [ ] **4.6 Client CI** — add `ng build` + `ng test` (and lint, once 4.7 lands) steps for `app/client/` to `/.github/workflows/ci.yml` (currently API-only). Trigger: Fase 1 merged. _(Scaffold-state gap — no PRD/DESIGN statement.)_
- [ ] **4.7 ESLint config** — add `@angular-eslint` (none present today) before enforcing lint in CI. _(Scaffold-state gap — no PRD/DESIGN statement.)_

### Definition of done

- [ ] Each item closed only against its real trigger, with the corresponding `docs/PRD.md` / `docs/DESIGN.md` gap reference noted in its completion entry.

### Completion notes

_(filled by the implementer)_

---

## Verification (every phase)

- **Build:** `pnpm ng build` — 0 errors, within the 500 kB warning / 1 MB error initial-JS budget.
- **Unit:** `pnpm ng test --watch=false --browsers=ChromeHeadless` — green; each new service has an `HttpTestingController` spec.
- **Manual:** run the API locally (`http://localhost:5000/v1`), `pnpm ng serve`, walk each new view through loading / empty / error; force a 409/422 and confirm an `AppError` renders keyed off `code`.
- **Type parity:** every `types/*.ts` field-matches the DTO tables in `docs/DESIGN.md` §3.
- **Traceability:** every step above cites a `docs/PRD.md` `§`/`US` or `docs/DESIGN.md` `§`; every Phase 4 item cites its gap number.
