# Client DESIGN — Personal Finance (Angular 20)

> Technical design for wiring the Angular 20 client to the PersonalFinance `.NET 10` API.
> Counterpart of `app/api/docs/DESIGN.md`. Governed by `.claude/CLAUDE.md` and
> `.claude/rules/typescript-frontend-style.md` — every symbol below obeys that style guide.
> **UI/visual design is out of scope** (separate task); this document defines structure, types,
> services, HTTP wiring, and configuration only.

## 1. Principles

- **Feature-based / screaming structure.** Folders name business capabilities (instruments,
  financing, subscriptions, parties, reports), mirroring the API's bounded contexts — not technical
  layers.
- **Signals-first, zoneless.** State lives in signals; derived state in `computed()`. No `effect()`
  for data flow. Templates read signals by invocation; **no async pipe**.
- **`inject()` DI, standalone, `OnPush`** on every component. `standalone: true` is never written
  (it is the default).
- **Container / presentational split.** This task defines container **pages** and the signal state
  they hold. Presentational leaves are placeholders for the UI task.
- **Hand-written types.** One `type` per file in `types/` folders, mirroring the API DTOs by hand.
  `/openapi/v1.json` is the reference to mirror — **no codegen**. Money is a branded type.
- **No business logic in the client.** Cycles, splits, rounding, and invariants are the API's.

## 2. Folder structure

```
src/app/
  core/
    http/
      base-url.interceptor.ts        -> baseUrlInterceptor
      problem-details.interceptor.ts -> problemDetailsInterceptor
    types/
      money.ts             -> Money            (branded minor-units)
      currency-code.ts     -> CurrencyCode     ('ARS')
      iso-instant.ts       -> IsoInstant       (ISO-8601 UTC string)
      iso-date.ts          -> IsoDate          (date-only 'YYYY-MM-DD' string)
      problem-details.ts   -> ProblemDetails   (wire error envelope)
      app-error.ts         -> AppError         (domain error the app handles)
    money/
      money.ts             -> fromMinorUnits / toMinorUnits / formatArs (pure fns)
    registry/
      instrument-registry-service.ts -> InstrumentRegistryService (local cache, gap #1)
    health/
      health-service.ts    -> HealthService
  features/
    instruments/
      instruments-service.ts
      types/  (instrument.ts, instrument-type.ts, create-instrument.ts, instrument-created.ts)
      pages/  instruments-page/
      instruments.routes.ts
    ledger/
      ledger-service.ts
      types/  (post-transaction.ts, transaction-line.ts, direction.ts,
               post-transaction-result.ts, reverse-transaction-result.ts, account-balance.ts)
      pages/  reverse-movement-page/
      ledger.routes.ts
    financing/
      financing-service.ts
      types/  (create-payment-plan.ts, split-participant.ts, create-payment-plan-result.ts,
               monthly-statement.ts, monthly-statement-installment.ts, pay-statement.ts,
               pay-statement-result.ts, card-future-schedule.ts, card-future-schedule-row.ts)
      pages/  statement-page/, load-expense-page/
      financing.routes.ts
    subscriptions/
      subscriptions-service.ts
      types/  (create-subscription.ts, frequency.ts, subscription-result.ts,
               active-subscription.ts)
      pages/  subscriptions-page/
      subscriptions.routes.ts
    parties/
      parties-service.ts
      types/  (create-party.ts, party-result.ts, current-account-balance.ts,
               current-account-timeline-row.ts, register-shared-expense.ts,
               shared-expense-participant.ts, shared-expense-result.ts,
               settle-current-account.ts, settlement-result.ts)
      pages/  parties-page/, party-detail-page/, shared-expense-page/
      parties.routes.ts
    reports/
      reports-service.ts
      types/  (monthly-expense-row.ts, card-due-row.ts, party-debt-row.ts, party-timeline-row.ts)
      pages/  dashboard-page/
      reports.routes.ts
  environments/
    environment.ts             (prod placeholder)
    environment.development.ts  (apiUrl: 'http://localhost:5000/v1')
  app.config.ts
  app.routes.ts
```

Naming per the style guide: files kebab-case, exports PascalCase with **no** `Component`/`Service`
suffix in the class name where the guide omits it (services keep the `Service` suffix per the guide's
own example, e.g. `FinancingService`; components drop the suffix, e.g. `DashboardPage`). Types carry
no `I-` prefix.

## 3. API type model (hand-written, exact field mapping)

.NET serializes with **camelCase**. Amounts are `long` minor units → TypeScript `Money` (a branded
`number`). C# `DateTimeOffset` → `IsoInstant`; C# `DateOnly` / date strings → `IsoDate`. GUIDs →
`string`. Below, each client type mirrors one DTO exactly (source of truth in parentheses).

**Core value types** (`core/types/`)
- `Money = number & { readonly __brand: 'Money' }` — minor units; never floated.
- `CurrencyCode = 'ARS'`.
- `IsoInstant = string` (ISO-8601 UTC), `IsoDate = string` (`YYYY-MM-DD`).

**Instruments** (`PostInstrumentDto` / `InstrumentCreatedDto`)
- `InstrumentType = 'debit' | 'credit' | 'cash'`
- `CreateInstrument = { type: InstrumentType; name: string; cutoffDate?: number }` (cutoff required
  when `type === 'credit'`, 1–31)
- `InstrumentCreated = { id: string; type: InstrumentType }`

**Ledger** (`PostTransactionDto`, `ReverseTransactionResultDto`, `AccountBalanceDto`)
- `Direction = 'Debit' | 'Credit'`
- `TransactionLine = { accountId: string; direction: Direction; amountMinorUnits: Money }`
- `PostTransaction = { lines: TransactionLine[]; postedOnUtc: IsoInstant; splitReferenceId?: string;
  installmentReferenceId?: string; description?: string }`
- `PostTransactionResult = { transactionId: string }`
- `ReverseTransactionResult = { reversalTransactionId: string; originalTransactionId: string;
  compensatingEntryPosted: boolean }`
- `AccountBalance = { accountId: string; balanceMinorUnits: Money; currencyCode: CurrencyCode;
  formatted: string }`

**Financing** (`CreatePaymentPlanDto`, `MonthlyStatementDetailDto`, `PayStatementDto`,
`CardFutureScheduleDto`)
- `SplitParticipant = { partyId: string; weight: number }`
- `CreatePaymentPlan = { amountMinorUnits: Money; cardId: string; installmentCount: number;
  purchaseDate: IsoDate; split?: SplitParticipant[] }`
- `CreatePaymentPlanResult = { paymentPlanId: string }`
- `MonthlyStatementInstallment = { planId: string; installmentId: string; sequence: number;
  installmentCount: number; purchaseDate: IsoDate; cycleYear: number; cycleMonth: number;
  amountMinorUnits: Money; isReversed: boolean }`
- `MonthlyStatement = { statementId: string; cardId: string; cardName: string; cycleYear: number;
  cycleMonth: number; amountDueMinorUnits: Money; isPaid: boolean; paidOnUtc: IsoInstant | null;
  installments: MonthlyStatementInstallment[] }`
- `PayStatement = { bankAccountId: string; paidOnUtc: IsoInstant }`
- `PayStatementResult = { statementId: string }`
- `CardFutureScheduleRow = { planId: string; installmentId: string; sequence: number;
  cycleYear: number; cycleMonth: number; amountMinorUnits: Money }`
- `CardFutureSchedule = { cardId: string; rows: CardFutureScheduleRow[] }`

**Subscriptions** (`CreateSubscriptionDto`, `SubscriptionResultDto`, `ActiveSubscriptionsDto`)
- `Frequency = 'monthly' | 'weekly' | 'daily' | 'annually'`
- `CreateSubscription = { name: string; amountMinorUnits: Money; category: string;
  fundingAccountId: string; frequency: Frequency; anchorDay: number }`
- `SubscriptionResult = { id: string }`
- `ActiveSubscription = { subscriptionId: string; name: string; amountMinorUnits: Money;
  category: string; frequency: Frequency; anchorDay: number; nextDueDate: IsoDate }`

**Parties** (`CreatePartyDto`, `RegisterSharedExpenseDto`, `SettleCurrentAccountDto`,
`CurrentAccountDTO`)
- `CreateParty = { name: string }`, `PartyResult = { id: string }`
- `SharedExpenseParticipant = { partyId: string; weight: number }`
- `RegisterSharedExpense = { description: string; totalMinorUnits: Money; expenseAccountId: string;
  fundingAccountId: string; incurredOnUtc: IsoInstant; participants: SharedExpenseParticipant[] }`
- `SharedExpenseResult = { splitReferenceId: string }`
- `SettleCurrentAccount = { amountMinorUnits: Money; bankAccountId: string; settledOnUtc: IsoInstant }`
- `SettlementResult = { ledgerTransactionId: string }`
- `CurrentAccountBalance = { partyId: string; name: string; balanceMinorUnits: Money }`
- `CurrentAccountTimelineRow = { movementOnUtc: IsoInstant; description: string;
  deltaMinorUnits: Money; runningBalanceMinorUnits: Money }`

**Reporting** (`ReportingDTOs`)
- `MonthlyExpenseRow = { month: string; category: string; amountMinorUnits: Money;
  currencyCode: CurrencyCode }` (`month` is `'YYYY-MM'`)
- `CardDueRow = { bucket: 'Accrued' | 'Future'; card: string; cycleYear: number | null;
  cycleMonth: number | null; amountMinorUnits: Money; currencyCode: CurrencyCode;
  cardId: string | null }`
- `PartyDebtRow = { partyId: string; partyName: string; netBalanceMinorUnits: Money;
  currencyCode: CurrencyCode }`
- `PartyTimelineRow = { movementOnUtc: IsoInstant; description: string; deltaMinorUnits: Money;
  runningBalanceMinorUnits: Money; currencyCode: CurrencyCode }`

## 4. Services

One `@Injectable({ providedIn: 'root' })` per bounded context, each `inject(HttpClient)` and a
`baseUrl` from `environment`, returning `Observable<DomainType>`. Signatures:

- **InstrumentsService** — `create(body: CreateInstrument): Observable<InstrumentCreated>`
- **LedgerService** — `postTransaction(body: PostTransaction): Observable<PostTransactionResult>`;
  `reverse(transactionId: string): Observable<ReverseTransactionResult>`;
  `getAccountBalance(accountId: string): Observable<AccountBalance>`
- **FinancingService** — `createPaymentPlan(body: CreatePaymentPlan): Observable<CreatePaymentPlanResult>`;
  `getStatement(id: string): Observable<MonthlyStatement>`;
  `payStatement(id: string, body: PayStatement): Observable<PayStatementResult>`;
  `getFutureSchedule(cardId: string): Observable<CardFutureSchedule>`
- **SubscriptionsService** — `create(body: CreateSubscription): Observable<SubscriptionResult>`;
  `cancel(id: string): Observable<void>`; `listActive(): Observable<ActiveSubscription[]>`
  (unwraps the `{ rows }` envelope)
- **PartiesService** — `create(body: CreateParty): Observable<PartyResult>`;
  `registerSharedExpense(body: RegisterSharedExpense): Observable<SharedExpenseResult>`;
  `settle(partyId: string, body: SettleCurrentAccount): Observable<SettlementResult>`;
  `getBalance(partyId: string): Observable<CurrentAccountBalance>`;
  `getTimeline(partyId: string): Observable<CurrentAccountTimelineRow[]>`
- **ReportsService** — `monthlyExpenses(month?: string): Observable<MonthlyExpenseRow[]>`;
  `cardDueByMonth(): Observable<CardDueRow[]>`; `debtSummary(): Observable<PartyDebtRow[]>`;
  `partyTimeline(partyId: string): Observable<PartyTimelineRow[]>`
- **HealthService** — `check(): Observable<...>` (`GET /health`, degradation aware)

Services that hit `{ rows: [...] }` list endpoints unwrap to the array via `map`, so components
receive plain arrays. All routes are built from `ApiRoutes` constants mirrored client-side.

## 5. HTTP wiring & error model

**`app.config.ts`**
```ts
provideHttpClient(
  withFetch(),
  withInterceptors([baseUrlInterceptor, problemDetailsInterceptor]),
)
```

**`baseUrlInterceptor`** — prefixes relative request URLs with `environment.apiUrl`
(`http://localhost:5000/v1` in dev). Keeps services free of the host.

**`problemDetailsInterceptor`** — catches `HttpErrorResponse` and rethrows a typed `AppError`.
The API envelope is RFC-9457 `ProblemDetails` and — because it is built with
`TypedResults.Problem(extensions:)` — the `code` and any metadata are **root-level members**, not
nested. Exact wire shape:
```json
{
  "type": "https://tools.ietf.org/html/rfc9457",
  "title": "The request conflicts with the current state of the resource.",
  "status": 409,
  "detail": "Ledger.CannotReverseAReversal",
  "code": "Ledger.CannotReverseAReversal"
}
```
- `ProblemDetails = { type?: string; title?: string; status?: number; detail?: string; code?: string;
  [key: string]: unknown }`
- `AppError = { code: string; title: string; detail: string; status: number;
  metadata: Record<string, unknown> }`
- The interceptor reads `error.error.code` (root), falls back to `status`-derived defaults if the
  body is missing (network / 5xx), and preserves extra root keys as `metadata`.
- **Status meaning** (from `ErrorHttpStatusHelper`): `*.NotFound` → 404; `AlreadyPaid` /
  `AlreadyAccrued` / `AlreadyReversed` / `NotActive` / `SettlementExceedsBalance` → 409; `Invalid*` /
  `NonPositive*` and other domain violations → 422; `Instruments.UnknownType` / `Request.Malformed` →
  400. Views key user-facing messages off `code`, not off `detail` text.

## 6. State & reactivity

- Container pages own state as `WritableSignal`s (double-annotated per the guide), derive with
  `computed()`, and render by signal invocation.
- Observables are confined to service I/O. Subscriptions in pages use the guide's single cleanup
  pattern (`private destroy$ = new Subject<void>()` + `takeUntil(this.destroy$)` +
  `ngOnDestroy`). No async pipe.
- **Reactive forms** for every input view (Load expense, Instruments, Pay, Shared expense,
  Subscription). Forms built in an `initXForm()` method via `FormBuilder`; validators as pure
  functions; error copy via an `ErrorConfig` map. Money inputs are entered in major units and
  converted with `toMinorUnits` at submit.

## 7. Eventual consistency & scheduler awareness

- **Split registration (API D8):** after `createPaymentPlan` with a `split`, the plan id returns
  before the Parties receivable is posted (async Outbox). The Load-expense page confirms the plan
  immediately, then reconciles by polling `PartiesService.getBalance` with a short bounded retry
  before showing the party's updated balance. A shared `pollUntil` helper (RxJS
  `timer`+`switchMap`+`take`/`retry`) lives in `core/http`.
- **Reversal credit (API D12):** the Statement page states that a reversed paid installment yields a
  card credit netted on the **next** statement (not cash back).
- **Scheduler-driven accrual/renewal (API D6):** the client cannot trigger these; it reflects the
  latest state on load. No optimistic UI for scheduled effects.

## 8. Local instrument registry (gap #1 workaround)

Because the API has no list endpoint for instruments/cards/accounts, `InstrumentRegistryService`
keeps the instruments the client created (id, type, name, cutoff) in a signal persisted to
`localStorage` (guarded by try/catch; empty-safe). Forms that need to offer a card or funding account
read from this registry until a real `GET /v1/instruments` exists. Cards for split targets and party
choices come from `ReportsService.debtSummary` / `cardDueByMonth` where possible. This is a
documented stopgap, not the intended end state (see PRD §7).

## 9. Endpoint → service → view traceability

| # | Method | Route | Service method | View |
|---|--------|-------|----------------|------|
| 1 | POST | `/v1/instruments` | `InstrumentsService.create` | Instruments setup |
| 2 | POST | `/v1/ledger/transactions` | `LedgerService.postTransaction` | (low-level; internal) |
| 3 | POST | `/v1/ledger/transactions/{id}/reversal` | `LedgerService.reverse` | Reverse movement |
| 4 | GET | `/v1/ledger/accounts/{id}/balance` | `LedgerService.getAccountBalance` | (detail widgets) |
| 5 | POST | `/v1/financing/payment-plans` | `FinancingService.createPaymentPlan` | Load expense |
| 6 | GET | `/v1/financing/statements/{id}` | `FinancingService.getStatement` | Statement detail |
| 7 | POST | `/v1/financing/statements/{id}/pay` | `FinancingService.payStatement` | Statement detail |
| 8 | GET | `/v1/financing/cards/{id}/future-schedule` | `FinancingService.getFutureSchedule` | Statement detail |
| 9 | POST | `/v1/subscriptions` | `SubscriptionsService.create` | Subscriptions |
| 10 | DELETE | `/v1/subscriptions/{id}` | `SubscriptionsService.cancel` | Subscriptions |
| 11 | GET | `/v1/subscriptions/active` | `SubscriptionsService.listActive` | Subscriptions |
| 12 | POST | `/v1/parties` | `PartiesService.create` | Parties |
| 13 | POST | `/v1/parties/shared-expenses` | `PartiesService.registerSharedExpense` | Party detail / Shared expense |
| 14 | POST | `/v1/parties/{id}/settlements` | `PartiesService.settle` | Party detail |
| 15 | GET | `/v1/parties/{id}/balance` | `PartiesService.getBalance` | Party detail |
| 16 | GET | `/v1/parties/{id}/timeline` | `PartiesService.getTimeline` | Party detail |
| 17 | GET | `/v1/reports/monthly-expenses` | `ReportsService.monthlyExpenses` | Dashboard |
| 18 | GET | `/v1/reports/card-due-by-month` | `ReportsService.cardDueByMonth` | Dashboard |
| 19 | GET | `/v1/reports/parties/{id}/timeline` | `ReportsService.partyTimeline` | Party detail |
| 20 | GET | `/v1/reports/parties/debt-summary` | `ReportsService.debtSummary` | Parties list |
| 21 | GET | `/health` | `HealthService.check` | (status indicator) |

`POST /v1/ledger/accounts` (dev-only account shortcut) is intentionally **not** wired — it is
removed outside Development.

## 10. Configuration & tooling

- **Environments (new).** Add `src/app/environments/environment.ts` (prod placeholder) and
  `environment.development.ts` (`apiUrl: 'http://localhost:5000/v1'`), plus an `angular.json`
  `fileReplacements` entry for the production build. CORS already allows `http://localhost:4200`
  (API D13), so `ng serve` works against a local API with no proxy.
- **Routing.** `app.routes.ts` uses `loadChildren` per feature `*.routes.ts` (lazy) — Fase-1
  features first.
- **Tailwind (config groundwork).** Not installed. It must be added before the UI task, but tokens,
  theme, and components belong to that task. This document only records the dependency.
- **Testing.** Jasmine + Karma are present. Each service gets an `HttpTestingController` spec
  asserting URL, verb, body, envelope unwrap, and `AppError` mapping. The `money` utilities and
  `problemDetailsInterceptor` get focused unit specs.

## 11. Open questions (see PRD §7)

1. `GET /v1/instruments` (and card/account listing) — removes the `localStorage` stopgap (§8).
2. `GET /v1/financing/cards/{id}/statements` — makes the Statement pay flow self-navigable.
3. A transactions feed (`GET /v1/ledger/transactions`) — enables a real history/reverse-picker view.
4. App-level auth surface — deferred; no backing endpoint today.
