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

- [x] `features/financing/validation-helpers.ts` — `positiveAmount`, `atMostTwoDecimals`, `positiveInteger` (covers both `installmentCount` and split `weight`), `isoDate` — control-level `ValidatorFn`s.
- [x] `features/financing/pages/load-expense-page/` — `LoadExpensePage`: OnPush container, reactive form — `amount` (major → `toMinorUnits` at submit, guarded by `atMostTwoDecimals`), `cardId` (credit-only `computed` over `InstrumentRegistryService`), `installmentCount`, `purchaseDate` (`YYYY-MM-DD`), optional `split` `FormArray` (`partyId` from `ReportsService.debtSummary` loaded on init + integer `weight`). On submit: `createPaymentPlan` (`split` omitted when empty), confirm `paymentPlanId` immediately, then per participant snapshot `getBalance` → `pollUntil(() => partiesService.getBalance(pid), b => b.balanceMinorUnits !== prior, { intervalMs: 800, maxAttempts: 5 })` → render reconciled balance, or a "still reconciling" note on `EmptyError` (`docs/DESIGN.md` §7; API D8). Client computes no cycle / no split cents.
- [x] `features/financing/pages/load-expense-page/load-expense-page.spec.ts` — form validity, minor-units submit body, `split` present/absent, `AppError` on 422, reconciliation reconciled-vs-stalled paths (RxJS `TestScheduler` virtual time, no `fakeAsync`).
- [x] Route `{ path: 'load-expense', component: LoadExpensePage }` in `financing.routes.ts` + `financing` `loadChildren` lazy-wired into `app.routes.ts` (first financing step).

### 1.6 — Statement detail & pay page (US-4 / `docs/PRD.md` §3.4)

- [x] `features/financing/pages/statement-page/` — `StatementPage`: OnPush container, statement id from `ActivatedRoute.paramMap` (no list endpoint — gap 2), stored for the Pay submit. `getStatement(id)` → render cycle (`cycleYear`-`cycleMonth`), `formatArs(amountDueMinorUnits)`, `isPaid`/`paidOnUtc`. Reactive Pay form (shown only while `!isPaid`): `bankAccountId` (`computed` over `InstrumentRegistryService` where `type === 'debit'`), `paidOnUtc` (`datetime-local` → `new Date(...).toISOString()` at submit) → `payStatement(id, body)`; on success `payStatus = 'paid'` + re-fetch via `getStatement(result.statementId)`; submit errors keyed off `AppError.code`. Plain-language note rendered unconditionally: reversing a **paid** installment posts a card credit netted against the **next** statement, not cash back (API D12, `docs/PRD.md` §3.4). Future-schedule section deferred (optional; `getFutureSchedule` stays service-only until a card view needs it).
- [x] `features/financing/pages/statement-page/installments-table.{ts,html,css}` — `InstallmentsTable`: presentational, `input.required<MonthlyStatementInstallment[]>()`, renders "N of M" (`sequence`/`installmentCount`), `purchaseDate`, `formatArs(amountMinorUnits)`, a `Reversed` badge when `isReversed`; empty-state note when the array is empty.
- [x] `features/financing/pages/statement-index/` — `StatementIndex`: minimal "open statement by id" form → `Router.navigate(['financing', 'statements', id])` (gap-2 reachability entry).
- [x] Specs: `installments-table.spec.ts` (3 — "N of M" + amount, single `Reversed` badge, empty note), `statement-page.spec.ts` (7 — loads by route param, renders cycle/amount/"N of M", reversal note present, Pay form validity, records payment + refetches with ISO instant, `AppError` on 409 `Financing.AlreadyPaid`, Pay form hidden when `isPaid`; `ActivatedRoute` stub `paramMap: of(convertToParamMap({ id }))`), `statement-index.spec.ts` (3 — creates, navigates trimming the id, no-nav on blank).
- [x] Routes `{ path: 'statements', component: StatementIndex }` + `{ path: 'statements/:id', component: StatementPage }` in `financing.routes.ts` (`financing` `loadChildren` already lazy-wired in 1.5).

### 1.7 — Ledger feature + Reverse-movement page (US-6 / `docs/PRD.md` §3.5)

- [x] `features/ledger/types/` — six files, one type each, mirroring `docs/DESIGN.md` §3: `direction.ts` (`Direction = 'Debit' | 'Credit'`), `transaction-line.ts` (`{ accountId: string; direction: Direction; amountMinorUnits: Money }`), `post-transaction.ts` (`{ lines: TransactionLine[]; postedOnUtc: IsoInstant; splitReferenceId?: string; installmentReferenceId?: string; description?: string }`), `post-transaction-result.ts` (`{ transactionId: string }`), `reverse-transaction-result.ts` (`{ reversalTransactionId: string; originalTransactionId: string; compensatingEntryPosted: boolean }`), `account-balance.ts` (`{ accountId: string; balanceMinorUnits: Money; currencyCode: CurrencyCode; formatted: string }`).
- [x] `features/ledger/ledger-service.ts` — `postTransaction(body): Observable<PostTransactionResult>` → `POST /v1/ledger/transactions` (internal, no view — deviation #3, still implemented + tested); `reverse(transactionId): Observable<ReverseTransactionResult>` → `POST /v1/ledger/transactions/{id}/reversal` (empty `{}` body); `getAccountBalance(accountId): Observable<AccountBalance>` → `GET /v1/ledger/accounts/{id}/balance` (object as-is). Relative URLs.
- [x] `features/ledger/ledger-service.spec.ts` — `HttpTestingController`: all three methods (URL, verb, body), reversal body `{}`, `AppError` on 409 `Ledger.CannotReverseAReversal`.
- [x] `features/ledger/pages/reverse-movement-page/` — `ReverseMovementPage`: OnPush, transaction id from `ActivatedRoute.paramMap` (surfaced by the view that posted it — no feed, gap 3), no form (single confirm button). `onConfirm()` → `reverse(id)` → result signal + status `'reversed'`; errors keyed off `AppError.code` (`Ledger.AlreadyReversed`, `Ledger.CannotReverseAReversal`, …). Result view renders `reversalTransactionId` / `originalTransactionId` / `compensatingEntryPosted` (card-credit-netted-next-statement wording when `true`, plain-reversing-entry wording when `false`). Append-only note rendered unconditionally: the original transaction is never deleted, the API posts a compensating entry (`docs/PRD.md` §3.5, §4). Post-reversal `getAccountBalance` poll left out (optional).
- [x] `features/ledger/pages/reverse-index/` — `ReverseIndex`: minimal "reverse by id" form → `Router.navigate(['ledger', 'transactions', id, 'reverse'])` (gap-3 reachability entry).
- [x] Specs: `reverse-index.spec.ts` (3 — creates, navigates trimming the id, no-nav on blank), `reverse-movement-page.spec.ts` (5 — id from route param, append-only note present, reverses on confirm + renders result, no-compensating-entry wording, `AppError` on 409).
- [x] `features/ledger/ledger.routes.ts` (`{ path: 'transactions', component: ReverseIndex }` + `{ path: 'transactions/:id/reverse', component: ReverseMovementPage }`) + `ledger` `loadChildren` lazy-wired into `app.routes.ts`; `{ path: '**', redirectTo: 'reports' }` wildcard added (last feature step).

### 1.8 — Fase-1 integration pass

- [x] All Fase-1 feature routes lazy-wired in `app.routes.ts` (`reports`, `instruments`, `financing`, `ledger`); `''` → `reports` redirect + `**` → `reports` wildcard; default route resolves to the dashboard.
- [x] `pnpm ng build` (255.50 kB initial JS, under the 500 kB warn budget) + `pnpm ng test --watch=false --browsers=ChromeHeadless` (99/99) green.
- [ ] Manual smoke against a live API — **deferred**, API not running on `https://localhost:7095` (D1) at integration time. See Completion notes.

### Definition of done

- [x] Dashboard, Instruments setup, Load expense, Statement detail & pay, Reverse movement are all reachable via lazy routes.
- [x] `ReportsService`, `InstrumentsService`, `FinancingService`, `LedgerService`, and `PartiesService` (partial) each have an `HttpTestingController` spec covering URL, verb, body, `{ rows }` unwrap, and `AppError` mapping.
- [x] Money is entered in major units and submitted as minor units everywhere; no float arithmetic on `Money`.
- [x] Reversal-credit wording (API D12) is present on the Statement page.

### Completion notes

Done 2026-09-02. Built as 8 gated steps (1.1 → 1.8), one conventional commit per step on
`feat/client-phase-1`, one PR to `main` after 1.8.

**Shipped — the five Fase-1 views, all lazy-routed:**

- `reports` — `DashboardPage` (US-1, US-2). Default route. `ReportsService.monthlyExpenses` /
  `cardDueByMonth` / `debtSummary`, all `{ rows }` unwrap.
- `instruments` — `InstrumentsPage` (§3.2). `InstrumentsService.create`; local list from
  `InstrumentRegistryService` (API gap 1).
- `financing/load-expense` — `LoadExpensePage` (US-3). Split `FormArray` + snapshot-then-
  `pollUntil` reconciliation; `EmptyError` → "still reconciling".
- `financing/statements/:id` (+ `statements` index) — `StatementPage` + presentational
  `InstallmentsTable` (US-4). Pay form → `payStatement` + refetch. Reversal-credit note (D12).
- `ledger/transactions/:id/reverse` (+ `transactions` index) — `ReverseMovementPage` (US-6).
  Confirm → `reverse`; append-only result view, `compensatingEntryPosted` wording.

**Build / tests:** `pnpm ng build` clean — initial JS **255.50 kB** (well under the 500 kB
warn budget). Every feature view sits in its own lazy chunk — `reports-routes` 5.44 kB,
`instruments-routes` 6.26 kB, `financing-routes` 22.23 kB, `ledger-routes` 6.88 kB — so the
initial bundle grew only by the router wiring across steps 1.1 → 1.7 (240.54 → 255.50 kB).
Tests **99/99** green (`CHROME_BIN=/usr/bin/brave`, ChromeHeadless).

**`status-view` extraction — decided against.** The inline `@if (status() === 'loading')` /
`'error'` chains are 3–4 lines per container and the copy differs per page; two of the four
containers use domain-specific action statuses (`'paying'`/`'paid'`, `'reversing'`/`'reversed'`)
rather than the plain `loading|ready|error` triple. A shared presentational component would
need a slot or input per message and would not shrink the pages. Left inline; revisit in
Phase 2+ only if a fifth view repeats the exact same triple.

**Intentional deviations (continuing D-numbering from Phase 0's D8):**

- **D9** — `InstrumentType` lives in `core/types/instrument-type.ts`, not
  `features/instruments/types/`. `core/types/registered-instrument.ts` needs it and `core`
  must not import from a feature. Closes the Phase-0 D3 placeholder. (Recorded inline at 1.2.)
- **D10** — the Statement page's **future-schedule section is deferred**. `FinancingService.
  getFutureSchedule` is implemented and spec-covered but no UI consumes it in Phase 1 — it
  belongs to a card-centric view, not the pay flow. US-4's pay path is fully covered without
  it.
- **D11** — `LedgerService.reverse` POSTs an empty `{}` body (`HttpClient.post` requires a
  body arg; the endpoint takes no payload). `postTransaction` is implemented + tested though
  no Phase-1 view drives it (plan deviation #3).

**Pending manual verification (needs the API up + trusted dev cert):**

1. `dotnet dev-certs https --trust`, run the API on `https://localhost:7095` (D1), `pnpm ng serve`.
2. Walk each of the five views through loading / empty / error.
3. Force a 409 (pay an already-paid statement; reverse a reversal) and a 422 (bad payment
   values) and confirm the rendered message is keyed off `AppError.code`, not `detail`.
4. Include a split on Load expense against a live API and confirm the reconciliation note
   resolves (or shows "still reconciling" after 5 attempts).

---

## Phase 2 — Fase 2: Subscriptions

**Goal:** Ship the Subscriptions view — list active, create, cancel.

**Traces to:** `docs/PRD.md` §5 "Fase 2" — US-5 (§3.6). Types and service per `docs/DESIGN.md` §3, §4.

**Depends on:** Phase 0 core wiring. Independent of the financing/ledger views (may run alongside Phase 1).

### Tasks

- [x] `features/subscriptions/types/frequency.ts` — shipped as `Frequency = 'monthly'` (single-value union — the API implements `Monthly` only; see **D12**).
- [x] `features/subscriptions/types/create-subscription.ts` — `CreateSubscription = { name: string; amountMinorUnits: Money; category: string; fundingAccountId: string; frequency: Frequency; anchorDay: number }`.
- [x] `features/subscriptions/types/subscription-result.ts` — `SubscriptionResult = { id: string }`.
- [x] `features/subscriptions/types/active-subscription.ts` — `ActiveSubscription = { subscriptionId: string; name: string; amountMinorUnits: Money; category: string; frequency: Frequency; anchorDay: number; nextDueDate: IsoDate }`.
- [x] `features/subscriptions/subscriptions-service.ts` — `listActive(): Observable<ActiveSubscription[]>` → `GET /v1/subscriptions/active` (unwrap `{ rows }`, normalise `"Monthly"` → `'monthly'` — D12); `create(body): Observable<SubscriptionResult>` → `POST /v1/subscriptions`; `cancel(id): Observable<void>` → `DELETE /v1/subscriptions/{id}`.
- [x] `features/subscriptions/subscriptions-service.spec.ts` — `HttpTestingController` (all three methods).
- [x] `features/subscriptions/pages/subscriptions-page/` — `SubscriptionsPage`: list (name, amount, category, frequency, anchor day, next due date); create form (note: first period is charged immediately — informational); cancel action (stops future renewals; past charges remain). No optimistic UI for scheduled effects — reflect latest state on load (API D6, `docs/DESIGN.md` §7).
- [x] `features/subscriptions/subscriptions.routes.ts` + lazy-wire into `app.routes.ts`.

### Definition of done

- [x] Subscriptions view reachable via a lazy route (`/subscriptions` → `subscriptions-routes` chunk).
- [x] `SubscriptionsService` spec covers URL, verb, body, `{ rows }` unwrap, and `AppError` mapping.
- [x] `pnpm ng build` + `pnpm ng test --watch=false --browsers=ChromeHeadless` green.

### Completion notes

Done 2026-09-03. Built as 3 gated steps (2.1 → 2.3), one conventional commit per step on
`feat/subscriptions-component`, one PR to `main` after 2.3.

**Shipped — the Subscriptions view (US-5, §3.6), lazy-routed at `/subscriptions`:**

- `subscriptions/types/` — `frequency`, `create-subscription`, `subscription-result`,
  `active-subscription`, field-matched to the verified `CreateSubscriptionDto` /
  `ActiveSubscriptionRowDto` (camelCase, `Money` minor units, `IsoDate` for `nextDueDate`).
- `SubscriptionsService` — `listActive` (`GET /v1/subscriptions/active`, `{ rows }` unwrap +
  `"Monthly"` → `'monthly'` normalisation), `create` (`POST /v1/subscriptions`), `cancel`
  (`DELETE /v1/subscriptions/{id}` — the client's first `.delete<void>()`). Spec covers
  URL/verb/body, envelope unwrap + normalisation, void DELETE, and `AppError` mapping on a
  flushed 422 (`Subscriptions.NonPositiveAmount`) and 404 (`Subscriptions.SubscriptionNotFound`).
- `SubscriptionsPage` — OnPush container: reactive create form (`name`, `amount` entered in
  major units → `toMinorUnits` at submit, `category`, funding account, `frequency`,
  `anchorDay`) + active list as a table with a per-row **Cancel** button. **No optimistic
  UI** — `loadActive()` re-fetches after every create and cancel (API D6, `docs/DESIGN.md`
  §7). Error copy keys off `AppError.code` (`Subscriptions.*` + `Http.*` fallbacks), never
  `detail`. Informational notes rendered for both mutations ("charges the first period
  immediately"; "Cancelling stops future renewals — past charges are not reversed").
- `validation-helpers.ts` — control-level `ValidatorFn`s `positiveAmount`,
  `atMostTwoDecimals`, `dayOfMonth` (integer 1–31).

**Build / tests:** `pnpm ng build` clean — initial JS **255.59 kB** raw / 71.98 kB transfer
(well under the 500 kB warn budget); `subscriptions-routes` is its own lazy chunk (11.73 kB
raw / 3.42 kB transfer). Tests **112/112** green (`CHROME_BIN=/usr/bin/brave`, ChromeHeadless)
— +5 service specs, +8 page specs over Phase 1's 99.

**Manual smoke against a live API — deferred**, like Phase 1: the API is not running on
`https://localhost:7095` (D1) and the browser needs the ASP.NET dev cert trusted. Steps when
run: register an instrument, create a subscription, confirm it lists with a `nextDueDate`,
cancel it, confirm it drops on re-fetch; force a 422 (`anchorDay` 40) and a 409 (cancel
twice) and confirm the message keys off `code`.

**Intentional deviations (continuing D-numbering from Phase 1's D11):**

- **D12** — **Frequency contract.** `docs/DESIGN.md` §3 lists `Frequency = 'monthly' |
  'weekly' | 'daily' | 'annually'`, but the API's `RecurrenceFrequency` enum defines only
  `Monthly`; `POST /v1/subscriptions` parses the string case-insensitively and
  `GET /v1/subscriptions/active` serialises it via `.ToString()` — the literal `"Monthly"`
  (PascalCase). The client types `Frequency = 'monthly'` (single-value union, mirrors what
  the API accepts), `SubscriptionsService.listActive` normalises the `"Monthly"` response to
  `'monthly'` in its `map`, and the create form renders `frequency` as a fixed, disabled
  control. `docs/DESIGN.md` §3 carries a reconciliation note.
- **D13** — **Funding-account picker source.** The create form's funding-account `<select>`
  offers **every** registered instrument (`debit` + `credit` + `cash`) from
  `InstrumentRegistryService`, not just debit accounts. This diverges from the Statement-pay
  `bankAccountId` precedent (debit-only, D-note at 1.6); a subscription may legitimately be
  funded by a card or cash, and the API validates `FundingAccountId` server-side regardless.

---

## Phase 3 — Fase 3: Parties

**Goal:** Ship the Parties list, party detail, and shared-expense views; complete `PartiesService` and the reporting party-timeline method.

**Traces to:** `docs/PRD.md` §5 "Fase 3" — US-7 (§3.7), plus reversal audit visibility (§3.5). Types and services per `docs/DESIGN.md` §3, §4, §9.

**Depends on:** Phase 1 (extends the Phase-1 `PartiesService.getBalance` and `ReportsService.debtSummary`).

### Tasks — types (`features/parties/types/`; `current-account-balance.ts` already exists from Step 1.4)

- [x] `create-party.ts` — `CreateParty = { name: string }`.
- [x] `party-result.ts` — `PartyResult = { id: string }`.
- [x] `current-account-timeline-row.ts` — `CurrentAccountTimelineRow = { movementOnUtc: IsoInstant; description: string; deltaMinorUnits: Money; runningBalanceMinorUnits: Money }`.
- [x] `shared-expense-participant.ts` — `SharedExpenseParticipant = { partyId: string; weight: number }`.
- [x] `register-shared-expense.ts` — `RegisterSharedExpense = { description: string; totalMinorUnits: Money; expenseAccountId: string; fundingAccountId: string; incurredOnUtc: IsoInstant; participants: SharedExpenseParticipant[] }`.
- [x] `shared-expense-result.ts` — `SharedExpenseResult = { splitReferenceId: string }`.
- [x] `settle-current-account.ts` — `SettleCurrentAccount = { amountMinorUnits: Money; bankAccountId: string; settledOnUtc: IsoInstant }`.
- [x] `settlement-result.ts` — `SettlementResult = { ledgerTransactionId: string }`.

### Tasks — services

- [x] `features/parties/parties-service.ts` — add `create(body): Observable<PartyResult>` → `POST /v1/parties`; `registerSharedExpense(body): Observable<SharedExpenseResult>` → `POST /v1/parties/shared-expenses`; `settle(partyId, body): Observable<SettlementResult>` → `POST /v1/parties/{id}/settlements`; `getTimeline(partyId): Observable<CurrentAccountTimelineRow[]>` → `GET /v1/parties/{id}/timeline` (**unwraps `{ rows }`** — D14).
- [x] `features/reports/types/party-timeline-row.ts` — `PartyTimelineRow = { movementOnUtc: IsoInstant; description: string; deltaMinorUnits: Money; runningBalanceMinorUnits: Money; currencyCode: CurrencyCode }`.
- [x] `features/reports/reports-service.ts` — add `partyTimeline(partyId): Observable<PartyTimelineRow[]>` → `GET /v1/reports/parties/{id}/timeline` (unwrap `{ rows }`; reporting equivalent of `getTimeline`).
- [x] Extend `parties-service.spec.ts` and `reports-service.spec.ts` for the new methods.

### Tasks — pages

- [x] `features/parties/pages/parties-page/` — `PartiesPage`: list every party with net balance from `ReportsService.debtSummary` (positive = they owe you); create-party form.
- [x] `features/parties/pages/party-detail-page/` — `PartyDetailPage`: party id from route param; current balance (`PartiesService.getBalance`) + movement timeline (chronological, running balance) from `ReportsService.partyTimeline` (currency-aware — D16); actions to register a shared expense and to register a settlement; reversal state rendered verbatim as its own negative timeline row via presentational `TimelineTable` (`docs/PRD.md` §3.5 audit visibility, §3.7). Cross-debts net server-side — the net is shown as-is.
- [x] `features/parties/pages/shared-expense-page/` — `SharedExpensePage`: reactive form — description, total (major → minor at submit), `expenseAccountId` (hand-entered UUID — D17), `fundingAccountId`, `incurredOnUtc`, participant `FormArray` (`partyId` + integer `weight`); calls `registerSharedExpense`. `?party=` query param pre-fills the first row.
- [x] `features/parties/parties.routes.ts` + lazy-wire into `app.routes.ts`. Final order: `''`, `'shared-expense'` (static), `':id'` (param) last.

### Definition of done

- [x] Parties list, party detail, and shared-expense views reachable via lazy routes.
- [x] `PartiesService` (full) and `ReportsService.partyTimeline` have `HttpTestingController` coverage.
- [x] Net balances and timelines render the API's values as-is (no client-side accounting).
- [x] `pnpm ng build` + `pnpm ng test --watch=false --browsers=ChromeHeadless` green. Manual smoke against a local API **deferred** (D1 — API not running on `https://localhost:7095`).

### Completion notes

**Shipped:** the three Fase-3 views (`PartiesPage`, `PartyDetailPage`, `SharedExpensePage`) plus
the presentational `TimelineTable`, the eight `features/parties/types/*` DTO types,
`features/reports/types/party-timeline-row.ts`, the full `PartiesService`
(`create` / `registerSharedExpense` / `settle` / `getTimeline`), `ReportsService.partyTimeline`,
and `features/parties/validation-helpers.ts` (`positiveAmount`, `atMostTwoDecimals`,
`positiveInteger`). Routes: `parties` lazy-wired in `app.routes.ts`; `parties.routes.ts` =
`''` → list, `'shared-expense'` → registration, `':id'` → detail.

**Build/test:** `pnpm ng build` clean — initial JS 263.61 kB (Phase 2 baseline 255.59 kB; +8 kB
one-time, esbuild promoted the router-link runtime to a shared initial chunk once a second lazy
chunk began using `RouterLink`; ~47 % under the 500 kB warn budget). `parties-routes` lazy chunk
26.42 kB. Tests **139/139** (Phase 2 baseline 112; +27: +9 service specs, +18 page/table specs).

**Intentional deviations (continuing D-numbering from Phase 2's D13):**

- **D14** — **`/v1/parties/{id}/timeline` envelope.** `docs/DESIGN.md` §9 and this file's task list
  described the endpoint as returning the timeline object as-is, but the API returns a
  `{ rows: [...] }` envelope (`CurrentAccountTimelineDto`). `PartiesService.getTimeline` unwraps
  `{ rows }` via `map`, like every other list endpoint. `docs/DESIGN.md` §4/§9 carry a
  reconciliation note.
- **D15** — **New parties are invisible until they have a movement.** `GET
  /v1/reports/parties/debt-summary` only returns parties that already carry a current-account
  movement, so a just-created party does not appear in `PartiesPage`'s list. The page shows a
  post-create confirmation panel with the new party id and a note ("new parties appear in the
  list once they take part in a shared expense") instead of a client-side merge — the server
  stays the single source of truth.
- **D16** — **Timeline source for the detail view.** `PartyDetailPage` renders
  `ReportsService.partyTimeline` (carries `currencyCode`, consistent with the debt-summary list).
  `PartiesService.getTimeline` is implemented and `HttpTestingController`-covered for service
  parity but is not consumed by any view (precedent: D11 `LedgerService.postTransaction`).
- **D17** — **`expenseAccountId` is hand-entered.** Expense accounts are not registered
  instruments and there is no list endpoint, so `SharedExpensePage` takes the expense-account
  UUID as free text with a helper note (same shape as the gap-2 / gap-3 id-index pages).
  `fundingAccountId` offers every registered instrument (as in Subscriptions D13).
- **D18** — **No `pollUntil` in Phase 3.** The shared-expense and settlement ledger postings are
  synchronous API-side, so `PartyDetailPage` plainly re-fetches balance + timeline after each
  mutation (Subscriptions D6 pattern) rather than running a reconciliation poll.

**Manual smoke — deferred** (D1): create a party → register a shared expense with it as a
participant → confirm it appears in the debt-summary list with a net balance → open detail,
confirm the timeline row and running balance → register a settlement → confirm the balance drops
and a negative timeline row appears → force a 422 (`weight` 0) and a 409 (settle more than owed)
and confirm the message keys off `code`.

---

## Phase 4 — Deviations & deferred items

**Not traced to any PRD view.** These are the gaps the documents already flag (`docs/PRD.md` §7, `docs/DESIGN.md` §11) plus two scaffold-state gaps. Work each only when its trigger lands — do not schedule them into Phases 0–3.

### Tasks

- [x] **4.1 Tailwind + UI-task groundwork** — install Tailwind and wire the build; tokens, theme, and components belong to the separate UI task. Trigger: UI task kickoff. (`docs/DESIGN.md` §10; `docs/PRD.md` §6) — **build wiring done** (see Completion notes; **D19**).
- [x] **4.2 API gap 1 — `GET /v1/instruments`** — **done (Phase 12, D21).** `InstrumentsService.list()` unwraps `{ rows }`; new `Instrument` type (`features/instruments/types/instrument.ts`); `InstrumentRegistryService` + `core/types/registered-instrument.ts` + `core/registry/` deleted. 6 consumers load the list into a local `WritableSignal<Instrument[]>` in `ngOnInit` and keep their existing `computed()` `<select>` filters; `InstrumentsPage` re-fetches after create. (`docs/DESIGN.md` §8/§9/§11.1; `docs/PRD.md` §7.1)
- [x] **4.3 API gap 2 — `GET /v1/financing/cards/{id}/statements`** — **done (Phase 12, D22).** `FinancingService.listStatements(cardId)` unwraps `{ rows }`; new `MonthlyStatementSummary` type; `StatementsPage` (credit-card picker → `StatementsTable` rows → row opens `statements/:id`) replaces the id-paste `StatementIndex`, which was deleted. (`docs/DESIGN.md` §2/§4/§9/§11.2; `docs/PRD.md` §7.2)
- [x] **4.4 API gap 3 — transactions feed (`GET /v1/ledger/transactions`)** — **done (Phase 12, D23).** `LedgerService.listTransactions(filter?)` unwraps `{ rows }`; new `TransactionRow` type; `TransactionsPage` (account + date-range filter → `TransactionsTable` rows → per-row **Reverse**, disabled on a reversal/already-reversed row) replaces the id-paste `ReverseIndex`, which was deleted. `ReverseMovementPage` (`transactions/:id/reverse`) unchanged. (`docs/DESIGN.md` §2/§4/§9/§11.3; `docs/PRD.md` §7.3)
- [ ] **4.5 API gap 4 — app-level auth surface** — when a backing endpoint exists, add login as a new cross-cutting feature (route guard, session). Not part of any view above. (`docs/DESIGN.md` §11.4; `docs/PRD.md` §2, §7.4)
- [x] **4.6 Client CI** — add `ng build` + `ng test` (and lint, once 4.7 lands) steps for `app/client/` to `/.github/workflows/ci.yml` (currently API-only). Trigger: Fase 1 merged. _(Scaffold-state gap — no PRD/DESIGN statement.)_ — **done** (`client-build-test` job; lint + build + test; API job untouched).
- [x] **4.7 ESLint config** — add `@angular-eslint` (none present today) before enforcing lint in CI. _(Scaffold-state gap — no PRD/DESIGN statement.)_ — **done** (`angular-eslint@20` flat config + `lint` target; `pnpm ng lint` clean).

### Definition of done

- [ ] Each item closed only against its real trigger, with the corresponding `docs/PRD.md` / `docs/DESIGN.md` gap reference noted in its completion entry.

### Completion notes

**Scope of the tooling pass.** Three tooling items closed against triggers that have landed — **4.7**
(ESLint), **4.1** (Tailwind — build wiring only), **4.6** (client CI).

**4.2 + 4.3 + 4.4 closed (Phase 12).** `GET /v1/instruments` (step 1),
`GET /v1/financing/cards/{id}/statements` (step 3) and `GET /v1/ledger/transactions` (step 5) shipped
on the API; the matching client steps retired the `localStorage` registry, the id-paste
`StatementIndex`, and the id-paste `ReverseIndex`, and **D20 is closed** (step 7 threaded the
reversible ledger transaction id onto the statement + timeline rows; step 8 added the per-row
Reverse buttons, D24) — see the completion notes below. **4.5 stays
open**: the auth surface has no design. **The phase DoD box stays unticked until 4.5 closes.**

**4.2 — `GET /v1/instruments` (Phase 12, D21).** `InstrumentsService` gains
`list(): Observable<Instrument[]>` → `GET /v1/instruments`, `{ rows }` unwrapped via `map`. New type
`features/instruments/types/instrument.ts` — `Instrument = { id; type: InstrumentType; name;
cutoffDate: number | null }` (field-matched to `/openapi/v1.json`). **Deleted:**
`core/registry/instrument-registry-service.ts` (+ spec), `core/types/registered-instrument.ts`, the
empty `core/registry/` dir. **6 consumers** dropped `inject(InstrumentRegistryService)` for
`inject(InstrumentsService)` + a `private instruments: WritableSignal<Instrument[]>` loaded in
`ngOnInit` via `list().pipe(takeUntil(this.destroy$))`; each existing `computed()` `<select>` filter
was repointed at `this.instruments()` unchanged (`instruments-page` all + re-fetch after create;
`load-expense-page` `type === 'credit'`; `statement-page` `type === 'debit'`; `subscriptions-page`
all, D13; `party-detail-page` `type === 'debit'`; `shared-expense-page` all, D17). Specs: each
consumer spec swapped its registry stub / `registry.add(...)` for
`{ provide: InstrumentsService, useValue: { list: () => of([...]) } }`;
`instruments-service.spec.ts` gained a `list()` case; `instrument-registry-service.spec.ts` deleted.
`pnpm ng test` → **135 pass**; `pnpm ng build` 261 kB initial (< 500 kB); `pnpm ng lint` clean.
Docs: `docs/DESIGN.md` §2/§4/§8/§9/§11, `docs/PRD.md` §7.1, `.claude/CLAUDE.md`. Not committed —
the user commits.

**4.3 — `GET /v1/financing/cards/{id}/statements` (Phase 12, D22).** `FinancingService` gains
`listStatements(cardId): Observable<MonthlyStatementSummary[]>` → `GET
/v1/financing/cards/{cardId}/statements`, `{ rows }` unwrapped via `map` (new module-scoped
`RowsEnvelope<T>` helper — the other list-returning methods on this service still hand their object
back as-is). New type `features/financing/types/monthly-statement-summary.ts` — `MonthlyStatement`
minus `installments` (`statementId`, `cardId`, `cardName`, `cycleYear`, `cycleMonth`,
`amountDueMinorUnits`, `isPaid`, `paidOnUtc`). New page `features/financing/pages/statements-page/`:
`StatementsPage` container (credit-card `<select>` from `instrumentsService.list()` filtered
`type === 'credit'`; a one-control reactive form whose `cardId` `valueChanges` drives
`listStatements`; `LoadStatus = 'idle' | 'loading' | 'ready' | 'error'` — `'idle'` until a card is
picked, back to `'idle'` if cleared) + `StatementsTable` presentational (`input.required` +
`openStatement = output<string>()`, zero-padded `YYYY-MM` cycle, `formatArs` amount, `Paid`/`Unpaid`
badge, per-row **Open** button; the container navigates `['financing', 'statements', statementId]`).
`financing.routes.ts`: `{ path: 'statements', component: StatementsPage }` (was `StatementIndex`);
`statements/:id` and `load-expense` unchanged. **Deleted:** `features/financing/pages/statement-index/`
(`.ts/.html/.css/.spec.ts`). Specs: `financing-service.spec.ts` gains a `listStatements` `{ rows }`
case; new `statements-table.spec.ts` (4 — padded cycle + amount, single `Paid` badge, `openStatement`
emits the id, empty note) and `statements-page.spec.ts` (5 — credit-only picker + `'idle'` start,
pick fetches + renders + `'ready'`, cleared picker no-fetch, `openStatement` navigates, load error →
`'error'`). `pnpm ng test` → **142 pass**; `pnpm ng build` 261.91 kB initial (< 500 kB); `pnpm ng
lint` clean. Docs: `docs/DESIGN.md` §2/§4/§9/§11.2, `docs/PRD.md` §7.2, `.claude/CLAUDE.md`. Not
committed — the user commits.

**4.4 — `GET /v1/ledger/transactions` (Phase 12, D23).** `LedgerService` gains
`listTransactions(filter: { accountId?; from?: IsoDate; to?: IsoDate } = {}): Observable<TransactionRow[]>`
→ `GET /v1/ledger/transactions`, `{ rows }` unwrapped via `map`; the filter goes out as `HttpParams`,
each key omitted when falsy (the `ReportsService.monthlyExpenses` precedent). New type
`features/ledger/types/transaction-row.ts` — field-matched to the Step-5 DTO (`transactionId`,
`postedOnUtc: IsoInstant`, `description`, `amountMinorUnits: Money`, `isReversal`, `isReversed`,
`installmentReferenceId: string | null`, `splitReferenceId: string | null`); **no `direction` field**
(the old TASK.md sketch had one — the API row carries a signed debit-side total, not a side). New page
`features/ledger/pages/transactions-page/`: `TransactionsPage` container (a three-control reactive
filter form — `accountId` `<select>` from `instrumentsService.list()` filtered `type !== 'credit'`
(credit-card ids are not Ledger account ids), `from` / `to` native date inputs; `(ngSubmit)` →
`applyFilter()` re-fetches; `LoadStatus = 'loading' | 'ready' | 'error'` — no `'idle'`, the feed
loads in `ngOnInit`) + `TransactionsTable` presentational (`input.required` + `reverseTransaction =
output<string>()`; `postedOnUtc.slice(0, 10)` date, `formatArs` amount; a **Reverse** button per row,
`[disabled]` + a short note (`Reversal entry` / `Already reversed`) when `isReversal || isReversed`;
the container navigates `['ledger', 'transactions', transactionId, 'reverse']`). `ledger.routes.ts`:
`{ path: 'transactions', component: TransactionsPage }` (was `ReverseIndex`); `transactions/:id/reverse`
→ `ReverseMovementPage` unchanged. **Deleted:** `features/ledger/pages/reverse-index/`
(`.ts/.html/.css/.spec.ts`). Specs: `ledger-service.spec.ts` gains two `listTransactions` cases (no
filter → no query params + `{ rows }` unwrap; full filter → `accountId`/`from`/`to` params asserted);
new `transactions-table.spec.ts` (4 — date/label/amount render, locked action on a reversal row +
note, `reverseTransaction` emits the id on an active click, empty note) and `transactions-page.spec.ts`
(4 — loads on init + non-credit-only account picker, Apply re-fetches with the filter object,
`openReverse` navigates the 4-segment path, load error → `'error'`). `pnpm ng test` → **149 pass**
(142 + 10 new − 3 deleted); `pnpm ng build` 261.91 kB initial (< 500 kB); `pnpm ng lint` clean.
Docs: `docs/DESIGN.md` §2/§4/§9/§11.3, `docs/PRD.md` §7.3, `.claude/CLAUDE.md`. **D20 stays open** —
it is closed in steps 7–8 (reversible-tx-id into the timeline / statement DTOs, then the per-row
buttons). Not committed — the user commits.

**4.7 — ESLint (scaffold-state gap).** `angular-eslint@20` flat config (`eslint.config.js`,
CommonJS — the client has no `"type": "module"`): `@eslint/js` recommended + `typescript-eslint@8`
recommended + `angular-eslint` ts-recommended for `**/*.ts`, template-recommended for `**/*.html`,
`processInlineTemplates` for inline templates. A `lint` architect target
(`@angular-eslint/builder:lint`, patterns `src/**/*.ts` + `src/**/*.html`) and a `pnpm lint`
script. `pnpm ng lint` passes clean on the current tree. One deliberate rule override:
`@angular-eslint/component-class-suffix` is **off** — `.claude/rules/typescript-frontend-style.md`
§3 drops the `Component` suffix on every component class, which the default rule would flag on all
14. Stylistic, template-accessibility, and type-checked rule sets are **excluded on purpose** —
turning them on is codebase-wide work for the UI task, not part of wiring the tool up. Pinned to
`eslint@9` / `angular-eslint@20`: the `@10` / `@22` majors on npm `latest` are not compatible
with this Angular 20.3 line.

**4.1 — Tailwind (build wiring only; `docs/DESIGN.md` §10, `docs/PRD.md` §6).** `tailwindcss@4` +
`@tailwindcss/postcss@4` + `postcss@8`; a project-root `.postcssrc.json`
(`{ "plugins": { "@tailwindcss/postcss": {} } }`, auto-discovered by `@angular/build`);
`@import 'tailwindcss';` prepended to `src/styles.css`. **No `tailwind.config.js`** — see **D19**.
Build stays green with no budget warning: emitted `styles*.css` is ~6.6 kB raw / ~1.8 kB transfer
(Tailwind preflight + base layer only — zero utilities in use), initial-JS total 263.61 kB →
270.20 kB raw. DESIGN §10's "tokens, theme, and components belong to that task" is unchanged; this
pass only makes the utility layer compile. Cosmetic: v4 preflight resets browser defaults, so
placeholder pages look flatter until the UI task styles them — no spec asserts computed styles, so
the 139 tests are unaffected.

**4.6 — Client CI (scaffold-state gap).** A second job `client-build-test` in
`/.github/workflows/ci.yml` — the API `build-test` job is **untouched**. `ubuntu-latest`, working
dir `app/client`: `actions/checkout@v4` → `pnpm/action-setup@v4` → `actions/setup-node@v4`
(Node 22, `cache: pnpm`) → `pnpm install --frozen-lockfile` → `pnpm ng lint` →
`pnpm ng build --configuration production` → `pnpm ng test --watch=false --browsers=ChromeHeadless`
with `CHROME_BIN=/usr/bin/google-chrome` (preinstalled on the runner — non-root with user
namespaces, so no `--no-sandbox` and no `karma.conf.js`). `"packageManager": "pnpm@11.8.0"` added
to `package.json` so `action-setup` and `cache: pnpm` resolve the version. Both jobs run in
parallel under the existing `ci-${{ github.ref }}` cancel-in-progress group. No `paths:` filter —
matches the API job; split into a separate workflow if Actions-minutes cost ever matters.

**Intentional deviation (continuing D-numbering from Phase 3's D18):**

- **D19** — **Tailwind v4 CSS-first — no `tailwind.config.js`.** `docs/DESIGN.md` §10 calls this
  "config groundwork", wording that predates Tailwind v4. v4 moves configuration into CSS
  (`@import 'tailwindcss'` now; `@theme` tokens later, in `styles.css`) and auto-detects template
  sources, so there is no JS config file to add — the wiring is `.postcssrc.json` plus the one
  `@import` line. The UI task will add `@theme` tokens in CSS rather than a `theme.extend` object;
  choosing v4 now avoids a v3→v4 migration mid-UI-work. **DESIGN §10 still reads "config
  groundwork" — update it to the CSS-first wiring when the UI task picks this up.**

- **D20** — **Reverse triggers on the installments table and the party timeline table —
  blocked on the API, not wired.** The `task/client-design` nav-wiring pass planned a per-row
  "Reverse" button on `InstallmentsTable` (statement page) and on `TimelineTable` (party
  detail), each navigating to `/ledger/transactions/{txId}/reverse`. Verify-first check against
  both the client types and the API DTOs: **neither row carries a ledger transaction id.**
  `MonthlyStatementInstallmentRowDto` exposes `PlanId` + `InstallmentId` (Financing aggregate
  ids); `CurrentAccountTimelineRowDto` exposes no id at all. `ReverseTransactionCommand` takes
  `OriginalTransactionId` — a Ledger transaction id — and there is no client-reachable way to
  resolve one from an `installmentId` or a timeline row. The buttons were deliberately **not**
  added (no null/guessed id, no repurposing `installmentId`). Unblocked by **gap 3 / task 4.4**
  (`GET /v1/ledger/transactions`), or by the statement / timeline DTOs growing a reversible
  transaction id — whichever ships first. Until then the only reverse entry point stays the
  id-lookup page (`/ledger/transactions`, reachable from the nav shell).
  **Closed (Phase 12, steps 7–8, D24).** API step 7 threaded a reversible ledger transaction id
  onto both surfaces: `MonthlyStatementInstallmentRowDto.reversalTransactionId` (the accrual tx,
  or `null`) and `CurrentAccountTimelineRowDto` / `PartyTimelineRowDto` `.transactionId`. Client
  step 8 gave `InstallmentsTable` and `TimelineTable` a per-row `reverseClick = output<string>()`
  and a Reverse button — disabled for an installment with no live accrual (`reversalTransactionId`
  null or `isReversed`) and for a timeline row that is itself a `'Reversal'`. `StatementPage` and
  `PartyDetailPage` inject `Router` and navigate `['ledger', 'transactions', $event, 'reverse']`.

- **D21** — **`InstrumentRegistryService` retired for `GET /v1/instruments`.** The Phase-0 D3
  `localStorage` instrument registry (and `core/types/registered-instrument.ts`, `core/registry/`)
  was deleted once the API endpoint shipped (API Phase 12). `InstrumentsService.list()` replaces it;
  the client-owned `Instrument` type (`features/instruments/types/instrument.ts`) mirrors the DTO
  (`cutoffDate: number | null`, always present — was `cutoffDate?: number`). Each of the 6 pickers
  loads the list into its own `WritableSignal<Instrument[]>` in `ngOnInit` rather than sharing one
  root signal — the registry's single source of truth is gone, but the lists are small and each page
  wants a fresh read on entry (`InstrumentsPage` also re-fetches after a successful create instead of
  the old optimistic `registry.add`).

- **D22** — **`StatementIndex` id-paste page replaced by a real `StatementsPage`.** The Phase-1
  gap-2 stopgap (`features/financing/pages/statement-index/` — paste a `statementId`, navigate to
  `statements/:id`) was deleted once `GET /v1/financing/cards/{id}/statements` shipped (API Phase 12,
  step 3). The new `StatementsPage` picks a credit card and lists its statements; a row's **Open**
  button navigates to the same `statements/:id` detail route, so `StatementPage` is unchanged. The
  card `<select>` reuses the D21 `instrumentsService.list()` pattern (`type === 'credit'` filter).
  `LoadStatus` carries an `'idle'` state (no card picked yet) that the other pages' three-state
  `LoadStatus` does not — the fetch is user-triggered here, not `ngOnInit`-triggered. New
  `MonthlyStatementSummary` type is `MonthlyStatement` without `installments` (the list endpoint
  omits them by design); the itemized `MonthlyStatement` is still what `statement-page` loads.

- **D23** — **`ReverseIndex` id-paste page replaced by a real `TransactionsPage`.** The Phase-1
  gap-3 stopgap (`features/ledger/pages/reverse-index/` — paste a `transactionId`, navigate to
  `transactions/:id/reverse`) was deleted once `GET /v1/ledger/transactions` shipped (API Phase 12,
  step 5). `TransactionsPage` lists the ledger feed with an account + date-range filter; each row's
  **Reverse** button navigates to the same `transactions/:id/reverse` confirm route, so
  `ReverseMovementPage` is unchanged. The account `<select>` filters `instrumentsService.list()` to
  `type !== 'credit'` (only debit/cash instruments map to a Ledger `accountId`). `LoadStatus` has no
  `'idle'` state — unlike `StatementsPage` (D22), the feed loads in `ngOnInit`, not on a pick. The
  `TransactionRow` type drops the `direction` field the old sketch carried (the API row is a signed
  debit-side total). D20 is **not** closed here — the per-row Reverse buttons on `InstallmentsTable`
  / `TimelineTable` still need a reversible-transaction id threaded through those DTOs (steps 7–8).

- **D24** — **D20 closed: per-row Reverse on `InstallmentsTable` + `TimelineTable`.** Both tables
  stay presentational — a `reverseClick = output<string>()` emits the ledger transaction id; the
  container (`StatementPage` / `PartyDetailPage`, both now inject `Router`) navigates to
  `transactions/:id/reverse` (the same confirm route the feed's rows use). Types grew a field to
  match API step 7: `monthly-statement-installment.ts` `+ reversalTransactionId: string | null`,
  `party-timeline-row.ts` + `current-account-timeline-row.ts` `+ transactionId: string`. The
  installments button is disabled when `reversalTransactionId` is null or `isReversed`; the timeline
  button is disabled on a row whose `description === 'Reversal'`. `pnpm ng test` → 155 pass
  (149 + 6); build 261.91 kB initial (unchanged — the deltas are inside the lazy feature chunks);
  lint clean. Docs: `docs/DESIGN.md` §9/§11.3, `docs/PRD.md` §3.4/§3.5.

---

#### Gap checklists — execute-ready (one green-lit step each, when the endpoint ships)

**4.2 — `GET /v1/instruments` (`docs/DESIGN.md` §11.1, `docs/PRD.md` §7.1)**
- Service: `InstrumentsService.list(): Observable<Instrument[]>` → `GET /v1/instruments`, unwrap `{ rows }`.
- New type: `features/instruments/types/instrument.ts` — `Instrument = { id; type: InstrumentType; name; cutoffDate: number | null }`, field-matched to `/openapi/v1.json` when it ships.
- Retires: `core/registry/instrument-registry-service.ts` (+ spec) and the `localStorage` stopgap; reconcile `core/types/registered-instrument.ts`, DESIGN §8, `.claude/CLAUDE.md` "Current state".
- 6 consumer sites switch from `registry.instruments()` to an `InstrumentsService.list()` call + local `WritableSignal` loaded in `ngOnInit`: `instruments-page` (re-fetch after create instead of `registry.add`), `load-expense-page` (`type === 'credit'`), `statement-page` (`type === 'debit'`), `subscriptions-page` (all types, D13), `party-detail-page` (settlement account), `shared-expense-page` (funding account, D17).
- Routes: none.
- Specs: `instruments-service.spec.ts` gains a `list()` case (URL, GET, `{ rows }` unwrap, `AppError`); each consumer spec swaps the registry stub for an `InstrumentsService` stub returning `of([...])`; delete `instrument-registry-service.spec.ts`.
- Closes the Phase-0 D3 lineage; assign the next free D-number.

**4.3 — `GET /v1/financing/cards/{id}/statements` (`docs/DESIGN.md` §11.2, `docs/PRD.md` §7.2)**
- Service: `FinancingService.listStatements(cardId): Observable<MonthlyStatementSummary[]>` → `GET /v1/financing/cards/{id}/statements`, unwrap `{ rows }`.
- New type: `features/financing/types/monthly-statement-summary.ts` — the `MonthlyStatement` row shape minus `installments`.
- Retires placeholder `features/financing/pages/statement-index/` → new `statements-page/` (`StatementsPage`: pick a credit card, list its statements, row → `statements/:id`).
- Routes (`financing.routes.ts`): `{ path: 'statements', component: StatementIndex }` → `StatementsPage`; keep `statements/:id` and `load-expense`. Delete `statement-index.{ts,html,css,spec.ts}`.
- Specs: `financing-service.spec.ts` gains `listStatements`; new `statements-page.spec.ts` (rows, row-nav, loading/empty/error); delete `statement-index.spec.ts`.
- Next free D-number.

**4.4 — transactions feed `GET /v1/ledger/transactions` (`docs/DESIGN.md` §11.3, `docs/PRD.md` §7.3)**
- Service: `LedgerService.listTransactions(params?: { accountId?; from?: IsoDate; to?: IsoDate }): Observable<TransactionRow[]>` → `GET /v1/ledger/transactions`, unwrap `{ rows }`, omit absent params.
- New type: `features/ledger/types/transaction-row.ts` — `{ transactionId; postedOnUtc: IsoInstant; description: string | null; amountMinorUnits: Money; direction: Direction; isReversal: boolean; isReversed: boolean }`.
- Retires placeholder `features/ledger/pages/reverse-index/` → new `transactions-page/` (`TransactionsPage`: history list + per-row "Reverse" → `transactions/:id/reverse`). Keep `ReverseMovementPage` as the confirm step.
- Routes (`ledger.routes.ts`): `{ path: 'transactions', component: ReverseIndex }` → `TransactionsPage`; keep `transactions/:id/reverse`. Delete `reverse-index.{ts,html,css,spec.ts}`.
- Specs: `ledger-service.spec.ts` gains `listTransactions`; new `transactions-page.spec.ts` (rows, reverse-nav trimming id, already-reversed badge disables the action, loading/empty/error); delete `reverse-index.spec.ts`.
- Also closes **D20**: with a `transactionId` now reachable, add the deferred per-row "Reverse" buttons to `InstallmentsTable` and `TimelineTable` (presentational — `output<string>()`; the container navigates to `/ledger/transactions/{txId}/reverse`) — either from this feed or from a reversible-transaction-id field added to the statement / timeline DTOs.
- Next free D-number.

**Common to 4.2 / 4.3 / 4.4 when worked:** build stays < 500 kB initial; `pnpm ng test` green;
`pnpm ng lint` clean; update `docs/DESIGN.md` §4/§9 + §8/§11, `docs/PRD.md` §7,
`.claude/CLAUDE.md`; one conventional commit; record the closing D-number (D20 onward — D19 is
taken by the Tailwind wiring above). **4.5 (auth) stays deferred** — `docs/DESIGN.md` §11.4,
`docs/PRD.md` §7.4; no backing endpoint, nothing to design to.

---

## Phase 5 — Creditors (Slice 1: CRUD)

**Goal:** Ship a self-contained CRUD vertical for a new `Creditor` reference entity (who the user pays), each with optional free-text destination accounts (label + CBU/CVU/alias identifier). Prerequisite for a later Load-expense enhancement (Slice 2) that lets the user record who was paid and to which account — not part of any of the original three Fases (`docs/PRD.md` never named this capability).

**Traces to:** `docs/creditor-expense-fields/slice-1-creditors-crud.md` (a standalone planning doc, not `docs/PRD.md`/`docs/DESIGN.md` — those are unmodified for Slice 1).

**Depends on:** Phase 1 conventions (mirrors `features/instruments/` for the service/types/routes shape, and `load-expense-page`'s split `FormArray` for the dynamic account rows).

### Tasks
- [x] `features/creditors/types/creditor.ts`, `create-creditor.ts`, `creditor-created.ts` — field-matched to the API's `CreditorsDTO.cs` (`identifier: string | null`, after the nullable-identifier follow-up).
- [x] `features/creditors/creditors-service.ts` — `list()` (unwrap `{ rows }`), `create()`.
- [x] `features/creditors/creditors.routes.ts` + lazy-wired into `app.routes.ts`; `{ label: 'Creditors', path: '/creditors' }` added to `app.ts` `navItems`.
- [x] `features/creditors/pages/creditors-page/` — `CreditorsPage`: reactive form (`name` required; `accounts` `FormArray` starting with one row, `label` required, `identifier` optional after the follow-up fix); add/remove row; submit → create → reset form → reload list; list loading/empty/error states. Styled to `docs/SYSTEM.md` from the start (no unstyled-then-restyled pass, unlike every Phase 0–3 view).
- [x] `creditors-page.spec.ts` — form validation (blank label blocks submit, blank identifier does not), trimming, submitted body shape, list re-fetch after create.
- [x] **Follow-up: identifier made genuinely optional**, matching the API's `MakeCreditorAccountIdentifierNullable` migration — dropped `Validators.required` on the identifier control, widened the type to `string | null`, submit sends `null` for a blank/whitespace value (mirroring the domain's own normalization), list rendering hides the "— identifier" suffix when absent.

### Definition of done
- [x] `/creditors` reachable via a lazy route from the nav.
- [x] `CreditorsService` has coverage via the page spec (mocked service, not a dedicated service spec — see Completion notes).
- [x] `pnpm ng build` + `pnpm ng lint` clean; `pnpm ng test --watch=false --browsers=ChromeHeadless` → **164 passed** (155 baseline + 9 new).

### Completion notes (2026-09-04)

Built one green-lit step at a time per `docs/creditor-expense-fields/slice-1-creditors-crud.md`. **Deviation from the usual service-spec convention:** unlike every other feature, `creditors-service.ts` did not get its own dedicated `HttpTestingController` spec — coverage lives entirely in `creditors-page.spec.ts` via a jasmine spy on `CreditorsService`. Worth adding a dedicated service spec later for parity with the rest of the codebase. Slice 2 (wiring a creditor + destination account into `load-expense-page`) is **not started**. Not committed by this session for most of the work — the user commits their own.

---

## Phase 6 — Creditors (Slice 2: Load-Expense integration)

**Goal:** Wire the Creditor entity into the Load-Expense form as pure optional metadata — a toggle to mark a payment with a specific creditor and destination account.

**Traces to:** `docs/creditor-expense-fields/slice-2-load-expense-integration.md` (continued planning doc).

**Depends on:** Phase 5 (Creditors CRUD must exist first) + Phase 1 (Load-Expense page is the primary consumer).

### Tasks

- [x] `features/financing/types/create-payment-plan.ts` — add `creditorId?: string; creditorAccountId?: string;` to the type.
- [x] `features/financing/pages/load-expense-page/load-expense-page.ts` — inject `CreditorsService`; add `creditors: WritableSignal<Creditor[]>` loaded on init; extend `LoadExpenseForm` with `differentCreditor: FormControl<boolean>` (default false), `creditorId: FormControl<string>`, `creditorAccountId: FormControl<string>`; add `creditorAccounts: WritableSignal<CreditorAccount[]>` populated by subscription to `creditorId.valueChanges`; add validator-toggle subscription for `differentCreditor.valueChanges` (sets/clears `Validators.required` on the two fields, resets them when toggled off); modify `onSubmit()` to spread `{ creditorId, creditorAccountId }` into request body only when `differentCreditor` is true.
- [x] `features/financing/pages/load-expense-page/load-expense-page.html` — add "Creditor" `<section>` after Split section with checkbox toggle; conditionally-revealed (on toggle true) native `<select>`s for creditor and account-to-pay, styled to `docs/SYSTEM.md` tokens.
- [x] `features/financing/pages/load-expense-page/load-expense-page.spec.ts` — add 4 new specs: toggle-on makes both fields required; selecting a creditor populates accounts and auto-selects first; submit body includes creditor fields when toggle is on; submit body omits them when toggle is off. Original 9 specs remain green (regression bar).

### Definition of done

- [x] Load-Expense form extends to wire optional Creditor + Account-to-Pay metadata, gated by toggle.
- [x] All 13 Load-Expense specs pass (original 9 + 4 new), zero regressions.
- [x] `pnpm ng build` + `pnpm ng lint` clean; `pnpm ng test --watch=false --browsers=ChromeHeadless` → **168 passed** (164 baseline + 4 new).

### Completion notes (2026-09-04)

Slice 2 built and verified live per `docs/creditor-expense-fields/slice-2-load-expense-integration.md`. All changes localized to the Load-Expense page (form shape, template markup, subscription logic, request body shape); `CreatePaymentPlan` type extended with two optional fields; `financing-service.ts` requires no change (same endpoint, same shape pass-through). Creditor selection auto-populates accounts and auto-selects the first account id (UX convenience, no server-side dependency). No deviations from the spec, no new D-numbers needed (no drift from PRD/DESIGN docs). Zero scope creep — creditor/account metadata is optional, read-only-on-views, no ledger posting or balance tracking yet. Not committed by this session — the user commits their own.

---

## Phase 7 — Expense description field (Slice 1)

**Goal:** Add a required description to the Load-Expense form and echo it back on the confirmation panel, closing the loop the API's Slice 1 opened (the create-payment-plan response used to be proven only by a bare GUID).

**Traces to:** `docs/expense-description/slice-1-description-field.md`.

**Depends on:** Phase 1 (`load-expense-page` exists) + the API's own Phase 15.

### Tasks
- [x] `features/financing/types/create-payment-plan.ts` — add `description: string;`.
- [x] `features/financing/validation-helpers.ts` — add `noBlank` (rejects whitespace-only) and `noNewline` (rejects `\n`/`\r`) `ValidatorFn`s alongside the existing `positiveAmount`/`atMostTwoDecimals`/`positiveInteger`/`isoDate`.
- [x] `features/financing/pages/load-expense-page/load-expense-page.ts` — add `description: FormControl<string>` to `LoadExpenseForm` and `initLoadExpenseForm()` with `[Validators.required, Validators.maxLength(120), noBlank, noNewline]`; add `confirmedDescription: WritableSignal<string | null>`, set alongside `confirmedPlanId` on a confirmed submit; submit body includes `description: this.form.controls.description.value.trim()`.
- [x] `load-expense-page.html` — add a description input near the top of the form; confirmation panel headlines the description (id kept small/secondary).
- [x] `load-expense-page.spec.ts` — extend `fillValidForm()`; new facts: form invalid until `description` filled, rejects whitespace-only, rejects `>120` chars, submits the trimmed value, confirmation panel renders the description.

### Definition of done
- [x] The Load-Expense form requires a description and won't submit without one.
- [x] After saving, the confirmation panel shows the description, not just a plan id.
- [x] `pnpm ng build` + `pnpm ng lint` clean; `pnpm ng test --watch=false --browsers=ChromeHeadless` green.

### Completion notes

Slice 1 was already complete when this documentation pass ran — built and verified in a prior session on `feat/expense-description`, alongside the API's Phase 15. Not committed by this session — the user commits their own.

---

## Phase 8 — Card-debt drill-down (Slice 2)

**Goal:** Make the Dashboard's "Card Debt by Cycle" rows expandable into the individual outstanding purchases behind each card's total, each showing its Phase-7 description.

**Traces to:** `docs/expense-description/slice-2-card-debt-drilldown.md`.

**Depends on:** Phase 7 (description exists and is populated) + Phase 1 (`DashboardPage` exists) + the API's Phase 16 (`GET /v1/financing/cards/{id}/purchases`).

### Tasks
- [x] `features/financing/types/card-purchase-row.ts` (new) — mirrors the API contract: `planId`, `description`, `totalMinorUnits: Money`, `installmentCount`, `outstandingCount`, `purchaseDate: IsoDate`, `isCreditorPayment`.
- [x] `features/financing/financing-service.ts` — add `cardPurchases(cardId: string): Observable<CardPurchaseRow[]>` → `GET financing/cards/{cardId}/purchases`, `{ rows }` unwrapped via `map` (mirrors `listStatements`).
- [x] `features/reports/pages/dashboard-page/dashboard-page.ts` — `CardCycle` type gains `cardId: string | null` (threaded through from `CardDueRow.cardId` via a label→id lookup built inside the `cycleByCard` computed, since the existing label-based `Grouping` pipeline had dropped it). New state: `expandedCardId: WritableSignal<string | null>`, `purchasesStatus: WritableSignal<LoadStatus>`, `expandedPurchases: WritableSignal<CardPurchaseRow[]>`, plus a `purchasesByCardId` in-memory cache `Map` so re-expanding a card doesn't refetch. New `toggleCardPurchases(cardId: string | null)`: no-ops on `null`, collapses on re-click of the same card, serves from cache when available, otherwise calls `financing.cardPurchases(cardId)`.
- [x] `dashboard-page.html` — each "Card Debt by Cycle" row with a `cardId` becomes a `<button>` disclosure (`aria-expanded`, `aria-controls`, a ▾/▸ text-indicator swap, no new motion/animation); rows without a `cardId` stay a plain non-interactive row. Expanded panel: loading/error/empty states, then each purchase's description, a subtle `•` + `sr-only` "Creditor payment" marker when `isCreditorPayment`, "N of M installments outstanding" context, and the formatted total. No new hardcoded hex — existing `docs/SYSTEM.md` tokens only.
- [x] `dashboard-page.spec.ts` — 4 new facts: expanding a card calls the service and renders a purchase's description; a second toggle collapses without refetching (proven via the cache — `cardPurchases` call count stays 1); a `null`-cardId toggle is a no-op that never calls the service; a card row with no `cardId` renders no expand button.

### Definition of done
- [x] Clicking a card row with a `cardId` calls the service and renders the returned rows; a second click collapses.
- [x] Each rendered purchase shows its description; a card row with `cardId == null` is not expandable.
- [x] `pnpm ng build --configuration production` + `pnpm ng lint` clean; `pnpm ng test --watch=false --browsers=ChromeHeadless` → **176 passed** (168 baseline at Phase 6 + Phase 7's own additions + these 4 new facts).
- [ ] Manual live E2E walk (run the API, let a card accrue real installments, expand it on a running Dashboard, confirm against the DB) — **not yet performed**, offered to the user and left pending.

### Completion notes (2026-09-04)

Built one green-lit step at a time, in lockstep with the API's Phase 16 (contract → handler → host wiring → API tests → client type → client service → dashboard state → dashboard template → client tests → full verification). No new D-numbers needed — no drift from `docs/PRD.md`/`docs/DESIGN.md`, this is additive scope like the Creditors phases. The API side caught and fixed a genuine ordering bug during this session's own review (see the API's Phase 16 completion notes) before the client work started, so the client's ordering-dependent behavior (current-cycle rows render first, per the API's response order) was never built against the buggy version. Full verification: `pnpm ng build --configuration production` clean, `pnpm ng test` 176/176, `pnpm ng lint` clean; API side `dotnet test --solution` 170/170. Not committed by this session — the user commits their own.

---

## Phase 9 — Recent purchases view (Slice 3)

**Goal:** Add a standalone "Recent purchases" page — a newest-first chronological list of every loaded expense, independent of card grouping or debt state — reachable from the global nav and the Dashboard hub.

**Traces to:** `docs/expense-description/slice-3-recent-purchases-view.md` (final slice).

**Depends on:** Phase 7 (description exists and is populated) + the API's Phase 17 (`GET /v1/financing/purchases/recent`).

### Tasks
- [x] `features/financing/types/recent-purchase-row.ts` (new) — mirrors the API row: `planId`, `description`, `cardName`, `purchaseDate: IsoDate`, `totalMinorUnits: Money`, `installmentCount`, `isCreditorPayment`.
- [x] `features/financing/financing-service.ts` — add `recentPurchases(): Observable<RecentPurchaseRow[]>` → `GET financing/purchases/recent`, `{ rows }` unwrapped via `map`; `financing-service.spec.ts` gains its `HttpTestingController` spec.
- [x] `features/financing/pages/recent-purchases-page/` (new) — container `recent-purchases-page.ts/.html/.css` (loads on `ngOnInit`, `loadStatus` state machine, no card picker — this view spans every card) + presentational `recent-purchases-table.ts/.html/.css` (description headline, `•` + `sr-only` creditor marker, installment count, card name, date, `formatArs` amount; staggered row-in animation matching `statements-table`).
- [x] `features/financing/financing.routes.ts` — `{ path: 'recent-purchases', component: RecentPurchasesPage }`.
- [x] `app.ts` — "Recent purchases" nav entry between Statements and Reverse.
- [x] `features/reports/pages/dashboard-page/dashboard-page.html` — second quick-action link alongside "Record an expense".
- [x] `recent-purchases-page.spec.ts` (3 facts: fetches and renders on init, empty state, error surfaced without throwing) + `recent-purchases-table.spec.ts` (3 facts: renders description/card/amount, flags only the creditor row, empty note when no rows).

### Definition of done
- [x] The page loads every purchase on init and renders it newest-first (per the API's response order), each showing its description.
- [x] The page is reachable from the global nav and the Dashboard hub.
- [x] `pnpm ng build --configuration production` + `pnpm ng lint` clean; `pnpm ng test --watch=false --browsers=ChromeHeadless` → **183 passed** (176 baseline at Phase 8 + these 7 new facts).
- [x] Manual live E2E walk — **performed this session** (API-side `curl` walk against a running host; documented in the API's Phase 17 completion notes — no browser session available in this environment).

### Completion notes (2026-09-05)

Built one green-lit step at a time, in lockstep with the API's Phase 17. No new D-numbers needed — no drift from `docs/PRD.md`/`docs/DESIGN.md`, this is additive scope like the Creditors phases (added to `docs/PRD.md` §3.9 as "not part of the original 7-view scope", same pattern as Creditors' §3.8). Container/presentational split mirrors `statements-page`/`statements-table` rather than introducing a new shape — deliberately no card-picker form, since this view spans every card by design. Full verification: `pnpm ng build --configuration production` clean, `pnpm ng test` 183/183, `pnpm ng lint` clean; API side `dotnet test --solution` 175/175. Not committed by this session — the user commits their own.

---

## Phase 10 — Creditor-financed expenses (Slice 1)

**Goal:** Turn the Load-Expense "Different creditor" checkbox into a two-way payment-mode selector (My credit card / Financed by a creditor) so a creditor-financed purchase submits with no `cardId` and required creditor + account fields; leave room for a third debit-cash mode.

**Traces to:** `docs/expense-payment-modes/slice-1-creditor-financed.md`; API side is the API's Phase 18 (`cardId` optional on `POST /v1/financing/payment-plans`).

**Depends on:** Phase 6 (the "Different creditor" toggle + creditor/account pickers on `load-expense-page`).

### Tasks
- [x] `features/financing/types/create-payment-plan.ts` — `cardId: string` → `cardId?: string`.
- [x] `features/financing/pages/load-expense-page/load-expense-page.ts` — drop `differentCreditor: FormControl<boolean>`, add `mode: FormControl<'card' | 'creditor'>` (init `'card'`); new `modeOptions`; `watchDifferentCreditorToggle` → `watchModeChange` (creditor: clear + blank `cardId`, require `creditorId`/`creditorAccountId`; card: the reverse + `creditorAccounts.set([])`); `onSubmit` body spreads `mode === 'card' ? { cardId } : { creditorId, creditorAccountId }` then the split spread; `ngOnInit` swaps the watcher.
- [x] `load-expense-page.html` — segmented control (`<fieldset>` + `peer`/`peer-checked` radio, copied from `instruments-page.html`) at the top of "The purchase"; card `<select>` under `@if(mode.value === 'card')`, creditor + account `<select>`s under `@else`; the standalone "Creditor" `<section>` + checkbox deleted; split section unchanged, visible in both modes.
- [x] `isCreditorPayment` retirement (API-driven) — remove from `features/financing/types/card-purchase-row.ts` and the `dashboard-page.html` card-purchases drill-down marker; trim the `dashboard-page.spec.ts` fixture. `recent-purchase-row.ts` + `recent-purchases-*` keep their own field (Phase 9).
- [x] `load-expense-page.spec.ts` — rework the three `differentCreditor` specs to drive `form.controls.mode`; add: card-mode body has `cardId` and no creditor fields; creditor-mode body omits `cardId`, has `creditorId` + `creditorAccountId`, form valid; DOM shows the right `<select>`s per mode; split stays rendered in both modes; a split added in creditor mode is in the body.

### Definition of done
- [x] Card mode submits `{ cardId, ... }`; creditor mode submits `{ creditorId, creditorAccountId, ... }` with no `cardId`; validators follow the mode.
- [x] The split section is visible and submittable in both modes.
- [x] `pnpm ng lint` clean; `pnpm ng build --configuration production` clean; `pnpm ng test --watch=false --browsers=ChromeHeadless` → **187 passed** (183 baseline at Phase 9 + net 4 from the `load-expense-page.spec.ts` rework — 21 facts there).
- [ ] Manual live E2E walk (card mode unchanged; creditor mode with and without a split; confirm the payload shape and the confirmation panel) — **handed to the user**, not run this session.

### Completion notes (2026-09-05)

Built one green-lit step at a time in lockstep with the API's Phase 18. No new D-numbers — additive scope; `docs/PRD.md` §3.3 + §3.8 updated (the "Different creditor" toggle of §3.8 is superseded by the §3.3 mode selector). Segmented control reuses the `instruments-page` `<fieldset>` + `peer-checked` pattern verbatim rather than a new component. Full verification: `pnpm ng lint` clean, `pnpm ng build --configuration production` clean, `pnpm ng test` 187/187; API side `dotnet test --solution` 192/192, 0 warnings. Manual E2E walk handed to the user. Not committed by this session — the user commits their own.

---

## Phase 20 — Owed to creditors list (Slice 2)

**Goal:** Add a standalone, next-due-first list of every creditor with outstanding balance across all creditor-financed purchases.

**Traces to:** `docs/expense-payment-modes/slice-2-owed-to-creditors-list.md`; API side is the API's Phase 19 (`GET /v1/financing/creditor-payables`).

**Depends on:** Phase 10 (card-less payment plans exist).

### Tasks
- [x] `features/financing/types/creditor-payable-row.ts` — `CreditorPayableRow = { creditorId: string; creditorName: string; outstandingMinorUnits: Money; nextDueDate: IsoDate | null; accounts: CreditorPayableAccountBreakdown[] }`.
- [x] `features/financing/types/creditor-payable-account.ts` — `CreditorPayableAccountBreakdown = { accountId: string; label: string; outstandingMinorUnits: Money }`.
- [x] `features/financing/financing-service.ts` — add `creditorPayables(): Observable<CreditorPayableRow[]>` → `GET /v1/financing/creditor-payables`, `{ rows }` unwrapped via `map`; `financing-service.spec.ts` gains its `HttpTestingController` spec (envelope unwrap + `AppError` on error).
- [x] `features/financing/pages/creditor-payables-page/` (new) — container `creditor-payables-page.ts/.html/.css` (loads on `ngOnInit`, `loadStatus` state machine) + presentational `creditor-payables-table.ts/.html/.css` (creditor name + muted account-labels sub-line, next-due date or `—` when null, `formatArs` outstanding; staggered row-in animation matching `statements-table`; empty state "You don't owe any creditors.").
- [x] `features/financing/financing.routes.ts` — `{ path: 'creditor-payables', component: CreditorPayablesPage }`.
- [x] `app.ts` — "Owed to creditors" nav entry after "Recent purchases".
- [x] `creditor-payables-page.spec.ts` (3 facts: fetches and renders on init, empty state, error surfaced without throwing) + `creditor-payables-table.spec.ts` (3 facts: renders creditor name with account sub-line, next-due date or dash, amount; empty note).

### Definition of done
- [x] The page loads every creditor on init and renders it in API response order, showing creditor name, account breakdown, next-due date, and outstanding total.
- [x] The page is reachable from the global nav after "Recent purchases".
- [x] `pnpm ng lint` clean; `pnpm ng build --configuration production` clean; `pnpm ng test --watch=false --browsers=ChromeHeadless` → **194 passed** (187 baseline at Phase 10 + these 7 new facts).
- [ ] Manual live E2E walk (run the API, load creditor-financed plans with/without splits, confirm the list grouping + sums + next-due + account breakdown) — **handed to the user**, not run this session.

### Completion notes

Built one green-lit step at a time in lockstep with the API's Phase 19. No new D-numbers — additive scope; `docs/PRD.md` §3.10 added as "not part of the original 7-view scope" (same pattern as §3.8–§3.9). Container/presentational split mirrors `statements-page`/`statements-table`. The limitation (no per-installment settlement tracking) is accepted and documented in both client PRD and API OpenAPI description — a future session with a payment concept will refine it. Full verification: `pnpm ng lint` clean, `pnpm ng build --configuration production` clean, `pnpm ng test` 194/194; API side `dotnet test --solution` 198/198, 0 warnings. Manual E2E walk handed to the user. Not committed by this session — the user commits their own.

---

## Phase 21 — Debit/cash expenses with categories (Slice 3)

**Goal:** Light up the third *My debit-cash* mode of §3.3's Load-Expense form — a debit/cash instrument dropdown, a required free-type category (existing list + type-new), installments hidden, submit to the Ledger endpoint instead of the payment-plan endpoint.

**Traces to:** `docs/expense-payment-modes/slice-3-debit-cash-categories.md` (final slice of the payment-modes initiative); API side is the API's Phase 20 (`GET /v1/expense-categories`, `POST /v1/ledger/expenses`).

**Depends on:** Phase 10 (the two-way payment-mode selector on `load-expense-page`).

### Tasks
- [x] `features/ledger/types/record-debit-expense.ts` — `RecordDebitExpense = { amountMinorUnits: Money; sourceInstrumentId: string; categoryName: string; purchaseDate: IsoDate; description: string; split?: DebitExpenseParticipant[] }`. `record-debit-expense-result.ts` — `RecordDebitExpenseResult = { id: string }`. `debit-expense-participant.ts` — `DebitExpenseParticipant = { partyId: string; weight: number }`.
- [x] `features/ledger/ledger-service.ts` — `recordDebitExpense(body): Observable<RecordDebitExpenseResult>` (`POST ledger/expenses`) + `listExpenseCategories(): Observable<string[]>` (`GET expense-categories`, `map`-unwrapping the `{ rows: [{ name }] }` envelope to names); `ledger-service.spec.ts` +3 facts (POST → `id`, POST with a split payload, GET envelope → names).
- [x] `features/financing/pages/load-expense-page/load-expense-page.ts` — `mode` widened to `'card' | 'creditor' | 'debit'`; `modeOptions` gains *My debit-cash* in slot 2; form gains `sourceInstrumentId` + `categoryName` `nonNullable` controls; new signals `bankAndCashInstruments` (`computed` — `instruments()` filtered `type === 'debit' || 'cash'`), `expenseCategories: WritableSignal<string[]>` (populated in `ngOnInit` via a one-shot `loadExpenseCategories()`), `confirmedKind: WritableSignal<'plan' | 'expense'>`; `LedgerService` injected. `watchModeChange` is 3-way — `'debit'` makes `sourceInstrumentId` required + `categoryName` `[Validators.required, noBlank]`, drops the `installmentCount` validator and pins it to `1`, clears + blanks all card- and creditor-mode fields (the `'card'`/`'creditor'` branches unchanged — D11). `onSubmit` branches: `'debit'` builds a `RecordDebitExpense` (category `.trim()`ed, split `FormArray` carried — D9) → `ledgerService.recordDebitExpense(...)` mapped to `.id`; else the existing `createPaymentPlan(...)` mapped to `.paymentPlanId`; shared `subscribe` sets `confirmedPlanId`/`confirmedDescription`/`submitStatus`, still reconciles participants on a split, and sets `confirmedKind`. `submitErrorMessages` gains `Ledger.AccountNotFound`, `Ledger.SourceAccountNotSpendable`, `Ledger.InvalidExpenseCategory`.
- [x] `load-expense-page.html` — the purchase-section `<select>` is now `@if(card) … @else if(creditor) … @else { <debit block> }`. Debit block: "Paid from" `<select id="sourceInstrumentId">` fed by `bankAndCashInstruments()` (empty-state "No debit or cash accounts registered yet — add one on the Instruments page."), and a free-type Category `<input type="text" list="expense-category-options">` + `<datalist>` of `expenseCategories()` + hint line — a `<datalist>`, not a bare native `<select>`, per `docs/SYSTEM.md`. The installments `<div>` is wrapped in `@if(mode !== 'debit')`. Confirmation panel reads "expense recorded" / `Expense <code>{id}</code>` for debit, "payment plan created" / `Plan …` otherwise.
- [x] `load-expense-page.spec.ts` +9 facts (debit validators swap; both debit + cash instruments offered; blank category rejected; submit routes to `recordDebitExpense` not `createPaymentPlan` with the right payload + `confirmedKind() === 'expense'`; category trimmed; split included in the debit payload; `debit → card` switch restores card mode; DOM shows source/category + hides installments/cardId/creditorId; headline "expense recorded").
- [x] `docs/PRD.md` §3.3 updated for the third mode + new §3.11 "Debit/cash expenses with categories"; `docs/DESIGN.md` §3 Ledger types + §4 `LedgerService` methods + §9 traceability rows 26–27.

### Definition of done
- [x] Debit mode shows the instrument + category fields, hides installments; category is required (blank rejected); submit calls `POST /v1/ledger/expenses`, not the payment-plan path.
- [x] Card and creditor modes are byte-for-byte unchanged; switching away from debit restores their fields.
- [x] `pnpm ng lint` clean; `pnpm ng build --configuration production` clean (`financing-routes` chunk 50 → 56.40 kB, under the 500 kB budget); `pnpm ng test --watch=false --browsers=ChromeHeadless` → **206 passed** (194 baseline at Phase 20 + 12 new facts).
- [ ] Manual live E2E walk (run the API + client, register a `debit` instrument, record a "Groceries" expense, confirm the balanced transaction + the "Groceries" row in the monthly view, re-use "Groceries" and confirm no second account) — **handed to the user**, not run this session.

### Completion notes

Built one green-lit step at a time in lockstep with the API's Phase 20 (step 3 of the slice). No new D-numbers — additive scope reusing the Phase-10 payment-mode selector. The category field is an `<input>` + `<datalist>` (pick-existing-or-type-new), not a `<select>`, staying within `docs/SYSTEM.md`'s caution against shipping an unstyled native `<select>` as the design. The split `FormArray` carries into debit mode unchanged (D9). Full verification: `pnpm ng lint` clean, `pnpm ng build --configuration production` clean, `pnpm ng test` 206/206; API side `dotnet test --solution` 222/222, 0 warnings, `dotnet restore --locked-mode` clean. Manual sanity walk handed to the user. Not committed by this session — the user commits their own.

---

## Phase 22 — Dashboard fixes: installments-paid wording + card name on future rows (Slices 1–2)

**Goal:** Fix three Dashboard "Card Debt by Cycle" defects across two slices — the drill-down installment line phrased as "outstanding" instead of "paid" (Slice 1), and future-installment cards showing a GUID for a name plus expanding one card opening every row of the same physical card (Slice 2).

**Traces to:** `docs/dashboard-fixes/slice-1-installments-paid-of-total.md` and `slice-2-card-name-and-expand.md` (standalone planning docs — `docs/PRD.md` §3.1 already describes the drill-down and stays unmodified; `docs/DESIGN.md` unchanged on the client side). Slice 2's API half is `app/api` Phase 21 (`vw_card_future_schedule` + `card_due_by_month.sql` + migration `20260906031333`).

**Depends on:** the `docs/expense-description/` Slice 2 card-debt drill-down phase (`dashboard-page.ts` `cycleByCard()` + `expandedCardId` + the `#card-purchases-*` disclosure).

### Tasks
- [x] Slice 1 — `features/reports/pages/dashboard-page/dashboard-page.html` (~line 136): the drill-down purchase line changed from `{{ outstandingCount }} of {{ installmentCount }} installments outstanding` to `{{ installmentCount - outstandingCount }} of {{ installmentCount }} installments paid`. No `.ts`/type change — both fields already on `CardPurchaseRow`.
- [x] Slice 2 — `dashboard-page.ts` `cycleByCard()` regrouped by the stable `cardId` instead of the label string. Grouping key `row.cardId ?? ('label:' + row.card)`; display label per card prefers the Accrued-bucket label, falls back to the Future label, never a GUID; two-pass ordering (accrued cards first, then future-only) preserved. `accruedByCard()`/`futureByCard()` untouched. A null-`cardId` row stays a plain non-expandable row, as before.
- [x] Slice 2 — `dashboard-page.html`: `@for(card of cycleByCard(); track card.card)` → `track card.cardId ?? card.card`. No structural change (the `@if(card.cardId; as cardId)` guards + `expandedCardId() === cardId` stay correct once each `cardId` is on exactly one row).
- [x] Slice 2 — `dashboard-page.spec.ts` +2 facts (`DashboardView` gains `cycleByCard`): an Accrued + a Future row sharing one `cardId` but different `card` labels collapse into exactly one `cycleByCard()` row (Accrued label wins) and expanding it renders exactly one `#card-purchases-*` block; a lone Future row keeps its name as the label.

### Definition of done
- [x] The drill-down line reads "N of M installments paid".
- [x] A card with future installments shows its name (from the API's Phase 21), and expanding it reveals only its own purchases — one `cardId` maps to one rendered row regardless of any Accrued/Future label mismatch.
- [x] `pnpm ng lint` clean; `pnpm ng build --configuration production` clean (no budget change, `financing-routes` chunk unchanged); `pnpm ng test --watch=false --browsers=ChromeHeadless` → **208 passed** (206 baseline at Phase 21 + 2 new facts).
- [ ] Manual live E2E walk (run the API + client, open a Dashboard with a card that has future installments, confirm the card name shows and expanding it opens only its own purchases) — **handed to the user**, not run this session.

### Completion notes

Bug-fix initiative, not a feature — the drill-down itself was built in the `docs/expense-description/` Slice 2 phase. No new D-numbers, no client-side PRD/DESIGN change (the API's `docs/DESIGN.md` §D11 gains one clause for the Future-half label). Slice 1 was a one-line template change; Slice 2's client half is the `cardId` regroup, its API half is `app/api` Phase 21. Full verification: `pnpm ng lint` clean, `pnpm ng build` clean, `pnpm ng test` 208/208; API side `dotnet test --solution` 223/223. Manual browser walk handed to the user. Committed by the user as `3b7a098` (Slice 1) + `979eb54` (Slice 2).

---

## Phase 23 — Dashboard fixes: Parties list endpoint wiring (Slice 3)

**Goal:** Make a created party visible on the Parties page and selectable in the load-expense split immediately — stop enumerating parties through `ReportsService.debtSummary()` (whose INNER JOIN hides movement-less parties) and consume the new `GET /v1/parties` roster endpoint instead.

**Traces to:** `docs/dashboard-fixes/slice-3-parties-list-endpoint.md` (final slice of the dashboard-fixes initiative — fixes bug #2). API half is `app/api` Phase 22. `docs/DESIGN.md` §9 gains a `GET /v1/parties` row; `docs/PRD.md` §3.7 note updated (`debt-summary` no longer the only way to enumerate parties).

**Depends on:** the `docs/creditor-expense-fields/` Slice 1 phase (`CreditorsService.list()` — the pattern mirrored).

### Tasks
- [x] `features/parties/types/party.ts` (new) — `Party = { id: string; name: string }`. `features/parties/parties-service.ts` — `list(): Observable<Party[]>` (`GET parties`, `{ rows }` envelope unwrap), mirroring `CreditorsService.list()`.
- [x] `features/parties/pages/parties-page/parties-page.ts` + `.html` — `loadParties()` does `forkJoin({ roster: partiesService.list(), debts: reports.debtSummary() })`, merges by id into a local `PartyListRow` VM (`{ partyId, partyName, netBalanceMinorUnits }` — same field names as the used `PartyDebtRow` subset, so the template + `balanceHint`/`tickWidth`/totals need no rename); a party with no debt row → `netBalanceMinorUnits: fromMinorUnits(0)` → existing `balanceHint` renders "Settled up" / `$0.00`. Empty state "No parties with movements yet." → "No parties yet."; created-party note reworded.
- [x] `features/financing/pages/load-expense-page/load-expense-page.ts` + `.html` — `loadParties()` source `reportsService.debtSummary()` → `partiesService.list()`; `parties` signal `PartyDebtRow[]` → `Party[]`; `partyName()` + the split `<select>` migrated `partyId`/`partyName` → `id`/`name`; `ReportsService` import + field removed. "Add participant" `[disabled]="parties().length === 0"` now reflects real party existence.
- [x] `parties-page.spec.ts` rewritten (9 facts — merge of `list()` + `debtSummary()`, a party absent from the debt summary renders as "Settled up" at zero, "No parties yet." empty state, roster-load failure → error). `load-expense-page.spec.ts` reworked (`PartyDebtRow` → `Party`, `debtSummary` spy → `PartiesService.list`, `ReportsService` provider dropped).

### Definition of done
- [x] The Parties page lists every registered party; one with no movements shows "settled" / `$0`. The load-expense split offers a brand-new party immediately.
- [x] `pnpm ng lint` clean; `pnpm ng build` clean; `pnpm ng test --watch=false --browsers=ChromeHeadless` → **210 passed** (208 baseline at Phase 22 + 2 net new facts).
- [ ] Manual live E2E walk (create a fresh party with no shared expense, confirm it shows on the Parties page as "settled" and is selectable in the load-expense split) — **handed to the user**, not run this session.

### Completion notes

Client half of the slice, built one green-lit step at a time in lockstep with the API's Phase 22. The Parties page keeps `debt-summary` as the balances source and merges it with the new roster endpoint; the load-expense split drops `debt-summary` entirely. The `PartyListRow` VM deliberately keeps `PartyDebtRow`'s field names so the Parties-page template and helpers needed no rename. Full verification: `pnpm ng lint` clean, `pnpm ng build` clean, `pnpm ng test` 210/210; API side `dotnet test --solution` 227/227. Manual browser walk handed to the user. Not committed by this session — the user commits their own.

---

## Verification (every phase)

- **Build:** `pnpm ng build` — 0 errors, within the 500 kB warning / 1 MB error initial-JS budget.
- **Unit:** `pnpm ng test --watch=false --browsers=ChromeHeadless` — green; each new service has an `HttpTestingController` spec.
- **Manual:** run the API locally (`http://localhost:5000/v1`), `pnpm ng serve`, walk each new view through loading / empty / error; force a 409/422 and confirm an `AppError` renders keyed off `code`.
- **Type parity:** every `types/*.ts` field-matches the DTO tables in `docs/DESIGN.md` §3.
- **Traceability:** every step above cites a `docs/PRD.md` `§`/`US` or `docs/DESIGN.md` `§`; every Phase 4 item cites its gap number.
